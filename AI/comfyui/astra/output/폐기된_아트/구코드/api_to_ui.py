# -*- coding: utf-8 -*-
"""API 포맷 워크플로 → ComfyUI 편집기 save(graph) 포맷 변환.

widgets_values 순서는 추측이 아니라 이 인스턴스의 get_node 가 돌려준 실제 입력 순서다.
IMAGE / MASK 처럼 링크로 들어오는 입력은 위젯이 아니므로 widgets_values 에서 빠진다.
"""
import json
import io
import os

ROOT = os.path.dirname(os.path.abspath(__file__))

# class_type -> (위젯 입력 이름 순서, 링크 입력 이름 순서, 출력 타입)
SCHEMA = {
    "OpenAIGPTImageNodeV2": (
        ["prompt", "model", "model.size", "model.custom_width", "model.custom_height",
         "model.background", "model.quality", "n", "seed"],
        ["model.images.image_1", "model.mask"],
        ["IMAGE"],
    ),
    "ImageScale": (["upscale_method", "width", "height", "crop"], ["image"], ["IMAGE"]),
    "ImageQuantize": (["colors", "dither"], ["image"], ["IMAGE"]),
    "Change Channel Count": (["kind"], ["image"], ["IMAGE"]),
    "LoadImage": (["image"], [], ["IMAGE", "MASK"]),
    "SaveImage": (["filename_prefix"], ["images"], []),
    "PreviewImage": ([], ["images"], []),
}

# 보기 좋은 배치: 마스터 열 → 도감 열 → 컷씬 열
POS = {
    "10": (40, 40),    "11": (520, 40),   "12": (520, 200),
    "20": (40, 420),   "21": (520, 420),  "22": (520, 580),
    "23": (520, 740),  "24": (740, 740),  "25": (960, 740),
    "26": (1180, 740), "27": (1400, 740), "28": (1400, 900),
    "30": (40, 1120),  "31": (520, 1120), "32": (520, 1280),
    "33": (520, 1440), "34": (740, 1440), "35": (960, 1440),
    "36": (1180, 1440), "37": (1400, 1440), "38": (1400, 1600),
    "40": (40, 1900), "41": (520, 1900), "50": (40, 2280), "51": (520, 2280),
}


def convert(api):
    nodes, links = [], []
    link_id = 1
    # 먼저 각 노드의 입력 슬롯 인덱스를 확정해야 링크를 걸 수 있다
    order = sorted(api, key=int)
    slot_of = {}
    for nid in order:
        ct = api[nid]["class_type"]
        widget_names, link_names, _ = SCHEMA[ct]
        present = [n for n in link_names if isinstance(api[nid]["inputs"].get(n), list)]
        slot_of[nid] = {name: i for i, name in enumerate(present)}

    for nid in order:
        node = api[nid]
        ct = node["class_type"]
        widget_names, link_names, out_types = SCHEMA[ct]
        ins = node["inputs"]

        inputs = []
        for name in link_names:
            v = ins.get(name)
            if not isinstance(v, list):
                continue
            src_nid, src_slot = v[0], v[1]
            inputs.append({"name": name, "type": "IMAGE" if name != "model.mask" else "MASK",
                           "link": link_id})
            links.append([link_id, int(src_nid), src_slot, int(nid),
                          slot_of[nid][name], "IMAGE" if name != "model.mask" else "MASK"])
            link_id += 1

        outputs = []
        for i, t in enumerate(out_types):
            consumers = [l[0] for l in links if l[1] == int(nid) and l[2] == i]
            outputs.append({"name": t, "type": t, "links": consumers, "slot_index": i})

        x, y = POS.get(nid, (40, 40))
        nodes.append({
            "id": int(nid),
            "type": ct,
            "pos": [x, y],
            "size": [420, 400] if ct == "OpenAIGPTImageNodeV2" else [300, 120],
            "flags": {},
            "order": order.index(nid),
            "mode": 0,
            "inputs": inputs,
            "outputs": outputs,
            "properties": {"Node name for S&R": ct},
            "widgets_values": [ins[n] for n in widget_names if n in ins],
        })

    # 출력 links 를 두 번째 패스로 채운다 (첫 패스에서는 아직 링크가 다 안 생겼다)
    for n in nodes:
        for i, o in enumerate(n["outputs"]):
            o["links"] = [l[0] for l in links if l[1] == n["id"] and l[2] == i]

    return {
        "last_node_id": max(int(n) for n in order),
        "last_link_id": link_id - 1,
        "nodes": nodes,
        "links": links,
        "groups": [
            {"title": "1. ASTRA MASTER REFERENCE", "bounding": [20, 0, 940, 380],
             "color": "#3f789e", "font_size": 24},
            {"title": "2. CODEX (도감) — master 참조 편집 + 픽셀 마감",
             "bounding": [20, 380, 1600, 700], "color": "#8A8", "font_size": 24},
            {"title": "3. ULTIMATE CUTSCENE — master 참조 편집 + 픽셀 마감",
             "bounding": [20, 1080, 1600, 700], "color": "#a1309b", "font_size": 24},
        ],
        "config": {},
        "extra": {"ds": {"scale": 0.55, "offset": [0, 0]}},
        "version": 0.4,
    }


def main():
    api = json.load(io.open(os.path.join(ROOT, "Astra_Production_Workflow_API.json"),
                            encoding="utf-8"))
    ui = convert(api)
    io.open(os.path.join(ROOT, "Astra_Production_Workflow_UI.json"), "w",
            encoding="utf-8").write(json.dumps(ui, ensure_ascii=False, indent=1))

    # 자체 검증: 노드 수, 링크 수, 댕글링 링크
    ids = {n["id"] for n in ui["nodes"]}
    dangling = [l for l in ui["links"] if l[1] not in ids or l[3] not in ids]
    api_links = sum(1 for n in api.values() for v in n["inputs"].values()
                    if isinstance(v, list) and len(v) == 2 and isinstance(v[0], str))
    print("UI nodes:", len(ui["nodes"]), "| links:", len(ui["links"]),
          "| API links:", api_links, "| dangling:", len(dangling))
    assert len(ui["nodes"]) == len(api)
    assert len(ui["links"]) == api_links
    assert not dangling
    print("OK")


if __name__ == "__main__":
    main()
