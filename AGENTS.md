# MuDickLand Updater Agent Rules

## Release Sync

- When a user asks to update the public updater, installer, UI, version, release, or download, treat it as an end-to-end release task unless they explicitly say it is local-only.
- In the same task, update every public surface that can serve stale updater behavior: source code, version constants, README/docs, `updater*.json`, site download zip, site text, `latest.json` required updater metadata/message, Telegram announcement draft, GitHub commit, tag, and release artifact.
- Verify the actual downloadable artifact after packaging. Do not stop after changing source code or a local zip.
