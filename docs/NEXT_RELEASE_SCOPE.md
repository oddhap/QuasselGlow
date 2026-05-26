# Next Release Scope

This document proposes the next focused reliability slice after `v0.2.9`.

## Target

- Proposed version: `v0.2.10`
- Release theme: daily-driver reliability for local user state and discreet visible failures

## Why This Scope

QuasselGlow already covers core connection, backlog loading, message sending, theme selection, tray support, local message cache, and broad localization. The next release should make the desktop client more trustworthy when local state cannot be loaded, saved, protected, or refreshed.

## Must Ship

- Connection preferences health
  Report load and save failures through explicit Infrastructure result types, while still allowing the desktop client to keep running with safe defaults or current in-memory state.
- Credential protection health
  Treat remembered-login saves without operating-system credential protection as degraded credential protection, visible after save and recovered after a successful save without a remembered secret or with protected storage.
- Message cache health
  Report local message-cache read and write failures as a global degraded cache condition, without blocking chat use or treating the failure as Quassel core backlog failure.
- Status-area visibility
  Show the highest-priority active local user state failure in the existing status area: connection preferences first, credential protection second, message cache third.
- Automatic recovery
  Clear each visible failure automatically after a later successful operation proves that same local user state boundary is healthy again.
- Regression coverage
  Add tests for settings load/save failures, degraded credential protection, message-cache failures, priority ordering, and recovery.

## Explicitly Deferred

- New toast, modal, or notification surfaces for local state failures
- Per-buffer message-cache warnings
- Full operating-system keychain integration for macOS and Linux
- Persistent composer drafts and input history
- Broader Quassel protocol sync expansion

## Exit Criteria

- `dotnet build Quassel.slnx -c Release` succeeds
- Application and protocol tests pass
- Local state failures remain discreet but visible in the existing status area
- Chat use continues when settings or cache persistence is degraded
- Release notes describe the local-state reliability behavior and known credential-protection limitations
