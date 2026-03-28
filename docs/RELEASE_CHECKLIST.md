# Release Checklist

This checklist is meant to keep the next QuasselGlow release predictable and easy to verify.

## Planning

- [x] Confirm the target version number, for example `v0.2.0`
- [x] Confirm the release scope against `docs/NEXT_RELEASE_SCOPE.md`
- [x] Agree which fixes and features are allowed into the release
- [x] Keep release work on a dedicated branch until the scope is stable
- [x] Decide whether any user-facing notes in `README.md` should change for this release

## Nice to Have Review

- [x] Implement theme switching and dark mode for this release
- [x] Implement private-message and mention alerts for this release
- [x] Implement tray icon and minimize-to-tray for this release
- [x] Implement emoji-friendly font fallback for this release

## Must Ship Review

- [x] Tighten connection-state text and failure handling during login, sync, disconnect, and error recovery
- [x] Keep backlog loading predictable when the selected buffer changes and after session sync restores buffers
- [x] Improve buffer ordering, unread counters, and highlight visibility for active chat use
- [x] Keep the desktop project path, published app binary, and release artifacts aligned on the `QuasselGlow` name
- [x] Centralize app version metadata so packaging can default to the release version from source
- [x] Keep the packaging flow scripted and repeatable for the four target runtimes
- [x] Add regression coverage for desktop/session state transitions and buffer-ordering behavior

## Before Tagging

- [x] Verify the working tree is clean
- [x] Review commits included since the previous tag
- [x] Write release notes for the new version using `.artifacts/releases/vX.Y.Z/RELEASE_NOTES.md` as the target location
- [x] Confirm the release notes list the main highlights, known gaps, and validation steps
- [x] If this is the first release after the desktop output rename, mention that the shipped app binary is now `QuasselGlow`
- [x] Mention any shipped nice-to-have items from `docs/NEXT_RELEASE_SCOPE.md` in the release notes

## Validation

- [x] Run `dotnet build Quassel.slnx -c Release`
- [x] Run `dotnet test tests/Quassel.Client.Application.Tests/Quassel.Client.Application.Tests.csproj --no-build`
- [x] Run `dotnet test tests/Quassel.Client.Protocol.Tests/Quassel.Client.Protocol.Tests.csproj --no-build`
- [x] Smoke-test the desktop client with `dotnet run --project .\src\QuasselGlow\QuasselGlow.csproj`

## Packaging

- [x] Produce release artifacts under `.artifacts/releases/vX.Y.Z/`
- [x] Run `.\scripts\Publish-Release.ps1 -Version vX.Y.Z` unless you have a specific reason to package manually
- [x] Build and package at least `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64`
- [x] Confirm the published app binary is named `QuasselGlow` (`QuasselGlow.exe` on Windows) and the desktop project path is `src/QuasselGlow/QuasselGlow.csproj`
- [x] Generate `SHA256SUMS.txt` for the published archives
- [x] Check that archive names follow the `QuasselGlow-vX.Y.Z-<rid>.zip` pattern

## Publish

- [ ] Create and push the git tag `vX.Y.Z`
- [ ] Upload release archives and `SHA256SUMS.txt`
- [ ] Publish the release notes together with the tag
- [ ] Record any post-release follow-up items for the next cycle
