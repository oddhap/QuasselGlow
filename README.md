# QuasselGlow

QuasselGlow is a cross-platform desktop client for the Quassel protocol, built with Avalonia and .NET.

The project aims to offer a modern desktop experience inspired by classic IRC clients while staying lightweight, responsive, and usable on Windows, Linux, and macOS.

## Status

QuasselGlow is an early-stage project. The current build includes:

- Quassel core connection and authentication
- Network, buffer, and backlog loading
- Message sending
- A custom desktop UI built with Avalonia
- Local connection settings storage
- Language selection covering the full locale list shipped by the official Quassel client

## Tech Stack

- .NET 10
- Avalonia UI
- CommunityToolkit.Mvvm
- xUnit

## Solution Layout

- `src/Quassel.Client.Desktop`
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
dotnet run --project .\src\Quassel.Client.Desktop\Quassel.Client.Desktop.csproj
```

### Run tests

```powershell
dotnet test Quassel.slnx
```

## Configuration

The app stores local connection settings in the user's local application data folder. Credentials are protected per user on Windows through DPAPI when possible.

The selected UI language is also stored locally. QuasselGlow exposes the same locale list as the official Quassel translation set and ships with translated UI labels for that full locale set.

## Notes

- This project is not an official Quassel release.
- The repository does not include any server credentials or private deployment settings.

## Roadmap Ideas

- More complete Quassel sync coverage
- Richer buffer and user list handling
- Better packaging for Windows, Linux, and macOS
- Installer polish
