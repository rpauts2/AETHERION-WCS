# 🦴 CS2 SKELETON BONE REMAP (game_engine → citizen)
Источник имён CS2: DeepWiki — itzlaith/DragonBurn, "Bone Index Constants"
(pelvis=0, spine_0..3=1-4, neck_0=5, head=6, arm_upper/lower/hand_L=8-10/_R=13-15,
 leg_upper/lower/ankle_L=22-24/_R=25-27)

## Назначение
MakeHuman экспортирует скелет game_engine (UE-style имена). CS2 требует имена citizen-скелета.
Эта таблица переименовывает 53 кости в Blender перед компиляцией → модель совместима с РОДНЫМИ анимациями CS2.

## Применение
Blender: arm.data.bones[old].name = new + mesh.vertex_groups[old].name = new

## Таблица (53 кости)
```json
{
  "Root": "root",
  "pelvis": "pelvis",
  "spine_01": "spine_0",
  "spine_02": "spine_1",
  "spine_03": "spine_2",
  "neck_01": "neck_0",
  "head": "head",
  "clavicle_l": "clavicle_L",
  "upperarm_l": "arm_upper_L",
  "lowerarm_l": "arm_lower_L",
  "hand_l": "hand_L",
  "clavicle_r": "clavicle_R",
  "upperarm_r": "arm_upper_R",
  "lowerarm_r": "arm_lower_R",
  "hand_r": "hand_R",
  "index_01_l": "finger_index_0_L",
  "index_02_l": "finger_index_1_L",
  "index_03_l": "finger_index_2_L",
  "middle_01_l": "finger_middle_0_L",
  "middle_02_l": "finger_middle_1_L",
  "middle_03_l": "finger_middle_2_L",
  "pinky_01_l": "finger_pinky_0_L",
  "pinky_02_l": "finger_pinky_1_L",
  "pinky_03_l": "finger_pinky_2_L",
  "ring_01_l": "finger_ring_0_L",
  "ring_02_l": "finger_ring_1_L",
  "ring_03_l": "finger_ring_2_L",
  "thumb_01_l": "finger_thumb_0_L",
  "thumb_02_l": "finger_thumb_1_L",
  "thumb_03_l": "finger_thumb_2_L",
  "index_01_r": "finger_index_0_R",
  "index_02_r": "finger_index_1_R",
  "index_03_r": "finger_index_2_R",
  "middle_01_r": "finger_middle_0_R",
  "middle_02_r": "finger_middle_1_R",
  "middle_03_r": "finger_middle_2_R",
  "pinky_01_r": "finger_pinky_0_R",
  "pinky_02_r": "finger_pinky_1_R",
  "pinky_03_r": "finger_pinky_2_R",
  "ring_01_r": "finger_ring_0_R",
  "ring_02_r": "finger_ring_1_R",
  "ring_03_r": "finger_ring_2_R",
  "thumb_01_r": "finger_thumb_0_R",
  "thumb_02_r": "finger_thumb_1_R",
  "thumb_03_r": "finger_thumb_2_R",
  "thigh_l": "leg_upper_L",
  "calf_l": "leg_lower_L",
  "foot_l": "ankle_L",
  "ball_l": "ball_L",
  "thigh_r": "leg_upper_R",
  "calf_r": "leg_lower_R",
  "foot_r": "ankle_R",
  "ball_r": "ball_R"
}
```

## Проверено
Ледяная Дева: 53 кости переименованы, веса лимит 4, скомпилирована OK: 1 compiled, 0 failed.
Итог: game/csgo_addons/aetherion/models/races/frost_maiden.vmdl_c (879 KB)
