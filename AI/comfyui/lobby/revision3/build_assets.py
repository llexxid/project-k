"""Export the reviewed single-tower backdrop; retain the shipped revision-2 hero/UI assets."""
from pathlib import Path
import json
from PIL import Image
ROOT=Path(__file__).resolve().parent
DEST=Path('Assets/UGUI/Art/Lobby')
source=ROOT/'source/Background_SingleTower.png'
Image.open(source).convert('RGB').resize((1536,1536),Image.Resampling.LANCZOS).save(DEST/'Lobby_Background.png',optimize=True)
manifest_path=DEST/'Lobby.layers.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
manifest['layers']=[layer for layer in manifest['layers'] if layer['name']!='BanditRight']
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
assert [l['name'] for l in manifest['layers']]==['BanditLeft','Mage','Archer','Knight','Sword']
print('Single-tower background exported; right bandit excluded from the layer manifest.')
print('Background bytes:',(DEST/'Lobby_Background.png').stat().st_size)
