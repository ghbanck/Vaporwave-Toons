"""Build docs/hero.png, the banner at the top of the README, from the theme's own sprites.

The sprites are scaled by whole numbers with nearest-neighbour sampling, so they stay as
crisp as they are on the desktop. Needs Pillow and two fonts that ship with Windows 11
(Segoe UI and Cascadia Mono). Run from anywhere:
    python tools/make_hero.py
"""
import os
import random
import re

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
THEME = os.path.join(ROOT, 'theme')
FONTS = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts')

W, H = 1280, 640
HORIZON = 448
K = 3                                   # sprite scale

INK = (36, 27, 47)
VOID = (21, 14, 31)
LILAC = (185, 103, 255)
PINK = (255, 113, 206)
CYAN = (1, 205, 254)
MINT = (5, 255, 161)
BUTTER = (255, 251, 150)
ORANGE = (255, 194, 94)
SHELL_HI = (232, 217, 255)
SHELL_LT = (199, 179, 232)
SHELL_DK = (111, 90, 148)
SHELL_SH = (69, 53, 107)

# 5x7 pixel letters for the title, in the spirit of the sprites.
GLYPHS = {
    'V': ['X...X', 'X...X', 'X...X', '.X.X.', '.X.X.', '.X.X.', '..X..'],
    'A': ['.XXX.', 'X...X', 'X...X', 'XXXXX', 'X...X', 'X...X', 'X...X'],
    'P': ['XXXX.', 'X...X', 'X...X', 'XXXX.', 'X....', 'X....', 'X....'],
    'O': ['.XXX.', 'X...X', 'X...X', 'X...X', 'X...X', 'X...X', '.XXX.'],
    'R': ['XXXX.', 'X...X', 'X...X', 'XXXX.', 'X.X..', 'X..X.', 'X...X'],
    'W': ['X...X', 'X...X', 'X...X', 'X.X.X', 'X.X.X', 'X.X.X', '.X.X.'],
    'E': ['XXXXX', 'X....', 'X....', 'XXXX.', 'X....', 'X....', 'XXXXX'],
    'T': ['XXXXX', '..X..', '..X..', '..X..', '..X..', '..X..', '..X..'],
    'N': ['X...X', 'XX..X', 'X.X.X', 'X..XX', 'X...X', 'X...X', 'X...X'],
    'S': ['.XXXX', 'X....', 'X....', '.XXX.', '....X', '....X', 'XXXX.'],
    ' ': ['...', '...', '...', '...', '...', '...', '...'],
}


# ----------------------------------------------------------------- helpers

def load_xpm(name):
    """A theme sprite sheet as an RGBA image (1 char per pixel, #RRGGBB or None)."""
    text = open(os.path.join(THEME, name + '.xpm'), encoding='latin-1').read()
    s = re.findall(r'"((?:[^"\\]|\\.)*)"', re.sub(r'/\*.*?\*/', '', text, flags=re.S))
    w, h, n, cpp = map(int, s[0].split()[:4])
    pal = {}
    for line in s[1:1 + n]:
        spec = line[cpp:].split()
        col = spec[spec.index('c') + 1]
        pal[line[:cpp]] = (0, 0, 0, 0) if col.lower() == 'none' else \
            tuple(int(col[i:i + 2], 16) for i in (1, 3, 5)) + (255,)
    im = Image.new('RGBA', (w, h))
    px = im.load()
    for y, row in enumerate(s[1 + n:1 + n + h]):
        for x in range(w):
            px[x, y] = pal[row[x * cpp:(x + 1) * cpp]]
    return im


def frame(sheet, index, row=0, fw=30, fh=30, k=K):
    f = load_xpm(sheet).crop((index * fw, row * fh, (index + 1) * fw, (row + 1) * fh))
    return f.resize((fw * k, fh * k), Image.NEAREST)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def gradient(stops, t):
    """Colour at t in [0, 1] along [(t0, colour), (t1, colour), ...]."""
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            return lerp(c0, c1, 0 if t1 == t0 else (t - t0) / (t1 - t0))
    return stops[-1][1]


def vertical(size, stops):
    w, h = size
    strip = Image.new('RGBA', (1, h))
    for y in range(h):
        strip.putpixel((0, y), gradient(stops, y / max(1, h - 1)) + (255,))
    return strip.resize((w, h), Image.NEAREST)


def font(name, size):
    try:
        return ImageFont.truetype(os.path.join(FONTS, name), size)
    except OSError:
        print(f'warning: {name} not found, using the default font')
        return ImageFont.load_default(size)


def glow(layer, radius, strength=1.0):
    g = layer.filter(ImageFilter.GaussianBlur(radius))
    if strength != 1.0:
        g.putalpha(g.getchannel('A').point(lambda a: min(255, int(a * strength))))
    return g


# ----------------------------------------------------------------- layers

