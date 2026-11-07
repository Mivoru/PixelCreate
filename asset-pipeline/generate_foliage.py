"""Paint the leaf atlas used on tree and bush cards.

Usage: python asset-pipeline/generate_foliage.py
Writes client/assets/textures/foliage.png: four cells of cut-out leaves in shades of grey, which the
foliage shader tints. Cells, left to right and top to bottom: broad, small, needles, hanging strands.
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "client" / "assets" / "textures" / "foliage.png"
SIZE = 1024
CELL = SIZE // 2


def leaf(draw, x, y, length, width, angle, shade):
    """A pointed leaf with a lighter half, which gives the mass of leaves a drawn look."""
    tip = (x + math.cos(angle) * length, y + math.sin(angle) * length)
    side = (math.cos(angle + math.pi / 2) * width, math.sin(angle + math.pi / 2) * width)
    middle = (x + math.cos(angle) * length * 0.45, y + math.sin(angle) * length * 0.45)
    left = (middle[0] + side[0], middle[1] + side[1])
    right = (middle[0] - side[0], middle[1] - side[1])
    draw.polygon([(x, y), left, tip, right], fill=(shade, shade, shade, 255))
    light = min(255, shade + 34)
    draw.polygon([(x, y), left, tip], fill=(light, light, light, 255))


def in_blob(rng, cx, cy, radius):
    angle = rng.uniform(0, math.tau)
    reach = radius * math.sqrt(rng.random())
    return cx + math.cos(angle) * reach, cy + math.sin(angle) * reach, reach / radius


def broad(draw, ox, oy, rng):
    centre = CELL / 2
    for index in range(620):
        x, y, edge = in_blob(rng, ox + centre, oy + centre, centre * 0.9)
        shade = int(120 + (index / 620) * 110 - edge * 30 + rng.uniform(-16, 16))
        leaf(draw, x, y, rng.uniform(26, 44), rng.uniform(9, 15), rng.uniform(0, math.tau), shade)


def small(draw, ox, oy, rng):
    centre = CELL / 2
    for index in range(1300):
        x, y, edge = in_blob(rng, ox + centre, oy + centre, centre * 0.92)
        shade = int(130 + (index / 1300) * 110 - edge * 26 + rng.uniform(-14, 14))
        leaf(draw, x, y, rng.uniform(13, 23), rng.uniform(5, 9), rng.uniform(0, math.tau), shade)


def needles(draw, ox, oy, rng):
    """A bough seen from above: a stem from the top down with needles sweeping outwards."""
    centre = ox + CELL / 2
    for bough in range(9):
        y0 = oy + 10 + bough * 54
        width = 40 + bough * 24
        for index in range(46):
            t = index / 46
            x = centre + rng.uniform(-8, 8)
            y = y0 + t * 60
            for side in (-1, 1):
                length = width * (1 - t * 0.5) * rng.uniform(0.7, 1)
                shade = int(110 + rng.uniform(0, 110))
                draw.line([(x, y), (x + side * length, y + length * 0.55)], fill=(shade, shade, shade, 255), width=5)


def strands(draw, ox, oy, rng):
    """Long hanging twigs with leaves along them, for willows."""
    for strand in range(26):
        x = ox + 20 + strand * (CELL - 40) / 26 + rng.uniform(-6, 6)
        y = oy + rng.uniform(0, 40)
        end = oy + rng.uniform(CELL * 0.7, CELL)
        sway = rng.uniform(-14, 14)
        while y < end:
            t = (y - oy) / CELL
            shade = int(120 + rng.uniform(0, 110))
            leaf(draw, x + sway * t, y, rng.uniform(24, 36), rng.uniform(5, 8), math.pi / 2 + rng.uniform(-0.5, 0.5), shade)
            y += rng.uniform(7, 13)


def main():
    rng = random.Random(9)
    image = Image.new("RGBA", (SIZE, SIZE), (128, 128, 128, 0))
    draw = ImageDraw.Draw(image)
    broad(draw, 0, 0, rng)
    small(draw, CELL, 0, rng)
    needles(draw, 0, CELL, rng)
    strands(draw, CELL, CELL, rng)
    image.save(OUT)


if __name__ == "__main__":
    main()
