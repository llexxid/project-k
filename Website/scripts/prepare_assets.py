"""Encode existing project artwork for the website; never modify source assets."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, __version__ as pillow_version

SITE = Path(__file__).resolve().parents[1]
OUT = SITE / 'public' / 'assets'
ROOT = SITE.parent


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    artwork = "Assets/UGUI/Art/Lobby/Lobby_Background.png"
    operations = [
        (artwork, "kingdom-hero-1536.webp", (0, 240, 1536, 1104), (1536, 864), False),
        (artwork, "kingdom-hero-1024.webp", (0, 240, 1536, 1104), (1024, 576), False),
        (artwork, "kingdom-hero-mobile.webp", (192, 0, 1344, 1536), (768, 1024), False),
        ("Assets/UGUI/Art/Lobby/Lobby_Logo_KO.png", "kingdom-logo.webp", None, (768, 539), False),
        ("Docs/QA/ShopRevision20260927/battle-current.png", "gameplay-battle.webp", None, None, True),
        ("Docs/QA/ShopRevision20260927/mage-current.png", "gameplay-mage.webp", None, None, True),
        ("Docs/QA/ShopRevision20260927/equipment-gacha-current.png", "gameplay-equipment.webp", None, None, True),
    ]
    missing = [source for source, *_ in operations if not (ROOT / source).is_file()]
    if missing:
        raise FileNotFoundError("Missing archived source assets: " + ", ".join(missing))
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = {
        "date": "2026-09-27",
        "purpose": "Ludos Interactive local website source asset selection and web optimization",
        "tool": {"name": "Pillow", "version": pillow_version},
        "generation": {"new_images": 0, "paid_jobs": 0, "comfy_catalog_connected": True, "comfy_tool_count": 41},
        "artwork_provenance": "AI/comfyui/lobby/revision5/README.md; currently shipped title background and logo",
        "screenshot_note": "Actual game views rendered in Unity Editor PlayMode on 2026-09-27, version 0.15.0. Isolated local QA fixture; no sign-in or server transaction. These are not Android device captures. No generated or composited game UI.",
        "screenshot_capture": {
            "unity_version": "6000.3.21f1",
            "game_version": "0.15.0",
            "environment": "Unity Editor PlayMode",
            "physical_android_device": False,
            "fixture": "FoundationPlayValidation.RunShopReview",
            "capture_output": "Recordings/ShopRevision/EditorScenes",
            "source_archive": "Docs/QA/ShopRevision20260927",
            "viewport": [1080, 1920],
        },
        "assets": [],
    }
    for source_name, output_name, crop, size, lossless in operations:
        source = ROOT / source_name
        before = digest(source)
        with Image.open(source) as original:
            image = original.copy()
            original_size = original.size
            if output_name.startswith("gameplay-"):
                assert original_size == (1080, 1920), "Update gallery dimensions when changing the capture viewport"
            alpha = image.mode == "RGBA" and image.getextrema()[3][0] < 255
            if crop:
                image = image.crop(crop)
            if size and image.size != size:
                image = image.resize(size, Image.Resampling.LANCZOS)
            output = OUT / output_name
            quality = 100 if lossless else 92 if output_name == "kingdom-logo.webp" else 86
            image.save(output, "WEBP", lossless=lossless, quality=quality, method=6)
            with Image.open(output) as check:
                check.load()
                assert check.size == image.size
                if lossless:
                    assert check.convert("RGBA").tobytes() == image.convert("RGBA").tobytes()
            assert digest(source) == before, "Source asset must stay unchanged"
            manifest["assets"].append({
                "output": output_name,
                "source": source_name,
                "source_sha256": before,
                "source_size": original_size,
                "crop_box": crop,
                "size": image.size,
                "transparent": alpha,
                "webp_lossless": lossless,
                "webp_quality": quality,
                "bytes": output.stat().st_size,
                "sha256": digest(output),
            })
    (SITE / 'content' / 'asset-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"files": len(operations), "bytes": sum(a["bytes"] for a in manifest["assets"]), "source_assets_unchanged": True}))


if __name__ == "__main__":
    main()
