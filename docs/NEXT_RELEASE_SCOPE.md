# Next Release Scope

This document proposes the target and scope for the release after `v0.1.0`.

## Target

- Proposed version: `v0.2.0`
- Release theme: stabilize the MVP and make the desktop client feel safer to use as a daily Quassel companion

## Why This Scope

The current `v0.1.0` release already covers core connection, backlog loading, message sending, settings persistence, and broad language support. The next release should improve reliability and polish before the project takes on larger feature areas such as full sync coverage or richer user-list functionality.

## Must Ship

- Connection and session polish
  Make connect, disconnect, sync, and error states more predictable and easier to understand in the UI.
- Buffer and message usability
  Improve unread and highlight behavior, message presentation, and buffer-state clarity so the app is easier to scan during active chat use.
- Release-facing naming cleanup
  Keep the desktop project, published app binary, and release artifacts aligned on the `QuasselGlow` name instead of the older `Quassel.Client.Desktop` identity.
- Release engineering
  Make versioning and packaging more repeatable so the release can be rebuilt confidently for `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64`.
- Regression coverage
  Add automated tests in the areas most likely to break visible behavior, especially settings persistence, protocol handling, and desktop/session state logic.

## Good Candidates

- Tighten connection-state text and failure handling during login and sync
- Review backlog loading behavior when switching buffers and reconnecting
- Improve buffer ordering, unread counters, and highlight visibility
- Add desktop-layer tests around `MainWindowViewModel` state transitions
- Centralize app version metadata instead of relying on ad hoc release bookkeeping
- Document or script the packaging flow used to create release archives

## Nice to Have

- Theme switching and dark mode
  Add a persisted appearance setting, ship multiple color themes, include a dark mode variant, and verify the main shell remains readable in both light and dark presentation.
- Private-message and mention alerts
  Detect direct messages and nickname mentions, surface them clearly in the buffer list and selected-buffer state, and make them available to any desktop or tray notification flow.
- Tray icon and minimize-to-tray
  Add a tray icon, let the app minimize to tray intentionally, and provide a clear restore and quit path from the tray menu or icon interaction.
- Emoji support
  Ensure emoji render correctly in message history and the composer, and confirm that the chosen fonts or fallbacks cover common emoji usage well enough for everyday chat.

## Explicitly Deferred

- Full Quassel sync surface coverage
- Rich user list and presence management
- Native installers, code signing, and notarization
- Auto-update support
- Large protocol-expansion work that would broaden the release too much

## Exit Criteria

- The release branch has a clear, fixed scope with no open "maybe" items
- `dotnet build Quassel.slnx -c Release` succeeds
- Application and protocol tests pass
- The desktop client is smoke-tested against a real Quassel core
- Release notes are prepared before tagging
- Release artifacts are produced for the current four target runtimes

## Working Rule

If a proposed change does not clearly improve stability, chat usability, or release repeatability for `v0.2.0`, it should probably wait for the release after this one.
