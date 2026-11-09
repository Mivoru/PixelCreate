"""
Paints the seven pictures of the intro (320x180 pixel art, ENDESGA-32 colours) and writes them to
client/assets/intro, with a preview sheet in export/previews/intro.png. The script is the source of
the pictures: change it and run it again, then Godot's --import.

1 a rainy crossing at night, a man waits with an umbrella, 2 a child runs after a ball, 3 the lorry's
lights, 4 the push, 5 white, with what was dropped, 6 dark with the Lattice's words, 7 falling through
clouds over the valley, with the title.

Everything is whole pixels in palette colours; glows and skies are dithered between neighbours of a
ramp (ordered 4x4), never blended.
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "client" / "assets" / "intro"
FONT = ROOT / "client" / "assets" / "ui" / "Nunito.ttf"
W, H = 320, 180
HORIZON = 104  # where the far kerb meets the houses

C = {
    "ink": "181425", "night": "262b44", "dusk": "3a4466", "steel": "5a6988", "fog": "8b9bb4", "pale": "c0cbdc",
    "white": "ffffff", "cream": "ead4aa", "skin": "e8b796", "skin_dark": "c28569", "red": "e43b44", "blood": "a22633",
    "orange": "f77622", "amber": "feae34", "yellow": "fee761", "green": "63c74d", "teal": "193c3e", "deep": "124e89",
    "blue": "0099db", "cyan": "2ce8f5", "brown": "733e39", "earth": "3e2731", "pink": "f6757a", "purple": "68386c",
    "leaf": "3e8948", "forest": "265c42", "wood": "b86f50", "plum": "3e2731",
}

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def rgb(name):
    value = C[name]
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def canvas(color="ink"):
    image = Image.new("RGB", (W, H), rgb(color))
    return image, ImageDraw.Draw(image)


def ramp_pixel(ramp, t, x, y):
    """The colour of a ramp at t (0 to 1), dithered between its two nearest steps."""
    t = min(max(t, 0.0), 0.9999) * (len(ramp) - 1)
    low = int(t)
    return rgb(ramp[low + 1] if (t - low) * 16 > BAYER[y % 4][x % 4] + 0.5 else ramp[low])


def gradient(image, top, bottom, ramp, power=1.0):
    for y in range(top, bottom):
        t = ((y - top) / max(1, bottom - top - 1)) ** power
        for x in range(W):
            image.putpixel((x, y), ramp_pixel(ramp, t, x, y))


def glow(image, cx, cy, radius, ramp, squash=1.0, floor=None):
    """A dithered light: the ramp runs from the rim (first) to the core (last). Only brightens what is there
    when floor names the ramp step below which nothing is painted."""
    for y in range(max(0, int(cy - radius * squash)), min(H, int(cy + radius * squash) + 1)):
        for x in range(max(0, int(cx - radius)), min(W, int(cx + radius) + 1)):
            d = math.hypot(x - cx, (y - cy) / squash) / radius
            if d >= 1:
                continue
            t = (1 - d) ** 1.6
            if t * 16 * (len(ramp) - 1) <= BAYER[y % 4][x % 4] + 0.5 and floor is None:
                continue
            if floor is not None and t < floor:
                continue
            image.putpixel((x, y), ramp_pixel(ramp, t, x, y))


def skyline(draw, rng, base, low, high, colour, window, lit):
    x = -4
    while x < W:
        width = rng.randint(14, 34)
        height = rng.randint(low, high)
        top = base - height
        draw.rectangle([x, top, x + width, base], fill=rgb(colour))
        if rng.random() < 0.4:
            # A water tank or a mast on the roof.
            draw.rectangle([x + width // 2 - 2, top - 5, x + width // 2 + 2, top], fill=rgb(colour))
            draw.line([x + width // 2, top - 12, x + width // 2, top - 5], fill=rgb(colour))
        for wy in range(top + 4, base - 4, 7):
            for wx in range(x + 3, x + width - 3, 5):
                if rng.random() < lit:
                    draw.rectangle([wx, wy, wx + 1, wy + 3], fill=rgb(window if rng.random() < 0.75 else "pale"))
        x += width + rng.randint(0, 3)


def street(image, draw, rng, glare=None):
    """The wet road in perspective, from the far kerb down to the bottom edge, with the crossing."""
    gradient(image, HORIZON, H, ["night", "dusk", "dusk", "steel"], 1.6)
    draw.rectangle([0, HORIZON - 3, W, HORIZON - 1], fill=rgb("steel"))
    draw.line([0, HORIZON - 4, W, HORIZON - 4], fill=rgb("fog"))
    # Zebra bars narrow towards the far kerb.
    for i in range(-2, 10):
        near_x, far_x = 60 + i * 30, 112 + i * 14
        draw.polygon([(far_x, HORIZON + 4), (far_x + 8, HORIZON + 4), (near_x + 19, H), (near_x, H)], fill=rgb("pale"))
        draw.line([(far_x + 8, HORIZON + 4), (near_x + 19, H)], fill=rgb("fog"))
    # Puddles and streaks of reflected light.
    for _ in range(26):
        x, y = rng.randint(0, W), rng.randint(HORIZON + 6, H - 2)
        draw.line([x, y, x + rng.randint(4, 14), y], fill=rgb(glare or "steel"))


def reflect(image, x, colour, length=40):
    """A light's shimmer on the wet road: broken vertical dashes under it."""
    for y in range(HORIZON + 2, min(H, HORIZON + length), 1):
        if (y // 2 + x) % 3 != 0:
            wobble = (1 if (y // 3) % 2 == 0 else -1) if y > HORIZON + 10 else 0
            if 0 <= x + wobble < W:
                image.putpixel((x + wobble, y), rgb(colour))


def rain(draw, rng, count=240, colour="fog", slant=2):
    for _ in range(count):
        x, y = rng.randint(0, W + 8), rng.randint(-6, H)
        draw.line([x, y, x - slant, y + 5], fill=rgb(colour))


def traffic_light(image, draw, x, colour):
    draw.rectangle([x, HORIZON - 62, x + 1, HORIZON - 4], fill=rgb("ink"))
    draw.rectangle([x - 3, HORIZON - 78, x + 4, HORIZON - 60], fill=rgb("ink"))
    for i, name in enumerate(("red", "amber", "green")):
        lit = name == colour
        draw.rectangle([x - 1, HORIZON - 76 + i * 6, x + 2, HORIZON - 73 + i * 6], fill=rgb(name if lit else "night"))
        if lit:
            glow(image, x + 0.5, HORIZON - 74.5 + i * 6, 9, ["blood" if name == "red" else "leaf", name])
            draw.rectangle([x - 1, HORIZON - 76 + i * 6, x + 2, HORIZON - 73 + i * 6], fill=rgb("white" if name != "red" else "pink"))
            reflect(image, x, name, 34)
            reflect(image, x + 1, "blood" if name == "red" else "leaf", 26)


def street_lamp(image, draw, x):
    draw.rectangle([x, HORIZON - 54, x + 1, HORIZON - 4], fill=rgb("ink"))
    draw.line([x, HORIZON - 54, x + 8, HORIZON - 58], fill=rgb("ink"))
    glow(image, x + 9, HORIZON - 56, 16, ["brown", "orange", "amber", "yellow"])
    draw.rectangle([x + 8, HORIZON - 58, x + 10, HORIZON - 57], fill=rgb("white"))
    for dx in (8, 9, 10):
        reflect(image, x + dx, "amber" if dx == 9 else "orange", 46)


def child(draw, x, y, running=True):
    """A small child in a yellow raincoat, 9 pixels tall, feet at y."""
    draw.rectangle([x + 1, y - 9, x + 4, y - 6], fill=rgb("skin"))
    draw.point([(x + 1, y - 10), (x + 2, y - 10), (x + 3, y - 10), (x + 4, y - 10), (x, y - 9)], fill=rgb("brown"))
    draw.rectangle([x, y - 6, x + 5, y - 2], fill=rgb("yellow"))
    draw.line([x, y - 6, x, y - 2], fill=rgb("amber"))
    if running:
        draw.line([x + 5, y - 5, x + 8, y - 6], fill=rgb("yellow"))
        draw.line([x + 1, y - 2, x - 1, y], fill=rgb("deep"))
        draw.line([x + 4, y - 2, x + 6, y], fill=rgb("deep"))
    else:
        draw.line([x + 1, y - 2, x + 1, y], fill=rgb("deep"))
        draw.line([x + 4, y - 2, x + 4, y], fill=rgb("deep"))


def ball(draw, x, y):
    draw.ellipse([x, y, x + 5, y + 5], fill=rgb("red"))
    draw.point([(x + 1, y + 1), (x + 2, y + 1)], fill=rgb("pink"))
    draw.line([x + 1, y + 5, x + 4, y + 5], fill=rgb("blood"))


def umbrella(draw, x, y, colour="deep", rim="blue"):
    draw.pieslice([x - 11, y - 8, x + 11, y + 10], 180, 360, fill=rgb(colour))
    draw.arc([x - 11, y - 8, x + 11, y + 10], 180, 360, fill=rgb(rim))
    draw.line([x - 11, y + 1, x + 11, y + 1], fill=rgb("ink"))
    draw.line([x, y + 1, x, y + 14], fill=rgb("fog"))


def man(draw, x, y, pose="stand"):
    """The hero seen from behind at the kerb, 30 pixels tall, feet at y: dark coat, a satchel strap."""
    if pose == "stand":
        draw.rectangle([x + 2, y - 30, x + 8, y - 24], fill=rgb("earth"))          # hair, seen from behind
        draw.rectangle([x + 3, y - 24, x + 7, y - 22], fill=rgb("skin_dark"))      # neck
        draw.rectangle([x, y - 22, x + 10, y - 9], fill=rgb("steel"))              # coat
        draw.line([x, y - 22, x, y - 9], fill=rgb("dusk"))
        draw.line([x + 10, y - 22, x + 10, y - 9], fill=rgb("fog"))
        draw.line([x + 8, y - 22, x + 2, y - 10], fill=rgb("brown"))               # strap
        draw.rectangle([x + 1, y - 9, x + 4, y], fill=rgb("night"))
        draw.rectangle([x + 6, y - 9, x + 9, y], fill=rgb("night"))
        draw.rectangle([x + 10, y - 21, x + 12, y - 14], fill=rgb("steel"))        # the arm that holds the umbrella
    elif pose == "turn":
        draw.rectangle([x + 2, y - 30, x + 8, y - 24], fill=rgb("earth"))
        draw.rectangle([x + 6, y - 28, x + 9, y - 24], fill=rgb("skin"))           # the face, turned to the road
        draw.point((x + 8, y - 27), fill=rgb("ink"))
        draw.rectangle([x + 3, y - 24, x + 7, y - 22], fill=rgb("skin_dark"))
        draw.rectangle([x, y - 22, x + 10, y - 9], fill=rgb("steel"))
        draw.line([x, y - 22, x, y - 9], fill=rgb("dusk"))
        draw.line([x + 8, y - 22, x + 2, y - 10], fill=rgb("brown"))
        draw.rectangle([x + 1, y - 9, x + 4, y], fill=rgb("night"))
        draw.rectangle([x + 6, y - 9, x + 9, y], fill=rgb("night"))
        draw.rectangle([x + 10, y - 21, x + 12, y - 14], fill=rgb("steel"))


def lorry(image, draw, cx, base, scale):
    """The front of a lorry coming straight on: cab, windscreen, grille, bumper and two lamps."""
    w, h = int(46 * scale), int(52 * scale)
    left, top = cx - w, base - h
    draw.rectangle([left, top, cx + w, base - int(6 * scale)], fill=rgb("plum"))
    draw.rectangle([left + 2, top - int(10 * scale), cx + w - 2, top], fill=rgb("earth"))            # the box behind the cab
    draw.rectangle([left + int(5 * scale), top + int(5 * scale), cx + w - int(5 * scale), top + int(22 * scale)], fill=rgb("night"))
    draw.line([left + int(5 * scale), top + int(22 * scale), cx + w - int(5 * scale), top + int(22 * scale)], fill=rgb("ink"))
    # Wipers and the driver's shape behind the glass.
    draw.line([cx - int(18 * scale), top + int(21 * scale), cx - int(4 * scale), top + int(9 * scale)], fill=rgb("ink"))
    draw.line([cx + int(10 * scale), top + int(21 * scale), cx + int(24 * scale), top + int(9 * scale)], fill=rgb("ink"))
    draw.rectangle([cx + int(14 * scale), top + int(12 * scale), cx + int(24 * scale), top + int(21 * scale)], fill=rgb("ink"))
    for i in range(6):                                                                               # grille
        y = top + int((27 + i * 3) * scale)
        draw.line([cx - int(20 * scale), y, cx + int(20 * scale), y], fill=rgb("ink"))
    draw.rectangle([left - 2, base - int(8 * scale), cx + w + 2, base - int(2 * scale)], fill=rgb("ink"))  # bumper
    draw.rectangle([left + 2, base - int(2 * scale), left + int(12 * scale), base + int(4 * scale)], fill=rgb("ink"))
    draw.rectangle([cx + w - int(12 * scale), base - int(2 * scale), cx + w - 2, base + int(4 * scale)], fill=rgb("ink"))
    for side in (-1, 1):                                                                             # mirrors
        mx = cx + side * (w + int(4 * scale))
        draw.rectangle([mx - 2, top + int(8 * scale), mx + 2, top + int(18 * scale)], fill=rgb("ink"))
    lamps = [(cx - int(32 * scale), base - int(16 * scale)), (cx + int(32 * scale), base - int(16 * scale))]
    for lx, ly in lamps:
        glow(image, lx, ly, 34 * scale, ["brown", "orange", "amber", "yellow", "white"])
    for lx, ly in lamps:
        r = max(2, int(6 * scale))
        draw.ellipse([lx - r, ly - r, lx + r, ly + r], fill=rgb("white"))
    return lamps


def text(image, words, y, size, colour, outline=None, x=None, spacing=1):
    """Words in the game's own font, without smoothing, centred unless x is given."""
    font = ImageFont.truetype(str(FONT), size)
    widths = [font.getbbox(ch)[2] for ch in words]
    total = sum(widths) + spacing * (len(words) - 1)
    layer = Image.new("L", (total + 4, size * 2), 0)
    pen = ImageDraw.Draw(layer)
    pen.fontmode = "1"
    cursor = 2
    for ch, width in zip(words, widths):
        pen.text((cursor, 0), ch, font=font, fill=255)
        cursor += width + spacing
    left = (W - total) // 2 if x is None else x
    if outline:
        for dx in (-1, 0, 1, 2):
            for dy in (-1, 0, 1, 2):
                if dx or dy:
                    image.paste(Image.new("RGB", layer.size, rgb(outline)), (left + dx, y + dy), layer)
    image.paste(Image.new("RGB", layer.size, rgb(colour)), (left, y), layer)
    if size >= 20:
        # Big words get a second pass a pixel over, so the thin strokes of the font read as solid.
        image.paste(Image.new("RGB", layer.size, rgb(colour)), (left + 1, y), layer)


def night_city(seed, lit=0.3):
    rng = random.Random(seed)
    image, draw = canvas("ink")
    gradient(image, 0, HORIZON, ["ink", "ink", "night", "purple"], 2.2)
    skyline(draw, random.Random(11), HORIZON - 4, 44, 92, "night", "amber", lit * 0.7)
    skyline(draw, random.Random(12), HORIZON - 4, 20, 56, "ink", "yellow", lit)
    # Shop fronts along the far pavement: lit glass and awnings.
    shops = random.Random(13)
    x = 0
    while x < W:
        width = shops.randint(26, 44)
        glass = shops.choice(["amber", "cyan", "pink", "cream"])
        draw.rectangle([x + 2, HORIZON - 20, x + width - 2, HORIZON - 5], fill=rgb("earth"))
        draw.rectangle([x + 4, HORIZON - 16, x + width - 4, HORIZON - 6], fill=rgb(glass))
        draw.rectangle([x + 2, HORIZON - 21, x + width - 2, HORIZON - 18], fill=rgb(shops.choice(["blood", "deep", "forest"])))
        for sx in range(x + 5, x + width - 5, 3):
            reflect(image, sx, glass, 14 + shops.randint(0, 10))
        x += width
    street(image, draw, rng)
    return image, draw, rng


def slide_1():
    image, draw, rng = night_city(1)
    street_lamp(image, draw, 246)
    traffic_light(image, draw, 86, "red")
    man(draw, 150, H - 6)
    umbrella(draw, 161, H - 50)
    rain(draw, rng)
    return image


def slide_2():
    image, draw, rng = night_city(2)
    street_lamp(image, draw, 246)
    traffic_light(image, draw, 86, "red")
    ball(draw, 150, HORIZON + 22)
    draw.line([137, HORIZON + 28, 147, HORIZON + 28], fill=rgb("steel"))        # the ball's wet track
    child(draw, 118, HORIZON + 24)
    man(draw, 196, H - 6, "turn")
    umbrella(draw, 207, H - 50)
    rain(draw, rng)
    return image


def slide_3():
    rng = random.Random(3)
    image, draw = canvas("ink")
    gradient(image, 0, 96, ["ink", "ink", "night"], 2.0)
    skyline(draw, random.Random(31), 92, 30, 70, "night", "amber", 0.12)
    gradient(image, 92, H, ["night", "dusk", "steel"], 1.4)
    for i in range(-3, 9):                                                       # the crossing, seen along the road
        x = 40 + i * 34
        draw.polygon([(x, 150), (x + 22, 150), (x + 26, 166), (x - 4, 166)], fill=rgb("pale"))
    lamps = lorry(image, draw, 160, 132, 1.35)
    for lx, ly in lamps:                                                         # the beams lie on the wet road
        for y in range(ly + 10, H):
            spread = (y - ly) * 0.9
            for x in range(int(lx - spread), int(lx + spread)):
                if 0 <= x < W and (x + y) % 2 == 0 and BAYER[y % 4][x % 4] < 9 - (y - ly) / 12:
                    image.putpixel((x, y), rgb("yellow" if abs(x - lx) < spread * 0.4 else "amber"))
    ball(draw, 180, 160)
    child(draw, 154, 166, running=False)
    draw.rectangle([153, 167, 160, 168], fill=rgb("ink"))                        # the child's shadow, thrown back
    rain(draw, rng, 220, "pale", 3)
    return image


def slide_4():
    rng = random.Random(4)
    image, draw = canvas("white")
    gradient(image, 0, H, ["yellow", "white", "white", "yellow", "amber"], 1.0)
    glow(image, 96, 70, 78, ["yellow", "white", "white"])
    glow(image, 236, 70, 78, ["yellow", "white", "white"])
    # The lorry is only a dark shape behind its own light.
    draw.rectangle([60, 0, 270, 30], fill=rgb("plum"))
    for x in range(60, 271):
        for y in range(30, 44):
            if BAYER[y % 4][x % 4] > (y - 30) * 1.2:
                image.putpixel((x, y), rgb("plum"))
    draw.rectangle([0, H - 26, W, H], fill=rgb("steel"))
    for i in range(-2, 9):
        x = 20 + i * 44
        draw.polygon([(x, H - 22), (x + 28, H - 22), (x + 34, H), (x - 6, H)], fill=rgb("white"))
    # The man in mid-dive, arms out; black against the glare.
    body = [(132, 96), (176, 84), (182, 92), (150, 108), (118, 122), (104, 118)]
    draw.polygon(body, fill=rgb("ink"))
    draw.ellipse([176, 76, 190, 90], fill=rgb("ink"))                            # head
    draw.line([182, 92, 214, 96], fill=rgb("ink"), width=4)                      # the pushing arms
    draw.line([178, 96, 210, 104], fill=rgb("ink"), width=4)
    draw.line([112, 120, 84, 132], fill=rgb("ink"), width=5)                     # legs trailing
    draw.line([120, 114, 92, 116], fill=rgb("ink"), width=5)
    draw.line([150, 100, 168, 88], fill=rgb("brown"))                            # the strap, the only colour on him
    # The child, thrown clear, and the umbrella let go.
    draw.rectangle([226, 108, 238, 118], fill=rgb("amber"))
    draw.rectangle([230, 100, 237, 107], fill=rgb("skin"))
    draw.line([226, 118, 220, 126], fill=rgb("deep"), width=2)
    draw.line([236, 118, 242, 126], fill=rgb("deep"), width=2)
    draw.line([238, 110, 248, 104], fill=rgb("amber"), width=2)
    draw.pieslice([40, 40, 76, 76], 120, 300, fill=rgb("deep"))
    draw.line([58, 58, 74, 46], fill=rgb("ink"))
    ball(draw, 262, 136)
    rain(draw, rng, 90, "amber", 4)
    return image


def slide_5():
    rng = random.Random(5)
    image, draw = canvas("white")
    # White that is not empty: the last things, small and far apart, and the rain stopped in the air.
    for _ in range(70):
        x, y = rng.randint(0, W - 1), rng.randint(0, H - 1)
        draw.line([x, y, x, y + rng.randint(1, 3)], fill=rgb("pale"))
    draw.pieslice([214, 96, 250, 132], 200, 20, fill=rgb("pale"))
    draw.line([232, 114, 244, 134], fill=rgb("fog"))
    draw.ellipse([92, 118, 100, 126], fill=rgb("pink"))
    draw.point([(94, 120), (95, 120)], fill=rgb("white"))
    draw.line([86, 128, 106, 128], fill=rgb("pale"))
    return image


def glyph(draw, rng, x, y, colour):
    """One sign of the Aurens' script: strokes hung from a top bar, with a dot."""
    draw.line([x, y, x + 6, y], fill=rgb(colour))
    for _ in range(rng.randint(2, 3)):
        sx = x + rng.choice([0, 2, 3, 4, 6])
        ey = y + rng.choice([4, 6, 8])
        draw.line([sx, y, sx, ey], fill=rgb(colour))
        if rng.random() < 0.6:
            draw.line([sx, ey, sx + rng.choice([-3, -2, 2, 3]), ey + rng.choice([0, 2])], fill=rgb(colour))
    if rng.random() < 0.5:
        draw.point((x + rng.randint(1, 5), y + 10), fill=rgb(colour))


def lattice_ring(image, draw, cx, cy, radius, colour="cyan", dim="deep"):
    """The Lattice as a wheel: rings, spokes and knots of light."""
    for r, c in ((radius, colour), (radius * 0.72, dim), (radius * 0.4, colour)):
        steps = int(r * 8)
        for i in range(steps):
            a = i / steps * math.tau
            x, y = int(cx + math.cos(a) * r), int(cy + math.sin(a) * r * 0.5)
            if 0 <= x < W and 0 <= y < H and (i // 3) % 4 != 3:
                image.putpixel((x, y), rgb(c))
    for i in range(12):
        a = i / 12 * math.tau
        x1, y1 = cx + math.cos(a) * radius * 0.4, cy + math.sin(a) * radius * 0.2
        x2, y2 = cx + math.cos(a) * radius, cy + math.sin(a) * radius * 0.5
        draw.line([x1, y1, x2, y2], fill=rgb(dim))
        draw.rectangle([x2 - 1, y2 - 1, x2 + 1, y2 + 1], fill=rgb(colour))
        draw.point((x2, y2), fill=rgb("white"))


def slide_6():
    rng = random.Random(6)
    image, draw = canvas("ink")
    glow(image, 160, 64, 150, ["ink", "night", "deep"], 0.5)
    lattice_ring(image, draw, 160, 60, 120)
    lattice_ring(image, draw, 160, 60, 46, "cyan", "deep")
    # One small light at the middle of the wheel: the caught soul.
    glow(image, 160, 60, 14, ["deep", "blue", "cyan", "white"])
    for i in range(18):
        glyph(draw, rng, 53 + i * 12, 112, "blue" if i % 5 else "cyan")
    text(image, "Unfinished.", 130, 13, "pale")
    text(image, "Bound.", 150, 13, "cyan")
    return image


def cloud(draw, x, y, w, light="white", shade="pale", under="fog"):
    draw.ellipse([x, y + 4, x + w, y + 14], fill=rgb(shade))
    for i in range(3):
        bx = x + w * (0.12 + i * 0.26)
        bw = w * (0.3 + (i % 2) * 0.08)
        draw.ellipse([bx, y - (i % 2) * 3, bx + bw, y + 11], fill=rgb(shade))
        draw.ellipse([bx + 1, y - (i % 2) * 3, bx + bw - 3, y + 7], fill=rgb(light))
    draw.line([x + 5, y + 14, x + w - 5, y + 14], fill=rgb(under))


def slide_7():
    rng = random.Random(7)
    image, draw = canvas("deep")
    gradient(image, 0, 118, ["deep", "deep", "blue", "cyan", "pale"], 1.25)
    # The Lattice, faint, high in the sky: only where the fall began.
    for i in range(-4, 12):
        for t in range(0, 60):
            for x, y in ((i * 34 + t, t), (i * 34 + 60 - t, t)):
                if 0 <= x < W and (t // 2) % 3 != 2 and BAYER[y % 4][x % 4] < 14 - t / 5:
                    image.putpixel((x, y), rgb("cyan"))
    # The valley far below: ridges, forest, fields, the lake, the river, a village.
    draw.polygon([(0, 118), (40, 100), (76, 112), (120, 96), (170, 110), (214, 94), (262, 108), (320, 98), (320, 124), (0, 124)], fill=rgb("steel"))
    draw.polygon([(120, 96), (132, 103), (112, 104)], fill=rgb("pale"))
    draw.polygon([(214, 94), (226, 102), (204, 102)], fill=rgb("pale"))
    gradient(image, 120, H, ["forest", "leaf", "leaf", "green"], 1.0)
    for _ in range(150):
        x, y = rng.randint(0, W), rng.randint(122, H)
        draw.ellipse([x, y, x + rng.randint(4, 9), y + rng.randint(2, 4)], fill=rgb(rng.choice(["forest", "forest", "leaf", "teal"])))
    for fx, fy, fw in ((32, 150, 30), (70, 162, 36), (250, 156, 34)):               # fields
        draw.polygon([(fx, fy), (fx + fw, fy - 3), (fx + fw + 6, fy + 9), (fx + 4, fy + 12)], fill=rgb("amber"))
        for i in range(3):
            draw.line([fx + 4 + i * 9, fy + 1, fx + 8 + i * 9, fy + 10], fill=rgb("orange"))
    draw.ellipse([176, 132, 232, 148], fill=rgb("deep"))                            # Mirror Lake
    draw.ellipse([180, 133, 228, 144], fill=rgb("blue"))
    draw.line([192, 137, 214, 137], fill=rgb("cyan"))
    draw.line([198, 140, 208, 140], fill=rgb("white"))
    points = [(204, 147), (198, 156), (206, 164), (196, 172), (200, 180)]           # the river
    draw.line(points, fill=rgb("blue"), width=2)
    for hx, hy in ((120, 150), (128, 154), (114, 156), (124, 160), (134, 148)):     # Hearthmere
        draw.rectangle([hx, hy, hx + 4, hy + 3], fill=rgb("cream"))
        draw.polygon([(hx - 1, hy), (hx + 2, hy - 3), (hx + 5, hy)], fill=rgb("blood"))
    draw.line([128, 146, 128, 140], fill=rgb("pale"))                               # chimney smoke
    draw.point([(129, 138), (130, 136)], fill=rgb("pale"))
    cloud(draw, -18, 84, 90)
    cloud(draw, 236, 70, 110)
    cloud(draw, 96, 104, 64, "pale", "fog", "steel")
    cloud(draw, 20, 30, 70, "pale", "fog", "steel")
    # The falling man, small, with a trail of light above him.
    for t in range(40):
        x, y = 166 - t // 5, 76 - t
        if (t // 2) % 2 == 0:
            image.putpixel((x, y), rgb("white" if t < 14 else "cyan"))
    draw.polygon([(160, 78), (170, 76), (174, 84), (164, 88)], fill=rgb("steel"))
    draw.ellipse([170, 84, 176, 90], fill=rgb("skin_dark"))
    draw.rectangle([170, 84, 175, 86], fill=rgb("earth"))
    draw.line([161, 78, 152, 72], fill=rgb("night"), width=2)
    draw.line([165, 77, 160, 68], fill=rgb("night"), width=2)
    draw.line([172, 80, 182, 76], fill=rgb("steel"), width=2)
    draw.line([166, 88, 160, 96], fill=rgb("steel"), width=2)
    text(image, "FORTY CROWNS", 8, 26, "cream", "ink", spacing=2)
    return image


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    slides = [slide_1(), slide_2(), slide_3(), slide_4(), slide_5(), slide_6(), slide_7()]
    for index, image in enumerate(slides, 1):
        image.save(OUT / f"intro_{index}.png")
    sheet = Image.new("RGB", ((W * 2 + 10) * 2 + 10, (H * 2 + 10) * 4 + 10), (40, 44, 60))
    for i, image in enumerate(slides):
        big = image.resize((W * 2, H * 2), Image.NEAREST)
        sheet.paste(big, (10 + (i % 2) * (W * 2 + 10), 10 + (i // 2) * (H * 2 + 10)))
    target = ROOT / "export" / "previews" / "intro.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(target)
    print(f"{len(slides)} slides in {OUT}, sheet {target}")


if __name__ == "__main__":
    main()
