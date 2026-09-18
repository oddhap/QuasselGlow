# Desktop UI redesign — Classic layout

This document describes the redesigned QuasselGlow desktop interface. It explains
the problems with the previous layout, the principles behind the new one, and what
changed in code.

## Summary

The desktop client now uses a flat, three-column IRC workbench instead of the
card-and-gradient dashboard of earlier builds:

- **Title bar** — thin, app-rendered chrome with brand, the active buffer name and
  the window controls.
- **Status strip (top)** — the connection state, detail text, session summary and
  the connection actions now live directly under the title bar.
- **Work area** — network/buffer tree on the left, a flat message log in the centre
  and the nick list on the right.
- **Composer** — a single-line input pinned to the bottom.
- **Overlays** — settings and connection editing open as centred overlays instead
  of permanent top panels.

All existing behaviour is preserved: every command, context menu, alert badge,
day separator, link and theme still works.

## Why the previous UI felt generic

- **Cards inside cards.** Every surface was a rounded panel (18–22 px radius),
  so the visual hierarchy flattened out — everything shouted equally.
- **Four accent colours at once** (rust, teal, sky and sand) used as chips and
  pills everywhere. When everything is highlighted, nothing is.
- **Gradient backdrop and floating drop-shadow window** — a marketing/dashboard
  look rather than a chat tool.
- **Permanent brand block.** A 28 px “Quassel / Glow” wordmark occupied prime
  vertical space that belongs to the conversation.
- **Duplicated status.** The same connection facts appeared in the status text,
  a detail line and three separate chips.
- **Messages as individual cards** with 10 px gaps, which breaks the scannability
  of a log.
- **A decorative composer label box** that did no work.

## Design principles

1. **One interactive accent.** Selection and interactive states use a single
   accent colour. A second colour is reserved for mentions and PM alerts.
2. **Hierarchy from typography, not decoration.** Size, weight and spacing
   separate regions — no nested cards.
3. **Flat surfaces.** No backdrop gradients, no drop shadows, no card-in-card.
   Corner radius is 6–7 px.
4. **Dense, scannable logs.** Messages are flat rows with a thin hover tint and a
   three-pixel left edge for mentions.
5. **Always-visible status.** Connection state stays visible without interrupting
   chat, which keeps the domain rule of a *discreet visible failure* intact.

## Layout

```
┌───────────────────────────────────────────────────────────────┐
│ title bar: brand · active buffer · settings · window controls │
├───────────────────────────────────────────────────────────────┤
│ status strip: ● state · detail · session · Edit · Connect     │
├───────────────┬───────────────────────────────┬───────────────┤
│ network /     │ message log                   │ nick list     │
│ buffer tree   │ (flat rows, day separators)   │ (collapsible) │
├───────────────┴───────────────────────────────┴───────────────┤
│ composer: [ input ........................................ ] Send│
└───────────────────────────────────────────────────────────────┘
```

The status strip sits **above** the work area and the composer stays pinned to the
**bottom** of the window.

## What changed

| Area | Before | After |
| --- | --- | --- |
| Top region | Three rounded top panels (brand/theme, status, connection) | Thin title bar + single status strip |
| Settings | Popup anchored to a panel | Centred overlay dialog |
| Connection editing | Popup anchored to a panel, plus a low-resolution overview variant | Centred overlay dialog, same at every size |
| Buffer list | Nested cards per network | Flat tree; network label + dense rows |
| Message log | Bordered cards with 10 px gaps | Flat rows; hover tint; 3 px mention edge |
| Nick list | Rounded rows inside a drawer shell | Flat rows in a sidebar with a seam border |
| Accents | Four accents as chips/pills | One interactive accent, one alert accent |
| Radius | 10–22 px | 6–7 px |
| Status line | Bottom bar | Top strip directly under the title bar |

### Code changes

- `src/QuasselGlow/Views/MainWindow.axaml`
  - Rewritten to the flat three-column layout.
  - Settings and connection editors are now centred overlays instead of
    `Popup`s, so they work identically at every window size.
  - Removed the separate compact/low-resolution top-panel and overview variants.
- `src/QuasselGlow/Views/MainWindowBase.cs`
  - Shared window behaviour extracted from the code-behind: tray icon, chat
    auto-scroll, composer keys, window chrome and responsive layout.
- `src/QuasselGlow/Views/ClassicMainWindow.axaml` / `.axaml.cs`
  - The previous card layout, restored as a second window for the toggle.
- `src/QuasselGlow/App.axaml.cs`
  - Owns the shared view model and swaps the active window when the layout
    preference changes.
- `src/QuasselGlow/App.axaml`
  - Reduced default `TextBox`/`Button` corner radius from 10 to 7.
- `src/QuasselGlow/Appearance/AppThemeCatalog.cs`
  - The default `glow` theme now uses a neutral surface/accent palette.
    The other twenty themes are unchanged.

## Theming

The redesign reuses the existing palette tokens (`ShellBg`, `ShellPanel`,
`ShellPanelMuted`, `ShellBorder`, `InkStrong`, `InkSoft`, `AccentTeal`,
`AccentRust`, `AccentSky`, `AccentSand`, message-row colours), so every theme
continues to work. Only the default `glow` palette was neutralised:

- Light: `#E9ECEF` shell, `#FFFFFF` log, `#2F6FD0` interactive accent.
- Dark: `#0B0F14` shell, `#0F141A` log, `#4D94FF` interactive accent.

## Responsive behaviour

- The nick list switches between an inline column and an overlay based on width,
  as before.
- Because the editors are overlays, they no longer depend on available height.
  The old low-resolution “Overview” screen is gone.

## Classic layout toggle

The previous card-based interface is kept as a second window and can be selected
at runtime:

- The preference is stored as `UseClassicLayout` in the local connection settings
  and defaults to `true`, so existing installations keep the classic layout.
- The **Classic interface** checkbox in Settings switches layouts live. The app
  creates the other window with the same `MainWindowViewModel`, transfers the
  window bounds, and closes the old window without disposing the view model, so
  the Quassel core session and all chat state survive the switch.
- Both windows share behaviour through `MainWindowBase` (tray, chat auto-scroll,
  composer keys, window chrome); each layout only supplies its own named controls.
- `MainWindow` hosts the flat layout and `ClassicMainWindow` hosts the card layout.

## Verification

- `dotnet build Quassel.slnx` — succeeds with no warnings.
- `dotnet test Quassel.slnx` — 109 tests pass (103 application, 6 protocol).
- The app launches without runtime exceptions.

## Not yet done

- README screenshots still show the previous layout and should be regenerated
  from a running build.

## Related

- `docs/gui-proposals.html` — the three design directions that were considered
  (Classic, Sidebar-first, Reading-first). The Classic direction was implemented.
