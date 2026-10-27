from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import validate_visual_assets as validator
import race_balance_report as balance_report


class VisualAssetValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_dir.name)

    def tearDown(self) -> None:
        self.temp_dir.cleanup()

    def write_json(self, name: str, payload: object) -> Path:
        path = self.root / name
        path.write_text(json.dumps(payload), encoding="utf-8")
        return path

    def test_valid_model_entry_has_no_errors(self) -> None:
        path = self.write_json("models.json", {
            "2000": {
                "race": "Test", "baseModel": "characters/test.vmdl",
                "customModel": "", "fx": "particles/test.vpcf",
                "tint": [0, 127, 255], "glow": 2
            }
        })
        errors, warnings, ids = validator.validate_models(path)
        self.assertEqual(errors, [])
        self.assertEqual(ids, {2000})

    def test_boolean_is_not_accepted_as_tint_integer(self) -> None:
        path = self.write_json("models.json", {
            "2000": {"race": "Test", "baseModel": "test.vmdl", "tint": [True, 0, 0]}
        })
        errors, _, _ = validator.validate_models(path)
        self.assertTrue(any("tint values must be integers" in item for item in errors))

    def test_invalid_glow_is_rejected(self) -> None:
        path = self.write_json("models.json", {
            "2000": {"race": "Test", "baseModel": "test.vmdl", "glow": 11}
        })
        errors, _, _ = validator.validate_models(path)
        self.assertTrue(any("glow must be a number" in item for item in errors))

    def test_duplicate_race_ids_are_reported(self) -> None:
        path = self.write_json("races.json", [
            {"id": 2000, "name": "A"},
            {"id": 2000, "name": "B"},
        ])
        errors, _ = validator.validate_races(path, {2000})
        self.assertTrue(any("duplicate race ID 2000" in item for item in errors))

    def test_missing_model_mapping_is_a_warning(self) -> None:
        path = self.write_json("races.json", [{"id": 9999, "name": "Unmapped"}])
        errors, warnings = validator.validate_races(path, set())
        self.assertEqual(errors, [])
        self.assertTrue(any("no entry in models.json" in item for item in warnings))

    def test_invalid_json_has_actionable_location(self) -> None:
        path = self.root / "broken.json"
        path.write_text('{"race": ', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "invalid JSON"):
            validator.load_json(path)


    def test_balance_report_counts_duplicate_ids_across_catalogs(self) -> None:
        first = self.write_json("first.json", [{
            "id": 2000, "name": "Test One", "tier": 1, "division": 1,
            "abilities": [{"n": "Hit", "t": "Active", "effect": "aoe_damage", "value": 25, "cd": 2, "max": 5}]
        }])
        second = self.write_json("second.json", [{
            "id": 2000, "name": "Test Two", "tier": 2, "division": 3,
            "abilities": [{"n": "Guard", "t": "Passive", "effect": "shield", "value": 2, "cd": 0, "max": 3}]
        }])
        report = balance_report.build_report([first, second])
        self.assertIn("Race definitions scanned: **2**", report)
        self.assertIn("Duplicate IDs across catalogs: **1**", report)
        self.assertIn("`aoe_damage`", report)

    def test_balance_report_flags_races_without_abilities(self) -> None:
        path = self.write_json("empty.json", [{"id": 3000, "name": "Empty", "abilities": []}])
        report = balance_report.build_report([path])
        self.assertIn("Races with no abilities: **1**", report)


if __name__ == "__main__":
    unittest.main()
