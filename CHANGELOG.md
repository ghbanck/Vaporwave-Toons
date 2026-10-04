# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-10-03

First open-source release.

### Added

- English user interface. Portuguese is still there: the program follows the Windows display
  language, and the new **Language** menu (or `--lang en|pt`) picks one.
- The About box shows the license and the project page.
- MIT license, third-party notices, README in English and Portuguese, a demo video and a banner.
- Continuous integration: every push builds the program and runs the self-test; tags starting
  with `v` produce a draft release with the `.exe` and a zip.
- `tools/package.ps1` builds the release files locally, and `tools/make_hero.py` rebuilds the
  README banner from the theme's sprites.

### Changed

- Diagnostics (`--selftest`, `--list-windows`, `--log`) are in English.
- `--help` gives the real range of `--scale`, 1 to 6.
- The theme's `about` file no longer carries the maintainer's contact details, and its
  description lists the eight toons the theme actually has.

### Removed

- An undocumented testing switch, `--menu-test`.

## 1.0.0 - 2026-09-28

- First version, not published: the XPenguins Vaporwave theme on Windows 10 and 11, with all 18
  activities for each of the eight toons, a notification-area menu, per-monitor DPI, virtual
  desktop support and a single `.exe` with nothing to install.

[1.1.0]: https://github.com/ghbanck/Vaporwave-Toons/releases/tag/v1.1.0
