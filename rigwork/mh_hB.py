
import sys, os, types, importlib.util
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
import getpath, numpy as np
import human, files3d
from core import G
class StubApp:
    def __init__(s,hu): s.selectedHuman=hu; s.settings={}
    def getSetting(s,k,d=None): return d
    def progress(s,*a,**k): pass
import skeleton
base = files3d.loadMesh(getpath.getSysDataPath("3dobjs/base.obj"))
h = human.Human(base)
G.app = StubApp(h)

ref = skeleton.load(getpath.getSysDataPath("rigs/game_engine.mhskel"), h.meshData)
h.setSkeleton(ref)
print("SKEL_BONES:", ref.getBoneCount())

# веса: у скелета есть vertexWeights (из .mhskel/.mhw). Биндим к мешу.
vw = ref.getVertexWeights()
print("SKEL_HAS_WEIGHTS:", vw is not None, getattr(vw,'vertexCount',None))
if vw is None:
    # построим веса из reference (default) — game_engine ремап с базового
    import getpath as gp
    baseSkelPath = gp.getSysDataPath("rigs/default.mhskel")
    baseRef = skeleton.load(baseSkelPath, h.meshData)
    baseVW = baseRef.getVertexWeights()
    print("BASE_VW:", baseVW is not None)
    vw = ref.getVertexWeights(baseVW, force_remap=True)
    print("REMAPPED_VW:", vw is not None)
# делаем ref базовым скелетом и биндим
h.setBaseSkeleton(ref) if hasattr(h,'setBaseSkeleton') else None
h.addBoundMesh(h.meshData, vw)
print("BOUND_OK, weights vertexCount:", vw.vertexCount if vw else None)

# проверка getVertexWeights
test = h.getVertexWeights()
print("HUMAN_VW_OK:", test is not None)

# экспорт
pkgdir = os.path.join(mh,"plugins","9_export_fbx")
pkg = types.ModuleType("fbxexport"); pkg.__path__=[pkgdir]; sys.modules["fbxexport"]=pkg
def loadmod(name):
    spec=importlib.util.spec_from_file_location("fbxexport."+name, os.path.join(pkgdir,name+".py"))
    m=importlib.util.module_from_spec(spec); sys.modules["fbxexport."+name]=m; spec.loader.exec_module(m); return m
loadmod("fbx_utils"); mh2fbx=loadmod("mh2fbx")
from export import ExportConfig
cfg = ExportConfig()
cfg.feetOnGround=True; cfg.scale=1.0; cfg.unit="m"; cfg.useNormals=True
cfg.useRelPaths=False; cfg.customPrefix=""; cfg.setHuman(h)
cfg.binary=True; cfg.hiddenGeom=False; cfg.helpers=False
cfg.expressions=False; cfg.useMaterials=True; cfg.yUpFaceZ=True
out = r"C:\Users\Administrator\Desktop\AETHERION-WCS\rigwork\frost_maiden_body.fbx"
try:
    mh2fbx.exportFbx(out, cfg)
    print("FBX_DONE:", os.path.exists(out), os.path.getsize(out) if os.path.exists(out) else 0)
except Exception as e:
    import traceback; traceback.print_exc()
