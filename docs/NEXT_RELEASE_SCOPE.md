# Next Release Scope

This document proposes the next focused reliability slice after `v0.2.10`.

## Target

- Proposed version: `v0.2.11`
- Release theme: daily-driver reliability for lost Quassel core connections

## Why This Scope

QuasselGlow already covers core connection, backlog loading, message sending, theme selection, tray support, local message cache, broad localization, and discreet visible failures for local user state. The next release should make the desktop client more trustworthy when an established Quassel core session silently dies or the network path stops returning data.

## Must Ship

- Quassel core heartbeat monitoring
  Send DataStream heartbeat requests after session sync and move the desktop client out of connected chat use when the Quassel core does not reply.
- Qt-compatible heartbeat serialization
  Encode and decode heartbeat timestamps as Qt `QDateTime` values so heartbeat packets remain compatible with Quassel's DataStream protocol.
- Automatic reconnect preference
  Add a saved connection preference for reconnecting after an active Quassel core session is lost, separate from startup auto-connect.
- Manual disconnect boundary
  Treat a user-requested disconnect as intentional and do not auto-reconnect from that state.
- Regression coverage
  Add tests for heartbeat timestamp serialization, missed heartbeat detection, reconnect scheduling, manual disconnect suppression, and persistence of the reconnect preference.

## Explicitly Deferred

- New toast, modal, or notification surfaces for local state failures
- More aggressive reconnect backoff controls
- Reconnect attempts after failed first login
- Full operating-system keychain integration for macOS and Linux
- Broader Quassel protocol sync expansion

## Exit Criteria

- `dotnet build Quassel.slnx -c Release` succeeds
- Application and protocol tests pass
- Lost Quassel core connections become visible without waiting for user input
- Automatic reconnect retries after a live session is lost and remains disabled after manual disconnect
- Release notes describe the heartbeat detection and reconnect preference behavior
