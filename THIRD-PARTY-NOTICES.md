# Third-party notices

Vaporwave Toons is MIT-licensed (see [LICENSE](LICENSE)). It ships one piece of third-party
content and is modelled on one third-party program.

## The "Vaporwave" theme for XPenguins

Everything in [`theme/`](theme): the `config` file, the 137 `.xpm` sprite sheets, `about` and
`icon.png`. The theme was made for Arthur, and its author released it under
**CC0 1.0 Universal**, a public domain dedication: "No rights reserved. Do what you like with it."
See <https://creativecommons.org/publicdomain/zero/1.0/>.

Changes made in this repository: the `about` file no longer carries the maintainer's contact
details, and its description now lists the eight toons the theme actually contains. The sprites
and the `config` file are unchanged.

Derived from the theme's art: the program icon (`assets/vaporwave.ico`, built by
`tools/make_icon.py`), the README banner (`docs/hero.png`, built by `tools/make_hero.py`) and
the toon line-up (`docs/preview.gif`).

## XPenguins

[XPenguins](https://xpenguins.seul.org/) is by Robin Hogan and is distributed under the GNU
General Public License, version 2 or later. Vaporwave Toons reimplements XPenguins' behaviour
for Windows, in C#, and contains no XPenguins source code. It reads the same theme format, so
other XPenguins themes can be loaded with `--theme`.
