# -*- coding: utf-8 -*-
"""IGNIS 파생 워크플로 — 확정된 마스터에서 도감 + 컷씬만 다시 뽑는다.

마스터가 이미 옳을 때(디자인·의상·무기 전부 승인됨) 포즈나 구도만 손보려고
build_workflow.py 를 돌리면 마스터가 t2i 로 재추첨돼 의상까지 흔들린다. 확정본을
업로드해 LoadImage 로 물리고 파생물만 다시 만든다.

  [1] LoadImage (확정 마스터)
    +-- [20]/[21]/[22] CODEX 3시드   (참조 편집) -> RAW
    +-- [30]/[40]/[50] ULTIMATE 3시드 (참조 편집, 크로마 그린) -> RAW

도감도 3시드로 뽑는다. 손 파지는 시드 운이 크고 — 프롬프트를 아무리 조여도 손가락이
뭉개지는 컷이 섞여 나온다 — 골라 쓰는 편이 다시 돌리는 것보다 싸다.
그래프 안 픽셀 체인은 뺐다. 마감은 항상 로컬 finalize.py 가 하고 (고정 팔레트가 필요하다)
그래프 출력은 쓰지 않았다.

유료 노드 6개를 한 작업에 담으면 429 (api.rate_limit) 로 작업 전체가 죽는다 — 실측.
그래서 도감 3시드 / 컷씬 3시드를 별개 작업으로 쪼개 순차 실행한다.

  python build_derive.py codex     -> Ignis_Derive_Codex_UI.json
  python build_derive.py pommel    -> Ignis_Derive_Pommel_UI.json    (손을 폼멜 위에)
  python build_derive.py lean      -> Ignis_Derive_Lean_UI.json      (검을 대각선으로)
  python build_derive.py hang      -> Ignis_Derive_Hang_UI.json      (컷씬과 같은 파지, 아래로)
  python build_derive.py hang2     -> 같은 프롬프트, 다른 3시드
  python build_derive.py handfix   -> Ignis_Derive_Handfix_UI.json   (확정 도감에서 손만 수정)
  python build_derive.py armline   -> Ignis_Derive_Armline_UI.json   (팔·검 배치 교정)
  python build_derive.py griphilt  -> Ignis_Derive_Griphilt_UI.json  (파지 위치·검 비례)
  python build_derive.py refpose   -> Ignis_Derive_Refpose_UI.json   (레퍼런스 자세 대안)
  python build_derive.py cloth     -> Ignis_Derive_Cloth_UI.json     (옷/머리 분리)
  python build_derive.py hair      -> 스탠딩 머리카락만 (흐르는 불길)
  python build_derive.py hairult   -> 컷씬 머리카락만 (흐르는 불길)
  python build_derive.py facehair  -> 스탠딩 얼굴 매끈 + 머리 미니멀 불길
  python build_derive.py cutscene  -> Ignis_Derive_Cutscene_UI.json

도감 파지 안들: 손 모양이 반복해서 틀리면 프롬프트가 아니라 자세를 의심한다.
관절 각도가 0인(혹은 과한) 구도에서는 어떤 문구로도 자연스러운 손이 나오지 않는다.

**seed 는 동작하지 않는다.** get_node 실측: OpenAIGPTImageNodeV2 의 seed 툴팁이
"not implemented yet in backend". 시드를 바꿔 노드를 3개 두는 건 의미가 없었고
변주는 그냥 API 비결정성이었다. 후보를 여러 장 뽑을 때는 노드 하나에 n 을 올린다 —
유료 노드 수가 줄어 429 (api.rate_limit) 도 같이 피한다.
"""
import json
import io
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, ROOT)
from ignis_spec import (CODEX, CODEX_POMMEL, CODEX_LEAN, CODEX_HANG,  # noqa: E402
                        CODEX_HANDFIX, CODEX_ARMLINE, CODEX_GRIPHILT,  # noqa: E402
                        CODEX_REFPOSE, CODEX_CLOTH, CODEX_HAIR,  # noqa: E402
                        ULTIMATE_HAIR, CODEX_FACEHAIR, ULTIMATE)

sys.path.insert(0, os.path.dirname(ROOT))   # AI/comfyui
from _shared import workflow_json as api_to_ui  # noqa: E402
from _shared.workflow_json import convert  # noqa: E402

# 확정 마스터(v11, 의상 확정 + 롱소드)의 업로드 파일명
MASTER_UPLOAD = "7e616e46ef4b441db6969cd1ad6d27b41f19158822a9711a15aa158a282c4c2e.png"

# 확정 도감(v16 hang C)의 업로드 파일명. handfix 는 마스터가 아니라 이 도감을 참조한다 —
# 손만 고치는 작업이라 나머지가 흔들리면 안 된다.
CODEX_UPLOAD = "3f603360a35cf08d7145b72ff082737041d2e68d0c63e28185656bdd15614f7e.png"

# 채택된 대안 자세 도감(01B). cloth 는 이걸 참조한다.
CODEXALT_UPLOAD = "6fcd39b69c66f9a8950dbbe7330b70a80321e57831510bd2ed08fce8813d2f5a.png"

