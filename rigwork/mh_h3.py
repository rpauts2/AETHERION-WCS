
import sys, os, types, importlib.util, inspect
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
import getpath, numpy as np
import human, files3d
from core import G
import skeleton

base = files3d.loadMesh(getpath.getSysDataPath("3dobjs/base.obj"))
h = human.Human(base)
class StubApp:
    def __init__(s,hu): s.selectedHuman=hu; s.settings={}
    def getSetting(s,k,d=None): return d
G.app = StubApp(h)
ref = skeleton.load(getpath.getSysDataPath("rigs/game_engine.mhskel"), h.meshData)
h.setSkeleton(ref)
print("SKEL_BONES:", ref.getBoneCount())

# загрузка плагина как пакета
pkgdir = os.path.join(mh,"plugins","9_export_fbx")
pkg = types.ModuleType("fbxexport"); pkg.__path__=[pkgdir]; sys.modules["fbxexport"]=pkg
def loadmod(name):
    spec=importlib.util.spec_from_file_location("fbxexport."+name, os.path.join(pkgdir,name+".py"))
    m=importlib.util.module_from_spec(spec); sys.modules["fbxexport."+name]=m; spec.loader.exec_module(m); return m
try:
    fbx_utils=loadmod("fbx_utils")
    mh2fbx=loadmod("mh2fbx")
    print("FBX_IMPORT_OK")
    print("exportFbx sig:", inspect.signature(mh2fbx.exportFbx))
    src=inspect.getsource(mh2fbx.exportFbx)
    print("--- exportFbx source (first 1500) ---")
    print(src[:1500])
except Exception as e:
    import traceback; traceback.print_exc()
