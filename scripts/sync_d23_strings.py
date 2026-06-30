import json, pathlib
p = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/configs/races.json')
obj = json.loads(p.read_text(encoding='utf-8'))
strings = set()
for r in obj:
    if r.get('division') in (2,3):
        strings.add(r.get('name',''))
        for ab in r.get('abilities', []):
            strings.add(ab.get('n',''))
            strings.add(ab.get('d',''))
strings = [s for s in strings if s]
ru_path = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/lang/ru.json')
en_path = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/lang/en.json')
ru = json.loads(ru_path.read_text(encoding='utf-8'))
en = json.loads(en_path.read_text(encoding='utf-8'))
added = 0
for s in strings:
    if s not in ru:
        ru[s] = s
        added += 1
    if s not in en:
        en[s] = s
ru_path.write_text(json.dumps(ru, ensure_ascii=False, indent=2), encoding='utf-8')
en_path.write_text(json.dumps(en, ensure_ascii=False, indent=2), encoding='utf-8')
print('added', added, 'keys')
