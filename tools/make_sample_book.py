"""Generates a placeholder comic book for testing the reader.

Draws simple page art with Pillow and writes book.json with panel boxes taken
from the same layout, so the coordinates always match the pictures.

Run:  python3 tools/make_sample_book.py
"""
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CONTENT = Path(__file__).resolve().parent.parent / "content"
OUT = CONTENT / "books/zolas-kite"
W, H = 1200, 1800
MARGIN, GUTTER = 50, 30
BORDER = 8

FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def font(size, bold=True):
    return ImageFont.truetype(FONT_BOLD if bold else FONT, size)


# ---------- little drawing helpers ----------

def sky(d, box, top=(120, 190, 255), bottom=(200, 235, 255)):
    x0, y0, x1, y1 = box
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        d.line([(x0, y), (x1, y)], fill=c)


def ground(d, box, level, colour=(110, 180, 80)):
    x0, y0, x1, y1 = box
    gy = y0 + int((y1 - y0) * level)
    d.rectangle([x0, gy, x1, y1], fill=colour)
    return gy


def sun(d, cx, cy, r):
    for a in range(0, 360, 30):
        ra = math.radians(a)
        d.line([(cx + math.cos(ra) * r * 1.2, cy + math.sin(ra) * r * 1.2),
                (cx + math.cos(ra) * r * 1.6, cy + math.sin(ra) * r * 1.6)],
               fill=(255, 190, 40), width=8)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 210, 60), outline=(230, 150, 20), width=4)


def cloud(d, cx, cy, s):
    for dx, dy, r in [(-1.0, 0.2, 0.7), (0, 0, 1.0), (1.0, 0.2, 0.7), (0.5, -0.4, 0.6)]:
        x, y, rr = cx + dx * s, cy + dy * s, r * s
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill="white")


def kid(d, x, gy, s, shirt=(230, 60, 90), arm_up=False, happy=True):
    """Simple cartoon kid standing on ground line gy, centred on x."""
    skin, hair = (120, 75, 45), (30, 20, 15)
    body_h, head_r = 1.6 * s, 0.55 * s
    # legs
    d.line([(x - 0.25 * s, gy - 0.9 * s), (x - 0.3 * s, gy)], fill=skin, width=int(0.18 * s))
    d.line([(x + 0.25 * s, gy - 0.9 * s), (x + 0.3 * s, gy)], fill=skin, width=int(0.18 * s))
    # dress / shirt
    top = gy - 0.9 * s - body_h * 0.7
    d.polygon([(x - 0.35 * s, top), (x + 0.35 * s, top), (x + 0.6 * s, gy - 0.8 * s), (x - 0.6 * s, gy - 0.8 * s)],
              fill=shirt, outline="black")
    # arms
    sh_y = top + 0.15 * s
    d.line([(x - 0.35 * s, sh_y), (x - 0.8 * s, sh_y + 0.6 * s)], fill=skin, width=int(0.16 * s))
    if arm_up:
        d.line([(x + 0.35 * s, sh_y), (x + 0.8 * s, sh_y - 0.7 * s)], fill=skin, width=int(0.16 * s))
        hand = (x + 0.8 * s, sh_y - 0.7 * s)
    else:
        d.line([(x + 0.35 * s, sh_y), (x + 0.8 * s, sh_y + 0.6 * s)], fill=skin, width=int(0.16 * s))
        hand = (x + 0.8 * s, sh_y + 0.6 * s)
    # head
    hy = top - head_r * 0.9
    d.ellipse([x - head_r, hy - head_r, x + head_r, hy + head_r], fill=skin, outline="black", width=3)
    # hair puffs
    for px in (-0.55, 0.55):
        d.ellipse([x + px * s - 0.25 * s, hy - head_r - 0.1 * s, x + px * s + 0.25 * s, hy - head_r + 0.4 * s], fill=hair)
    d.chord([x - head_r, hy - head_r, x + head_r, hy + head_r * 0.2], 180, 360, fill=hair)
    # face
    d.ellipse([x - 0.22 * s - 5, hy - 5, x - 0.22 * s + 5, hy + 5], fill="black")
    d.ellipse([x + 0.22 * s - 5, hy - 5, x + 0.22 * s + 5, hy + 5], fill="black")
    if happy:
        d.arc([x - 0.2 * s, hy, x + 0.2 * s, hy + 0.3 * s], 20, 160, fill="black", width=4)
    else:
        d.arc([x - 0.2 * s, hy + 0.2 * s, x + 0.2 * s, hy + 0.45 * s], 200, 340, fill="black", width=4)
    return hand


