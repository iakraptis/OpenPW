"""Draw the shroud and fog tiles (mods/pw/shroud.png): original art, not taken from Primitive Wars, whose fog is
shaded per pixel through lookup tables (JW2_02 Fogmask1/2.trt) rather than drawn from tiles.

The engine's ShroudRenderer (corner mode) draws, on each cell next to the shroud, the tile for the set of its corners
that touch the shroud (ShroudRenderer.Index): 12 edge tiles, then a full tile. Each tile here is black, its opacity
blended smoothly between the corners. Fog is the same at half strength.

Usage: python tools/shroud_art.py   (needs Pillow)
"""
import os

from PIL import Image
from PIL.PngImagePlugin import PngInfo

SIZE = 32
INDEX = [12, 9, 8, 3, 1, 6, 4, 2, 13, 11, 7, 14]   # ShroudRenderer's default: corner flags of each tile
TOP_LEFT, TOP_RIGHT, BOTTOM_RIGHT, BOTTOM_LEFT = 1, 2, 4, 8
FOG = 0.5


def smooth(t):
    return t * t * (3 - 2 * t)


def tile(corners, strength):
    image = Image.new('RGBA', (SIZE, SIZE))
    tl, tr = bool(corners & TOP_LEFT), bool(corners & TOP_RIGHT)
    bl, br = bool(corners & BOTTOM_LEFT), bool(corners & BOTTOM_RIGHT)
    for y in range(SIZE):
        v = smooth((y + 0.5) / SIZE)
        for x in range(SIZE):
            u = smooth((x + 0.5) / SIZE)
            a = (tl * (1 - u) * (1 - v) + tr * u * (1 - v) + bl * (1 - u) * v + br * u * v)
            image.putpixel((x, y), (0, 0, 0, round(255 * strength * a)))
    return image


def main():
    tiles = [tile(c, 1) for c in INDEX] + [tile(15, 1)] + [tile(c, FOG) for c in INDEX] + [tile(15, FOG)]
    sheet = Image.new('RGBA', (SIZE * len(tiles), SIZE))
    for i, t in enumerate(tiles):
        sheet.paste(t, (i * SIZE, 0))
    meta = PngInfo()
    meta.add_text('FrameSize', f'{SIZE},{SIZE}')
    meta.add_text('FrameAmount', str(len(tiles)))
    out = os.path.join(os.path.dirname(__file__), '..', 'mods', 'pw', 'shroud.png')
    sheet.save(out, pnginfo=meta)
    print(out, sheet.size)


if __name__ == '__main__':
    main()
