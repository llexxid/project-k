# -*- coding: utf-8 -*-
"""IGNIS 프로덕션 워크플로 빌더.

구조는 Astra 와 동일하다:
  [10] MASTER (t2i)
    +-- [20] CODEX  (master 참조 편집) -> 그래프 안 픽셀 체인 -> FINAL
    +-- [30]/[40] ULTIMATE 2시드 (master 참조 편집, 크로마 그린) -> RAW 만
                  마감은 로컬 finalize (그린키 + 팔레트 스냅 + 알파 이진화)

컷씬을 2시드로 뽑는 이유: Astra 때 한 시드가 뻗은 손이 프레임 우측에 33px 걸려 잘렸다.
프레이밍은 시드 운이라 두 장 뽑고 가장자리 접촉을 수치로 재서 고르는 편이 싸다.
"""
import json
import io
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ROOT)
from ignis_spec import MASTER, CODEX, ULTIMATE  # noqa: E402

sys.path.insert(0, os.path.dirname(ROOT))   # AI/comfyui
from _shared.workflow_json import convert, SCHEMA  # noqa: E402


def gpt(prompt, size, images=None, seed=0, quality="high"):
    node = {
        "class_type": "OpenAIGPTImageNodeV2",
        "inputs": {
            "prompt": prompt,
            "model": "gpt-image-2",
            "model.size": size,
            "model.custom_width": 1024,
            "model.custom_height": 1024,
            "model.background": "opaque",
            "model.quality": quality,
            "n": 1,
            "seed": seed,
        },
    }
    if images:
        node["inputs"]["model.images.image_1"] = images
    return node


def build():
    api = {}
    # --- MASTER : 내부 기준 이미지 ---
    api["10"] = gpt(MASTER, "1024x1536", seed=20260910)
    api["11"] = {"class_type": "SaveImage", "inputs": {
        "images": ["10", 0], "filename_prefix": "Ignis/00_Ignis_Master_Raw"}}
    api["12"] = {"class_type": "PreviewImage", "inputs": {"images": ["10", 0]}}

    # --- CODEX : master 참조 편집 + 그래프 안 픽셀 체인 ---
    api["20"] = gpt(CODEX, "1024x1536", images=["10", 0], seed=20260911)
    api["21"] = {"class_type": "SaveImage", "inputs": {
        "images": ["20", 0], "filename_prefix": "Ignis/01_Ignis_Codex_Raw"}}
    api["22"] = {"class_type": "PreviewImage", "inputs": {"images": ["20", 0]}}
    # ChangeChannelCount(RGB) 가 먼저 와야 한다 — gpt-image-2 는 opaque 여도 4채널을
    # 내보내고 코어 ImageQuantize 는 RGBA 입력에서 터진다.
    api["23"] = {"class_type": "Change Channel Count", "inputs": {
        "image": ["20", 0], "kind": "RGB"}}
    api["24"] = {"class_type": "ImageScale", "inputs": {
        "image": ["23", 0], "upscale_method": "area",
        "width": 256, "height": 384, "crop": "disabled"}}
    api["25"] = {"class_type": "ImageQuantize", "inputs": {
        "image": ["24", 0], "colors": 32, "dither": "none"}}
    api["26"] = {"class_type": "ImageScale", "inputs": {
        "image": ["25", 0], "upscale_method": "nearest-exact",
        "width": 1024, "height": 1536, "crop": "disabled"}}
    api["27"] = {"class_type": "SaveImage", "inputs": {
        "images": ["26", 0], "filename_prefix": "Ignis/01_Ignis_Codex_Pixel_Final"}}
    api["28"] = {"class_type": "PreviewImage", "inputs": {"images": ["26", 0]}}

    # --- ULTIMATE : 크로마 그린, RAW 만. 마감은 로컬. ---
    # 프레이밍(잘림·구도)은 시드 운이라 3장 뽑고 가장자리 접촉 픽셀 수로 고른다.
    for i, (nid, sid, seed) in enumerate([("30", "31", 20260912),
                                          ("40", "41", 20260913),
                                          ("50", "51", 20260914)]):
        api[nid] = gpt(ULTIMATE, "1152x2048", images=["10", 0], seed=seed)
        api[sid] = {"class_type": "SaveImage", "inputs": {
            "images": [nid, 0],
            "filename_prefix": f"Ignis/02_Ignis_Ultimate_Raw_{'ABC'[i]}"}}
    return api


POS_EXTRA = {"30": (40, 1120), "31": (520, 1120), "40": (40, 1560), "41": (520, 1560)}


def main():
    api = build()
    io.open(os.path.join(ROOT, "Ignis_Production_Workflow_API.json"), "w",
            encoding="utf-8").write(json.dumps(api, ensure_ascii=False, indent=1))

    import api_to_ui
    api_to_ui.POS.update(POS_EXTRA)
    ui = convert(api)
    io.open(os.path.join(ROOT, "Ignis_Production_Workflow_UI.json"), "w",
            encoding="utf-8").write(json.dumps(ui, ensure_ascii=False, indent=1))

    md = [
        "# Ignis 프로덕션 프롬프트\n\n",
        "- 생성 모델: `OpenAIGPTImageNodeV2` / `gpt-image-2` / quality `high`\n",
        "- 마스터 1024x1536 → 도감 1024x1536(edit), 컷씬 1152x2048(edit, 크로마 그린)\n",
        "- 디자인: 붉은 장발 / 여성형 / 불꽃 무희복 / 연한 갈색 피부 / 홍안 / 화염 롱소드\n",
    ]
    for title, prompt in [("1. MASTER", MASTER), ("2. CODEX (도감)", CODEX),
                          ("3. ULTIMATE (궁극기 컷씬)", ULTIMATE)]:
        md += ["\n## ", title, "\n\n```text\n", prompt, "\n```\n"]
    io.open(os.path.join(ROOT, "Ignis_Prompts.md"), "w", encoding="utf-8").write("".join(md))

    ids = {n["id"] for n in ui["nodes"]}
    dangling = [l for l in ui["links"] if l[1] not in ids or l[3] not in ids]
    api_links = sum(1 for n in api.values() for v in n["inputs"].values()
                    if isinstance(v, list) and len(v) == 2 and isinstance(v[0], str))
    print("API nodes:", len(api), "| UI nodes:", len(ui["nodes"]),
          "| links:", len(ui["links"]), "/", api_links, "| dangling:", len(dangling))
    assert len(ui["nodes"]) == len(api) and len(ui["links"]) == api_links and not dangling
    print("OK")


if __name__ == "__main__":
    main()
