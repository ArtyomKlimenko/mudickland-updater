# MuDickLand Updater

Open-source Windows updater for the MuDickLand Experimental Minecraft Forge modpack.

The updater only manages modpack files. It does not implement Minecraft authentication,
does not bypass account licensing, and does not ship Minecraft itself.

## What It Does

- Downloads a signed `latest.json` + `manifest.json` from the pack server.
- Verifies `manifest.json.sig` before touching local files.
- Downloads only missing or changed files.
- Checks every downloaded file with SHA-256.
- Deletes stale files only inside manifest-managed directories.
- Keeps user data outside managed directories, such as saves, screenshots, `options.txt`,
  and launcher account files.

## User Flow

1. Download the latest updater release from GitHub Releases.
2. Run `MuDickLand.Updater.exe`.
3. Pick an install directory, for example `%APPDATA%\.minecraft-pz-exp`.
4. Press `Update`.
5. Open your Minecraft launcher and point its game directory to the selected folder.

## Managed Directories

The first pack channel is expected to manage:

- `mods`
- `config`
- `defaultconfigs`
- `kubejs`
- `tacz`
- `mod_data`
- `data`
- `patchouli_books`
- `fancymenu_data`

The manifest builder rejects common server-only and private paths, including worlds,
saves, logs, crash reports, server lists, bridge env files, backups, and private archives.

## Server-Side Manifest Build

Example:

```bash
python3 tools/manifest-builder/build_manifest.py \
  --source /opt/minecraft-zomboid/experimental/pz-exp \
  --output /opt/minecraft-zomboid/site/public/downloads/experimental \
  --base-url https://YOUR_DOMAIN/downloads/experimental \
  --version experimental-2026.05.02 \
  --private-key /home/o1o4/mudickland-updater-signing/manifest_private.pem
```

This creates:

- `latest.json`
- `manifest.json`
- `manifest.json.sig`
- `blobs/<sha-prefix>/<sha256>`

`releaseNumber` is a monotonic UTC timestamp by default. The updater stores the
last installed release number and refuses older signed manifests to reduce replay
or downgrade risk.

## Local Configuration

The updater reads optional `updater.json` next to the executable:

```json
{
  "latestUrl": "https://YOUR_DOMAIN/downloads/experimental/latest.json",
  "siteUrl": "https://YOUR_DOMAIN/",
  "telegramUrl": "https://t.me/pz_family_chat_bot",
  "supportUrl": "https://github.com/ArtyomKlimenko/mudickland-updater/issues",
  "telemetryUrl": "https://YOUR_DOMAIN/api/updater-event",
  "launcherPath": ""
}
```

If `telemetryUrl` is empty, the updater sends no client-side telemetry.

## Build

```bash
dotnet publish src/MuDickLand.Updater/MuDickLand.Updater.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true
```

GitHub Actions builds the Windows release artifact on tags like `v0.1.0`.

## Privacy

See `docs/PRIVACY.md`. The updater never collects process lists, account tokens,
Minecraft credentials, hardware IDs, or folder contents.

## Security

See `docs/SECURITY.md`.
