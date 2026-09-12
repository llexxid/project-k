"""Export the reviewed crystal/bolt background without changing layout or texture budgets."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent
DEST = Path('Assets/UGUI/Art/Lobby/Lobby_Background.png')
image = Image.open(ROOT / 'source/Background_CrystalBolts.png').convert('RGB')
assert image.width == image.height
image.resize((1536, 1536), Image.Resampling.LANCZOS).save(DEST, optimize=True)
print(f'Background: 1536 x 1536 RGB, {DEST.stat().st_size:,} bytes')
