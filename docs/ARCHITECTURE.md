# Quassel Client Architecture

## Solution layout

- `src/QuasselGlow`
  - Avalonia desktop shell, app startup, windows, views, and platform-facing composition.
- `src/Quassel.Client.Application`
  - View models, use cases, commands, and app state orchestration.
- `src/Quassel.Client.Domain`
  - Core models and rules such as identities, buffers, messages, sessions, and connection state.
- `src/Quassel.Client.Protocol`
  - Quassel protocol transport, framing, serialization, handshake, sync, and reconnect behavior.
- `src/Quassel.Client.Infrastructure`
  - Persistence, configuration, notifications, logging, and adapters used by the application layer.
- `tests/Quassel.Client.Application.Tests`
  - Unit tests for state transitions and view-model behavior.
- `tests/Quassel.Client.Protocol.Tests`
  - Unit tests for parsing, encoding, reconnect logic, and protocol edge cases.

## Dependency direction

- `Desktop` -> `Application` + `Infrastructure`
- `Infrastructure` -> `Application` + `Protocol`
- `Application` -> `Domain`
- `Protocol` -> `Domain`
- `Tests` -> project under test

## Early implementation priorities

1. Define domain models for users, networks, buffers, and messages.
2. Build a minimal protocol client that can connect, authenticate, and receive buffer state.
3. Add an application session service that exposes connection state and active buffer data to the UI.
4. Replace the template main window with a three-pane chat layout:
   networks, buffer list, and message view.
5. Add local persistence for connection profiles and last-used session settings.
