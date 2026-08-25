# -*- coding: utf-8 -*-
"""ComfyUI 노드 래퍼 — 마감 구현은 _shared/pixel_finalize.py 를 그대로 쓴다.

구현을 두 벌 두지 않는다. 로컬 ComfyUI 에 넣어 쓸 때만 의미가 있고,
Comfy Cloud 는 custom_nodes 설치를 지원하지 않는다.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from pixel_finalize import finalize, palette_array, snap_to_palette, hex_to_rgb  # noqa: E402,F401


# ---------------------------------------------------------------- ComfyUI 노드
try:
    import torch

    class AstraPixelFinalize:
        @classmethod
        def INPUT_TYPES(cls):
            return {
                "required": {
                    "image": ("IMAGE",),
                    "logical_width": ("INT", {"default": 256, "min": 8, "max": 2048}),
                    "logical_height": ("INT", {"default": 384, "min": 8, "max": 2048}),
                    "scale": ("INT", {"default": 4, "min": 1, "max": 16}),
                    "dither": (["none", "floyd-steinberg"], {"default": "none"}),
                },
            }

        RETURN_TYPES = ("IMAGE",)
        FUNCTION = "run"
        CATEGORY = "AstraTools"

        def run(self, image, logical_width, logical_height, scale, dither):
            outs = []
            for t in image:
                arr = (t.cpu().numpy() * 255.0).clip(0, 255).astype(np.uint8)
                mode = "RGBA" if arr.shape[2] == 4 else "RGB"
                pil = Image.fromarray(arr, mode)
                res = finalize(pil, logical_width, logical_height, scale)
                res_arr = np.asarray(res).astype(np.float32) / 255.0
                if res_arr.shape[2] == 4 and arr.shape[2] == 3:
                    res_arr = res_arr[:, :, :3]
                outs.append(torch.from_numpy(res_arr))
            return (torch.stack(outs),)

    NODE_CLASS_MAPPINGS = {"AstraPixelFinalize": AstraPixelFinalize}
    NODE_DISPLAY_NAME_MAPPINGS = {"AstraPixelFinalize": "Astra Pixel Finalize"}
except ImportError:
    # torch 없이 순수 파이썬으로 import 될 때 (로컬 마감 실행 경로)
    NODE_CLASS_MAPPINGS = {}
    NODE_DISPLAY_NAME_MAPPINGS = {}