def kite(d, cx, cy, s, angle=0.0):
    pts = [(0, -1.2), (0.8, 0), (0, 1.5), (-0.8, 0)]
    ca, sa = math.cos(angle), math.sin(angle)
    rot = [(cx + (px * ca - py * sa) * s, cy + (px * sa + py * ca) * s) for px, py in pts]
    d.polygon(rot, fill=(255, 140, 0), outline="black")
    d.line([rot[0], rot[2]], fill="black", width=3)
    d.line([rot[1], rot[3]], fill="black", width=3)
    tail = rot[2]
    for i in range(4):
        tx, ty = tail[0] + (i + 1) * 0.35 * s * math.sin(-angle) + (15 if i % 2 else -15), tail[1] + (i + 1) * 0.45 * s
        d.polygon([(tx - 14, ty - 10), (tx + 14, ty - 10), (tx, ty + 10)], fill=(40, 120, 220))
    return tail


def baobab(d, x, gy, s):
    trunk = (150, 110, 80)
    d.polygon([(x - 0.7 * s, gy), (x + 0.7 * s, gy), (x + 0.45 * s, gy - 2.2 * s), (x - 0.45 * s, gy - 2.2 * s)],
              fill=trunk, outline="black")
    for dx in (-1.1, -0.5, 0.2, 0.9):
        d.line([(x + dx * 0.4 * s, gy - 2.1 * s), (x + dx * s, gy - 2.9 * s)], fill=trunk, width=int(0.18 * s))
        d.ellipse([x + dx * s - 0.5 * s, gy - 3.3 * s, x + dx * s + 0.5 * s, gy - 2.6 * s], fill=(80, 150, 60))


def bubble(d, box, text, tail_to, size=44):
    """Speech bubble inside box (x0,y0,x1,y1) with a tail pointing at tail_to."""
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    d.polygon([(cx - 30, cy + (y1 - y0) / 2 - 10), (cx + 30, cy + (y1 - y0) / 2 - 10), tail_to],
              fill="white", outline="black")
    d.ellipse(box, fill="white", outline="black", width=5)
    d.polygon([(cx - 26, cy + (y1 - y0) / 2 - 14), (cx + 26, cy + (y1 - y0) / 2 - 14),
               (tail_to[0] * 0.15 + cx * 0.85, tail_to[1] * 0.15 + (cy + (y1 - y0) / 2) * 0.85)], fill="white")
    f = font(size)
    lines = text.split("\n")
    lh = size * 1.15
    ty = cy - lh * len(lines) / 2
    for line in lines:
        tw = d.textlength(line, font=f)
        d.text((cx - tw / 2, ty), line, font=f, fill="black")
        ty += lh


def caption(d, box, text, size=40):
    x0, y0, _, y1 = box
    f = font(size, bold=False)
    x1 = x0 + d.textlength(text, font=f) + 36  # fit the box to the text
    d.rectangle((x0, y0, x1, y1), fill=(255, 240, 150), outline="black", width=4)
    d.text((x0 + 18, y0 + (y1 - y0 - size) / 2 - 4), text, font=f, fill="black")


def panel_frame(d, box):
    d.rectangle(box, outline="black", width=BORDER)


# ---------- layout ----------

def rows(spec):
    """spec: list of (height_fraction, [width_fractions]). Returns panel boxes in reading order."""
    boxes = []
    usable_h = H - 2 * MARGIN - GUTTER * (len(spec) - 1)
    y = MARGIN
    for hf, cols in spec:
        ph = int(usable_h * hf)
        usable_w = W - 2 * MARGIN - GUTTER * (len(cols) - 1)
        x = MARGIN
        for wf in cols:
            pw = int(usable_w * wf)
            boxes.append((x, y, x + pw, y + ph))
            x += pw + GUTTER
        y += ph + GUTTER
    return boxes


def clip_draw(img, box, painter):
    """Paint into a panel-sized layer so art never spills over the gutters."""
    x0, y0, x1, y1 = box
    layer = Image.new("RGB", (x1 - x0, y1 - y0), "white")
    ld = ImageDraw.Draw(layer)
    painter(ld, (0, 0, x1 - x0, y1 - y0))
    img.paste(layer, (x0, y0))


