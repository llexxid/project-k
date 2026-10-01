"""Compose the normal Lightning icon from its actual runtime sprite frames.

This extends the project's deterministic actual-VFX icon composition pipeline
(20260918-playability/derive_icons.py), rather than redrawing the effect.
Only Lightning.png is installed; its existing meta and bloom icon are preserved.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import shutil

from PIL import Image, ImageDraw, ImageFont, __version__ as PILLOW_VERSION

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
SOURCE = ROOT / "Assets/_Project/Art/VFX/MageTower/Refined/LightningOriginal.png"
DEST = ROOT / "Assets/_Project/Art/Icons/MageTower/Lightning.png"
BLOOM = DEST.with_name("Lightning_Bloom.png")
PREFAB = ROOT / "Assets/_Project/Prefabs/VFX/MageTower/Lightning.prefab"
ANIMATION = ROOT / "Assets/_Project/Art/Animations/VFX/MageTower/Lightning_0.anim"
SOURCE_GUID = "f1fd6d3372c18374da5fdf904e2b144b"
ICON_GUID = "75501532964f34f4884b1f721d69e3ae"
PALETTE = [(15, 172, 241), (19, 166, 255), (25, 255, 255),
           (132, 251, 247), (151, 255, 255), (255, 255, 255)]
# Each crop is derived from its actual SpriteSheet cell and opaque bounds.
# The center is the first impact frame; the flanking frames give a quick rhythm.
LAYERS = [
    {"frame": 1, "x": 3, "y": 5, "height": 35},
    {"frame": 2, "x": 33, "y": 8, "height": 36},
    {"frame": 0, "x": 16, "y": 3, "height": 42},
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(path):
    return path.relative_to(ROOT).as_posix()


def pixels(image):
    return list(image.get_flattened_data())


def frame(sheet, number):
    cell = sheet.crop((number * 70, 0, (number + 1) * 70, 149))
    cell.putalpha(cell.getchannel("A").point(lambda a: 255 if a >= 128 else 0))
    bounds = cell.getbbox()
    return cell.crop(bounds), list(bounds)


def compose(coverage=True):
    source = Image.open(SOURCE).convert("RGBA")
    canvas = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    records = []
    for layer in LAYERS:
        sprite, crop = frame(source, layer["frame"])
        height = layer["height"]
        width = round(sprite.width * height / sprite.height)
        # A subpixel-width source core would flicker or vanish under plain
        # nearest subsampling. Each icon pixel instead selects an actual opaque
        # source pixel from its footprint, preserving the thin strike path.
        sampled = sprite.resize((width, height), Image.Resampling.NEAREST)
        if coverage:
            for ty in range(height):
                for tx in range(width):
                    footprint = sprite.crop((math.floor(tx * sprite.width / width),
                                             math.floor(ty * sprite.height / height),
                                             math.ceil((tx + 1) * sprite.width / width),
                                             math.ceil((ty + 1) * sprite.height / height)))
                    sampled.putpixel((tx, ty), max(pixels(footprint), key=lambda p: (p[3], sum(p[:3]))))
        # Sample only colors already present in the effect. No invented tint,
        # outline, cloud, sparks, or bolt geometry is introduced.
        sampled.putdata([
            min(PALETTE, key=lambda c: sum((p[k] - c[k]) ** 2 for k in range(3))) + (255,)
            if p[3] else (0, 0, 0, 0)
            for p in pixels(sampled)
        ])
        canvas.alpha_composite(sampled, (layer["x"], layer["y"]))
        records.append({**layer, "sourceOpaqueBoundsInCell": crop,
                        "targetWidth": width, "sampleMethod": "highest-alpha-then-brightness source coverage" if coverage else "NEAREST"})
    return canvas, records


def font(size, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf" if bold else
                              "C:/Windows/Fonts/malgun.ttf", size)


def preview(icon, before, target):
    sheet = Image.new("RGB", (1120, 650), "#151c26")
    draw = ImageDraw.Draw(sheet)
    draw.rounded_rectangle((30, 28, 105, 87), 12, fill="#1b687d")
    draw.text((45, 33), "91", font=font(34, True), fill="#efffff")
    draw.text((127, 27), "일반 라이트닝 · 세 번의 낙뢰", font=font(30, True), fill="#f2f5f6")
    draw.text((128, 70), "실제 청록색 스킬 스프라이트로 구성", font=font(18), fill="#a9bccd")
    for box in [(30, 118, 360, 530), (384, 118, 744, 530), (768, 118, 1090, 530)]:
        draw.rounded_rectangle(box, 14, fill="#202a36", outline="#334453", width=1)
    draw.text((52, 139), "기존 아이콘", font=font(20, True), fill="#9eafbf")
    draw.text((407, 139), "개선 아이콘", font=font(20, True), fill="#dbfcff")
    draw.text((790, 139), "실제 표시 크기", font=font(20, True), fill="#dbfcff")
    old = Image.open(before).convert("RGBA").resize((240, 240), Image.Resampling.NEAREST)
    sheet.paste(old, (75, 218), old)
    large = icon.resize((288, 288), Image.Resampling.NEAREST)
    sheet.paste(large, (420, 195), large)
    for scale, x, y, label in [(1, 803, 228, "48 px"), (2, 921, 204, "96 px")]:
        size = 48 * scale
        draw.rounded_rectangle((x - 12, y - 12, x + size + 12, y + size + 12),
                               8, fill="#141d27", outline="#60727a", width=2)
        result = icon.resize((size, size), Image.Resampling.NEAREST)
        sheet.paste(result, (x, y), result)
        draw.text((x, y + size + 23), label, font=font(16), fill="#a9bccd")
    draw.text((791, 380), "청록 / 흰색 원본 팔레트", font=font(17), fill="#a9bccd")
    draw.text((791, 415), "세 낙뢰의 간격과 높이 차", font=font(17), fill="#a9bccd")
    draw.text((791, 450), "48 × 48 · 투명 배경", font=font(17), fill="#a9bccd")
    draw.text((33, 557), "원본: LightningOriginal · 기본 스킬의 0 / 1 / 2 프레임", font=font(19), fill="#c1d2df")
    draw.text((33, 593), "중앙 타격을 강조하고 양옆의 낙뢰를 엇갈려 배치했습니다.", font=font(18), fill="#8fa7bc")
    sheet.save(target)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview-dir", type=Path)
    args = parser.parse_args()
    before = HERE / "before-Lightning.png"
    if not before.exists():
        shutil.copy2(DEST, before)
    meta_path = Path(str(DEST) + ".meta")
    meta_before, bloom_before, source_before = digest(meta_path), digest(BLOOM), digest(SOURCE)
    assert SOURCE_GUID in PREFAB.read_text(encoding="utf-8")
    assert SOURCE_GUID in ANIMATION.read_text(encoding="utf-8")
    assert ICON_GUID in meta_path.read_text(encoding="utf-8")
    icon, layers = compose()
    rejected, _ = compose(coverage=False)
    rejected.save(HERE / "rejected-nearest.png")
    out = HERE / "Lightning.png"
    icon.save(out)
    shutil.copy2(out, DEST)
    preview_path = HERE / "91-lightning-preview.png"
    preview(icon, before, preview_path)
    if args.preview_dir:
        args.preview_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy2(preview_path, args.preview_dir / preview_path.name)
    unique = set(pixels(icon))
    assert len(unique) <= 16
    assert {p[3] for p in unique} == {0, 255}
    assert all((icon.getbbox()[i] >= 3 if i < 2 else icon.getbbox()[i] <= 45) for i in range(4))
    assert digest(meta_path) == meta_before
    assert digest(BLOOM) == bloom_before
    assert digest(SOURCE) == source_before
    manifest = {
        "date": "2026-10-01", "previewNumber": 91,
        "pipeline": "Existing actual-VFX code composition: sprite crop / aspect-preserving source coverage sampling / source palette / binary alpha",
        "priorPipeline": "AI/comfyui/mage-skills/20260918-playability/derive_icons.py",
        "toolChoice": "The user requested the actual normal skill sprite. A parameterized extension of the existing sprite-composition generator preserves its exact authored geometry without model redrawing. This is a code-native composition artifact, not generated replacement artwork.",
        "runtimeProof": {"prefab": relative(PREFAB), "animation": relative(ANIMATION),
                         "spriteGuid": SOURCE_GUID, "frameInternalIds": [-855562806, -1838817195, -166954139]},
        "source": {"path": relative(SOURCE), "size": [350, 149], "cellSize": [70, 149], "sha256": source_before},
        "settings": {"logicalSize": [48, 48], "alphaThreshold": 128,
                     "paletteFromSource": PALETTE, "layersBackToFront": layers,
                     "outline": False, "redrawnEffectPixels": False},
        "output": {"path": relative(DEST), "sha256": digest(DEST), "bytes": DEST.stat().st_size,
                   "rgbaColorsIncludingTransparent": len(unique), "opaqueBounds": list(icon.getbbox()),
                   "uncompressedTextureBytes": 48 * 48 * 4, "newDrawCalls": 0},
        "preservation": {"iconGuid": ICON_GUID, "iconMetaSha256": meta_before,
                         "bloomPath": relative(BLOOM), "bloomSha256": bloom_before},
        "before": {"path": relative(before), "sha256": digest(before)},
        "iteration": {"rejected": relative(HERE / "rejected-nearest.png"),
                      "changedSetting": "sampleMethod: NEAREST -> highest-alpha source coverage",
                      "reason": "Visual inspection at 48px showed nearest subsampling removed thin source cores, leaving intermittent specks. Source coverage retains those paths using only source pixels.",
                      "retainedSettings": "Same original frames, aspect ratios, positions, palette and alpha threshold"},
        "preview": relative(preview_path),
        "execution": {"python": __import__("sys").version.split()[0], "Pillow": PILLOW_VERSION,
                      "model": None, "prompt": None, "seed": "deterministic",
                      "cloudJob": None, "submittedApiJson": None, "uiGraph": None,
                      "paidCalls": 0, "actualExternalGenerationSpendUSD": 0,
                      "comfyAvailability": "No callable ComfyUI tools exposed in this task's live tool catalog; no Cloud workflow submitted.",
                      "command": "python AI/comfyui/mage-skills/20261001-lightning/compose_icon.py --preview-dir <outputs>"},
        "checks": {"sourceAndBloomUnmodified": True, "metaGuidUnmodified": True,
                   "binaryAlpha": True, "paletteLimit16": True, "marginAtLeast3px": True,
                   "unityImportAndAndroid": "Pending main-agent verification"},
    }
    (HERE / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest["output"]))


if __name__ == "__main__":
    main()
