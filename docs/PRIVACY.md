# Privacy

## Client-Side Telemetry

The updater can send one minimal event when configured with `telemetryUrl`:

- event name, such as `check`, `update`, or `error`
- status
- updater version
- pack version
- random install id
- timestamp

The random install id is stored in the updater state file and is not based on hardware,
Minecraft accounts, usernames, or launcher data.

If `telemetryUrl` is empty, client-side telemetry is disabled. Users can also uncheck
the telemetry option in the GUI.

## What Is Not Collected

The updater does not collect:

- running process lists or installed programs
- Minecraft credentials, tokens, accounts, or nicknames
- hardware IDs, disk serials, MAC addresses, or Windows account names
- file contents, folder listings, saves, screenshots, or logs

## Server Access Logs

The download server may log standard HTTP access metadata:

- source IP address
- request path
- timestamp
- HTTP status
- bytes transferred
- user agent

Use raw IP logs only for short-term operations and abuse protection. Keep long-term
statistics aggregated by day, pack version, and rough network region instead of raw IP.

