
import sys, os, inspect
mh = r"C:\Program Files\makehuman-community\makehuman"
for p in ["", "lib", "core", "shared", "apps"]:
    sys.path.insert(0, os.path.join(mh, p))
os.chdir(mh)
from export import ExportConfig
print("--- ExportConfig __init__ ---")
print(inspect.getsource(ExportConfig.__init__))
props=[n for n,v in inspect.getmembers(type(ExportConfig()), lambda o: isinstance(o,property))]
print("PROPERTIES:", props)
# методы
meths=[n for n in dir(ExportConfig) if not n.startswith("_")]
print("MEMBERS:", meths)
