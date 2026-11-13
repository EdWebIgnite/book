"""
Turns the painted artwork for Moss and the Biggest Puddle into the page images the reader loads.

    python3 tools/make_puddle_pages.py

Reads the PNGs in art/biggest-puddle/export/ and writes JPEGs into content/books/biggest-puddle/.
The camera stops live in that folder's book.json and are edited in the book editor, so this
script never touches book.json. Needs Pillow (pip install pillow).

The PNGs are enlargements of smaller paintings (see art/biggest-puddle/README.txt), so the pages
are saved a little smaller than the PNGs: nothing is lost and they load faster on a phone.
"""
import os
from PIL import Image, ImageDraw, ImageFont

Image.MAX_IMAGE_PIXELS = None
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SRC = os.path.join(ROOT, "art", "biggest-puddle", "export")
OUT = os.path.join(ROOT, "content", "books", "biggest-puddle")

JOBS = [("page%d" % n, (2400, 1500)) for n in range(1, 12)] + [("page12", (4800, 1500)), ("cover", (1200, 1800))]

def title(im):
    """The painting has no lettering, so the title goes on here, in the calm sky at the top."""
    font = os.path.join(ROOT, "tools", "art", "fonts", "baloo-2-latin-800-normal.woff2")
    d = ImageDraw.Draw(im)
    w = im.width
    y = round(im.height * 0.045)
    for text, px in (("Moss", 0.2), ("and the", 0.085), ("Biggest Puddle", 0.125)):
        f = ImageFont.truetype(font, round(w * px))
        d.text((w / 2, y), text, font=f, anchor="ma", fill="#fff6dc", stroke_width=round(w * px * 0.09), stroke_fill="#2f4f7a")
        y += round(w * px * 1.12)


for name, size in JOBS:
    im = Image.open(os.path.join(SRC, name + ".png")).convert("RGB")
    if abs(im.width / im.height - size[0] / size[1]) > 0.01:
        raise SystemExit("%s.png is %dx%d, expected the shape of %dx%d" % (name, im.width, im.height, size[0], size[1]))
    out = os.path.join(OUT, name + ".jpg")
    im = im.resize(size, Image.LANCZOS)
    if name == "cover":
        title(im)
    im.save(out, "JPEG", quality=82, optimize=True, progressive=True)
    print("%s.jpg  %dx%d  %d KB" % (name, size[0], size[1], os.path.getsize(out) // 1024))
