"""Draw the victory and defeat screens (mods/pw/chrome/assets/end-<race>.png, end-defeat.png), PwEndScreenWidget's art.

Victory: one painted picture per race (made with an image generator, not taken from Primitive Wars; the sources are
kept outside git, see test_feedback.md for the prompts), 1248 x 832. Defeat: a plain aged parchment drawn here.
Each picture gets the message panel at the right middle baked in: the picture behind it blurred and darkened, rounded
corners, a thin rule, the title and the line under it. Victory is lettered in the race's font and colours (those of
its Menu button, OpenRA.Mods.Pw/AssetInstaller/MenuButtons.cs), defeat in OpenRA's FreeSans. The widget writes the
elapsed time and "Click to continue" in the panel's lower part (PANEL below; keep the two in step).

Each sheet is 2048 x 1024 (chrome sheets must be a power of two): the picture at 0,0 and, at 1280,0, a small blurred
and darkened copy (256 x 171) that the widget stretches over the window's margins when it is wider than the picture.

Usage: python tools/end_screens.py <folder with the victory pictures>   (needs Pillow and numpy)
"""
import glob
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, 'OpenRA.Mods.Pw', 'AssetInstaller', 'Fonts')
FREESANS = os.path.join(ROOT, 'engine', 'mods', 'common')
OUT = os.path.join(ROOT, 'mods', 'pw', 'chrome', 'assets')

W, H = 1248, 832
# The panel in picture pixels: centre x, centre y, width, height (PwEndScreenWidget's Panel).
PANEL = (1010, 416, 426, 212)
BACK = (256, 171)


def C(r, g, b):
    return (r, g, b)


# The picture (a file name part in the source folder), the race's lettering, and its colours.
RACES = {
    'primitive': dict(image='primitive', name='Primitive', font='CaesarDressing-Regular.ttf',
                      fill=[C(255, 214, 120), C(226, 112, 26)], outline=C(36, 18, 6), glow=C(255, 140, 30), light=C(255, 240, 190)),
    'tyrano': dict(image='reptilian', name='Tyrano', font='MetalMania-Regular.ttf',
                   fill=[C(222, 220, 190), C(150, 148, 116)], outline=C(24, 24, 16), glow=C(120, 130, 90), light=C(250, 248, 226)),
    'demon': dict(image='demon', name='Demon', font='PirataOne-Regular.ttf',
                  fill=[C(246, 206, 186), C(176, 92, 82)], outline=C(16, 10, 10), glow=C(170, 24, 18), light=C(255, 236, 222)),
    'elf': dict(image='elf', name='Elf', font='UncialAntiqua-Regular.ttf',
                fill=[C(246, 184, 138), C(196, 112, 72)], outline=C(34, 12, 4), glow=C(255, 170, 110), light=C(255, 218, 182)),
}


