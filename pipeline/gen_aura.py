
import bpy, math, os, sys
argv = sys.argv[sys.argv.index("--")+1:]
name, r, g, b, style = argv[0], float(argv[1]), float(argv[2]), float(argv[3]), argv[4]
out_dir = argv[5]

bpy.ops.wm.read_factory_settings(use_empty=True)
objs=[]

if style == "ring":   # D1: простое кольцо-нимб
    bpy.ops.mesh.primitive_torus_add(major_radius=10, minor_radius=1.2, location=(0,0,0))
    objs.append(bpy.context.active_object)
elif style == "double": # D2: двойное кольцо
    bpy.ops.mesh.primitive_torus_add(major_radius=10, minor_radius=1.0, location=(0,0,0))
    objs.append(bpy.context.active_object)
    bpy.ops.mesh.primitive_torus_add(major_radius=7, minor_radius=0.8, location=(0,0,3), rotation=(0.3,0,0))
    objs.append(bpy.context.active_object)
elif style == "spikes": # D3: кольцо с шипами-кристаллами (корона бури)
    bpy.ops.mesh.primitive_torus_add(major_radius=10, minor_radius=1.0, location=(0,0,0))
    objs.append(bpy.context.active_object)
    for i in range(8):
        a=(math.pi*2/8)*i
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.4, radius2=0, depth=7,
            location=(math.cos(a)*10, math.sin(a)*10, 3.5), rotation=(0,0,a))
        objs.append(bpy.context.active_object)
else:  # "crown" D∞: величественная корона с шипами и верхним кристаллом
    bpy.ops.mesh.primitive_torus_add(major_radius=11, minor_radius=1.3, location=(0,0,0))
    objs.append(bpy.context.active_object)
    for i in range(12):
        a=(math.pi*2/12)*i
        h=9 if i%2==0 else 5
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.6, radius2=0, depth=h,
            location=(math.cos(a)*11, math.sin(a)*11, h/2), rotation=(0,0,a))
        objs.append(bpy.context.active_object)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=2.5, location=(0,0,9))
    objs.append(bpy.context.active_object)

bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
if len(objs)>1: bpy.ops.object.join()
aura = bpy.context.active_object
aura.name = name
for p in aura.data.polygons: p.use_smooth=True

mat = bpy.data.materials.new(name+"_mat"); mat.use_nodes=True
bsdf = mat.node_tree.nodes.get("Principled BSDF")
if bsdf:
    bsdf.inputs["Base Color"].default_value=(r,g,b,1)
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value=(r,g,b,1)
        bsdf.inputs["Emission Strength"].default_value=5.0
aura.data.materials.append(mat)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)

out=os.path.join(out_dir, name+".fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, axis_forward='Y', axis_up='Z')
print("AURA_OK", name, len(aura.data.vertices))
