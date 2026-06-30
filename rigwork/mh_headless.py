
import sys, os
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps", "plugins", "plugins/9_export_fbx"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)

# headless: подавим GUI
import getpath
try:
    import numpy as np
    import human, files3d, material
    from core import G
    import skeleton
    print("MODULES_OK")
except Exception as e:
    print("MOD_ERR:", repr(e)); sys.exit(1)

try:
    # базовый меш
    base = files3d.loadMesh(getpath.getSysDataPath("3dobjs/base.obj"))
    print("BASE_MESH:", base is not None, base.getVertexCount() if base else 0)
    h = human.Human(base)
    print("HUMAN_OK")
    # скелет game_engine
    skelPath = getpath.getSysDataPath("rigs/game_engine.mhskel")
    print("SKEL_EXISTS:", os.path.exists(skelPath))
    ref = skeleton.load(skelPath, h.meshData)
    h.setSkeleton(ref)
    print("SKEL_BONES:", ref.getBoneCount())
except Exception as e:
    import traceback; print("BUILD_ERR:", repr(e)); traceback.print_exc(); sys.exit(2)

try:
    import mh2fbx
    from export import Exporter
    # конфиг экспорта
    class Cfg:
        pass
    import mh2fbx as fbxmod
    cfg = type("C",(object,),{})()
    print("FBX_MODULE_OK", dir(fbxmod)[:10])
except Exception as e:
    import traceback; print("FBX_ERR:", repr(e)); traceback.print_exc()
