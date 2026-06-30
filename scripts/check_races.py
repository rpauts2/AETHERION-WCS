import json
with open('configs/races.json','r',encoding='utf-8') as f:
    data=json.load(f)
gaps=[]
for r in data:
    ab=r.get('abilities',[])
    if not ab:
        gaps.append((r['id'], r['name']))
    else:
        types=set(a.get('t','') for a in ab)
        if not {'Active','Passive','Ultimate'} & types:
            gaps.append((r['id'], r['name'], 'no valid types'))
print('Total', len(data))
print('Need rewrite:', len(gaps))
for row in gaps[:25]:
    print(row)
