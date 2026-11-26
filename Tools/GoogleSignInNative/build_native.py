"""Rebuild the pinned GoogleSignIn native bridge; never invokes Unity or changes SDK inputs."""
import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import zipfile

HERE = Path(__file__).resolve().parent
FORMATS = ("StringFromJava %s == %s", "User Display Name is  %s",
           "In Wrapper display name is  %s", "helper: %x method: %x activity: %x")


def sha(data):
    return hashlib.sha256(data).hexdigest()


def validate_output_paths(output, source, original_aar, ndk, executables):
    def overlaps(first, second):
        return first == second or first.is_relative_to(second) or second.is_relative_to(first)

    if any(overlaps(output, path) for path in (source, original_aar, ndk, *executables)):
        raise ValueError("Output must be separate from all source, AAR and toolchain inputs")
    if output == HERE or HERE.is_relative_to(output):
        raise ValueError("Output must not contain this tool")
    if output.is_relative_to(HERE) and output.relative_to(HERE).parts[0] != "build":
        raise ValueError("Use the ignored Tools/GoogleSignInNative/build folder or an external output directory")
    if output.is_relative_to(HERE.parents[1] / "Assets"):
        raise ValueError("Native build outputs must not be imported as Unity Assets")
    if output.exists() and (not output.is_dir() or any(output.iterdir())):
        raise ValueError("Output must be a new or empty directory; existing files are preserved")


def inspect_elf(data):
    if data[:4] != b"\x7fELF" or data[5] != 1 or data[4] not in (1, 2):
        raise ValueError("Expected a little-endian ELF32/ELF64 library")
    wide = data[4] == 2
    head = struct.unpack_from("<16sHHIQQQIHHHHHH" if wide else "<16sHHIIIIIHHHHHH", data)
    section_fmt = "<IIQQQQIIQQ" if wide else "<IIIIIIIIII"
    sections = [struct.unpack_from(section_fmt, data, head[6] + i * head[11]) for i in range(head[12])]
    string_section = sections[head[13]]
    names = data[string_section[4]:string_section[4] + string_section[5]]
    sections = {names[s[0]:].split(b"\0", 1)[0].decode(): s for s in sections}
    strings = sections[".dynstr"]
    strings = data[strings[4]:strings[4] + strings[5]]
    name = lambda offset: strings[offset:].split(b"\0", 1)[0].decode()
    sec = sections[".dynamic"]
    dynamic = [struct.unpack_from("<qQ" if wide else "<iI", data, sec[4] + i)
               for i in range(0, sec[5], 16 if wide else 8)]
    sec = sections[".dynsym"]
    raw_symbols = [struct.unpack_from("<IBBHQQ" if wide else "<IIIBBH", data, sec[4] + i)
                   for i in range(0, sec[5], sec[9])]
    symbols = raw_symbols if wide else [(s[0], s[3], s[4], s[5], s[1], s[2]) for s in raw_symbols]
    exports = sorted({name(s[0]) for s in symbols if s[3] and s[1] >> 4 in (1, 2) and s[2] & 3 in (0, 3)})
    imports = sorted({name(s[0]) for s in symbols if not s[3] and name(s[0])})
    raw_ph = [struct.unpack_from("<IIQQQQQQ" if wide else "<IIIIIIII", data, head[5] + i * head[9]) for i in range(head[10])]
    # type, flags, offset, address, file bytes, memory bytes, alignment
    ph = [(p[0], p[1], p[2], p[3], p[5], p[6], p[7]) if wide else
          (p[0], p[6], p[1], p[2], p[4], p[5], p[7]) for p in raw_ph]
    loads = [p for p in ph if p[0] == 1]
    relro = []
    for p in ph:
        if p[0] == 0x6474e552:
            end = p[3] + p[5]
            rounded = (end + 16383) // 16384 * 16384
            tail = any(x[1] & 2 and max(end, x[3]) < min(rounded, x[3] + x[5]) for x in loads)
            relro.append({"start": hex(p[3]), "end": hex(end), "roundedEnd16kb": hex(rounded), "writableTail16kb": tail})
    return {"sha256": sha(data), "bytes": len(data), "machine": head[2],
            "soname": [name(v) for t, v in dynamic if t == 14], "needed": [name(v) for t, v in dynamic if t == 1],
            "exports": exports, "imports": imports,
            "publicExports": [s for s in exports if s.startswith("GoogleSignIn_") or s == "JNI_OnLoad"],
            "loads": [{"offset": hex(p[2]), "vaddr": hex(p[3]), "fileBytes": p[4], "memBytes": p[5], "flags": p[1], "align": hex(p[6])} for p in loads],
            "load16kb": bool(loads) and all(p[6] >= 16384 and (p[3] - p[2]) % 16384 == 0 for p in loads),
            "relro": relro, "relro16kb": bool(relro) and not any(r["writableTail16kb"] for r in relro),
            "noRWX": all(not (p[1] & 1 and p[1] & 2) for p in loads),
            "noExecutableStack": all(not p[1] & 1 for p in ph if p[0] == 0x6474e551),
            "bindNow": any(t == 24 or (t == 30 and v & 8) or (t == 0x6ffffffb and v & 1) for t, v in dynamic)}


