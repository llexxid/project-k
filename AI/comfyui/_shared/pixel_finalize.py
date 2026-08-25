# -*- coding: utf-8 -*-
"""도트 마감 파이프라인 — 전 캐릭터 공용.

축소(BOX) -> 고정 팔레트 스냅 -> 고립 픽셀 제거 -> 톤 통일 -> 알파 이진화 -> 정수배 NEAREST.

팔레트는 캐릭터마다 다르므로 반드시 인자로 받는다. 여기에 특정 신의 팔레트를 두지 않는다.
despeckle / unify_colors 대상 색도 호출부가 정한다 (보통 피부 램프 + 백열색).
"""
import numpy as np
from PIL import Image


def hex_to_rgb(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def palette_array(palette):
    """헥스 리스트 -> (N,3) int16. 팔레트는 캐릭터마다 다르므로 기본값을 두지 않는다."""
    return np.array([hex_to_rgb(h) for h in palette], dtype=np.int16)


def snap_to_palette(rgb_u8, pal):
    """가장 가까운 팔레트 색으로 치환. 거리는 지각 가중 (녹색에 민감한 눈 보정)."""
    h, w, _ = rgb_u8.shape
    flat = rgb_u8.reshape(-1, 1, 3).astype(np.int16)
    diff = flat - pal.reshape(1, -1, 3)
    w_rgb = np.array([0.30, 0.59, 0.11], dtype=np.float32)
    dist = (diff.astype(np.float32) ** 2 * w_rgb).sum(axis=2)
    idx = dist.argmin(axis=1)
    return pal[idx].astype(np.uint8).reshape(h, w, 3)


def despeckle(rgb_u8, colors, max_same=1, mask=None):
    """고립 픽셀 제거 — 축소+팔레트 스냅 뒤 남는 '반점'을 없앤다.

    피부의 스페큘러 하이라이트는 축소되면 1~2px 짜리 아주 밝은 점으로 남고, 고정
    팔레트로 스냅되면 주변 살색과 몇 단계 떨어진 색이 되어 반점처럼 읽힌다.
    HD 원본의 광택은 그대로 두고 도트에서만 정리하는 것이 목적이다.

    colors  : 정리 대상 색 목록 (RGB 튜플). 피부 램프 + 백열색만 넣는다 —
              전체 색에 적용하면 룬 글리프 같은 의도된 1px 디테일이 날아간다.
    max_same: 같은 색 이웃이 이 수 이하이면 고립으로 본다 (1 = 1~2px 덩어리).
    """
    h, w, _ = rgb_u8.shape
    key = (rgb_u8[:, :, 0].astype(np.int32) << 16 |
           rgb_u8[:, :, 1].astype(np.int32) << 8 |
           rgb_u8[:, :, 2].astype(np.int32))

    target = np.zeros((h, w), bool)
    for c in colors:
        target |= key == (int(c[0]) << 16 | int(c[1]) << 8 | int(c[2]))
    if mask is not None:
        target &= mask
    if not target.any():
        return rgb_u8, 0

    pad = np.pad(key, 1, mode="edge")
    # 마스크 밖(투명 배경) 픽셀은 투표에서 제외한다. 넣으면 실루엣 가장자리의 피부가
    # 배경색을 집어와 검은 점이 박힌다 — 실측으로 확인된 버그.
    valid = np.ones((h, w), bool) if mask is None else mask
    vpad = np.pad(valid, 1, constant_values=False)

    same = np.zeros((h, w), np.int16)
    neigh, nvalid = [], []
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy == 0 and dx == 0:
                continue
            n = pad[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
            v = vpad[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
            same += (n == key) & v
            neigh.append(n)
            nvalid.append(v)

    orphan = target & (same <= max_same)
    if not orphan.any():
        return rgb_u8, 0

    stack = np.stack(neigh, 0)
    vstack = np.stack(nvalid, 0)
    out = rgb_u8.copy()
    ys, xs = np.where(orphan)
    n_done = 0
    for y, x in zip(ys, xs):
        vals = stack[:, y, x][vstack[:, y, x]]
        if len(vals) == 0:
            continue
        u, c = np.unique(vals, return_counts=True)
        m = int(u[c.argmax()])
        out[y, x] = ((m >> 16) & 255, (m >> 8) & 255, m & 255)
        n_done += 1
    return out, n_done


def unify_colors(rgb_u8, colors, mask=None, iters=1):
    """지정한 색 무리 안에서만 3x3 최빈값 필터 — 얼룩덜룩한 톤을 하나로 모은다.

    despeckle 은 완전히 고립된 1~2px 만 지운다. 광택이 넓게 퍼진 피부에서는 서로 다른
    피부 단계가 3~5px 덩어리로 섞여 남아 여전히 얼룩으로 읽힌다. 이 필터는 피부 픽셀
    각각을 주변 3x3 안에서 가장 많은 피부색으로 바꾼다. 큰 명암 영역과 그 경계는
    그대로 두면서 소수 톤만 흡수되므로 셰이딩이 무너지지 않는다.

    colors 에 피부 램프만 넣는다 — 불꽃이나 머리카락에 걸면 디테일이 뭉갠다.
    """
    h, w, _ = rgb_u8.shape
    key = (rgb_u8[:, :, 0].astype(np.int32) << 16 |
           rgb_u8[:, :, 1].astype(np.int32) << 8 |
           rgb_u8[:, :, 2].astype(np.int32))
    keys = [int(c[0]) << 16 | int(c[1]) << 8 | int(c[2]) for c in colors]

    target = np.zeros((h, w), bool)
    for k in keys:
        target |= key == k
    if mask is not None:
        target &= mask
    if not target.any():
        return rgb_u8, 0

    out = rgb_u8.copy()
    changed = 0
    for _ in range(max(1, iters)):
        cur = (out[:, :, 0].astype(np.int32) << 16 |
               out[:, :, 1].astype(np.int32) << 8 |
               out[:, :, 2].astype(np.int32))
        counts = np.zeros((len(keys), h, w), np.int16)
        for i, k in enumerate(keys):
            m = ((cur == k) & target).astype(np.int16)
            pad = np.pad(m, 1)
            acc = np.zeros((h, w), np.int16)
            for dy in (0, 1, 2):
                for dx in (0, 1, 2):
                    acc += pad[dy:dy + h, dx:dx + w]
            counts[i] = acc
        best = counts.argmax(0)
        new = np.array(keys, dtype=np.int32)[best]
        upd = target & (new != cur)
        changed += int(upd.sum())
        ys, xs = np.where(upd)
        v = new[ys, xs]
        out[ys, xs, 0] = (v >> 16) & 255
        out[ys, xs, 1] = (v >> 8) & 255
        out[ys, xs, 2] = v & 255
    return out, changed


def finalize(img: Image.Image, logical_w: int, logical_h: int, scale: int,
             palette, keep_alpha=True, despeckle_colors=None,
             despeckle_max_same=1, unify=None, unify_iters=1) -> Image.Image:
    """축소 → 팔레트 스냅 → 정수배 최근접 확대. 반투명 테두리를 남기지 않는다."""
    has_alpha = keep_alpha and img.mode in ("RGBA", "LA", "P")
    img = img.convert("RGBA") if has_alpha else img.convert("RGB")

    # ① 축소: BOX(면적 평균)가 형태를 가장 덜 무너뜨린다. NEAREST 는 가는 선을 통째로 날린다.
    small = img.resize((logical_w, logical_h), Image.BOX)
    arr = np.asarray(small)

    if has_alpha:
        rgb, a = arr[:, :, :3], arr[:, :, 3]
    else:
        rgb, a = arr[:, :, :3], None

    # ② 고정 팔레트 스냅 (디더링 없음 — 도트 아트에서 디더는 노이즈다)
    rgb = snap_to_palette(rgb, palette_array(palette))

    # ②-b 고립 픽셀 정리: 광택이 만든 1~2px 반점만 주변 색으로 흡수시킨다.
    if despeckle_colors:
        m = (a > 0) if a is not None else None
        rgb, n = despeckle(rgb, despeckle_colors, despeckle_max_same, m)
        if n:
            print("    despeckle : %d px 흡수" % n)

    # ②-c 톤 통일: 피부처럼 넓은 면에서 소수 톤이 얼룩으로 남는 것을 흡수한다.
    if unify:
        m = (a > 0) if a is not None else None
        rgb, n = unify_colors(rgb, unify, m, unify_iters)
        if n:
            print("    unify     : %d px 통일" % n)

    if a is not None:
        # ③ 알파 이진화: 반투명 테두리를 남기지 않는다는 요구사항
        a = np.where(a >= 128, 255, 0).astype(np.uint8)
        rgb = np.where(a[:, :, None] > 0, rgb, 0)
        out = np.dstack([rgb, a])
        mode = "RGBA"
    else:
        out = rgb
        mode = "RGB"

    # ④ 정확한 정수배 최근접 확대 → 픽셀 블록이 균일해진다
    return Image.fromarray(out, mode).resize(
        (logical_w * scale, logical_h * scale), Image.NEAREST)