# 현행 확정본 (옷+손 반영). hair / hairult 는 이걸 참조한다.
STANDING_UPLOAD = "137745f64be39a966c4c5e3308e93bbdbcabdfa151e31b2de89290425e69b7b9.png"
CUTSCENE_UPLOAD = "541004b9d87e3edc43e16bf2558d6d941a178fc549222bf1d11492db5252e027.png"

SEEDS_CODEX = (20261041, 20261042, 20261043)
SEEDS_CODEX2 = (20261044, 20261045, 20261046)
SEEDS_ULT = (20261022, 20261023, 20261024)


def gpt(prompt, size, images, seed, n=1):
    # seed 는 백엔드 미구현이라 값에 의미가 없다. 후보는 n 으로 뽑는다.
    return {"class_type": "OpenAIGPTImageNodeV2", "inputs": {
        "prompt": prompt, "model": "gpt-image-2", "model.size": size,
        "model.custom_width": 1024, "model.custom_height": 1024,
        "model.background": "opaque", "model.quality": "high", "n": n, "seed": seed,
        "model.images.image_1": images}}


def build(part):
    if part in ("hair", "facehair"):
        src = STANDING_UPLOAD
    elif part == "hairult":
        src = CUTSCENE_UPLOAD
    elif part == "cloth":
        src = CODEXALT_UPLOAD
    elif part in ("handfix", "armline", "griphilt", "refpose"):
        src = CODEX_UPLOAD
    else:
        src = MASTER_UPLOAD

    api = {"1": {"class_type": "LoadImage", "inputs": {"image": src}}}

    if part == "facehair":
        api["20"] = gpt(CODEX_FACEHAIR, "1024x1536", ["1", 0], 0, n=4)
        api["21"] = {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": "Ignis/01_Ignis_FaceHair"}}
        return api

    if part == "hair":
        api["20"] = gpt(CODEX_HAIR, "1024x1536", ["1", 0], 0, n=4)
        api["21"] = {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": "Ignis/01_Ignis_Hair"}}
        return api

    if part == "hairult":
        api["20"] = gpt(ULTIMATE_HAIR, "1152x2048", ["1", 0], 0, n=4)
        api["21"] = {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": "Ignis/02_Ignis_Hair"}}
        return api

    if part == "cloth":
        api["20"] = gpt(CODEX_CLOTH, "1024x1536", ["1", 0], 0, n=4)
        api["21"] = {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": "Ignis/01B_Ignis_Cloth"}}
        return api

    if part == "refpose":
        api["20"] = gpt(CODEX_REFPOSE, "1024x1536", ["1", 0], 0, n=4)
        api["21"] = {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": "Ignis/01_Ignis_RefPose"}}
        return api

    if part == "griphilt":
        api["20"] = gpt(CODEX_GRIPHILT, "1024x1536", ["1", 0], 0, n=4)
        api["21"] = {"class_type": "SaveImage", "inputs": {
            "images": ["20", 0], "filename_prefix": "Ignis/01_Ignis_GripHilt"}}
        return api

    if part == "armline":
        # 노드 2개 x n=4 = 후보 8장. 유료 노드는 2개뿐이라 레이트리밋에 안 걸린다.
        for nid, sid in (("20", "21"), ("22", "23")):
            api[nid] = gpt(CODEX_ARMLINE, "1024x1536", ["1", 0], 0, n=4)
            api[sid] = {"class_type": "SaveImage", "inputs": {
                "images": [nid, 0], "filename_prefix": f"Ignis/01_Ignis_Armline_{nid}"}}
        return api

    prompts = {"codex": CODEX, "pommel": CODEX_POMMEL, "lean": CODEX_LEAN,
               "hang": CODEX_HANG, "hang2": CODEX_HANG, "handfix": CODEX_HANDFIX}
    if part in prompts:
        for i, (nid, sid) in enumerate([("20", "21"), ("22", "23"), ("24", "25")]):
            seeds = SEEDS_CODEX2 if part in ("hang2", "handfix") else SEEDS_CODEX
            api[nid] = gpt(prompts[part], "1024x1536", ["1", 0], seeds[i])
            api[sid] = {"class_type": "SaveImage", "inputs": {
                "images": [nid, 0],
                "filename_prefix": f"Ignis/01_Ignis_Codex_{part}_{'ABC'[i]}"}}
    else:
        api["30"] = gpt(ULTIMATE, "1152x2048", ["1", 0], 0, n=4)
        api["31"] = {"class_type": "SaveImage", "inputs": {
            "images": ["30", 0], "filename_prefix": "Ignis/02_Ignis_Ultimate_Raw"}}
    return api


def main():
    part = sys.argv[1] if len(sys.argv) > 1 else "codex"
    assert part in ("codex", "pommel", "lean", "hang", "hang2", "handfix",
                    "armline", "griphilt", "refpose", "cloth",
                    "hair", "hairult", "facehair", "cutscene"), "usage: build_derive.py <part>"
    api = build(part)
    api_to_ui.POS.update({"1": (40, 40), "20": (40, 300), "21": (520, 300),
                          "22": (40, 740), "23": (520, 740),
                          "24": (40, 1180), "25": (520, 1180),
                          "30": (40, 1620), "31": (520, 1620),
                          "40": (40, 2060), "41": (520, 2060),
                          "50": (40, 2500), "51": (520, 2500)})
    ui = convert(api)
    out = "Ignis_Derive_%s_UI.json" % part.capitalize()
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
