
import sys, os
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps", "plugins", "plugins/9_export_fbx"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
import getpath, numpy as np
import human, files3d
from core import G
import skeleton

base = files3d.loadMesh(getpath.getSysDataPath("3dobjs/base.obj"))
h = human.Human(base)

# заглушка глобального app с selectedHuman = наше тело
class StubApp:
    def __init__(self, human):
        self.selectedHuman = human
        self.settings = {}
    def getSetting(self, k, d=None): return d
G.app = StubApp(h)
print("APP_STUB_OK")

skelPath = getpath.getSysDataPath("rigs/game_engine.mhskel")
ref = skeleton.load(skelPath, h.meshData)
h.setSkeleton(ref)
print("SKEL_BONES:", ref.getBoneCount())
bones = [b.name for b in ref.getBones()[:20]]
print("BONES_SAMPLE:", bones)

# Попробуем FBX-экспорт
try:
    sys.path.insert(0, os.path.join(mh,"plugins","9_export_fbx"))
    import mh2fbx
    cfg = type("Cfg",(object,),{
        "useRelPaths": False, "feetOnGround": True, "scale": 1.0, "unit":"m",
        "useNormals": True, "subdivide": False, "binary": True,
        "useMaterials": True, "yUpFaceZ": False, "zUp": False,
        "exporter": None, "human": h, "rigOptions": None,
        "offsetVerts": (lambda *a, **k: None)
    })()
    cfg.human = h
    out = r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\frost_maiden_body.fbx"
    mh2fbx.exportFbx(out, cfg)
    print("FBX_EXPORT_DONE:", os.path.exists(out), os.path.getsize(out) if os.path.exists(out) else 0)
except Exception as e:
    import traceback; print("FBX_ERR:", repr(e)); traceback.print_exc()
