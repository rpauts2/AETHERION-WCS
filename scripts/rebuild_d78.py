import json, pathlib
p = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/configs/races.json')
obj = json.loads(p.read_text(encoding='utf-8'))
idx_by_id = {r['id']: i for i, r in enumerate(obj)}
updates = {
  2151: {'name':'Страж Эона','abilities':[{'n':'Эонный Щит','t':'Passive','max':5,'d':'Поглощает урон · Пассивно · макс 5','effect':'shield','value':32},{'n':'Удар Эона','t':'Active','max':4,'d':'Рывок сAoE уроном · Актив · макс 4 · КД 18с','effect':'leap','value':300,'cd':18},{'n':'Эонный Взрыв','t':'Ultimate','max':5,'d':'Массовый урон+отбрасывание · Ульта · макс 5 · КД 36с','effect':'nova_knockback','value':36,'cd':36}]},
  2152: {'name':'Взор Хаоса','abilities':[{'n':'Хаос Аура','t':'Passive','max':5,'d':'Случайный бафф/дебафф на врагов · Пассивно · макс 5','effect':'conditional_buff','value':24},{'n':'Фокус Хаоса','t':'Active','max':4,'d':'Случайный телепорт удар · Актив · макс 4 · КД 16с','effect':'blink','value':280,'cd':16},{'n':'Катаклизм Хаоса','t':'Ultimate','max':5,'d':'Беспорядочные взрывыAoE · Ульта · макс 5 · КД 38с','effect':'aoe_explosion','value':34,'cd':38}]},
  2153: {'name':'Ведьма Тумана','abilities':[{'n':'Туманный Шёпот','t':'Passive','max':5,'d':'Замедляет атакующих · Пассивно · макс 5','effect':'slow','value':38},{'n':'Туманное Заколдовывание','t':'Active','max':4,'d':'Замедляет врагов вAoE · Актив · макс 4 · КД 18с','effect':'frost_nova','value':30,'cd':18},{'n':'Туманный Крик','t':'Ultimate','max':5,'d':'Оглушает и замедляетAoE · Ульта · макс 5 · КД 36с','effect':'stun_aoe','value':32,'cd':36}]},
  2154: {'name':'Железный Коготь','abilities':[{'n':'Железный Хвост','t':'Passive','max':5,'d':'Отражает урон при ударе · Пассивно · макс 5','effect':'damage_reflect','value':30},{'n':'Атакующий Рывок','t':'Active','max':4,'d':'Рывок с пробитием брони · Актив · макс 4 · КД 16с','effect':'armor_break','value':30,'cd':16},{'n':'Железная Буря','t':'Ultimate','max':5,'d':'Вихрь ударов поAoE · Ульта · макс 5 · КД 36с','effect':'frost_nova','value':32,'cd':36}]},
  2155: {'name':'Зов Вулкана','abilities':[{'n':'Жар Кожи','t':'Passive','max':5,'d':'Поджигает атакующих · Пассивно · макс 5','effect':'damage_reflect','value':30},{'n':'Лавовый Поток','t':'Active','max':4,'d':'Скачок с извержением лавыAoE · Актив · макс 4 · КД 18с','effect':'aoe_explosion','value':30,'cd':18},{'n':'Кольцо Огня','t':'Ultimate','max':5,'d':'Кольцевой огоньAoE · Ульта · макс 5 · КД 36с','effect':'frost_nova','value':32,'cd':36}]},
}
for rid,info in updates.items():
    idx=idx_by_id[rid]; obj[idx]['name']=info['name']; obj[idx]['abilities']=info['abilities']
p.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf-8')
print('updated_d78',len(updates))
