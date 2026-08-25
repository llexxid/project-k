# -*- coding: utf-8 -*-
"""로컬 ComfyUI 용 노드 패키지. Comfy Cloud 에서는 쓸 수 없다 (custom_nodes 설치 불가).

Cloud 로만 작업하면 이 폴더는 쓰이지 않는다. 마감은 항상 로컬 _shared/god_finalize.py 가 한다.
"""
from .pixel_finalize_node import NODE_CLASS_MAPPINGS, NODE_DISPLAY_NAME_MAPPINGS  # noqa: F401
