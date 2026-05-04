from __future__ import annotations

import importlib.util
import json
import subprocess
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
            (source / "mods" / "desktop.ini").write_bytes(b"windows metadata")
            (source / "mods" / "old.jar.bak").write_bytes(b"secret")
            (source / "mods" / "client-private-with-map.zip").write_bytes(b"secret")
            (source / "mods" / "documentation" / "palladium").mkdir(parents=True)
            (source / "mods" / "documentation" / "palladium" / "abilities.html").write_text("docs", encoding="utf-8")
            (source / "mods" / ".connector").mkdir()
            (source / "mods" / ".connector" / "cache.jar").write_bytes(b"cache")
            (source / "saves").mkdir()
            (source / "saves" / "world").mkdir()
            (source / "saves" / "world" / "level.dat").write_bytes(b"save")
            (source / "screenshots").mkdir()
            (source / "screenshots" / "shot.png").write_bytes(b"png")
            (source / "options.txt").write_text("fov:0.5", encoding="utf-8")
            (source / "servers.dat").write_bytes(b"servers")
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
                delete_globs=[],
            )

            self.assertEqual([entry["path"] for entry in manifest["files"]], ["mods/a.jar"])
            self.assertEqual(manifest["releaseNumber"], 202605020001)
            self.assertEqual(manifest["managedDirs"], ["mods"])
            self.assertTrue((output / "blobs").is_dir())

    def test_rejects_world_as_managed_dir(self) -> None:
        self.assertIn("world", build_manifest.BLOCKED_TOP_LEVEL)

    def test_incremental_build_changes_only_changed_hashes(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            source = tmp_path / "source"
            output = tmp_path / "output"
            (source / "mods").mkdir(parents=True)
            file_path = source / "mods" / "a.jar"
            file_path.write_bytes(b"v1")

            first = build_manifest.build_manifest(
                source=source,
                output=output,
                base_url="https://example.test/downloads/experimental",
                pack_id="pack",
                channel="experimental",
                version="v1",
                release_number=1,
                managed_dirs=["mods"],
                delete_globs=[],
            )
            first_hash = first["files"][0]["sha256"]
            self.assertTrue((output / "blobs" / first_hash[:2] / first_hash).is_file())

            file_path.write_bytes(b"v2")
            second = build_manifest.build_manifest(
                source=source,
                output=output,
                base_url="https://example.test/downloads/experimental",
                pack_id="pack",
                channel="experimental",
                version="v2",
                release_number=2,
                managed_dirs=["mods"],
                delete_globs=[],
            )
            second_hash = second["files"][0]["sha256"]
            self.assertNotEqual(first_hash, second_hash)
            self.assertTrue((output / "blobs" / second_hash[:2] / second_hash).is_file())

    def test_normalize_relative_rejects_escape(self) -> None:
        with self.assertRaises(ValueError):
            build_manifest.normalize_relative(Path("../secrets.env"))

    def test_cli_writes_updater_self_update_fields(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            source = tmp_path / "source"
            output = tmp_path / "output"
            (source / "mods").mkdir(parents=True)
            (source / "mods" / "a.jar").write_bytes(b"jar")

            subprocess.run(
                [
                    sys.executable,
                    str(MODULE_PATH),
                    "--source",
                    str(source),
                    "--output",
                    str(output),
                    "--base-url",
                    "https://example.test/downloads/experimental",
                    "--version",
                    "v1",
                    "--release-number",
                    "1",
                    "--required-updater-version",
                    "0.1.4",
                    "--updater-download-url",
                    "https://example.test/downloads/updater.zip",
                    "--updater-page-url",
                    "https://example.test/experimental.html",
                    "--updater-message",
                    "Обновите апдейтер.",
                ],
                check=True,
                stdout=subprocess.DEVNULL,
            )

            latest = json.loads((output / "latest.json").read_text(encoding="utf-8"))
            self.assertEqual(latest["requiredUpdaterVersion"], "0.1.4")
            self.assertEqual(latest["updaterDownloadUrl"], "https://example.test/downloads/updater.zip")
            self.assertEqual(latest["updaterPageUrl"], "https://example.test/experimental.html")
            self.assertEqual(latest["updaterMessage"], "Обновите апдейтер.")

    def test_disabled_client_mods_become_delete_globs(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            tmp_path = Path(tmp)
            source = tmp_path / "source"
            (source / "disabled-client-mods").mkdir(parents=True)
            (source / "disabled-client-mods" / "create-1.20.1-6.0.8.jar").write_bytes(b"jar")

            self.assertEqual(
                build_manifest.build_disabled_client_delete_globs(source),
                ["mods/create-1.20.1-6.0.8.jar"],
            )


if __name__ == "__main__":
    unittest.main()