# ---------- the story ----------

def page1(img, d):
    boxes = rows([(0.40, [1.0]), (0.30, [0.5, 0.5]), (0.30, [1.0])])

    def p1(ld, b):
        sky(ld, b)
        gy = ground(ld, b, 0.75)
        sun(ld, b[2] - 160, 140, 70)
        cloud(ld, 260, 150, 70)
        cloud(ld, 620, 230, 55)
        baobab(ld, 220, gy, 90)
        kid(ld, 720, gy, 110)
        caption(ld, (20, 20, 640, 90), "It was a windy day in the village.")

    def p2(ld, b):
        sky(ld, b, (255, 220, 160), (255, 245, 220))
        gy = ground(ld, b, 0.85, (190, 140, 90))
        kid(ld, b[2] // 2, gy, 70, happy=True)
        bubble(ld, (40, 20, b[2] - 40, 170), "Gogo, can I\nfly my kite?", (b[2] // 2 - 10, 195))

    def p3(ld, b):
        sky(ld, b, (255, 220, 160), (255, 245, 220))
        gy = ground(ld, b, 0.85, (190, 140, 90))
        # Gogo: taller kid figure in a purple dress with a head wrap
        s = 80
        kid(ld, b[2] // 2, gy, s, shirt=(140, 70, 170))
        hy = gy - 0.9 * s - 1.12 * s - 0.55 * s * 0.9  # head centre, matches kid()
        ld.rectangle([b[2] // 2 - 50, hy - 0.55 * s - 12, b[2] // 2 + 50, hy - 0.55 * s + 16], fill=(240, 180, 30))
        bubble(ld, (30, 15, b[2] - 30, 150), "Yes! Hold\nthe string tight.", (b[2] // 2 + 30, 175), size=38)

    def p4(ld, b):
        sky(ld, b)
        gy = ground(ld, b, 0.8)
        hand = kid(ld, 300, gy, 100, arm_up=True)
        tail = kite(ld, 820, 170, 70, angle=0.3)
        ld.line([hand, (820, 240)], fill="black", width=3)
        bubble(ld, (520, 280, b[2] - 40, 440), "Up, up, up!", (420, 300))

    for box, painter in zip(boxes, [p1, p2, p3, p4]):
        clip_draw(img, box, painter)
        panel_frame(d, box)
    return boxes


def page2(img, d):
    boxes = rows([(0.33, [0.6, 0.4]), (0.34, [1.0]), (0.33, [0.4, 0.6])])

    def p1(ld, b):
        sky(ld, b)
        kite(ld, b[2] // 2, b[3] // 2 - 20, 90, angle=-0.4)
        cloud(ld, 120, 110, 50)
        for i in range(5):
            y = 80 + i * 90
            ld.arc([b[2] - 260, y, b[2] - 40, y + 60], 200, 340, fill="white", width=6)
        caption(ld, (20, b[3] - 90, 520, b[3] - 20), "WHOOSH! went the wind.")

    def p2(ld, b):
        sky(ld, b, (255, 220, 160), (255, 245, 220))
        gy = ground(ld, b, 0.85, (190, 140, 90))
        kid(ld, b[2] // 2, gy, 80, happy=False)
        bubble(ld, (20, 20, b[2] - 20, 170), "Oh no!", (b[2] // 2, 250))

    def p3(ld, b):
        sky(ld, b)
        gy = ground(ld, b, 0.8)
        baobab(ld, b[2] // 2, gy, 110)
        kite(ld, b[2] // 2 + 60, gy - 3.1 * 110, 55, angle=0.9)
        caption(ld, (20, 20, 760, 90), "The kite was stuck in the big baobab.")

    def p4(ld, b):
        sky(ld, b, (255, 220, 160), (255, 245, 220))
        gy = ground(ld, b, 0.85, (190, 140, 90))
        # a little goat friend
        ld.ellipse([110, gy - 170, 330, gy - 60], fill="white", outline="black", width=4)
        ld.ellipse([290, gy - 230, 390, gy - 140], fill="white", outline="black", width=4)
        for lx in (140, 190, 260, 300):
            ld.line([(lx, gy - 70), (lx, gy)], fill="black", width=8)
        ld.ellipse([345, gy - 200, 357, gy - 188], fill="black")
        bubble(ld, (20, 30, b[2] - 20, 200), "Meeh?", (300, 300))

    def p5(ld, b):
        sky(ld, b, (255, 220, 160), (255, 245, 220))
        gy = ground(ld, b, 0.85, (190, 140, 90))
        kid(ld, 120, gy, 75)
        bubble(ld, (200, 30, b[2] - 15, 240), "Thabo! Help\nme get my kite!", (180, 290), size=36)

    for box, painter in zip(boxes, [p1, p2, p3, p4, p5]):
        clip_draw(img, box, painter)
        panel_frame(d, box)
    return boxes


def page3(img, d):
    boxes = rows([(0.5, [0.5, 0.5]), (0.5, [1.0])])

    def p1(ld, b):
        sky(ld, b)
        gy = ground(ld, b, 0.85)
        baobab(ld, b[2] // 2 + 60, gy, 100)
        # goat jumps
        gx, gyy = 160, gy - 260
        ld.ellipse([gx, gyy, gx + 200, gyy + 100], fill="white", outline="black", width=4)
        ld.ellipse([gx + 170, gyy - 60, gx + 260, gyy + 20], fill="white", outline="black", width=4)
        ld.text((40, 40), "BOING!", font=font(70), fill=(230, 40, 60))

    def p2(ld, b):
        sky(ld, b)
        kite(ld, b[2] // 2, b[3] // 2 - 40, 80, angle=0.2)
        ld.text((60, b[3] - 140), "Free!", font=font(80), fill=(40, 120, 220))

    def p3(ld, b):
        sky(ld, b, (255, 170, 110), (255, 220, 160))
        gy = ground(ld, b, 0.8)
        sun(ld, 160, 160, 80)
        hand = kid(ld, 520, gy, 120, arm_up=True)
        kite(ld, 900, 200, 75, angle=0.25)
        ld.line([hand, (900, 280)], fill="black", width=3)
        bubble(ld, (40, 300, 420, 470), "Thank you!", (450, 520))
        caption(ld, (b[2] - 320, b[3] - 90, b[2] - 20, b[3] - 20), "The End")

    for box, painter in zip(boxes, [p1, p2, p3]):
        clip_draw(img, box, painter)
        panel_frame(d, box)
    return boxes


def cover():
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    sky(d, (0, 0, W, H))
    gy = ground(d, (0, 0, W, H), 0.8)
    sun(d, 1030, 430, 70)
    hand = kid(d, 420, gy, 160, arm_up=True)
    kite(d, 820, 620, 110, angle=0.3)
    d.line([hand, (820, 720)], fill="black", width=4)
    title = "Zola's Kite"
    f = font(130)
    tw = d.textlength(title, font=f)
    d.text(((W - tw) / 2 + 6, 156), title, font=f, fill="black")
    d.text(((W - tw) / 2, 150), title, font=f, fill=(255, 140, 0))
    return img


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    cover().save(OUT / "cover.webp", quality=85)

    pages = []
    for i, painter in enumerate([page1, page2, page3], start=1):
        img = Image.new("RGB", (W, H), "white")
        d = ImageDraw.Draw(img)
        boxes = painter(img, d)
        name = f"page{i}.webp"
        img.save(OUT / name, quality=85)
        pages.append({
            "image": name,
            # Comic book: each stop is exactly one panel.
            "stops": [
                {"box": {"x": round(x0 / W, 4), "y": round(y0 / H, 4),
                         "w": round((x1 - x0) / W, 4), "h": round((y1 - y0) / H, 4)}}
                for x0, y0, x1, y1 in boxes
            ],
        })

    book = {"id": "zolas-kite", "title": "Zola's Kite", "author": "Sample book",
            "cover": "cover.webp", "focusStyle": "dim", "pages": pages}
    (OUT / "book.json").write_text(json.dumps(book, indent=2))
    # The web playground can't list folders, so it reads this list of books.
    ids = sorted(p.name for p in (CONTENT / "books").iterdir() if (p / "book.json").exists())
    (CONTENT / "library.json").write_text(json.dumps({"books": ids}, indent=2))
    print(f"Wrote {len(pages)} pages to {OUT}")


if __name__ == "__main__":
    main()