def apply_source_patch(source, patch):
    """Apply the checked-in fixed-context source diff without requiring a Git checkout."""
    lines = patch.read_bytes().decode("utf-8").splitlines(True)
    index = 0
    while index < len(lines):
        if not lines[index].startswith("--- a/"):
            raise ValueError("Unexpected source patch header")
        relative = lines[index][6:].rstrip("\n")
        if lines[index + 1] != "+++ b/" + relative + "\n":
            raise ValueError("Source patch may not rename files")
        target = (source / relative).resolve()
        if not target.is_relative_to(source.resolve()):
            raise ValueError("Source patch escapes staging directory")
        original = target.read_bytes().decode("utf-8").splitlines(True)
        position, output = 0, []
        index += 2
        while index < len(lines) and lines[index].startswith("@@ "):
            match = re.match(r"@@ -(\d+)(?:,\d+)? \+\d+(?:,\d+)? @@", lines[index])
            if not match:
                raise ValueError("Invalid source patch hunk")
            start = int(match.group(1)) - 1
            if start < position:
                raise ValueError("Overlapping source patch hunks")
            output.extend(original[position:start]); position = start; index += 1
            while index < len(lines) and not lines[index].startswith(("@@ ", "--- a/")):
                kind, value = lines[index][0], lines[index][1:]
                if kind in (" ", "-"):
                    if position >= len(original) or original[position] != value:
                        raise ValueError("Source patch context does not match pinned input")
                    position += 1
                if kind in (" ", "+"):
                    output.append(value)
                if kind not in (" ", "-", "+"):
                    raise ValueError("Unsupported source patch marker")
                index += 1
        output.extend(original[position:])
        target.write_bytes("".join(output).encode("utf-8"))


def verify_abi(original, candidate, abi):
    old, new = inspect_elf(original), inspect_elf(candidate)
    checks = {
        "machine": old["machine"] == new["machine"] == (183 if abi == "arm64-v8a" else 40),
        "soname": old["soname"] == new["soname"] == ["libnative-googlesignin.so"],
        "needed": old["needed"] == new["needed"],
        "publicExports20": old["publicExports"] == new["publicExports"] and len(new["publicExports"]) == 20,
        "relroRetained": bool(new["relro"]), "bindNowRetained": old["bindNow"] and new["bindNow"],
        "noRWX": new["noRWX"], "noExecutableStack": new["noExecutableStack"],
        "sensitiveBaseline": all(s.encode() in original for s in FORMATS),
        "sensitiveFormatsAbsent": not any(s.encode() in candidate for s in FORMATS)
    }
    if abi == "arm64-v8a":
        checks.update({"load16kb": new["load16kb"], "relro16kbNoWritableTail": new["relro16kb"]})
    return {"passed": all(checks.values()), "checks": checks, "old": old, "new": new,
            "missingAllExports": sorted(set(old["exports"]) - set(new["exports"])),
            "addedAllExports": sorted(set(new["exports"]) - set(old["exports"]))}


