import json
from pathlib import Path

p = Path('C:/Users/Administrator/Desktop/AETHERION-WCS/configs/races.json')
obj = json.loads(p.read_text(encoding='utf-8'))

# 1) Show current ID ranges
_ids = sorted(r['id'] for r in obj)
print('ID range:', _ids[0], '-', _ids[-1])

empty_sound = empty_model = 0
for r in obj:
    for a in r.get('abilities', []):
        if not a.get('customSound'):
            empty_sound += 1
        if not a.get('customModel'):
            empty_model += 1
print('empty customSound:', empty_sound)
print('empty customModel:', empty_model)

# 2) Normalize: drop empty optional fields to clean file
normalized = 0
for r in obj:
    for a in r.get('abilities', []):
        if 'customSound' in a and not a['customSound']:
            del a['customSound']
            normalized += 1
        if 'customModel' in a and not a['customModel']:
            del a['customModel']
            normalized += 1
p.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')
print('normalized empty fields:', normalized)
