from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts" / "audit_audio_assets.py"
SPEC = importlib.util.spec_from_file_location("audit_audio_assets", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class AudioAssetAuditTests(unittest.TestCase):
    def _write_config(self, root: Path, entries):
        config = root / "configs" / "assets" / "sounds.json"
        config.parent.mkdir(parents=True)
        config.write_text(json.dumps(entries), encoding="utf-8")
        return config

    def test_finds_missing_custom_audio_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {
                "10": {"customSound": "sounds/ui/click.vsnd"},
                "11": {"customSound": "sounds/ui/click.vsnd"},
                "12": {"customSound": ""},
            })
            result = MODULE.audit(config, root)
            self.assertEqual(result["entries"], 3)
            self.assertEqual(result["empty"], 1)
            self.assertEqual(result["missing"], ["sounds/ui/click.vsnd"])
            self.assertEqual(result["references"]["sounds/ui/click.vsnd"], ["10", "11"])

    def test_recognizes_source_wav_in_addon_content_tree(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {"1": {"customSound": "sounds/ui/click.vsnd"}})
            source = root / "content" / "csgo_addons" / "aetherion" / "sounds" / "ui" / "click.wav"
            source.parent.mkdir(parents=True)
            source.write_bytes(b"RIFF-test")
            result = MODULE.audit(config, root)
            self.assertEqual(result["missing"], [])
            self.assertIn("sounds/ui/click.vsnd", result["found_source"])

    def test_recognizes_compiled_vsnd_c_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {"1": {"customSound": "sounds/ui/click.vsnd"}})
            compiled = root / "game" / "csgo_addons" / "aetherion" / "sounds" / "ui" / "click.vsnd_c"
            compiled.parent.mkdir(parents=True)
            compiled.write_bytes(b"compiled-placeholder")
            result = MODULE.audit(config, root)
            self.assertEqual(result["missing"], [])
            self.assertIn("sounds/ui/click.vsnd", result["found_compiled"])

    def test_rejects_path_traversal_and_wrong_source1_style_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {
                "1": {"customSound": "sounds/../secret.vsnd"},
                "2": {"customSound": "sound/ui/click.wav"},
                "3": {"customSound": "sounds/ui/click.wav"},
            })
            result = MODULE.audit(config, root)
            self.assertEqual(len(result["malformed"]), 3)
            self.assertTrue(any("unsafe customSound path" in message for message in result["malformed"]))
            self.assertTrue(any("must start with sounds/" in message for message in result["malformed"]))
            self.assertTrue(any("must end in .vsnd" in message for message in result["malformed"]))

    def test_render_calls_out_that_playback_is_not_proven(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {"1": {"customSound": "sounds/ui/click.vsnd"}})
            report = MODULE.render(MODULE.audit(config, root))
            self.assertIn("does not prove", report)
            self.assertIn("MISSING", report)


if __name__ == "__main__":
    unittest.main()
