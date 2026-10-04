<p align="center">
  <img src="docs/hero.png" alt="Vaporwave Toons: little desktop toons that walk on your windows" width="100%">
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-3fb950"></a>
  <img alt="Platform: Windows 10 | 11" src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4">
  <img alt="Runtime: .NET Framework 4.8" src="https://img.shields.io/badge/runtime-.NET%20Framework%204.8-512bd4">
  <img alt="Renderer: layered windows" src="https://img.shields.io/badge/renderer-layered%20windows-8250df">
  <a href="https://github.com/ghbanck/Vaporwave-Toons/actions/workflows/build.yml"><img alt="Build" src="https://github.com/ghbanck/Vaporwave-Toons/actions/workflows/build.yml/badge.svg"></a>
  <a href="https://github.com/ghbanck/Vaporwave-Toons/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/ghbanck/Vaporwave-Toons?color=ff71ce&label=release"></a>
</p>

<p align="center"><b>English</b> · <a href="README.pt-BR.md">Português</a></p>

Vaporwave Toons brings the **Vaporwave** theme for [XPenguins](https://xpenguins.seul.org/) to
Windows 10 and 11. Eight toons drop onto your desktop, walk and run along your title bars,
climb window edges, tumble off ledges and parachute down under 3.5" floppy disks. Drag a window
onto them and they get squashed, and each one sends the part of itself that has a soul up to
heaven. The car sends its pine-tree air freshener.

It is a single small `.exe`: no installer, no runtime to download, nothing running as admin.

<p align="center">
  <img src="docs/demo.gif" alt="Toons walking on windows, riding a dragged window and getting squashed by another" width="100%">
  <br>
  <a href="docs/demo.mp4"><b>▶ Watch the full demo</b></a> (57 seconds, MP4)
</p>

## The toons

<p align="center"><img src="docs/preview.gif" alt="The eight toons walking" width="840"></p>

A CRT terminal, a cassette tape, a Greek statue, a palm tree, a dolphin, an 80s car, a flamingo
and a boombox. Every one of them has all 18 activities XPenguins knows: walking, running,
falling, tumbling, climbing, floating, six idle actions, four ways to die, an angel and a
power-off animation for when you quit.

## Download and run

1. Download `VaporwaveToons.exe` from the [latest release](https://github.com/ghbanck/Vaporwave-Toons/releases/latest).
2. Double-click it.

That's it. It runs on the .NET Framework 4.8, which is part of Windows 11 and of Windows 10
since version 1903. Windows SmartScreen may warn about an unknown publisher the first time,
because the executable isn't code-signed; choose **More info › Run anyway**, or build it
yourself from source.

A CRT monitor icon appears in the notification area, next to the clock (on Windows 11 it may
be hidden under the `^` arrow). Right-click it for the menu:

| Menu item | What it does |
|---|---|
| Pause / Hide | Freeze the toons or hide them. Double-clicking the icon also pauses. |
| Number of toons | 4 to 48. The theme's own default is 18. |
| Toons | Which of the eight appear. |
| Size | 1× to 4×. Automatic picks 2× on a 4K screen. |
| Speed | Slow, normal, fast or turbo. |
| Language | English or Português. Automatic follows the Windows display language. |
| Click a toon to zap it | The toon you click gets zapped, and the cursor turns into a crosshair over toons. |
| Gentle deaths | Every death becomes the theme's colourful explosion. |
| Angels rise to heaven | Turn it off and the toons simply vanish. |
| Walk in front of maximized and snapped windows | See below. |
| Hide in full screen | Hides the toons during games, videos and presentations. |
| Start with Windows | Starts the program when you sign in. |
| Exit | Every toon powers off like a CRT, then the program closes. |

Clicks go straight through the toons unless zap mode is on. Preferences are kept in
`%APPDATA%\VaporwaveToons\settings.ini`. To uninstall, untick *Start with Windows*, choose
*Exit*, and delete the `.exe` and that folder.

## What's different from XPenguins on Linux

- **Maximized and snapped windows are background.** In XPenguins every window is a solid
  block. On Windows it's common to keep windows maximized or snapped side by side, and with
  those solid there would be no room left, so the toons walk in front of them and along the
  taskbar instead. Floating windows are still floors and walls. You can turn this off.
- **Only a moving window squashes.** Dragging or resizing a window onto a toon squashes it. A
  window that merely appears on top of one (opened, restored, un-snapped) lets it walk out.
- **Rides.** Toons standing on a window, or clinging to its side, move with it.
- **Gravity.** Falling toons speed up to the theme's terminal velocity. With the original fixed
  speed, a fall down a 4K screen took about twenty seconds.
- **The parachute goes down**, as the floppy disk drawing suggests.
- **Mirroring.** One-directional animations (idle actions, falls, deaths) are mirrored when a
  toon faces left, so it doesn't flip around when it stops.
- **Multiple monitors, DPI and virtual desktops.** Everything is in physical pixels with
  per-monitor DPI awareness, the gap between monitors of different heights is a wall, and the
  toons follow you to another virtual desktop (Win+Ctrl+Arrow).

## Command line

```text
VaporwaveToons.exe --toons 30 --scale 3
VaporwaveToons.exe --theme C:\path\to\AnotherXPenguinsTheme
VaporwaveToons.exe --lang pt
VaporwaveToons.exe --selftest --log selftest.txt        check the theme, then exit
VaporwaveToons.exe --list-windows --log windows.txt     which windows count as solid, and why
VaporwaveToons.exe --help
```

Command-line options apply to that run only. `--theme` loads any XPenguins theme: a folder with
a `config` file and its `.xpm` sprites.

## Building from source

You need the [.NET SDK](https://dotnet.microsoft.com/download) 8 or later. If the .NET Framework
4.8 targeting pack isn't installed, its reference assemblies are fetched from NuGet
automatically.

```text
dotnet build -c Release
```

The result is `bin\Release\net48\VaporwaveToons.exe`, a single file with the theme embedded.
`--selftest` on the Vaporwave theme should report 8 toons, 137 pixmaps, 144 activity definitions
and "all good".

Other tools, all optional:

| Command | What it does |
|---|---|
| `pwsh tools/package.ps1` | Builds, runs the self-test and writes the release files to `dist\`. |
| `python tools/make_icon.py` | Rebuilds `assets/vaporwave.ico` from the theme's CRT art (needs Pillow). |
| `python tools/make_hero.py` | Rebuilds the README banner, `docs/hero.png` (needs Pillow and the Windows fonts). |

## How it works

| File | Role |
|---|---|
| `src/Program.cs` | Entry point, single instance, `--selftest`, `--list-windows`. |
| `src/Theme.cs` | Reads the theme `config` (the same grammar as XPenguins) and builds the sprite sheets. |
| `src/Xpm.cs` | XPM3 reader. |
| `src/World.cs` | Monitors, windows and collisions: what is floor, wall and sky. |
| `src/Engine.cs` | The toons' state machine: walking, falling, climbing, dying… |
| `src/ToonWindow.cs` | One transparent layered window per toon (`UpdateLayeredWindow`). |
| `src/TrayApp.cs` | Notification-area icon, menu and frame timer. |
| `src/Strings.cs` | Everything the user reads, in English and Portuguese. |
| `src/Settings.cs` | Preferences in `%APPDATA%\VaporwaveToons\settings.ini`. |
| `src/VirtualDesktops.cs` | Follows the switch to another virtual desktop. |
| `theme/` | The Vaporwave theme, embedded in the `.exe` as a zip at build time. |

**The world.** Free space is the work area of every monitor (the screen minus the taskbar) plus
a strip of "sky" above it, where toons are born and fall in from. Everything outside that is
solid, like the X root window is to XPenguins. Each window that is really visible (not
minimized, not cloaked by the window manager, not a popup, tooltip or overlay) is a solid
rectangle, measured by its visible frame bounds, without the invisible borders of Windows 10
and 11.

## Contributing

Issues and pull requests are welcome. A few things to keep in mind:

- The program stays a single, dependency-free `.exe` on the .NET Framework 4.8.
- Text the user reads lives in `src/Strings.cs`, in both languages. Code comments and
  diagnostics are in English.
- Run `--selftest` after touching the theme loader, and `--list-windows` after touching
  `src/World.cs`, on a desktop with a few floating, maximized and snapped windows.

## Credits and license

- Code: [MIT](LICENSE) © 2026 Gustavo Banck.
- The Vaporwave theme in `theme/` was made for Arthur and released under
  [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) (public domain).
- [XPenguins](https://xpenguins.seul.org/) is by Robin Hogan. This project reimplements its
  behaviour and contains none of its code.

Details in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
