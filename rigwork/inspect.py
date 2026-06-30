
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
try:
    bpy.ops.import_scene.gltf(filepath=r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\characters\models\ctm_sas\ctm_sas.glb")
except Exception as e:
    print("IMPORT_ERR:", e)
arms=[o for o in bpy.data.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.data.objects if o.type=='MESH']
print("ARMATURES:", len(arms))
for a in arms:
    print("  bones:", len(a.data.bones))
    names=[b.name for b in a.data.bones[:15]]
    print("  sample:", names)
print("MESHES:", len(meshes))
for m in meshes:
    print("  ", m.name, "verts:", len(m.data.vertices))
