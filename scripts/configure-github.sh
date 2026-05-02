#!/usr/bin/env bash
set -euo pipefail

OWNER="${OWNER:-ArtyomKlimenko}"
REPO="${REPO:-mudickland-updater}"
FULL="$OWNER/$REPO"

if ! command -v gh >/dev/null 2>&1; then
  echo "GitHub CLI (gh) is required for remote repo setup." >&2
  exit 1
fi

if ! gh repo view "$FULL" >/dev/null 2>&1; then
  gh repo create "$FULL" \
    --public \
    --description "Open-source updater for MuDickLand Experimental Minecraft modpack" \
    --source . \
    --remote origin \
    --push
fi

gh repo edit "$FULL" \
  --enable-issues=true \
  --enable-projects=false \
  --enable-wiki=false \
  --add-topic minecraft \
  --add-topic forge \
  --add-topic modpack \
  --add-topic updater \
  --add-topic windows

gh label create bug --repo "$FULL" --color d73a4a --description "Something is broken" --force
gh label create update-failed --repo "$FULL" --color b60205 --description "The updater failed while checking, downloading, verifying, or deleting files" --force
gh label create security --repo "$FULL" --color ee0701 --description "Security-sensitive behavior or hardening" --force
gh label create privacy --repo "$FULL" --color 5319e7 --description "Telemetry, logs, disclosure, or privacy policy" --force
gh label create feature --repo "$FULL" --color a2eeef --description "New updater or pack distribution capability" --force
gh label create pack-manifest --repo "$FULL" --color 0e8a16 --description "Manifest builder, pack metadata, or hosted file list" --force

gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  "/repos/$FULL/branches/main/protection" \
  -f required_pull_request_reviews='null' \
  -f enforce_admins=true \
  -f required_status_checks='{"strict":true,"contexts":["build-windows","test-manifest-builder"]}' \
  -f restrictions='null' \
  >/dev/null || echo "Branch protection was not applied. Check repository permissions."

echo "Configured $FULL"

