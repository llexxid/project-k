# -*- coding: utf-8 -*-
"""IGNIS 컷씬 전용 리롤 워크플로.

마스터와 도감은 이미 확정됐다. 컷씬만 다시 뽑을 때는 마스터를 t2i 로 재생성하면
안 된다 — 정체성이 흔들리고 도감과 어긋난다. 확정된 마스터 PNG 를 업로드해
LoadImage 로 물려 참조 편집만 돌린다.

  [1] LoadImage(확정 마스터)
    +-- [30]/[40]/[50] ULTIMATE 3시드 -> SaveImage (크로마 그린 RAW)
"""
import json
import io
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ROOT)
from ignis_spec import ULTIMATE  # noqa: E402

sys.path.insert(0, os.path.dirname(ROOT))   # AI/comfyui
from _shared import workflow_json as api_to_ui  # noqa: E402
from _shared.workflow_json import convert  # noqa: E402

# 확정 마스터의 업로드 파일명 (upload_file 이 돌려준 name)
MASTER_UPLOAD = "277590b885e062452f2a286c81afd8a95bc89b3fa2c823df98cce916d81e042c.png"


def main():
    api = {"1": {"class_type": "LoadImage", "inputs": {"image": MASTER_UPLOAD}}}
    for i, (nid, sid, seed) in enumerate([("30", "31", 20260921),
                                          ("40", "41", 20260922),
                                          ("50", "51", 20260923)]):
        api[nid] = {"class_type": "OpenAIGPTImageNodeV2", "inputs": {
            "prompt": ULTIMATE, "model": "gpt-image-2", "model.size": "1152x2048",
            "model.custom_width": 1024, "model.custom_height": 1024,
            "model.background": "opaque", "model.quality": "high", "n": 1, "seed": seed,
            "model.images.image_1": ["1", 0]}}
        api[sid] = {"class_type": "SaveImage", "inputs": {
            "images": [nid, 0],
            "filename_prefix": f"Ignis/02_Ignis_Ultimate_Raw_{'ABC'[i]}"}}

    api_to_ui.POS.update({"1": (40, 40), "30": (40, 400), "31": (520, 400),
                          "40": (40, 840), "41": (520, 840),
                          "50": (40, 1280), "51": (520, 1280)})
    ui = convert(api)
    io.open(os.path.join(ROOT, "Ignis_Cutscene_Only_UI.json"), "w",
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
