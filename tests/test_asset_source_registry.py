from __future__ import annotations

import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "configs" / "assets" / "asset_sources.json"


class AssetSourceRegistryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))

    def test_registry_has_unique_candidate_ids(self):
        ids = [item["id"] for item in self.data["additional_research_candidates"]]
        self.assertEqual(len(ids), len(set(ids)))

    def test_cc0_candidates_have_source_and_license_evidence(self):
        for item in self.data["additional_research_candidates"]:
            if item.get("license") == "CC0-1.0":
                self.assertTrue(item.get("source_page"), item["id"])
                self.assertTrue(item.get("license_evidence"), item["id"])

    def test_new_practical_candidates_are_registered(self):
        ids = {item["id"] for item in self.data["additional_research_candidates"]}
        self.assertTrue({
            "gobkit-dinosaur-pack",
            "gobkit-animal-pack-vol-5",
            "polyyai-low-poly-guns-25",
            "kenney-animated-characters-protagonists",
            "kenney-animated-characters-retro",
            "kenney-animated-characters-survivors",
            "quaternius-universal-animation-library",
            "quaternius-ultimate-animated-character-pack",
            "quaternius-rpg-character-pack",
            "quaternius-modular-weapons-pack",
            "kaykit-dungeon-pack-free-1-1",
            "sigilsvault-modular-dungeon-kit",
            "quaternius-modular-dungeon-pack-41",
            "quaternius-modular-dungeons-pack-48",
            "spritecanvas-cc0-vfx-sheets",
        }.issubset(ids))

    def test_spritepilot_sound_inventory_has_source_and_license(self):
        item = next(
            item for item in self.data["additional_research_candidates"]
            if item["id"] == "spritepilot-sfx-78"
        )
        self.assertIn("78 CC0 sound effects", item["inventory"])
        self.assertIn("license_evidence", item)
        self.assertEqual(item["status"], "candidate_requires_manual_download")

    def test_universal_animation_library_free_tier_is_not_overstated(self):
        item = next(
            item for item in self.data["additional_research_candidates"]
            if item["id"] == "quaternius-universal-animation-library"
        )
        self.assertIn("60-70%", item["inventory"])
        self.assertEqual(item["license"], "CC0-1.0")


if __name__ == "__main__":
    unittest.main()
