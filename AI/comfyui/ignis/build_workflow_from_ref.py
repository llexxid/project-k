# -*- coding: utf-8 -*-
"""IGNIS 워크플로 — 승인된 마스터를 레퍼런스로 물려 무기만 교체하는 변형.

무기 교체 지시가 들어왔을 때 build_workflow.py (마스터를 t2i 로 새로 생성) 를 그대로
돌리면 의상·신발·배경까지 같이 재추첨된다. 실측으로 확인된 실패다. 그래서 이 변형은
확정된 마스터 PNG 를 업로드해 LoadImage 로 물리고, 마스터 노드마저 참조 편집으로 돌린다.

  [1]  LoadImage (승인된 마스터)
    +-- [10] MASTER  참조 편집: 무기만 창 -> 롱소드
          +-- [20] CODEX  (새 마스터 참조 편집) -> 그래프 안 픽셀 체인 -> FINAL
          +-- [30]/[40]/[50] ULTIMATE 3시드 (새 마스터 참조 편집, 크로마 그린) -> RAW
"""
import json
import io
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ROOT)
from ignis_spec import MASTER_FROM_REF, CODEX, ULTIMATE  # noqa: E402

sys.path.insert(0, os.path.dirname(ROOT))   # AI/comfyui
from _shared import workflow_json as api_to_ui  # noqa: E402
from _shared.workflow_json import convert  # noqa: E402

# 승인된 마스터(v8, 의상·맨발 확정본)의 업로드 파일명
MASTER_UPLOAD = "277590b885e062452f2a286c81afd8a95bc89b3fa2c823df98cce916d81e042c.png"


def gpt(prompt, size, images, seed):
    return {"class_type": "OpenAIGPTImageNodeV2", "inputs": {
        "prompt": prompt, "model": "gpt-image-2", "model.size": size,
        "model.custom_width": 1024, "model.custom_height": 1024,
        "model.background": "opaque", "model.quality": "high", "n": 1, "seed": seed,
        "model.images.image_1": images}}


def build():
    api = {"1": {"class_type": "LoadImage", "inputs": {"image": MASTER_UPLOAD}}}

    api["10"] = gpt(MASTER_FROM_REF, "1024x1536", ["1", 0], 20260930)
    api["11"] = {"class_type": "SaveImage", "inputs": {
        "images": ["10", 0], "filename_prefix": "Ignis/00_Ignis_Master_Raw"}}
    api["12"] = {"class_type": "PreviewImage", "inputs": {"images": ["10", 0]}}

    api["20"] = gpt(CODEX, "1024x1536", ["10", 0], 20260931)
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

    for i, (nid, sid, seed) in enumerate([("30", "31", 20260932),
                                          ("40", "41", 20260933),
                                          ("50", "51", 20260934)]):
        api[nid] = gpt(ULTIMATE, "1152x2048", ["10", 0], seed)
        api[sid] = {"class_type": "SaveImage", "inputs": {
            "images": [nid, 0],
            "filename_prefix": f"Ignis/02_Ignis_Ultimate_Raw_{'ABC'[i]}"}}
    return api


def main():
    api = build()
    api_to_ui.POS.update({"1": (40, 40), "10": (40, 240), "11": (520, 240),
                          "12": (520, 400), "40": (40, 1900), "41": (520, 1900),
                          "50": (40, 2280), "51": (520, 2280)})
    ui = convert(api)
    io.open(os.path.join(ROOT, "Ignis_FromRef_Workflow_UI.json"), "w",
            encoding="utf-8").write(json.dumps(ui, ensure_ascii=False, indent=1))

    ids = {n["id"] for n in ui["nodes"]}
    dangling = [l for l in ui["links"] if l[1] not in ids or l[3] not in ids]
    api_links = sum(1 for n in api.values() for v in n["inputs"].values()
                    if isinstance(v, list) and len(v) == 2 and isinstance(v[0], str))
    print("nodes:", len(ui["nodes"]), "/", len(api), "| links:", len(ui["links"]),
          "/", api_links, "| dangling:", len(dangling))
    assert len(ui["nodes"]) == len(api) and len(ui["links"]) == api_links and not dangling
    print("OK")


if __name__ == "__main__":
    main()
