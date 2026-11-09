from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "audit_asset_references.py"
SPEC = importlib.util.spec_from_file_location("audit_asset_references", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class AssetReferenceAuditTests(unittest.TestCase):
    def test_only_custom_particle_namespace_is_audited(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "src" / "Races" / "Effects.cs"
            source.parent.mkdir(parents=True)
            source.write_text(
                'engine.SpawnParticle("particles/aether_dash.vpcf");\n'
                'engine.SpawnParticle("particles/burning_fx/env_fire_small.vpcf");\n',
                encoding="utf-8",
            )
            refs, missing, counts = MODULE.scan(root)
            self.assertEqual(list(refs), ["particles/aether_dash.vpcf"])
            self.assertEqual(missing, ["particles/aether_dash.vpcf"])
            self.assertEqual(counts["source_vpcf"], 0)

    def test_compiled_resource_satisfies_reference(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "configs" / "assets" / "models.json"
            source.parent.mkdir(parents=True)
            source.write_text('{"fx":"particles/aether_cast.vpcf"}', encoding="utf-8")
            compiled = root / "particles" / "aether_cast.vpcf_c"
            compiled.parent.mkdir(parents=True)
            compiled.write_bytes(b"compiled-resource-placeholder")
            refs, missing, counts = MODULE.scan(root)
            self.assertIn("particles/aether_cast.vpcf", refs)
            self.assertEqual(missing, [])
            self.assertEqual(counts["compiled_vpcf"], 1)

    def test_report_warns_about_quarantined_models(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            quarantined = root / "gb_models" / "extracted" / "sample" / "model.vmdl_c"
            quarantined.parent.mkdir(parents=True)
            quarantined.write_bytes(b"placeholder")
            refs, missing, counts = MODULE.scan(root)
            report = MODULE.render(refs, missing, counts)
            self.assertIn("not license-cleared", report)
            self.assertEqual(counts["quarantined_model_files"], 1)


if __name__ == "__main__":
    unittest.main()
