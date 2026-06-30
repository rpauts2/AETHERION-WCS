
import sys, os, inspect
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
import human as hm, skeleton
print("=== setSkeleton ===")
print(inspect.getsource(hm.Human.setSkeleton))
print("=== getBoundMesh ===")
print(inspect.getsource(hm.Human.getBoundMesh))
print("=== skeleton.load sig ===")
print(inspect.signature(skeleton.load))
print("=== addBoundMesh? ===")
print([n for n in dir(hm.Human) if 'ound' in n or 'kelet' in n])
