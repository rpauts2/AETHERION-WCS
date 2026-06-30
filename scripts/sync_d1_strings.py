import json, pathlib

p = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/configs/races.json')
obj = json.loads(p.read_text(encoding='utf-8'))

new_strings = []
for r in obj:
    for ab in r.get('abilities', []):
        n = ab.get('n')
        d = ab.get('d')
        if n:
            new_strings.append(n)
        if d:
            new_strings.append(d)

new_strings = sorted(set(new_strings))

ru_path = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/lang/ru.json')
en_path = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/lang/en.json')
ru = json.loads(ru_path.read_text(encoding='utf-8'))
en = json.loads(en_path.read_text(encoding='utf-8'))

added = 0
for s in new_strings:
    if s not in ru:
        ru[s] = s
        added += 1
    if s not in en:
        en[s] = s

ru_path.write_text(json.dumps(ru, ensure_ascii=False, indent=2), encoding='utf-8')
en_path.write_text(json.dumps(en, ensure_ascii=False, indent=2), encoding='utf-8')
print('added', added, 'keys')