def parchment():
    """A plain aged parchment: light sepia, faint mottling and grain, darkening gradually to brown at the edges."""
    rng = np.random.default_rng(7)

    def noise(scale, blur):
        n = rng.random((H // scale + 2, W // scale + 2))
        im = Image.fromarray((n * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(blur))
        a = np.asarray(im).astype(float) / 255
        return (a - a.mean()) / (a.std() + 1e-6)

    mottle = 0.45 * noise(90, 22) + 0.35 * noise(30, 7) + 0.20 * noise(10, 2)
    grain = Image.fromarray((rng.random((H, W)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7))
    grain = np.asarray(grain).astype(float) / 255 - 0.5
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.maximum(np.abs(xx - W / 2) / (W / 2), np.abs(yy - H / 2) / (H / 2)) * 0.55 + \
        np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) * 0.45
    d = d + 0.025 * noise(200, 40)
    burn = np.clip((d - 0.45) / 0.65, 0, 1) ** 1.8 * 0.85
    paper = np.array([226, 206, 166], float)
    burnt = np.array([88, 60, 34], float)
    col = paper[None, None, :] * (1 + 0.035 * mottle + 0.05 * grain)[..., None]
    col = col * (1 - burn[..., None]) + burnt[None, None, :] * burn[..., None]
    stains = np.clip(noise(70, 18) - 1.6, 0, None) * 0.08
    col = col * (1 - stains[..., None])
    return Image.fromarray(np.clip(col, 0, 255).astype(np.uint8))


def panel(img, rule):
    cx, cy, pw, ph = PANEL
    px, py = cx - pw // 2, cy - ph // 2
    region = ImageEnhance.Brightness(img.crop((px, py, px + pw, py + ph)).filter(ImageFilter.GaussianBlur(13))).enhance(0.55)
    rounded = Image.new('L', (pw, ph), 0)
    ImageDraw.Draw(rounded).rounded_rectangle([0, 0, pw - 1, ph - 1], radius=13, fill=255)
    img.paste(region, (px, py), rounded)
    tint = Image.new('RGBA', (pw, ph), (0, 0, 0, 0))
    ImageDraw.Draw(tint).rounded_rectangle([0, 0, pw - 1, ph - 1], radius=13, fill=(18, 10, 4, 70), outline=rule + (150,), width=2)
    img.alpha_composite(tint, (px, py))
    return px, py


def victory(src, race):
    r = RACES[race]
    img = src.convert('RGB').resize((W, H), Image.LANCZOS).convert('RGBA')
    px, py = panel(img, r['light'])
    cx = PANEL[0]
    cy = py + 72
    font = ImageFont.truetype(os.path.join(FONTS, r['font']), 78)
    sub = ImageFont.truetype(os.path.join(FONTS, r['font']), 23)
    # Title: gradient fill, dark outline and a glow behind, as the race's lettered Menu button.
    mask = Image.new('L', (W, H), 0)
    ImageDraw.Draw(mask).text((cx, cy), 'VICTORY', font=font, fill=255, anchor='mm')
    glow = mask.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(11)).point(lambda v: min(255, v * 2))
    img.paste(Image.new('RGBA', (W, H), r['glow'] + (255,)), (0, 0), glow)
    img.paste(Image.new('RGBA', (W, H), r['outline'] + (255,)), (0, 0), mask.filter(ImageFilter.MaxFilter(7)))
    box = mask.getbbox()
    grad = Image.new('RGB', (W, H))
    top, bottom = r['fill']
    for y in range(box[1], box[3]):
        t = (y - box[1]) / max(1, box[3] - box[1])
        grad.paste(tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)), (0, y, W, y + 1))
    img.paste(grad.convert('RGBA'), (0, 0), mask)
    ImageDraw.Draw(img).text((cx, cy + 61), f'The {r["name"]} tribe stands victorious', font=sub, fill=r['light'] + (255,),
                             anchor='mm', stroke_width=2, stroke_fill=r['outline'] + (255,))
    return img


def defeat():
    img = parchment().convert('RGBA')
    px, py = panel(img, C(222, 206, 176))
    cx, cy = PANEL[0], py + 68
    d = ImageDraw.Draw(img)
    d.text((cx, cy), 'Defeat', font=ImageFont.truetype(os.path.join(FREESANS, 'FreeSansBold.ttf'), 63), fill=(226, 214, 192, 255),
           anchor='mm', stroke_width=3, stroke_fill=(30, 22, 14, 255))
    d.text((cx, cy + 57), 'Your tribe has fallen', font=ImageFont.truetype(os.path.join(FREESANS, 'FreeSans.ttf'), 24),
           fill=(214, 200, 176, 255), anchor='mm', stroke_width=2, stroke_fill=(30, 22, 14, 255))
    return img


def sheet(picture, source, name):
    out = Image.new('RGBA', (2048, 1024), (0, 0, 0, 0))
    out.paste(picture, (0, 0))
    back = source.convert('RGB').resize((W // 2, H // 2)).filter(ImageFilter.GaussianBlur(12)).resize(BACK, Image.LANCZOS)
    out.paste(ImageEnhance.Brightness(back).enhance(0.35).convert('RGBA'), (1280, 0))
    path = os.path.join(OUT, name)
    out.save(path, optimize=True)
    print(path)


def main(source_folder):
    for race, r in RACES.items():
        found = [f for f in glob.glob(os.path.join(source_folder, '*')) if r['image'] in os.path.basename(f).lower()
                 and f.lower().endswith(('.jpg', '.jpeg', '.png'))]
        if len(found) != 1:
            sys.exit(f'{race}: expected one picture with "{r["image"]}" in its name in {source_folder}, found {found}')
        src = Image.open(found[0])
        sheet(victory(src, race), src.resize((W, H)), f'end-{race}.png')

    paper = parchment()
    sheet(defeat(), paper, 'end-defeat.png')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'research', 'previews', 'GenImages'))
