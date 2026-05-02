from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools" / "manifest-builder" / "build_manifest.py"
SPEC = importlib.util.spec_from_file_location("build_manifest", MODULE_PATH)
assert SPEC and SPEC.loader
build_manifest = importlib.util.module_from_spec(SPEC)
sys.modules["build_manifest"] = build_manifest
SPEC.loader.exec_module(build_manifest)


class ManifestBuilderTests(unittest.TestCase):
    def test_builder_includes_only_managed_safe_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            source = tmp_path / "source"
            output = tmp_path / "output"
            (source / "mods").mkdir(parents=True)
            (source / "mods" / "a.jar").write_bytes(b"jar")
            (source / "mods" / "old.jar.bak").write_bytes(b"secret")
            (source / "mods" / ".connector").mkdir()
            (source / "mods" / ".connector" / "cache.jar").write_bytes(b"cache")
            (source / "world").mkdir()
            (source / "world" / "level.dat").write_bytes(b"world")
            (source / "server.properties").write_text("server-port=25565", encoding="utf-8")

            manifest = build_manifest.build_manifest(
                source=source,
                output=output,
                base_url="https://example.test/downloads/experimental",
                pack_id="pack",
                channel="experimental",
                version="v1",
                release_number=202605020001,
                managed_dirs=["mods"],
            )

            self.assertEqual([entry["path"] for entry in manifest["files"]], ["mods/a.jar"])
            self.assertEqual(manifest["releaseNumber"], 202605020001)
            self.assertTrue((output / "blobs").is_dir())

    def test_rejects_world_as_managed_dir(self) -> None:
        self.assertIn("world", build_manifest.BLOCKED_TOP_LEVEL)


if __name__ == "__main__":
    unittest.main()
