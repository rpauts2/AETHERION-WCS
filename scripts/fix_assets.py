import json
from pathlib import Path

REPO = Path(r'C:\Users\Administrator\Desktop\AETHERION-WCS')
race_ids = [r['id'] for r in races]
sound_ids = [int(k) for k in sounds.keys()]
model_ids = [int(k) for k in models.keys()]

print(f"Race IDs: {min(race_ids or [0])}-{max(race_ids or [0])}, count={len(race_ids)}")
print(f"Sound IDs: {min(sound_ids or [0])}-{max(sound_ids or [0])}, count={len(sound_ids)}")
print(f"Model IDs: {min(model_ids or [0])}-{max(model_ids or [0])}, count={len(model_ids)}")

# Build mappings
sound_map = {k: v for k, v in sounds.items()}
model_map = {k: v for k, v in models.items()}

race_to_sound = {r['id']: r.get('customSound') or r.get('soundId') for r in races}
race_to_model = {r['id']: r.get('customModel') or r.get('modelId') for r in races}

# Check references
missing_sound = []
missing_model = []
for r in races:
    rid = r['id']
    rid_str = str(rid)
    if 'customSound' in r and r['customSound']:
        if rid_str not in sound_map and r['customSound'] not in sound_map:
            missing_sound.append((rid, r['customSound']))
    if 'customModel' in r and r['customModel']:
        if rid_str not in model_map and r['customModel'] not in model_map:
            missing_model.append((rid, r['customModel']))

# Check cross-reference by race name between sounds.json and races.json
race_names = {r['id']: r.get('name') for r in races}
sound_name_to_id = {v['race']: k for k, v in sounds.items()}
model_name_to_id = {v['race']: k for k, v in models.items()}

name_missing_sound = []
name_missing_model = []
for rid, name in race_names.items():
    if name and name not in sound_name_to_id:
        name_missing_sound.append((rid, name))
    if name and name not in model_name_to_id:
        name_missing_model.append((rid, name))

print('Missing sound entries for race names:', len(name_missing_sound))
for rid, name in name_missing_sound[:10]:
    print(f'  {rid}: {name}')

print('Missing model entries for race names:', len(name_missing_model))
for rid, name in name_missing_model[:10]:
    print(f'  {rid}: {name}')

# Fix 1: remove empty customSound/customModel from models.json/sounds.json structural entries
# Wait these don't have those fields owned here; let's examine ability entries in races.json
abilities_empty_sound = 0
abilities_empty_model = 0
for r in races:
    for a in r.get('abilities', []):
        if 'customSound' in a and not a['customSound']:
            abilities_empty_sound += 1
        if 'customModel' in a and not a['customModel']:
            abilities_empty_model += 1

print('Empty ability customSound:', abilities_empty_sound)
print('Empty ability customModel:', abilities_empty_model)

# Fix 2: remove empty optional fields
normalized = 0
for r in races:
    changed = False
    if 'customSound' in r and not r['customSound']:
        del r['customSound']
        changed = True
    if 'customModel' in r and not r['customModel']:
        del r['customModel']
        changed = True
    for a in r.get('abilities', []):
        if 'customSound' in a and not a['customSound']:
            del a['customSound']
            normalized += 1
        if 'customModel' in a and not a['customModel']:
            del a['customModel']
            normalized += 1
    if changed:
        pass  # r modified above

# Write races back
races_path.write_text(json.dumps(races, ensure_ascii=False, indent=2), encoding='utf-8')
print('Normalized empty fields in races.json:', normalized)
