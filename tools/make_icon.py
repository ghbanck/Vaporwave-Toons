"""Build assets/vaporwave.ico from the theme's own CRT art.

Every size is an integer nearest-neighbour scale of a hand-placed pixel grid,
so the icon stays as crisp as the sprites. Run from anywhere:
    python tools/make_icon.py
"""
import os
import struct
from io import BytesIO

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

PAL = {
    'a': (0x24, 0x1b, 0x2f), 'b': (0xc7, 0xb3, 0xe8), 'c': (0x45, 0x35, 0x6b),
    'd': (0x6f, 0x5a, 0x94), 'e': (0x5a, 0x2d, 0x76), 'f': (0xb9, 0x67, 0xff),
    'g': (0xe8, 0xd9, 0xff), 'h': (0xff, 0xfb, 0x96), 'i': (0xff, 0xc2, 0x5e),
    'j': (0xff, 0x71, 0xce), 'k': (0xff, 0x9e, 0xde), 'l': (0x2b, 0x1b, 0x3d),
    'm': (0x01, 0xcd, 0xfe), 'n': (0x7b, 0xf1, 0xe8), 'o': (0x9d, 0x86, 0xc4),
    'p': (0x05, 0xff, 0xa1),
}

# 16x16: the CRT from icon.png, squeezed to a 10-pixel screen.
MINI = [
    '..aaaaaaaaaaaa..',
    '.abbbbbbbbbbbba.',
    'abccccccccccccda',
    'abceeeeeeeeeecda',
    'abcffhhhhhfffcda',
    'abcfhhhhhhhffcda',
    'abcfiiiiiiiifcda',
    'abcjjjjjjjjjjcda',
    'abclmllmllmllcda',
    'abcmlmmlmmlmmcda',
    'abcnnnnnnnnnncda',
    'abccccccccccccda',
    'aobbbbbbbbbbbbda',
    '.aoodoooooopoda.',
    '..aaaaaaaaaaaa..',
    '......oddo......',
]


def grid(rows):
    im = Image.new('RGBA', (len(rows[0]), len(rows)), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(rows):
        assert len(row) == im.width, (y, row)
        for x, ch in enumerate(row):
            if ch != '.':
                px[x, y] = PAL[ch] + (255,)
    return im


def place(src, size, k):
    """src scaled by integer k, centred on a size x size transparent canvas."""
    big = src.resize((src.width * k, src.height * k), Image.NEAREST)
    assert big.width <= size and big.height <= size, (size, k, big.size)
    out = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    out.alpha_composite(big, ((size - big.width) // 2, (size - big.height) // 2))
    return out


def dib_entry(im):
    """32bpp BMP-style ICO entry (bottom-up XOR bitmap + 1bpp AND mask)."""
    w, h = im.size
    px = im.load()
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))
    row_bytes = ((w + 31) // 32) * 4
    andm = bytearray()
    for y in range(h - 1, -1, -1):
        bits = bytearray(row_bytes)
        for x in range(w):
            if px[x, y][3] == 0:
                bits[x // 8] |= 0x80 >> (x % 8)
        andm += bits
    hdr = struct.pack('<IiiHHIIiiII', 40, w, h * 2, 1, 32, 0, len(xor) + len(andm), 0, 0, 0, 0)
    return hdr + bytes(xor) + bytes(andm)


def png_entry(im):
    buf = BytesIO()
    im.save(buf, 'PNG')
    return buf.getvalue()


def main():
    icon = Image.open(os.path.join(ROOT, 'theme', 'icon.png')).convert('RGBA')   # 20x27
    mini = grid(MINI)                                                           # 16x16
    head = icon.crop((0, 0, 20, 18))                                            # CRT without legs

    images = [
        place(mini, 16, 1),
        place(head, 20, 1),
        place(head, 24, 1),
        place(mini, 32, 2),
        place(head, 40, 2),
        place(mini, 48, 3),
        place(icon, 64, 2),
        place(icon, 96, 3),
        place(icon, 128, 4),
        place(icon, 256, 9),
    ]
    entries = [(im, png_entry(im) if im.width >= 256 else dib_entry(im)) for im in images]

    out = bytearray(struct.pack('<HHH', 0, 1, len(entries)))
    offset = 6 + 16 * len(entries)
    for im, data in entries:
        w, h = im.size
        out += struct.pack('<BBBBHHII', w % 256, h % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for _, data in entries:
        out += data

    dst = os.path.join(ROOT, 'assets', 'vaporwave.ico')
    with open(dst, 'wb') as f:
        f.write(out)
    # contact sheet for eyeballing
    sheet = Image.new('RGBA', (sum(i.width for i in images) + 10 * len(images), 256), (40, 32, 56, 255))
    x = 0
    for im in images:
        sheet.alpha_composite(im, (x, 0))
        x += im.width + 10
    sheet.save(os.path.join(ROOT, 'assets', 'icon_preview.png'))
    print('wrote', dst, len(out), 'bytes,', len(images), 'sizes')


if __name__ == '__main__':
    main()
