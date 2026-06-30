import json, pathlib

p = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/configs/races.json')
obj = json.loads(p.read_text(encoding='utf-8'))

for idx, r in enumerate(obj):
    rid = r.get('id', 2000 + idx)
    level = 1 + idx
    r['unlock_level'] = level

p.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')
print('unlock_level set for', len(obj), 'races')
