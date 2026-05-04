#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
from datetime import datetime, timezone
from dataclasses import dataclass
from pathlib import Path


DEFAULT_MANAGED_DIRS = [
    "mods",
    "config",
    "defaultconfigs",
    "kubejs",
    "tacz",
    "mod_data",
    "data",
    "patchouli_books",
    "fancymenu_data",
]

BLOCKED_TOP_LEVEL = {
    "world",
    "saves",
    "logs",
    "crash-reports",
    "libraries",
    "bridge",
    "backups",
    "export",
    "import",
    "experimental",
}

BLOCKED_FILE_NAMES = {
    "server.properties",
    "ops.json",
    "whitelist.json",
    "banned-ips.json",
    "banned-players.json",
    "usercache.json",
    "usernamecache.json",
    "eula.txt",
    "desktop.ini",
    "thumbs.db",
}


@dataclass(frozen=True)
class FileEntry:
    path: str
    size: int
    sha256: str
    url: str


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as src:
        for chunk in iter(lambda: src.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def normalize_relative(path: Path) -> str:
    raw = path.as_posix()
    if raw.startswith("/") or ".." in path.parts:
        raise ValueError(f"unsafe path: {path}")
    return raw


def should_include(relative: Path, full_path: Path) -> bool:
    if full_path.is_symlink():
        return False

    parts = relative.parts
    if not parts:
        return False

    top = parts[0]
    name = full_path.name
    lower_name = name.lower()

    if any(part.startswith(".") for part in parts):
        return False
    if top in BLOCKED_TOP_LEVEL:
        return False
    if top.startswith("world"):
        return False
    if len(parts) >= 2 and parts[0] == "mods" and parts[1] == "documentation":
        return False
    if lower_name in BLOCKED_FILE_NAMES:
        return False
    if ".env" in lower_name or "bridge-agent" in lower_name:
        return False
    if ".bak" in lower_name or lower_name.endswith((".tmp", ".log", ".pid")):
        return False
    if "private" in lower_name or "do-not-share" in lower_name:
        return False

    return True


def atomic_write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True).encode("utf-8")
    with tempfile.NamedTemporaryFile("wb", delete=False, dir=path.parent) as tmp:
        tmp.write(data)
        tmp_path = Path(tmp.name)
    os.replace(tmp_path, path)


def copy_blob(source: Path, blobs_dir: Path, sha256: str) -> Path:
    target = blobs_dir / sha256[:2] / sha256
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists() or sha256_file(target) != sha256:
        tmp = target.with_suffix(".tmp")
        shutil.copy2(source, tmp)
        os.replace(tmp, target)
    return target


def build_manifest(
    source: Path,
    output: Path,
    base_url: str,
    pack_id: str,
    channel: str,
    version: str,
    release_number: int,
    managed_dirs: list[str],
    delete_globs: list[str],
) -> dict:
    files: list[FileEntry] = []
    blobs_dir = output / "blobs"
    base_url = base_url.rstrip("/")

    for managed_dir in managed_dirs:
        root = source / managed_dir
        if not root.exists():
            continue
        if not root.is_dir():
            raise ValueError(f"managed path is not a directory: {root}")

        for full_path in sorted(path for path in root.rglob("*") if path.is_file()):
            relative = full_path.relative_to(source)
            if not should_include(relative, full_path):
                continue

            rel = normalize_relative(relative)
            digest = sha256_file(full_path)
            copy_blob(full_path, blobs_dir, digest)
            files.append(
                FileEntry(
                    path=rel,
                    size=full_path.stat().st_size,
                    sha256=digest,
                    url=f"{base_url}/blobs/{digest[:2]}/{digest}",
                )
            )

    manifest = {
        "packId": pack_id,
        "channel": channel,
        "version": version,
        "releaseNumber": release_number,
        "managedDirs": managed_dirs,
        "deletePolicy": {"enabled": True, "globs": delete_globs},
        "files": [entry.__dict__ for entry in files],
    }
    return manifest


