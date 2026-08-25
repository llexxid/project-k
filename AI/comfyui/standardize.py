# -*- coding: utf-8 -*-
"""신 캐릭터 아트 폴더 표준화 — 모든 신에게 같은 이름/경로 규칙을 적용한다.

표준 (신 하나당):

  AI/comfyui/<god>/                     폴더명은 소문자
    <god>_spec.py                       팔레트 + 프롬프트 (단일 출처)
    build_*.py                          워크플로 빌더
    finalize.py                         도트 마감 + 검증
    workflows/                          생성된 워크플로 JSON (중간 산출물)
    output/                             납품물만. 합성 시트는 절대 넣지 않는다.
      <God>_00_Master.png               내부 기준. 게임에 쓰지 않는다.
      <God>_01_Standing_HD.png          스탠딩 HD        RGB  1024x1536
      <God>_01_Standing_Pixel.png       스탠딩 도트      RGB  1024x1536
      <God>_02_Cutscene_HD.png          컷씬 HD(크로마그린) RGB  1152x2048
      <God>_02_Cutscene_Cutout.png      컷씬 풀해상도 투명  RGBA 1152x2048
      <God>_02_Cutscene_Pixel.png       컷씬 도트        RGBA 1152x2048
    output/폐기된_아트/<날짜>_<사유>/     폐기본. RAW 는 지우지 않는다.

규칙
  - 확장자는 항상 .png. 도트본은 논리해상도 x4 정수배.
  - 신 이름은 파일명에서 첫 글자만 대문자 (Ignis, Astra).
  - 번호 00/01/02 는 용도 고정: 00 마스터, 01 스탠딩, 02 컷씬.
  - HD / Pixel / Cutout 접미사만 쓴다. Raw / Final 같은 옛 표기는 쓰지 않는다.
  - 여러 이미지를 붙인 리뷰 시트는 output 에 두지 않는다 (스크래치패드에서만 만든다).

  python standardize.py          미리보기
  python standardize.py --apply  실제 이동
"""
import os
import shutil
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
APPLY = "--apply" in sys.argv

# 신별 현재 파일 -> 표준 이름
RENAME = {
    "astra": {
        "00_Astra_Master_Raw.png": "Astra_00_Master.png",
        "01_Astra_Codex_Raw.png": "Astra_01_Standing_HD.png",
        "01_Astra_Codex_Pixel_Final.png": "Astra_01_Standing_Pixel.png",
        "02_Astra_Ultimate_Raw_A.png": "Astra_02_Cutscene_HD.png",
        "02_Astra_Ultimate_Raw_Cutout_A.png": "Astra_02_Cutscene_Cutout.png",
        "02_Astra_Ultimate_Pixel_Final.png": "Astra_02_Cutscene_Pixel.png",
    },
    "ignis": {
        "00_Ignis_Master_Raw.png": "Ignis_00_Master.png",
        "01_Ignis_Codex_Raw.png": "Ignis_01_Standing_HD.png",
        "01_Ignis_Codex_Pixel_Final.png": "Ignis_01_Standing_Pixel.png",
        "02_Ignis_Ultimate_Raw_C.png": "Ignis_02_Cutscene_HD.png",
        "02_Ignis_Ultimate_Raw_Cutout_C.png": "Ignis_02_Cutscene_Cutout.png",
        "02_Ignis_Ultimate_Pixel_Final.png": "Ignis_02_Cutscene_Pixel.png",
    },
}

DISCARD = "폐기된_아트"


def move(src, dst, why):
    rel = os.path.relpath(src, ROOT).replace("\\", "/")
    rd = os.path.relpath(dst, ROOT).replace("\\", "/")
    print("  %-58s -> %s   (%s)" % (rel, rd, why))
    if APPLY:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.move(src, dst)


def main():
    if os.name == "nt":
        try:
            sys.stdout.reconfigure(encoding="utf-8")
        except Exception:
            pass
    print("MODE:", "APPLY" if APPLY else "DRY RUN")
    for god, table in RENAME.items():
        gdir = os.path.join(ROOT, god)
        if not os.path.isdir(gdir):
            continue
        out = os.path.join(gdir, "output")
        disc = os.path.join(out, DISCARD)
        print("\n[%s]" % god)

        # 1) 워크플로 JSON 을 workflows/ 로
        wf = os.path.join(gdir, "workflows")
        for f in sorted(os.listdir(gdir)):
            if f.endswith(".json"):
                move(os.path.join(gdir, f), os.path.join(wf, f), "워크플로")

        # 2) 프로젝트에 남은 합성 리뷰 시트는 폐기 폴더로
        for f in sorted(os.listdir(gdir)):
            if f.endswith(".png") and ("review" in f or "final" in f.lower()):
                move(os.path.join(gdir, f),
                     os.path.join(disc, "합성시트", f), "합성 시트")

        # 3) 납품물 표준 이름으로
        if os.path.isdir(out):
            for old, new in table.items():
                p = os.path.join(out, old)
                if os.path.exists(p):
                    move(p, os.path.join(out, new), "납품물")

            # 4) archive -> 폐기된_아트
            a = os.path.join(out, "archive")
            if os.path.isdir(a):
                move(a, os.path.join(disc, "이전_반복본"), "아카이브 통합")

            # 5) 표준에 없는 잔여 png 는 폐기 폴더로
            for f in sorted(os.listdir(out)):
                p = os.path.join(out, f)
                if os.path.isfile(p) and f.endswith(".png") and f not in table.values():
                    move(p, os.path.join(disc, "미분류", f), "표준 외")

    print("\n" + ("완료" if APPLY else "미리보기만 함. 적용하려면 --apply"))


if __name__ == "__main__":
    main()
