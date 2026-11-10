from __future__ import annotations

import importlib.util
import json
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
            refs, missing, counts, audio = MODULE.scan(root)
            self.assertEqual(list(refs), ["particles/aether_dash.vpcf"])
            self.assertEqual(missing, ["particles/aether_dash.vpcf"])
            self.assertEqual(counts["source_vpcf"], 0)
            self.assertEqual(audio["unique_paths"], 0)

    def test_compiled_particle_resource_satisfies_reference(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "configs" / "assets" / "models.json"
            source.parent.mkdir(parents=True)
            source.write_text('{"fx":"particles/aether_cast.vpcf"}', encoding="utf-8")
            compiled = root / "particles" / "aether_cast.vpcf_c"
            compiled.parent.mkdir(parents=True)
            compiled.write_bytes(b"compiled-resource-placeholder")
            refs, missing, counts, audio = MODULE.scan(root)
            self.assertIn("particles/aether_cast.vpcf", refs)
            self.assertEqual(missing, [])
            self.assertEqual(counts["compiled_vpcf"], 1)

    def test_disabled_custom_audio_is_planned_not_reported_as_missing(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = root / "configs" / "assets" / "sounds.json"
            config.parent.mkdir(parents=True)
            config.write_text(json.dumps({
                "1081": {"sound": "ui.error", "customSound": "sounds/ui/error.vsnd"},
                "5006": {"event": "system", "category": "ui", "sound": "ui.error", "customSound": "sounds/ui/error.vsnd"},
            }), encoding="utf-8")
            refs, missing, counts, audio = MODULE.scan(root)
            self.assertEqual(audio["configured_entries"], 2)
            self.assertEqual(audio["active_paths"], [])
            self.assertEqual(audio["missing_paths"], [])
            self.assertEqual(audio["planned_paths"], ["sounds/ui/error.vsnd"])
            self.assertEqual(audio["missing_planned_paths"], ["sounds/ui/error.vsnd"])
            report = MODULE.render(refs, missing, counts, audio)
            self.assertIn("Planned disabled customSound entries: **2**", report)
            self.assertIn("PLANNED", report)
            self.assertNotIn("MISSING ACTIVE", report)

    def test_active_custom_audio_missing_is_reported(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = root / "configs" / "assets" / "sounds.json"
            config.parent.mkdir(parents=True)
            config.write_text(json.dumps({
                "1": {"sound": "ui.error", "customSound": "sounds/ui/error.vsnd", "customSoundEnabled": True}
            }), encoding="utf-8")
            refs, missing, counts, audio = MODULE.scan(root)
            self.assertEqual(audio["active_paths"], ["sounds/ui/error.vsnd"])
            self.assertEqual(audio["missing_paths"], ["sounds/ui/error.vsnd"])
            self.assertEqual(audio["planned_paths"], [])

    def test_missing_custom_audio_source_is_recognized(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = root / "configs" / "assets" / "sounds.json"
            config.parent.mkdir(parents=True)
            config.write_text(json.dumps({
                "1": {"customSound": "sounds/ui/click.vsnd", "customSoundEnabled": True}
            }), encoding="utf-8")
            wav = root / "content" / "csgo_addons" / "aetherion" / "sounds" / "ui" / "click.wav"
            wav.parent.mkdir(parents=True)
            wav.write_bytes(b"source-audio-placeholder")
            refs, missing, counts, audio = MODULE.scan(root)
            self.assertEqual(audio["missing_paths"], [])
            self.assertEqual(counts["source_audio"], 1)

    def test_report_warns_about_quarantined_models(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            quarantined = root / "gb_models" / "extracted" / "sample" / "model.vmdl_c"
            quarantined.parent.mkdir(parents=True)
            quarantined.write_bytes(b"placeholder")
            refs, missing, counts, audio = MODULE.scan(root)
            report = MODULE.render(refs, missing, counts, audio)
            self.assertIn("not license-cleared", report)
            self.assertEqual(counts["quarantined_model_files"], 1)


if __name__ == "__main__":
    unittest.main()
