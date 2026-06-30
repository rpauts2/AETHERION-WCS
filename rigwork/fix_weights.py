
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\frost_maiden_body.fbx")
mesh=[o for o in bpy.data.objects if o.type=='MESH'][0]
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
# Limit Total weights to 4 per vertex (требование CS2)
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all()
print("WEIGHTS_LIMITED_TO_4")
# реэкспорт
out=r"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\content\csgo_addons\aetherion\models\races\frost_maiden.fbx"
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, add_leaf_bones=False,
    bake_anim=False, mesh_smooth_type='FACE', path_mode='COPY')
print("REEXPORT_OK")
