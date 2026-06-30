
import bpy, math, os, sys
argv = sys.argv[sys.argv.index("--")+1:]
kind, name, r, g, b, out_dir = argv[0], argv[1], float(argv[2]), float(argv[3]), float(argv[4]), argv[5]
bpy.ops.wm.read_factory_settings(use_empty=True)
objs=[]
def add(o): objs.append(o)

if kind == "wings":   # Крылья Архангела — перья из вытянутых конусов с двух сторон
    for side in (-1, 1):
        for i in range(6):
            length = 16 - i*1.6
            zoff = i*2.5
            bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.6, radius2=0.15, depth=length,
                location=(side*(6+i*1.4), -2, 4+zoff),
                rotation=(math.radians(90), 0, side*math.radians(35 - i*4)))
            add(bpy.context.active_object)
elif kind == "scythe": # Коса Жнеца — древко + изогнутое лезвие
    bpy.ops.mesh.primitive_cylinder_add(radius=0.6, depth=34, location=(0,0,0))
    add(bpy.context.active_object)
    # лезвие из сплющенного тора (половина)
    bpy.ops.mesh.primitive_torus_add(major_radius=8, minor_radius=0.8, location=(6,0,16), rotation=(math.radians(90),0,0))
    bl = bpy.context.active_object; bl.scale=(1,0.25,1); add(bl)
elif kind == "staff":  # Посох Архимага — древко + парящий кристалл + кольца
    bpy.ops.mesh.primitive_cylinder_add(radius=0.5, depth=32, location=(0,0,0))
    add(bpy.context.active_object)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=3.2, location=(0,0,18))
    add(bpy.context.active_object)
    for zoff,scl in ((18,1.6),(18,1.2)):
        bpy.ops.mesh.primitive_torus_add(major_radius=4.5*scl, minor_radius=0.4, location=(0,0,zoff),
            rotation=(math.radians(70),0,0))
        add(bpy.context.active_object)
else:  # "halo" Нимб Светоносного — кольцо + лучи
    bpy.ops.mesh.primitive_torus_add(major_radius=9, minor_radius=0.9, location=(0,0,0))
    add(bpy.context.active_object)
    for i in range(16):
        a=(math.pi*2/16)*i
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.5, radius2=0, depth=5,
            location=(math.cos(a)*9, math.sin(a)*9, 0), rotation=(math.radians(90),0,a))
        add(bpy.context.active_object)

bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active=objs[0]
if len(objs)>1: bpy.ops.object.join()
acc=bpy.context.active_object; acc.name=name
for p in acc.data.polygons: p.use_smooth=True
mat=bpy.data.materials.new(name+"_mat"); mat.use_nodes=True
bsdf=mat.node_tree.nodes.get("Principled BSDF")
if bsdf:
    bsdf.inputs["Base Color"].default_value=(r,g,b,1)
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value=(r,g,b,1)
        bsdf.inputs["Emission Strength"].default_value=4.5
acc.data.materials.append(mat)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
out=os.path.join(out_dir,name+".fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, axis_forward='Y', axis_up='Z')
print("ACC_OK", name, len(acc.data.vertices))
