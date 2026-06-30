
import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\frost_maiden_body.fbx")
arms=[o for o in bpy.data.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.data.objects if o.type=='MESH']
print("ARMATURES:", len(arms))
for a in arms:
    print("  BONES:", len(a.data.bones))
    print("  SAMPLE:", [b.name for b in a.data.bones[:12]])
for m in meshes:
    vg=len(m.vertex_groups)
    print("  MESH:", m.name, "verts:", len(m.data.vertices), "vertex_groups(weights):", vg)
