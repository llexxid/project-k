# -*- coding: utf-8 -*-
"""크로마 그린 키잉 — 전 캐릭터 공용.

gpt-image-2 는 model.background='transparent' 를 거부하므로 평면 그린으로 뽑아 여기서 뺀다.
3패스여야 한다: 테두리 flood fill 만 하면 팔·머리카락이 둘러싼 닫힌 구멍에 그린이 남는다.
"""
from collections import deque

import numpy as np


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