def run(command, log):
    result = subprocess.run([str(s) for s in command], stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            text=True, encoding="utf-8", errors="replace")
    log.write_text(result.stdout, encoding="utf-8")
    if result.returncode:
        raise RuntimeError("Native tool failed; see " + str(log))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Extracted official pinned repository root")
    parser.add_argument("--original-aar", type=Path, required=True, help="Unmodified upstream 1.0.4 srcaar")
    parser.add_argument("--ndk", type=Path, required=True, help="NDK r27c root")
    parser.add_argument("--output", type=Path, required=True, help="External or ignored build directory")
    parser.add_argument("--cmake", type=Path, help="Optional CMake executable; defaults to sibling SDK CMake3.22.1 or PATH")
    args = parser.parse_args()
    args.source, args.original_aar, args.ndk, args.output = [p.resolve() for p in (args.source, args.original_aar, args.ndk, args.output)]
    if args.cmake:
        args.cmake = args.cmake.resolve()
    manifest = json.loads((HERE / "source-manifest.json").read_text(encoding="utf-8"))
    original_bytes = args.original_aar.read_bytes()
    if sha(original_bytes) != manifest["originalAarSha256"]:
        raise ValueError("Original AAR SHA256 does not match upstream 1.0.4; no output written")
    if sha((HERE / "source-redaction.patch").read_bytes()) != manifest["sourcePatchSha256"]:
        raise ValueError("Source redaction patch hash mismatch")
    for relative, expected in manifest["sourceFilesSha256"].items():
        if sha((args.source / relative).read_bytes()) != expected:
            raise ValueError("Source differs from pinned official commit: " + relative)
    if not re.search(r"Pkg.Revision\s*=\s*27\.2\.12479018\s*$", (args.ndk / "source.properties").read_text(), re.MULTILINE):
        raise ValueError("This recipe requires NDK r27c (27.2.12479018)")
    output = args.output.resolve()
    suffix = ".exe" if os.name == "nt" else ""
    host = "windows-x86_64" if os.name == "nt" else "darwin-x86_64" if sys.platform == "darwin" else "linux-x86_64"
    toolbin = args.ndk / "toolchains/llvm/prebuilt" / host / "bin"
    cmake = args.cmake or args.ndk.parent / ("SDK/cmake/3.22.1/bin/cmake" + suffix)
    if not cmake.is_file():
        found = shutil.which("cmake")
        if not found:
            raise ValueError("Pass --cmake or install CMake in the sibling SDK")
        cmake = Path(found)
    ninja = cmake.parent / ("ninja" + suffix)
    if not ninja.is_file():
        found = shutil.which("ninja")
        if not found:
            raise ValueError("Ninja is required beside CMake or on PATH")
        ninja = Path(found)
    validate_output_paths(output, args.source, args.original_aar, args.ndk, (cmake.resolve(), ninja.resolve()))
    output.mkdir(parents=True, exist_ok=True)
    toolchain = {}
    for executable in (cmake, ninja, toolbin / ("clang++" + suffix), toolbin / ("ld.lld" + suffix), toolbin / ("llvm-strip" + suffix)):
        version = subprocess.run([str(executable), "--version"], stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                 text=True, encoding="utf-8", errors="replace", check=True)
        toolchain[str(executable)] = {"sha256": sha(executable.read_bytes()), "version": version.stdout.strip()}
    source = output / "source"
    shutil.copytree(args.source / "native-googlesignin", source, dirs_exist_ok=True)
    apply_source_patch(source, HERE / "source-redaction.patch")
    remaining_logs = []
    for cpp in (source / "src/main/cpp").glob("*.cc"):
        remaining_logs.extend(re.findall(r"__android_log_print\([\s\S]*?\);", cpp.read_text()))
    if remaining_logs != ['__android_log_print(ANDROID_LOG_ERROR, TAG, "configuration is null!?");']:
        raise ValueError("Unexpected log call after source redaction")
    commands, libraries, report = {}, {}, {}
    with zipfile.ZipFile(args.original_aar) as original:
        for abi in ("arm64-v8a", "armeabi-v7a"):
            build = output / ("build-" + abi)
            flags = "--driver-mode=g++ -std=c++11 -Wall"
            configure = [cmake, "-S", source, "-B", build, "-G", "Ninja", "-DCMAKE_MAKE_PROGRAM=" + str(ninja),
                         "-DCMAKE_TOOLCHAIN_FILE=" + str(args.ndk / "build/cmake/android.toolchain.cmake"),
                         "-DANDROID_ABI=" + abi, "-DANDROID_PLATFORM=android-21", "-DANDROID_STL=c++_static",
                         "-DCMAKE_BUILD_TYPE=Release", "-DCMAKE_CXX_FLAGS=" + flags]
            if abi == "arm64-v8a":
                configure.append("-DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,max-page-size=16384 -Wl,-z,common-page-size=16384")
            stripped = output / ("libnative-googlesignin-" + abi + ".so")
            commands[abi] = {"configure": [str(s) for s in configure],
                             "build": [str(cmake), "--build", str(build), "--verbose"],
                             "strip": [str(toolbin / ("llvm-strip" + suffix)), "--strip-unneeded", "-o", str(stripped), str(build / "libnative-googlesignin.so")]}
            for stage, command in commands[abi].items():
                run(command, output / (abi + "-" + stage + ".log"))
            entry = "jni/" + abi + "/libnative-googlesignin.so"
            libraries[entry] = stripped.read_bytes()
            report[abi] = verify_abi(original.read(entry), libraries[entry], abi)
        if not all(value["passed"] for value in report.values()):
            (output / "verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
            raise RuntimeError("Native static verification failed; see verification.json")
        archive = output / "google-signin-support-1.0.4.srcaar"
        with zipfile.ZipFile(archive, "w") as candidate:
            candidate.comment = original.comment
            for info in original.infolist():
                candidate.writestr(copy.copy(info), libraries.get(info.filename, original.read(info.filename)))
        with zipfile.ZipFile(archive) as candidate:
            unchanged = {name: original.read(name) == candidate.read(name) for name in original.namelist() if name not in libraries}
            if not all(unchanged.values()) or candidate.namelist() != original.namelist():
                raise RuntimeError("Non-target AAR entries changed")
    for algorithm in ("sha1", "md5"):
        (output / (archive.name + "." + algorithm)).write_text(hashlib.new(algorithm, archive.read_bytes()).hexdigest(), encoding="ascii")
    result = {"passed": True, "sourceCommit": manifest["sourceCommit"], "sourcePatchSha256": manifest["sourcePatchSha256"],
              "originalAarSha256": sha(original_bytes), "candidateAarSha256": sha(archive.read_bytes()),
              "abis": report, "unchangedOtherMembers": unchanged, "commands": commands,
              "toolchain": toolchain,
              "limitations": ["No Google sign-in/runtime device/16KB device/Play acceptance test", "All dynsym identity is not claimed; full differences are recorded", "Build paths/tool details can affect build ID and output hash"]}
    (output / "verification.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": True, "output": str(archive), "sha256": result["candidateAarSha256"]}))


if __name__ == "__main__":
    main()
