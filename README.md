# QuasselGlow

QuasselGlow is a cross-platform desktop client for the Quassel protocol, built with Avalonia and .NET.

The project aims to offer a modern desktop experience inspired by classic IRC clients while staying lightweight, responsive, and usable on Windows, Linux, and macOS.

## Screenshots

### Light Theme

![QuasselGlow light overview](docs/screenshots/light-overview.png)

### Dark Theme

![QuasselGlow dark overview](docs/screenshots/dark-overview.png)

### Settings

![QuasselGlow settings](docs/screenshots/settings.png)

### Connection View

![QuasselGlow connection view](docs/screenshots/connection.png)

## Status

QuasselGlow is an early-stage project. The current build includes:

- Quassel core connection and authentication
- Network, buffer, and backlog loading
- Message sending
- Clickable links in chat messages
- Quassel-style per-buffer input history and draft recall
- A custom desktop UI built with Avalonia
- Local connection settings storage
- Theme selection, tray support, and localized UI labels
- Language selection covering the full locale list shipped by the official Quassel client

## Tech Stack

- .NET 10
- Avalonia UI
- CommunityToolkit.Mvvm
- xUnit

## Solution Layout

- `src/QuasselGlow`
  - Avalonia desktop application, views, styling, and window behavior
- `src/Quassel.Client.Application`
  - Application logic and shared app-level helpers
- `src/Quassel.Client.Domain`
  - Core models for sessions, networks, buffers, and messages
- `src/Quassel.Client.Protocol`
  - Quassel transport, framing, handshake, sync, and protocol handling
- `src/Quassel.Client.Infrastructure`
  - Persistence and runtime services
- `tests/Quassel.Client.Application.Tests`
  - Application-layer tests
- `tests/Quassel.Client.Protocol.Tests`
  - Protocol-layer tests

## Getting Started

### Requirements

- .NET SDK 10.0 or newer

### Build

```powershell
dotnet build Quassel.slnx
```

### Run the desktop client

```powershell
dotnet run --project .\src\QuasselGlow\QuasselGlow.csproj
```

### Run tests

```powershell
dotnet test Quassel.slnx
```

### Create release artifacts

```powershell
.\scripts\Publish-Release.ps1
```

This publishes self-contained release builds for `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64` into `.artifacts/releases/<version>/`, writes `SHA256SUMS.txt`, creates platform archives, and seeds a `RELEASE_NOTES.md` file if one does not already exist.

The release version is centralized in `Directory.Build.props`. If you omit `-Version`, the publish script will use that shared `VersionPrefix`. On macOS the script also emits a signed `QuasselGlow.app` bundle, a zip that contains the app bundle, and a `.dmg`.

## Configuration

The app stores local connection settings in the user's local application data folder. Credentials are protected per user on Windows through DPAPI when possible.

The selected UI language is also stored locally. QuasselGlow exposes the same locale list as the official Quassel translation set and ships with translated UI labels for that full locale set.

Recent desktop polish includes persisted themes with dark mode, PM and mention alerts, tray support, emoji-friendly font fallback, composer autofocus after connecting, and automatic channel switching after `/join` and `/j`.

## Notes

- This project is not an official Quassel release.
- The repository does not include any server credentials or private deployment settings.

## Roadmap Ideas

- More complete Quassel sync coverage
- Richer buffer and user list handling
- Better packaging for Windows, Linux, and macOS
- Installer polish