def sky(img):
    img.alpha_composite(vertical((W, HORIZON), [
        (0.00, (14, 9, 26)), (0.38, (42, 19, 72)), (0.74, (118, 36, 126)), (1.00, (255, 104, 178)),
    ]))
    rng = random.Random(7)
    stars = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(stars)
    for _ in range(90):
        x, y = rng.randrange(W), rng.randrange(int(HORIZON * 0.72))
        if abs(x - W // 2) < 210 and y > HORIZON - 200:
            continue
        a = int(255 * (1 - y / HORIZON) ** 0.6 * rng.uniform(0.45, 1))
        s = 2 if rng.random() < 0.8 else 4
        d.rectangle((x, y, x + s - 1, y + s - 1), fill=SHELL_HI + (a,))
    for x, y in [(118, 96), (1186, 84), (262, 268), (1032, 300), (980, 60), (330, 40)]:
        for dx, dy in [(0, 0), (-4, 0), (4, 0), (0, -4), (0, 4)]:
            d.rectangle((x + dx - 2, y + dy - 2, x + dx + 1, y + dy + 1), fill=(255, 255, 255, 230))
    img.alpha_composite(glow(stars, 3, 0.8))
    img.alpha_composite(stars)


def sun(img):
    r, cx = 172, W // 2
    top = HORIZON - r
    disc = Image.new('L', (W, H))
    ImageDraw.Draw(disc).ellipse((cx - r, HORIZON - r, cx + r, HORIZON + r), fill=255)
    # Slats: the classic cut-away stripes, thicker towards the horizon.
    d = ImageDraw.Draw(disc)
    y = HORIZON - int(r * 0.52)
    for gap in (3, 4, 6, 8, 10, 13):
        d.rectangle((0, y, W, y + gap - 1), fill=0)
        y += gap + 12
    d.rectangle((0, HORIZON, W, H), fill=0)
    fill = Image.new('RGBA', (W, H))
    fill.paste(vertical((W, r), [(0, BUTTER), (0.45, ORANGE), (1, PINK)]), (0, top))
    halo = Image.new('RGBA', (W, H))
    ImageDraw.Draw(halo).ellipse((cx - r - 10, top - 10, cx + r + 10, HORIZON + r + 10), fill=PINK + (150,))
    halo = halo.filter(ImageFilter.GaussianBlur(38))
    halo.paste((0, 0, 0, 0), (0, HORIZON, W, H))
    img.alpha_composite(halo)
    fill.putalpha(disc)
    img.alpha_composite(fill)


def floor(img):
    img.alpha_composite(vertical((W, H - HORIZON), [(0, (44, 14, 66)), (1, (12, 7, 22))]), (0, HORIZON))
    lines = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(lines)
    vx, depth = W // 2, H - HORIZON
    for i in range(-16, 17):
        d.line((vx + i * 6, HORIZON, vx + i * 104, H + 40), fill=CYAN + (255,), width=2)
    for k in range(1, 11):
        y = HORIZON + int(depth * (k / 10) ** 2.1)
        d.line((0, y, W, y), fill=CYAN + (255,), width=2)
    # Fade the grid into the haze at the horizon.
    fade = vertical((W, H), [(0, (0, 0, 0)), (HORIZON / H, (0, 0, 0)), (HORIZON / H + 0.05, (90, 90, 90)),
                             (1, (255, 255, 255))])
    lines.putalpha(ImageChops.multiply(lines.getchannel('A'), fade.getchannel('R')))
    img.alpha_composite(glow(lines, 5, 1.4))
    img.alpha_composite(lines)
    edge = Image.new('RGBA', (W, H))
    ImageDraw.Draw(edge).rectangle((0, HORIZON - 1, W, HORIZON + 1), fill=(255, 190, 230, 255))
    img.alpha_composite(glow(edge, 6, 1.5))
    img.alpha_composite(edge)


def title(img, text, top, s=10):
    widths = [len(GLYPHS[c][0]) for c in text]
    tw = (sum(widths) + len(text) - 1) * s
    mask = Image.new('L', (W, H))
    d = ImageDraw.Draw(mask)
    x = (W - tw) // 2
    for c, w in zip(text, widths):
        for gy, row in enumerate(GLYPHS[c]):
            for gx, ch in enumerate(row):
                if ch == 'X':
                    d.rectangle((x + gx * s, top + gy * s, x + gx * s + s - 1, top + gy * s + s - 1), fill=255)
        x += (w + 1) * s
    th = 7 * s

    # Extruded body, then a dark outline around everything, then the chrome face.
    depth = Image.new('L', (W, H))
    for i in range(1, 8):
        depth = ImageChops.lighter(depth, ImageChops.offset(mask, i // 2, i))
    outline = ImageChops.lighter(mask, depth).filter(ImageFilter.MaxFilter(7))
    shade = Image.new('RGBA', (W, H), INK + (255,))
    shade.putalpha(outline)
    neon = Image.new('RGBA', (W, H), PINK + (255,))
    neon.putalpha(outline)
    img.alpha_composite(glow(neon, 14, 0.9))
    img.alpha_composite(shade)
    side = vertical((W, th + 8), [(0, (130, 70, 200)), (1, (70, 30, 120))])
    body = Image.new('RGBA', (W, H))
    body.paste(side, (0, top))
    body.putalpha(depth)
    img.alpha_composite(body)
    face = Image.new('RGBA', (W, H))
    face.paste(vertical((W, th), [
        (0.00, (255, 255, 255)), (0.44, (150, 232, 255)), (0.47, (40, 30, 110)),
        (0.53, (255, 92, 190)), (1.00, (255, 226, 246)),
    ]), (0, top))
    face.putalpha(mask)
    img.alpha_composite(face)


def text(img, xy, s, fnt, fill, anchor='la'):
    ImageDraw.Draw(img).text(xy, s, font=fnt, fill=fill, anchor=anchor)


def window(img, box, caption):
    x0, y0, x1, y1 = box
    bar = 36
    glass = Image.new('RGBA', (W, H))
    d = ImageDraw.Draw(glass)
    d.rectangle(box, fill=(22, 14, 36, 214))
    img.alpha_composite(glass)
    d = ImageDraw.Draw(img)
    # Bevelled frame, Windows 95 style, in the theme's palette.
    d.rectangle((x0, y0, x1, y1), outline=SHELL_LT)
    d.line((x1, y0, x1, y1), fill=SHELL_SH, width=3)
    d.line((x0, y0, x1, y0), fill=SHELL_HI, width=3)
    d.line((x0, y0, x0, y1), fill=SHELL_HI, width=3)
    tb = Image.new('RGBA', (x1 - x0 - 8, bar))
    for x in range(tb.width):
        c = gradient([(0, (255, 92, 190)), (0.55, LILAC), (1, (60, 150, 255))], x / (tb.width - 1))
        ImageDraw.Draw(tb).line((x, 0, x, bar), fill=c + (255,))
    img.alpha_composite(tb, (x0 + 4, y0 + 4))
    ix, iy = x0 + 14, y0 + 4 + (bar - 20) // 2
    d.rectangle((ix, iy, ix + 23, iy + 19), fill=INK, outline=SHELL_HI, width=2)
    text(img, (ix + 5, iy + 9), '>_', font('CascadiaMono.ttf', 12), MINT, 'lm')
    text(img, (x0 + 48, y0 + 4 + bar // 2), caption, font('seguisb.ttf', 18), (255, 255, 255), 'lm')
    bx = x1 - 4 - 8 - 3 * 30
    for i, glyph in enumerate(('min', 'max', 'close')):
        l, t = bx + i * 30, y0 + 4 + 6
        d.rectangle((l, t, l + 26, t + 23), fill=SHELL_HI)
        d.line((l, t + 23, l + 26, t + 23), fill=SHELL_SH, width=2)
        d.line((l + 26, t, l + 26, t + 23), fill=SHELL_SH, width=2)
        if glyph == 'min':
            d.rectangle((l + 7, t + 15, l + 18, t + 17), fill=INK)
        elif glyph == 'max':
            d.rectangle((l + 6, t + 5, l + 19, t + 17), outline=INK, width=2)
            d.rectangle((l + 6, t + 5, l + 19, t + 7), fill=INK)
        else:
            d.line((l + 7, t + 6, l + 19, t + 17), fill=INK, width=3)
            d.line((l + 19, t + 6, l + 7, t + 17), fill=INK, width=3)
    return y0 + 4 + bar


def main():
    img = Image.new('RGBA', (W, H), VOID + (255,))
    sky(img)
    sun(img)
    floor(img)
    title(img, 'VAPORWAVE TOONS', 40)
    text(img, (W // 2, 150), 'Little desktop toons that walk on your windows',
         font('seguisb.ttf', 30), SHELL_HI, 'mt')
    text(img, (W // 2, 196), 'WINDOWS 10 & 11   ·   ONE .EXE, NOTHING TO INSTALL   ·   FREE & OPEN SOURCE',
         font('seguisb.ttf', 15), (255, 158, 222), 'mt')

    top = 482
    body = window(img, (150, top, 1130, H + 8), 'Terminal')
    mono = font('CascadiaMono.ttf', 16)
    for i, (s, c) in enumerate([
        ('C:\\> VaporwaveToons.exe --selftest', SHELL_HI),
        ('theme "Vaporwave": delay 60 ms, 8 toons, 137 pixmaps', CYAN),
        ('144 activity definitions', CYAN),
        ('all good', MINT),
    ]):
        text(img, (176, body + 14 + i * 24), s, mono, c)

    # The lineup, standing on the title bar.
    lineup = [('walker', 2, 1), ('walker_bird', 5, 0), ('walker_tape', 1, 1), ('walker_statue', 3, 1),
              ('walker_car', 0, 1), ('walker_boom', 4, 0), ('walker_dolphin', 2, 1), ('walker_palm', 6, 0)]
    for i, (sheet, f, row) in enumerate(lineup):
        x = 196 + i * 124
        img.alpha_composite(frame(sheet, f, row), (x, top - 30 * K))

    img.alpha_composite(frame('floater', 3, 0, fh=44), (70, 214))       # floppy parachute
    img.alpha_composite(frame('angel_car', 1, 0, fw=36), (1104, 236))    # the car's soul

    dst = os.path.join(ROOT, 'docs', 'hero.png')
    img.convert('RGB').save(dst, optimize=True)
    print('wrote', dst, img.size)


if __name__ == '__main__':
    main()
