
import sys, os, inspect
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
import human as hm, skeleton
print("=== Human.getVertexWeights ===")
print(inspect.getsource(hm.Human.getVertexWeights))
print("=== Human.addBoundMesh ===")
print(inspect.getsource(hm.Human.addBoundMesh))
print("=== Skeleton.getVertexWeights ===")
try: print(inspect.getsource(skeleton.Skeleton.getVertexWeights))
except Exception as e: print("no:",e)
