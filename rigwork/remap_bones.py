
import bpy, json
remap = json.load(open(r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\bone_remap.json".replace("\\","/")))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\frost_maiden_body.fbx")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
mesh=[o for o in bpy.data.objects if o.type=='MESH'][0]
# переименуем кости
ren=0
for b in arm.data.bones:
    if b.name in remap:
        newn=remap[b.name]
        arm.data.bones[b.name].name = newn  # имена костей
        ren+=1
# переименуем vertex groups в меше под новые имена
for vg in mesh.vertex_groups:
    if vg.name in remap:
        vg.name = remap[vg.name]
print("RENAMED_BONES:", ren)
# лимит весов 4 + нормализация
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all()
# реэкспорт
out=r"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\content\csgo_addons\aetherion\models\races\frost_maiden.fbx"
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', path_mode='COPY')
print("REMAP_EXPORT_OK")
# контроль новых имён
print("NEW_BONES:", [b.name for b in arm.data.bones[:8]])
