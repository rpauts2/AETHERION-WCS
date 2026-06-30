
import bpy, bmesh, math, os
from mathutils import Vector

# == AETHERION: процедурная модель Духа-компаньона (Aether Wisp) ==
# Чистим сцену
bpy.ops.wm.read_factory_settings(use_empty=True)

def shade_smooth(o):
    for p in o.data.polygons: p.use_smooth = True

# 1) Центральное ядро — икосфера
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=8.0, location=(0,0,0))
core = bpy.context.active_object
core.name = "wisp_core"
shade_smooth(core)

# 2) Энергетические шипы (кристаллы) вокруг ядра — 6 штук радиально
spikes = []
for i in range(6):
    ang = (math.pi*2/6)*i
    x, y = math.cos(ang)*9, math.sin(ang)*9
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=2.2, radius2=0.0, depth=10,
        location=(x, y, 0), rotation=(math.pi/2, 0, ang))
    s = bpy.context.active_object
    s.name = f"wisp_spike_{i}"
    spikes.append(s)

# 3) Верхний и нижний кристаллы
for z in (10, -10):
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=2.4, radius2=0.0, depth=11,
        location=(0,0,z), rotation=(0 if z>0 else math.pi,0,0))
    spikes.append(bpy.context.active_object)

# 4) Внешнее свечение-оболочка (полупрозрачная сфера)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=12.0, location=(0,0,0))
halo = bpy.context.active_object
halo.name = "wisp_halo"
shade_smooth(halo)

# Объединяем всё в один меш
bpy.ops.object.select_all(action='DESELECT')
for o in [core]+spikes+[halo]: o.select_set(True)
bpy.context.view_layer.objects.active = core
bpy.ops.object.join()
wisp = bpy.context.active_object
wisp.name = "aether_wisp"

# Материал с эмиссией (эфирное свечение)
mat = bpy.data.materials.new("aether_wisp_mat")
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes.get("Principled BSDF")
if bsdf:
    bsdf.inputs["Base Color"].default_value = (0.2, 0.6, 1.0, 1.0)
    # Emission в Blender 5.x
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (0.3, 0.7, 1.0, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 4.0
wisp.data.materials.append(mat)

# Применяем трансформы
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Экспорт FBX
out = r"C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike Global Offensive\\content\\csgo_addons\\aetherion\\models\\races\\aether_wisp.fbx"
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_unit_scale=True,
    global_scale=1.0, axis_forward='Y', axis_up='Z')
print("WISP_EXPORT_OK", out, "verts=", len(wisp.data.vertices))
