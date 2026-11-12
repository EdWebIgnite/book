"""Generates a placeholder picture book that uses focus areas instead of panels.

Each page is one big illustration. Stops point at areas of interest, one stop pans
(giraffe legs up to her head), captions carry the story text, and the book uses the
soft-edge focus style with one stop overriding it.

Run:  python3 tools/make_picture_book.py
"""
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CONTENT = Path(__file__).resolve().parent.parent / "content"
OUT = CONTENT / "books/waterhole"
W, H = 2400, 1500
FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def gradient(d, top, bottom, y0=0, y1=H):
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0)
        d.line([(0, y), (W, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))


def acacia(d, x, gy, s, leaf=(70, 120, 50)):
    trunk = (95, 65, 40)
    d.polygon([(x - 0.12 * s, gy), (x + 0.12 * s, gy), (x + 0.05 * s, gy - 1.6 * s), (x - 0.05 * s, gy - 1.6 * s)], fill=trunk)
    for dx in (-0.7, 0.7):
        d.line([(x, gy - 1.3 * s), (x + dx * s, gy - 2.0 * s)], fill=trunk, width=int(0.09 * s))
    d.ellipse([x - 1.5 * s, gy - 2.35 * s, x + 1.5 * s, gy - 1.85 * s], fill=leaf)
    d.ellipse([x - 1.1 * s, gy - 2.55 * s, x + 1.0 * s, gy - 2.05 * s], fill=leaf)


def elephant(d, x, gy, s):
    g, dark = (150, 150, 158), (90, 90, 98)
    for lx in (-0.55, -0.2, 0.25, 0.55):
        d.rectangle([x + lx * s - 0.12 * s, gy - 0.9 * s, x + lx * s + 0.12 * s, gy], fill=g, outline=dark, width=3)
    d.ellipse([x - 0.9 * s, gy - 1.6 * s, x + 0.8 * s, gy - 0.6 * s], fill=g, outline=dark, width=4)
    hx, hy = x - 0.95 * s, gy - 1.25 * s
    d.ellipse([hx - 0.45 * s, hy - 0.45 * s, hx + 0.45 * s, hy + 0.45 * s], fill=g, outline=dark, width=4)
    d.ellipse([hx - 0.1 * s, hy - 0.55 * s, hx + 0.55 * s, hy + 0.35 * s], fill=(170, 165, 170), outline=dark, width=4)
    # trunk curling down into the water
    pts = [(hx - 0.3 * s + math.sin(t / 10 * 1.4) * 0.15 * s, hy + t / 10 * 1.1 * s) for t in range(11)]
    d.line(pts, fill=g, width=int(0.2 * s), joint="curve")
    d.ellipse([hx - 0.22 * s - 8, hy - 0.08 * s - 8, hx - 0.22 * s + 8, hy - 0.08 * s + 8], fill="black")


def giraffe(d, x, gy, s):
    body, spot, dark = (235, 190, 90), (160, 95, 40), (120, 80, 40)
    for lx in (-0.35, -0.15, 0.2, 0.4):
        d.line([(x + lx * s, gy - 1.5 * s), (x + lx * s, gy)], fill=body, width=int(0.1 * s))
    d.ellipse([x - 0.55 * s, gy - 1.95 * s, x + 0.6 * s, gy - 1.3 * s], fill=body, outline=dark, width=3)
    d.polygon([(x + 0.2 * s, gy - 1.8 * s), (x + 0.45 * s, gy - 1.7 * s), (x + 0.75 * s, gy - 3.6 * s), (x + 0.55 * s, gy - 3.65 * s)], fill=body)
    for px, py in [(-0.3, -1.65), (0.0, -1.75), (0.3, -1.55), (0.45, -2.3), (0.58, -2.9), (0.5, -2.0)]:
        d.ellipse([x + px * s - 0.08 * s, gy + py * s - 0.06 * s, x + px * s + 0.08 * s, gy + py * s + 0.06 * s], fill=spot)
    hx, hy = x + 0.75 * s, gy - 3.7 * s
    d.ellipse([hx - 0.2 * s, hy - 0.12 * s, hx + 0.32 * s, hy + 0.14 * s], fill=body, outline=dark, width=3)
    for ox in (-0.08, 0.06):
        d.line([(hx + ox * s, hy - 0.1 * s), (hx + ox * s, hy - 0.3 * s)], fill=dark, width=8)
        d.ellipse([hx + ox * s - 10, hy - 0.3 * s - 10, hx + ox * s + 10, hy - 0.3 * s + 10], fill=dark)
    d.ellipse([hx + 0.05 * s - 7, hy - 0.04 * s - 7, hx + 0.05 * s + 7, hy - 0.04 * s + 7], fill="black")


def zebra(img, x, gy, s, flip=False):
    """Striped body drawn on its own layer through a mask so stripes stay inside the outline."""
    layer = Image.new("RGB", img.size, "white")
    mask = Image.new("L", img.size, 0)
    md, ld = ImageDraw.Draw(mask), ImageDraw.Draw(layer)
    f = -1 if flip else 1
    shapes = [
        ("e", [x - 0.6 * s, gy - 1.2 * s, x + 0.6 * s, gy - 0.6 * s]),
        ("p", [(x + f * 0.4 * s, gy - 1.1 * s), (x + f * 0.6 * s, gy - 1.2 * s), (x + f * 0.95 * s, gy - 1.6 * s), (x + f * 0.8 * s, gy - 1.75 * s)]),
        ("e", [min(x + f * 0.75 * s, x + f * 1.15 * s), gy - 1.8 * s, max(x + f * 0.75 * s, x + f * 1.15 * s), gy - 1.5 * s]),
    ]
    for lx in (-0.45, -0.25, 0.25, 0.45):
        shapes.append(("r", [x + lx * s - 0.06 * s, gy - 0.8 * s, x + lx * s + 0.06 * s, gy]))
    for kind, geo in shapes:
        getattr(md, {"e": "ellipse", "p": "polygon", "r": "rectangle"}[kind])(geo, fill=255)
    for i in range(-14, 16):
        sx = x + i * 0.09 * s
        ld.line([(sx, gy - 2 * s), (sx + 0.15 * s, gy)], fill="black", width=int(0.035 * s))
    img.paste(layer, (0, 0), mask)
    d = ImageDraw.Draw(img)
    d.ellipse(shapes[0][1], outline="black", width=4)
    ex = x + f * 1.0 * s
    d.ellipse([ex - 8, gy - 1.7 * s - 8, ex + 8, gy - 1.7 * s + 8], fill="black")


def bird(d, x, y, s):
    d.ellipse([x - s, y - 0.6 * s, x + s, y + 0.6 * s], fill=(40, 110, 200), outline="black", width=3)
    d.ellipse([x + 0.6 * s, y - 1.0 * s, x + 1.3 * s, y - 0.2 * s], fill=(40, 110, 200), outline="black", width=3)
    d.polygon([(x + 1.25 * s, y - 0.65 * s), (x + 1.7 * s, y - 0.5 * s), (x + 1.25 * s, y - 0.4 * s)], fill=(250, 180, 30))
    d.ellipse([x + 0.95 * s - 5, y - 0.7 * s - 5, x + 0.95 * s + 5, y - 0.7 * s + 5], fill="black")
    d.polygon([(x - 0.9 * s, y), (x - 1.8 * s, y - 0.3 * s), (x - 1.6 * s, y + 0.3 * s)], fill=(30, 80, 160))


def owl(d, x, y, s):
    d.ellipse([x - s, y - 1.2 * s, x + s, y + 1.2 * s], fill=(130, 95, 60), outline="black", width=3)
    for ex in (-0.4, 0.4):
        d.ellipse([x + ex * s - 0.35 * s, y - 0.75 * s, x + ex * s + 0.35 * s, y - 0.05 * s], fill=(255, 230, 120), outline="black", width=3)
        d.ellipse([x + ex * s - 0.12 * s, y - 0.52 * s, x + ex * s + 0.12 * s, y - 0.28 * s], fill="black")
    d.polygon([(x - 0.12 * s, y - 0.05 * s), (x + 0.12 * s, y - 0.05 * s), (x, y + 0.25 * s)], fill=(240, 160, 30))
    d.polygon([(x - 0.8 * s, y - 1.0 * s), (x - 0.5 * s, y - 1.6 * s), (x - 0.3 * s, y - 1.0 * s)], fill=(130, 95, 60))
    d.polygon([(x + 0.8 * s, y - 1.0 * s), (x + 0.5 * s, y - 1.6 * s), (x + 0.3 * s, y - 1.0 * s)], fill=(130, 95, 60))


def meerkat(d, x, gy, s):
    c, dark = (200, 160, 110), (110, 80, 50)
    d.ellipse([x - 0.25 * s, gy - 1.3 * s, x + 0.25 * s, gy], fill=c, outline=dark, width=3)
    d.ellipse([x - 0.2 * s, gy - 1.7 * s, x + 0.2 * s, gy - 1.25 * s], fill=c, outline=dark, width=3)
    for ex in (-0.08, 0.08):
        d.ellipse([x + ex * s - 9, gy - 1.52 * s - 9, x + ex * s + 9, gy - 1.52 * s + 9], fill="black")
    d.line([(x - 0.15 * s, gy - 0.95 * s), (x - 0.05 * s, gy - 0.8 * s)], fill=dark, width=8)
    d.line([(x + 0.15 * s, gy - 0.95 * s), (x + 0.05 * s, gy - 0.8 * s)], fill=dark, width=8)


def page_day():
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    gradient(d, (110, 180, 245), (210, 235, 250), 0, 900)
    gradient(d, (220, 190, 110), (195, 160, 90), 900, H)
    d.ellipse([1950, 90, 2150, 290], fill=(255, 215, 70))
    acacia(d, 1650, 900, 230)
    d.ellipse([500, 1050, 1550, 1350], fill=(70, 140, 200), outline=(50, 110, 170), width=6)
    elephant(d, 1350, 1150, 260)
    giraffe(d, 1900, 1180, 260)
    zebra(img, 330, 1180, 230)
    zebra(img, 620, 1230, 200, flip=True)
    bird(ImageDraw.Draw(img), 1290, 405, 40)
    return img


def page_evening():
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    gradient(d, (60, 40, 110), (245, 140, 70), 0, 950)
    gradient(d, (150, 110, 70), (110, 80, 55), 950, H)
    d.ellipse([300, 120, 470, 290], fill=(250, 245, 220))
    acacia(d, 1850, 950, 260, leaf=(40, 70, 40))
    owl(d, 1990, 410, 55)
    d.ellipse([350, 1050, 1550, 1380], fill=(50, 90, 140), outline=(40, 70, 110), width=6)
    # hippo: just ears and eyes above the water
    for ex in (850, 960):
        d.ellipse([ex - 40, 1120, ex + 40, 1185], fill=(120, 100, 120), outline="black", width=3)
        d.ellipse([ex - 12, 1135, ex + 12, 1160], fill="black")
    d.ellipse([820, 1095, 860, 1130], fill=(120, 100, 120))
    d.ellipse([950, 1095, 990, 1130], fill=(120, 100, 120))
    for i, mx in enumerate((1700, 1820, 1940)):
        meerkat(d, mx, 1330 - i * 10, 150)
    return img


def cover():
    img = page_day().crop((0, 0, 1000, 1500)).resize((1000, 1500))
    d = ImageDraw.Draw(img)
    f = ImageFont.truetype(FONT_BOLD, 110)
    for line, y in (("Who's at the", 120), ("Waterhole?", 250)):
        tw = d.textlength(line, font=f)
        d.text(((1000 - tw) / 2 + 5, y + 5), line, font=f, fill="black")
        d.text(((1000 - tw) / 2, y), line, font=f, fill=(255, 140, 0))
    return img


def box(x0, y0, x1, y1):
    """Pixel box on a W x H page to fractions."""
    return {"x": round(x0 / W, 4), "y": round(y0 / H, 4), "w": round((x1 - x0) / W, 4), "h": round((y1 - y0) / H, 4)}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    cover().save(OUT / "cover.webp", quality=85)
    page_day().save(OUT / "page1.webp", quality=85)
    page_evening().save(OUT / "page2.webp", quality=85)

    book = {
        "id": "waterhole",
        "title": "Who's at the Waterhole?",
        "author": "Sample book",
        "cover": "cover.webp",
        "focusStyle": "vignette",
        "pages": [
            {"image": "page1.webp", "stops": [
                {"box": box(900, 650, 1650, 1300), "caption": "The elephant was very thirsty."},
                {"box": box(1700, 820, 2250, 1220), "to": box(1800, 160, 2250, 560), "panSeconds": 4,
                 "caption": "The giraffe looked up, up, up..."},
                {"box": box(60, 650, 900, 1290), "caption": "The zebras waited for their turn."},
                {"box": box(1150, 300, 1450, 500), "style": "none", "caption": "And a little bird watched them all."},
            ]},
            {"image": "page2.webp", "stops": [
                {"box": box(650, 1000, 1200, 1250), "caption": "Something was hiding in the water."},
                {"box": box(1550, 1050, 2100, 1360), "caption": "The meerkats stood very still."},
                {"box": box(1850, 270, 2130, 560), "style": "dim", "caption": "\"Hoo!\" said the owl. \"Goodnight!\""},
            ]},
        ],
    }
    (OUT / "book.json").write_text(json.dumps(book, indent=2))
    ids = sorted(p.name for p in (CONTENT / "books").iterdir() if (p / "book.json").exists())
    (CONTENT / "library.json").write_text(json.dumps({"books": ids}, indent=2))
    print(f"Wrote {OUT}")


if __name__ == "__main__":
    main()
