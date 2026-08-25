# -*- coding: utf-8 -*-
"""ASTRA 도트 마감. 실제 구현은 _shared/god_finalize.py 에 있다.

  python finalize.py standing  Astra_01_Standing_HD.png   Astra_01_Standing_Pixel.png
  python finalize.py cutscene  Astra_02_Cutscene_HD.png   Astra_02_Cutscene_Pixel.png
"""
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(ROOT))
sys.path.insert(0, ROOT)

from _shared.god_finalize import main                        # noqa: E402
from astra_spec import ASTRA_PALETTE_32, ASTRA_SKIN, ASTRA_GLOSS  # noqa: E402

if __name__ == "__main__":
    sys.exit(main(ROOT, ASTRA_PALETTE_32, ASTRA_SKIN, ASTRA_GLOSS))
