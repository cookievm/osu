#!/usr/bin/env python3
# oily: turns a .ttf/.otf into the bitmap font files osu!framework reads
# (<Name>.bin, a BMFont binary v3 file, plus <Name>_<page>.png glyph pages), drawn at size 100 like lazer's own fonts.
#
#   python3 make_bmfont.py <font file> <output folder> <Name>
#
# Needs Pillow.

import struct
import sys
from pathlib import Path

from PIL import Image, ImageFont

SIZE = 100
PAGE = 1024
PAD = 2

# Latin, Latin-1, Latin Extended-A, general punctuation, and a few symbols stable shows (★ etc.).
CODEPOINTS = list(range(0x20, 0x7F)) + list(range(0xA0, 0x180)) + list(range(0x2010, 0x2028)) + [0x20AC, 0x2122, 0x2190, 0x2192, 0x2605, 0x2606]


def main():
    font_path, out_dir, name = sys.argv[1], Path(sys.argv[2]), sys.argv[3]
    out_dir.mkdir(parents=True, exist_ok=True)

    font = ImageFont.truetype(font_path, SIZE)
    ascent, descent = font.getmetrics()
    line_height = ascent + descent

    chars = []
    pages = [Image.new('RGBA', (PAGE, PAGE), (255, 255, 255, 0))]
    x = y = PAD
    row_h = 0

    for cp in CODEPOINTS:
        ch = chr(cp)

        # Skip characters the font doesn't have (they'd draw as the .notdef box).
        try:
            mask = font.getmask(ch)
        except Exception:
            continue

        if cp != 0x20 and mask.size[0] == 0 and cp not in (0xA0,):
            continue

        bbox = font.getbbox(ch, anchor='ls')  # relative to the baseline
        advance = font.getlength(ch)

        w = max(0, bbox[2] - bbox[0])
        h = max(0, bbox[3] - bbox[1])

        if w > 0 and h > 0:
            if x + w + PAD > PAGE:
                x = PAD
                y += row_h + PAD
                row_h = 0
            if y + h + PAD > PAGE:
                pages.append(Image.new('RGBA', (PAGE, PAGE), (255, 255, 255, 0)))
                x = y = PAD
                row_h = 0

            glyph = Image.new('L', (w, h), 0)
            from PIL import ImageDraw
            ImageDraw.Draw(glyph).text((-bbox[0], -bbox[1]), ch, font=font, fill=255, anchor='ls')
            white = Image.new('RGBA', (w, h), (255, 255, 255, 0))
            white.putalpha(glyph)
            pages[-1].paste(white, (x, y))
            gx, gy, page = x, y, len(pages) - 1
            x += w + PAD
            row_h = max(row_h, h)
        else:
            gx = gy = page = 0

        chars.append((cp, gx, gy, w, h, bbox[0], ascent + bbox[1], round(advance), page))

    for i, p in enumerate(pages):
        p.save(out_dir / f'{name}_{i}.png')

    kernings = []
    # Kerning pairs for common Latin letters (Pillow measures pairs with kerning applied).
    letters = [c for c in CODEPOINTS if chr(c).isalnum() or chr(c) in '.,-\'"():/[]']
    letters = [c for c in letters if c < 0x7F]
    adv = {c[0]: font.getlength(chr(c[0])) for c in chars}
    for a in letters:
        for b in letters:
            if a not in adv or b not in adv:
                continue
            k = round(font.getlength(chr(a) + chr(b)) - adv[a] - adv[b])
            if k != 0:
                kernings.append((a, b, k))

    family = name.split('-')[0]

    data = bytearray(b'BMF\x03')

    info = struct.pack('<hBBHBBBBBBBB', SIZE, 0b11000000, 0, 100, 1, 0, 0, 0, 0, 0, 0, 0) + family.encode() + b'\x00'
    data += struct.pack('<BI', 1, len(info)) + info

    common = struct.pack('<HHHHHBBBBB', line_height, ascent, PAGE, PAGE, len(pages), 0, 0, 4, 4, 4)
    data += struct.pack('<BI', 2, len(common)) + common

    page_names = b''.join(f'{name}_{i}.png'.encode() + b'\x00' for i in range(len(pages)))
    data += struct.pack('<BI', 3, len(page_names)) + page_names

    char_block = b''.join(struct.pack('<IHHHHhhhBB', cp, gx, gy, w, h, xo, yo, xa, pg, 15) for cp, gx, gy, w, h, xo, yo, xa, pg in chars)
    data += struct.pack('<BI', 4, len(char_block)) + char_block

    if kernings:
        kern_block = b''.join(struct.pack('<IIh', a, b, k) for a, b, k in kernings)
        data += struct.pack('<BI', 5, len(kern_block)) + kern_block

    (out_dir / f'{name}.bin').write_bytes(bytes(data))
    print(f'{name}: {len(chars)} glyphs, {len(pages)} page(s), {len(kernings)} kerning pairs, line height {line_height}, base {ascent}')


if __name__ == '__main__':
    main()
