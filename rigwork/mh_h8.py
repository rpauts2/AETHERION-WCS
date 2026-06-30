
import sys, os, inspect
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
import human as humanmod, skeleton
# как получаются веса
print("=== Human.getVertexWeights ===")
try: print(inspect.getsource(humanmod.Human.getVertexWeights))
except Exception as e: print(e)
print("=== Skeleton weight methods ===")
sm=[n for n in dir(skeleton.Skeleton) if 'eight' in n.lower() or 'uild' in n.lower()]
print(sm)
# mh2fbx around line 110-130
src=open(os.path.join(mh,"plugins","9_export_fbx","mh2fbx.py")).read().splitlines()
print("=== mh2fbx 108-130 ===")
print("\n".join(src[107:130]))
