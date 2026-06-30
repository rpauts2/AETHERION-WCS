
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\frost_maiden_body.fbx")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
for i,b in enumerate(arm.data.bones):
    print(f"{i}|{b.name}|{b.parent.name if b.parent else 'NONE'}")
