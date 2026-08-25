# -*- coding: utf-8 -*-
"""궁극기 컷씬 마감 — 크로마 그린 제거 → 투명 배경 → 32색 팔레트 고정 → 정수배 확대.

왜 그린스크린인가: `OpenAIGPTImageNodeV2` 는 카탈로그에 `background: transparent` 가
있지만 gpt-image-2 에서는 라이브 검증기가 거부한다("'transparent' is not a valid value").
그래서 평면 크로마 그린으로 뽑고 여기서 키잉한다. Astra 팔레트에는 녹색이 전혀 없어
(인디고/바이올렛/블루/실버/피부) 그린은 충돌 없는 키 색이다.

키잉은 3패스다. 테두리 flood fill 만으로는 팔·머리카락 리본이 만드는 **닫힌 구멍**의
배경이 남고, 가는 머리카락은 그린을 안티에일리어싱해 물고 있어 형광 테두리가 남는다.
"""
import os
import sys
from collections import deque

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "output")
sys.path.insert(0, os.path.join(ROOT, "custom_nodes", "ComfyUI-AstraTools"))
from astra_nodes import finalize, palette_array  # noqa: E402

LOGICAL_W, LOGICAL_H, SCALE = 288, 512, 4


def key_green(rgb):
    h, w, _ = rgb.shape
    r = rgb[:, :, 0].astype(np.int16)
    g = rgb[:, :, 1].astype(np.int16)
    b = rgb[:, :, 2].astype(np.int16)

    # ① 테두리 flood fill — 느슨한 기준, 배경과 이어진 것만
    loose = (g > 110) & (g - np.maximum(r, b) > 60)
    seen = np.zeros((h, w), bool)
    bg = np.zeros((h, w), bool)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if loose[y, x] and not seen[y, x]:
                seen[y, x] = True
                q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if loose[y, x] and not seen[y, x]:
                seen[y, x] = True
                q.append((y, x))
    while q:
        y, x = q.popleft()
        bg[y, x] = True
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and loose[ny, nx] and not seen[ny, nx]:
                seen[ny, nx] = True
                q.append((ny, nx))

    # ② 엄격한 전역 테스트 — 팔과 머리카락이 둘러싼 닫힌 구멍의 순수 그린
    bg |= (g > 170) & (g - np.maximum(r, b) > 110)

    # ③ 디프린지 — 배경에 닿은 옅은 그린 화소를 두 겹 벗긴다
    fringe = (g - np.maximum(r, b)) > 42
    for _ in range(2):
        pad = np.zeros((h + 2, w + 2), bool)
        pad[1:-1, 1:-1] = bg
        touching = (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:])
        peel = fringe & touching & ~bg
        if not peel.any():
            break
        bg |= peel
    return ~bg


def verify(path):
    im = Image.open(path)
    arr = np.asarray(im)
    h, w = arr.shape[:2]
    alpha = arr[:, :, 3]
    rgb = arr[:, :, :3]

    pal = {tuple(c) for c in palette_array()}
    opaque = alpha > 0
    used = {tuple(c) for c in rgb[opaque]}
    off = used - pal
    binary_alpha = set(np.unique(alpha).tolist()) <= {0, 255}

    blocks = alpha.reshape(LOGICAL_H, SCALE, LOGICAL_W, SCALE)
    uniform = bool((blocks == blocks[:, :1, :, :1]).all())
    cov = opaque.mean()

    print(f"  {os.path.basename(path)}")
    print(f"    size        : {w}x{h} (expected {LOGICAL_W*SCALE}x{LOGICAL_H*SCALE})"
          f" -> {'OK' if (w, h) == (LOGICAL_W*SCALE, LOGICAL_H*SCALE) else 'FAIL'}")
    print(f"    mode        : {im.mode}   bytes {os.path.getsize(path):,}")
    print(f"    palette     : {len(used)} used, off-palette {len(off)} -> {'OK' if not off else 'FAIL'}")
    print(f"    alpha       : binary {'OK' if binary_alpha else 'FAIL'}, opaque coverage {cov*100:.1f}%")
    print(f"    {SCALE}x blocks   : {'uniform OK' if uniform else 'NOT uniform FAIL'}")
    return (w, h) == (LOGICAL_W * SCALE, LOGICAL_H * SCALE) and not off and binary_alpha and uniform


def run(src_name, dst_name):
    sp = os.path.join(OUT, src_name)
    src = Image.open(sp).convert("RGB")
    mask = key_green(np.asarray(src))
    rgba = np.dstack([np.asarray(src), (mask * 255).astype(np.uint8)])
    cut = Image.fromarray(rgba, "RGBA")

    # 잘라낸 원본도 보관 (raw 는 지우지 않는다)
    cut.save(os.path.join(OUT, src_name.replace("_Raw", "_Raw_Cutout")))

    res = finalize(cut, LOGICAL_W, LOGICAL_H, SCALE, keep_alpha=True)
    dp = os.path.join(OUT, dst_name)
    res.save(dp)
    return verify(dp)


if __name__ == "__main__":
    ok = True
    for a, b in [(sys.argv[1], sys.argv[2])]:
        ok &= run(a, b)
    print("\nALL OK" if ok else "\nSOME CHECKS FAILED")
    sys.exit(0 if ok else 1)
