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
        }.issubset(ids))


if __name__ == "__main__":
    unittest.main()
