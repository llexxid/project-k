# -*- coding: utf-8 -*-
"""검신 직진성·테이퍼 수치 검증.

눈으로 "곧아 보인다"는 여러 번 틀렸다. 검신을 회전시켜 세운 뒤 행별 중심선을 뽑아
직선 피팅하고 최대 편차를 잰다. 폭 프로파일로 테이퍼 시작 지점도 같이 낸다.

  python check_blade.py <png> <x_guard> <y_guard> <x_tip> <y_tip> [half_width]

검신은 '어두운 배경 위의 달궈진 금속'이라 밝기+온기로 잡되, 가드→검끝을 잇는 축
주변 좁은 띠 안으로 한정한다. 그래야 불꽃 의상을 안 집어온다.

판정: 최대 편차 <= 검신 길이의 1% 이면 곧은 것으로 본다.
      테이퍼는 마지막 20% 안에서 시작해야 한다.
"""
import math
import sys

import numpy as np
from PIL import Image


def measure(path, p0, p1, half=34, thr=70):
    im = Image.open(path).convert("RGB")
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    length = math.hypot(dx, dy)
    ang = math.degrees(math.atan2(dx, dy))       # 축을 세로로 세울 회전각

    # 축이 세로가 되도록 이미지를 회전 (p0 를 중심으로)
    rot = im.rotate(-ang, resample=Image.BICUBIC, center=p0)
    a = np.asarray(rot, float)
    x0 = int(round(p0[0]))
    y0, y1 = int(round(p0[1])), int(round(p0[1] + length))
    band = a[y0:y1, max(0, x0 - half):x0 + half]
    if band.size == 0:
        return None

    lum = band.max(2)
    warm = (band[..., 0] - band[..., 2]) > 25
    m = (lum > thr) & warm

    rows, cx, wid = [], [], []
    for r in range(m.shape[0]):
        xs = np.where(m[r])[0]
        if len(xs) < 3:
            continue
        brk = np.where(np.diff(xs) > 5)[0]
        segs = [g for g in np.split(xs, brk + 1) if len(g) >= 3]
        if not segs:
            continue
        g = max(segs, key=len)                    # 띠 안에서는 검신이 가장 굵다
        rows.append(r)
        cx.append(g.mean())
        wid.append(len(g))
    if len(rows) < 60:
        return None

    rows = np.array(rows, float)
    cx = np.array(cx, float)
    wid = np.array(wid, float)

    k, b = np.polyfit(rows, cx, 1)
    dev = np.abs(cx - (k * rows + b)).max()
    span = rows.max() - rows.min()

    w = np.convolve(wid, np.ones(11) / 11, "same")
    full = np.percentile(w, 85)
    below = w < full * 0.8
    start = len(w)
    for i in range(len(w)):
        if below[i:].all():
            start = i
            break
    taper = (len(w) - start) / len(w) * 100
    return span, dev, dev / span * 100, taper, full


def main():
    path = sys.argv[1]
    p0 = (float(sys.argv[2]), float(sys.argv[3]))
    p1 = (float(sys.argv[4]), float(sys.argv[5]))
    half = int(sys.argv[6]) if len(sys.argv) > 6 else 34
    r = measure(path, p0, p1, half)
    name = path.replace("\\", "/").split("/")[-1]
    if r is None:
        print("%-30s blade not found" % name)
        return
    span, dev, pct, taper, full = r
    print("%-30s len %4.0fpx  width %3.0fpx | bow %5.1fpx (%.2f%%) %-4s | "
          "taper last %4.1f%% %s"
          % (name, span, full, dev, pct, "OK" if pct <= 1.0 else "BENT",
             taper, "OK" if taper <= 20 else "EARLY"))


if __name__ == "__main__":
    main()
