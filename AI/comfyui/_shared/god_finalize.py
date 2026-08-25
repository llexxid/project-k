# -*- coding: utf-8 -*-
"""신 아트 도트 마감 CLI — 전 캐릭터 공용.

캐릭터별로 달라지는 것은 팔레트와 '정리 대상 피부색' 두 가지뿐이다. 둘 다 <god>_spec.py
에서 받아온다. 그래서 각 신의 finalize.py 는 이 모듈을 부르는 열 줄짜리 껍데기다.

  python finalize.py standing  <src.png>  <dst.png>
  python finalize.py cutscene  <src.png>  <dst.png>

경로는 그 신의 output/ 기준 상대 이름이다.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from _shared.pixel_finalize import finalize, palette_array, hex_to_rgb  # noqa: E402
from _shared.chroma_key import key_green                     # noqa: E402

# 논리해상도 x 배율 = 최종 크기. 알파 여부.
SPECS = {
    "standing": (256, 384, 4, False),
    "cutscene": (288, 512, 4, True),
}
ALIASES = {"codex": "standing", "ultimate": "cutscene"}


def verify(path, palette, lw, lh, scale, want_alpha):
    im = Image.open(path)
    arr = np.asarray(im)
    h, w = arr.shape[:2]
    rgb = arr[:, :, :3]
    pal = {tuple(c) for c in palette_array(palette)}

    if want_alpha:
        alpha = arr[:, :, 3]
        opaque = alpha > 0
        binary = set(np.unique(alpha).tolist()) <= {0, 255}
        blocks = alpha.reshape(lh, scale, lw, scale)
    else:
        opaque = np.ones((h, w), bool)
        binary = True
        blocks = rgb[:, :, 0].reshape(lh, scale, lw, scale)

    used = {tuple(c) for c in rgb[opaque]}
    off = used - pal
    uniform = bool((blocks == blocks[:, :1, :, :1]).all())
    ok_size = (w, h) == (lw * scale, lh * scale)

    print("  %s" % os.path.basename(path))
    print("    size      : %dx%d (expected %dx%d) -> %s"
          % (w, h, lw * scale, lh * scale, "OK" if ok_size else "FAIL"))
    print("    mode      : %s  bytes %s" % (im.mode, format(os.path.getsize(path), ",")))
    print("    palette   : %d used, off-palette %d -> %s"
          % (len(used), len(off), "OK" if not off else "FAIL"))
    if want_alpha:
        print("    alpha     : binary %s, opaque %.1f%%"
              % ("OK" if binary else "FAIL", opaque.mean() * 100))
    print("    %dx blocks : %s" % (scale, "uniform OK" if uniform else "NOT uniform FAIL"))
    return ok_size and not off and binary and uniform


def main(root, palette, skin, gloss=None, argv=None):
    """skin=톤 통일 대상, gloss=고립 픽셀 제거 대상. gloss 생략 시 skin 을 쓴다."""
    argv = argv or sys.argv[1:]
    if len(argv) != 3:
        print(__doc__)
        return 2
    mode, src, dst = argv
    mode = ALIASES.get(mode, mode)
    if mode not in SPECS:
        print("mode must be one of:", ", ".join(SPECS))
        return 2

    lw, lh, scale, want_alpha = SPECS[mode]
    # spec 은 읽기 좋게 헥스로 적는다. 내부 필터는 RGB 튜플을 받는다.
    to_rgb = lambda L: [hex_to_rgb(c) if isinstance(c, str) else c for c in L]
    skin_rgb = to_rgb(skin)
    gloss_rgb = to_rgb(gloss) if gloss else skin_rgb
    out = os.path.join(root, "output")
    img = Image.open(os.path.join(out, src))

    if want_alpha:
        rgb = np.asarray(img.convert("RGB"))
        img = Image.fromarray(
            np.dstack([rgb, (key_green(rgb) * 255).astype(np.uint8)]), "RGBA")
        # 키잉 직후 풀해상도본도 남긴다. RAW 는 지우지 않는다.
        img.save(os.path.join(out, src.replace("_HD", "_Cutout")))

    res = finalize(img, lw, lh, scale, palette=palette, keep_alpha=want_alpha,
                   despeckle_colors=gloss_rgb, unify=skin_rgb, unify_iters=3)
    dp = os.path.join(out, dst)
    res.save(dp)
    ok = verify(dp, palette, lw, lh, scale, want_alpha)
    print("\nOK" if ok else "\nFAILED")
    return 0 if ok else 1
