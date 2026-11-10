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
                "10": {"customSound": "sound/ui/click.wav"},
                "11": {"customSound": "sound/ui/click.wav"},
                "12": {"customSound": ""},
            })
            result = MODULE.audit(config, root)
            self.assertEqual(result["entries"], 3)
            self.assertEqual(result["empty"], 1)
            self.assertEqual(result["missing"], ["sound/ui/click.wav"])
            self.assertEqual(result["references"]["sound/ui/click.wav"], ["10", "11"])

    def test_recognizes_addon_content_source_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {"1": {"customSound": "sound/ui/click.wav"}})
            source = root / "content" / "csgo_addons" / "aetherion" / "sound" / "ui" / "click.wav"
            source.parent.mkdir(parents=True)
            source.write_bytes(b"RIFF-test")
            result = MODULE.audit(config, root)
            self.assertEqual(result["missing"], [])
            self.assertIn("sound/ui/click.wav", result["found"])

    def test_rejects_path_traversal_and_unknown_extensions(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {
                "1": {"customSound": "sound/../secret.wav"},
                "2": {"customSound": "sound/ui/click.exe"},
            })
            result = MODULE.audit(config, root)
            self.assertEqual(result["malformed"], [
                "sound 1: unsafe customSound path: 'sound/../secret.wav'",
                "sound 2: unsupported customSound extension: 'sound/ui/click.exe'",
            ])

    def test_render_calls_out_that_playback_is_not_proven(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = self._write_config(root, {"1": {"customSound": "sound/ui/click.wav"}})
            report = MODULE.render(MODULE.audit(config, root))
            self.assertIn("does not prove", report)
            self.assertIn("MISSING", report)


if __name__ == "__main__":
    unittest.main()
