# -*- coding: utf-8 -*-
"""다운로드한 RAW 에 Astra 32색 팔레트 고정 마감을 적용한다.

클라우드 그래프의 ImageQuantize 는 적응형이라 이미지마다 색이 달라진다.
도감과 컷씬이 같은 팔레트를 공유해야 하므로, 최종본은 AstraPixelFinalize 로 스냅한다.
(같은 코드가 로컬 ComfyUI 에서는 그래프 안 노드로 돈다.)
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "output")
sys.path.insert(0, os.path.join(ROOT, "custom_nodes", "ComfyUI-AstraTools"))
from astra_nodes import finalize, ASTRA_PALETTE_32, palette_array, _hex_to_rgb  # noqa: E402

import numpy as np  # noqa: E402

JOBS = [
    ("01_Astra_Codex_Raw.png", "01_Astra_Codex_Pixel_Final.png", 256, 384, 4),
    ("02_Astra_Ultimate_Raw.png", "02_Astra_Ultimate_Pixel_Final.png", 288, 512, 4),
]


def verify(path, lw, lh, scale):
    """실제로 검사한다: 크기, 채널, 팔레트 준수, 정수배 픽셀 블록 균일성."""
    im = Image.open(path)
    arr = np.asarray(im.convert("RGB"))
    h, w = arr.shape[:2]
    ok_size = (w, h) == (lw * scale, lh * scale)

    pal = {tuple(c) for c in palette_array()}
    used = {tuple(c) for c in arr.reshape(-1, 3)}
    off = used - pal

    # 정수배 블록 검사: scale x scale 타일 안의 모든 화소가 같은 색이어야 한다
    blocks = arr.reshape(lh, scale, lw, scale, 3)
    uniform = bool((blocks == blocks[:, :1, :, :1]).all())

    print(f"  {os.path.basename(path)}")
    print(f"    size      : {w}x{h}  (expected {lw*scale}x{lh*scale})  -> {'OK' if ok_size else 'FAIL'}")
    print(f"    mode      : {im.mode}  bytes {os.path.getsize(path):,}")
    print(f"    colors    : {len(used)} used / 32 palette, off-palette {len(off)} -> {'OK' if not off else 'FAIL'}")
    print(f"    {scale}x blocks : {'uniform OK' if uniform else 'NOT uniform FAIL'}")
    return ok_size and not off and uniform


def main():
    print(f"palette: {len(ASTRA_PALETTE_32)} colors")
    all_ok = True
    for src, dst, lw, lh, scale in JOBS:
        sp = os.path.join(OUT, src)
        if not os.path.exists(sp):
            print(f"[skip] {src} 없음")
            all_ok = False
            continue
        img = Image.open(sp)
        res = finalize(img, lw, lh, scale, keep_alpha=False)
        dp = os.path.join(OUT, dst)
        res.save(dp)
        all_ok &= verify(dp, lw, lh, scale)
    print("\nALL OK" if all_ok else "\nSOME CHECKS FAILED")
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
