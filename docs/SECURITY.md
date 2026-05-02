# Security

## Threat Model

The updater assumes the network can be hostile and that downloaded files cannot be
trusted until verified. The pack server can be mirrored or cached, but the updater
must only trust a manifest signed by the embedded public key.

## Manifest Verification

- `latest.json` points to `manifest.json` and `manifest.json.sig`.
- The updater downloads both files.
- The updater verifies `manifest.json.sig` with the public key embedded in source.
- If verification fails, the updater stops before writing any pack file.
- The signed manifest contains a monotonic `releaseNumber`; after an update, the
  updater refuses older releases for the same pack id.

The private key must stay outside the GitHub repository. The initial local key path is:

```text
/home/o1o4/mudickland-updater-signing/manifest_private.pem
```

## File Verification

- Every manifest file has `path`, `size`, `sha256`, and `url`.
- Network URLs must use HTTPS outside localhost testing.
- Every downloaded file is written to a temporary cache first.
- Size and SHA-256 must match before the file replaces the installed target.
- The updater refuses absolute paths, drive paths, `..`, invalid file names, and writes
  through reparse points.

## Delete Policy

Deletion is limited to files inside `managedDirs` declared by the signed manifest.
The updater must not delete saves, screenshots, logs, account files, launcher configs,
or any file outside the selected install directory.

## Server-Side Exclusions

The manifest builder excludes common private/server data:

- `world*`, `saves`, `logs`, `crash-reports`
- `server.properties`, `ops.json`, `whitelist.json`, `banned-*.json`
- `usercache.json`, `usernamecache.json`
- `bridge*`, `.env`, backups, private archives, do-not-share archives

## Reporting

Report security issues through GitHub Issues only for non-sensitive reports. For
private issues, use the support contact published on the MuDickLand site.
