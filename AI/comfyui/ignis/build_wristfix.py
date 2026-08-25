# -*- coding: utf-8 -*-
"""손목 꺾임을 인페인팅으로 고친다 — 마스크 안(아래팔+손)만 다시 그린다.

get_node 실측으로 OpenAIGPTImageNodeV2 에 model.mask (흰 영역만 교체) 가 있는 것을 확인했다.
전체 재생성은 팔·검을 옮기다 손을 잃는다(v18 에서 4장 모두 손이 뭉갰다). 마스크를 쓰면
검신·의상·배경·얼굴이 픽셀 단위로 보존되고, 모델은 마스크 안 3% 만 다시 그린다.

  [1] LoadImage(확정 도감)                 -> image_1
  [2] LoadImage(마스크) -> [3] ImageToMask -> model.mask
  [20] gpt (n=4)  -> [21] Save

마스크 흰색 = 교체 영역. ImageToMask(channel=red) 로 명시 변환한다 — LoadImage 의 MASK
출력은 알파 반전이라 헷갈린다.
"""
import json
import io
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ROOT)
from ignis_spec import (CODEX_WRISTFIX, CODEX_HANDROLLBACK,  # noqa: E402
                        CODEX_HANDREF, CODEX_HANDREF2, CODEX_SWORDFIX)

sys.path.insert(0, os.path.dirname(ROOT))   # AI/comfyui
from _shared import workflow_json as api_to_ui  # noqa: E402
from _shared.workflow_json import convert, SCHEMA  # noqa: E402

SCHEMA.setdefault("ImageToMask", (["channel"], ["image"], ["MASK"]))

# 아래팔 마스크로 손목을 폈던 1차 (보존)
WRIST_SRC = "a4c890d1125acadb095d6531b58adf4395a6f0a1e406148740ebf2e9e66c6a83.png"
WRIST_MASK = "eba65d20c149e9d562571202450ae6ce0e94a64b317e299ce71e64420970a7c0.png"

# 손 마스크로 손 구조를 복원하는 2차 (현재)
HAND_SRC = "9aabd014d8674e3cac41b73c2e6c1b0d9988d59acaffbef063a48c9d1671a08a.png"
HAND_MASK = "07e9429b5a003fc360096965c55ef087ac86591fb9ba56d1c93dfb56f08ff427.png"

# 손을 레퍼런스 파지에 맞추는 3차. 마스크는 손 마스크를 그대로 재사용한다.
HANDREF_SRC = "3f603360a35cf08d7145b72ff082737041d2e68d0c63e28185656bdd15614f7e.png"

JOBS = {"wrist": (WRIST_SRC, WRIST_MASK, CODEX_WRISTFIX, "Ignis/01_Ignis_WristFix"),
        "hand": (HAND_SRC, HAND_MASK, CODEX_HANDROLLBACK, "Ignis/01_Ignis_HandRoll"),
        "handref": (HANDREF_SRC, HAND_MASK, CODEX_HANDREF, "Ignis/01_Ignis_HandRef"),
        # 대안 자세(옷 수정본) 위에서 손만 레퍼런스 파지로
        "handref2": ("b2820e8cc452ec99a2c6ceb3e6652bbe89cc964f04adffc918a090c48f62bb32.png",
                     "96b0f479fead069dfbf17a506494c2356fe80a3426eb033b3b12d99bda469626.png",
                     CODEX_HANDREF2, "Ignis/01B_Ignis_HandRef2"),
        # 머리카락 수정본 위에서 유령 손 제거 + 검 각도 미세 조정
        "swordfix": ("1cfb62450f7a7aadb68ec70f248edbdf4ea820d7464eca7a14823b6630d8fbef.png",
                     "2b663233de7c7d3ea68924704561e094b4e11c14868c128300b51d8cf4a133ce.png",
                     CODEX_SWORDFIX, "Ignis/01_Ignis_SwordFix")}


def main():
    job = sys.argv[1] if len(sys.argv) > 1 else "hand"
    src, mask, prompt, prefix = JOBS[job]
    api = {
        "1": {"class_type": "LoadImage", "inputs": {"image": src}},
        "2": {"class_type": "LoadImage", "inputs": {"image": mask}},
        "3": {"class_type": "ImageToMask", "inputs": {"image": ["2", 0], "channel": "red"}},
        "20": {"class_type": "OpenAIGPTImageNodeV2", "inputs": {
            "prompt": prompt, "model": "gpt-image-2", "model.size": "1024x1536",
            "model.custom_width": 1024, "model.custom_height": 1024,
            "model.background": "opaque", "model.quality": "high", "n": 4, "seed": 0,
            "model.images.image_1": ["1", 0], "model.mask": ["3", 0]}},
        "21": {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": prefix}},
    }
    api_to_ui.POS.update({"1": (40, 40), "2": (40, 240), "3": (300, 240),
                          "20": (560, 40), "21": (1040, 40)})
    ui = convert(api)
    out = "Ignis_MaskFix_%s_UI.json" % job
    io.open(os.path.join(ROOT, out), "w",
            encoding="utf-8").write(json.dumps(ui, ensure_ascii=False, indent=1))

    ids = {n["id"] for n in ui["nodes"]}
    dangling = [l for l in ui["links"] if l[1] not in ids or l[3] not in ids]
    api_links = sum(1 for n in api.values() for v in n["inputs"].values()
                    if isinstance(v, list) and len(v) == 2 and isinstance(v[0], str))
    print(out, "| nodes:", len(ui["nodes"]), "/", len(api), "| links:", len(ui["links"]),
          "/", api_links, "| dangling:", len(dangling))
    assert len(ui["nodes"]) == len(api) and len(ui["links"]) == api_links and not dangling
    print("OK")


if __name__ == "__main__":
    main()