def build_disabled_client_delete_globs(source: Path) -> list[str]:
    disabled_dir = source / "disabled-client-mods"
    if not disabled_dir.is_dir():
        return []

    globs = []
    for file in sorted(path for path in disabled_dir.iterdir() if path.is_file()):
        if file.name.startswith("."):
            continue
        globs.append(f"mods/{file.name}")
    return globs


def sign_manifest(manifest_path: Path, signature_path: Path, private_key: Path) -> None:
    tmp = signature_path.with_suffix(".sig.tmp")
    subprocess.run(
        [
            "openssl",
            "dgst",
            "-sha256",
            "-sign",
            str(private_key),
            "-out",
            str(tmp),
            str(manifest_path),
        ],
        check=True,
    )
    os.replace(tmp, signature_path)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build MuDickLand updater manifests.")
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--release-number", type=int)
    parser.add_argument("--pack-id", default="mudickland-experimental")
    parser.add_argument("--channel", default="experimental")
    parser.add_argument("--private-key", type=Path)
    parser.add_argument("--managed-dir", action="append", dest="managed_dirs")
    parser.add_argument("--delete-glob", action="append", dest="delete_globs")
    parser.add_argument("--no-disabled-client-delete-globs", action="store_true")
    parser.add_argument("--required-updater-version", default="0.1.5")
    parser.add_argument("--updater-download-url")
    parser.add_argument("--updater-page-url")
    parser.add_argument("--updater-message")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = args.source.resolve()
    output = args.output.resolve()
    managed_dirs = args.managed_dirs or DEFAULT_MANAGED_DIRS
    delete_globs = args.delete_globs or []
    if not args.no_disabled_client_delete_globs:
        delete_globs.extend(build_disabled_client_delete_globs(source))
    delete_globs = sorted(set(delete_globs))

    for managed_dir in managed_dirs:
        normalized = Path(managed_dir).as_posix().strip("/")
        if not normalized or "/" in normalized or normalized in BLOCKED_TOP_LEVEL or normalized.startswith("world"):
            raise ValueError(f"invalid managed dir: {managed_dir}")

    for glob in delete_globs:
        normalized = Path(glob).as_posix().strip("/")
        first = normalized.split("/", 1)[0]
        if not normalized or first not in managed_dirs or ".." in Path(normalized).parts or normalized.startswith("/"):
            raise ValueError(f"invalid delete glob: {glob}")

    output.mkdir(parents=True, exist_ok=True)
    release_number = args.release_number or int(datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S"))
    manifest = build_manifest(
        source=source,
        output=output,
        base_url=args.base_url,
        pack_id=args.pack_id,
        channel=args.channel,
        version=args.version,
        release_number=release_number,
        managed_dirs=managed_dirs,
        delete_globs=delete_globs,
    )

    manifest_path = output / "manifest.json"
    signature_path = output / "manifest.json.sig"
    latest_path = output / "latest.json"
    atomic_write_json(manifest_path, manifest)

    if args.private_key:
        sign_manifest(manifest_path, signature_path, args.private_key)

    latest = {
        "packId": args.pack_id,
        "channel": args.channel,
        "latestVersion": args.version,
        "releaseNumber": release_number,
        "manifestUrl": f"{args.base_url.rstrip('/')}/manifest.json",
        "signatureUrl": f"{args.base_url.rstrip('/')}/manifest.json.sig",
        "requiredUpdaterVersion": args.required_updater_version,
        "changelogUrl": f"{args.base_url.rstrip('/')}/changelog.html",
    }
    if args.updater_download_url:
        latest["updaterDownloadUrl"] = args.updater_download_url
    if args.updater_page_url:
        latest["updaterPageUrl"] = args.updater_page_url
    if args.updater_message:
        latest["updaterMessage"] = args.updater_message
    atomic_write_json(latest_path, latest)

    print(f"files={len(manifest['files'])}")
    print(f"manifest={manifest_path}")
    print(f"latest={latest_path}")
    if args.private_key:
        print(f"signature={signature_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
