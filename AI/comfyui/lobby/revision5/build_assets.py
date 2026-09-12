"""Assemble the ImageGen regional correction without changing the existing safe-crop canvas."""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
source = ROOT / 'source'
background = Image.open(source / 'Background_Review2.png').convert('RGB')
region = Image.open(source / 'Siege_Idle_Fit.png').convert('RGB').resize((378, 165), Image.Resampling.LANCZOS)
# Only generated paint is used. An 8px overlap avoids seams at the unchanged crop perimeter.
y, x = np.mgrid[:165, :378]
edge = np.minimum(np.minimum(x, 377-x), np.minimum(y, 164-y))
mask = Image.fromarray((np.clip(edge / 8, 0, 1) * 255).astype('uint8'))
background.paste(region, (580, 350), mask)
background.save(source / 'Background_Final.png', optimize=True)
dest = Path('Assets/UGUI/Art/Lobby/Lobby_Background.png')
background.resize((1536, 1536), Image.Resampling.LANCZOS).save(dest, optimize=True)
print(f'Background: 1536 x 1536 RGB, {dest.stat().st_size:,} bytes')
