# -*- coding: utf-8 -*-
"""IGNIS 도트 마감. 실제 구현은 _shared/god_finalize.py 에 있다.

  python finalize.py standing  Ignis_01_Standing_HD.png   Ignis_01_Standing_Pixel.png
  python finalize.py cutscene  Ignis_02_Cutscene_HD.png   Ignis_02_Cutscene_Pixel.png
"""
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(ROOT))
sys.path.insert(0, ROOT)

from _shared.god_finalize import main                        # noqa: E402
from ignis_spec import IGNIS_PALETTE_32, IGNIS_SKIN, IGNIS_GLOSS  # noqa: E402

if __name__ == "__main__":
    sys.exit(main(ROOT, IGNIS_PALETTE_32, IGNIS_SKIN, IGNIS_GLOSS))
