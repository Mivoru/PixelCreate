"""Generate tileable pixel textures, the merged game palette and the UI bitmap font.

Usage: python asset-pipeline/generate_textures.py
Writes PNG files to client/assets/textures, a font to client/assets/ui and client/src/PaletteData.cs.
"""
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
TEXTURES = ROOT / "client" / "assets" / "textures"
UI = ROOT / "client" / "assets" / "ui"
SIZE = 256


def rgb(value):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def tile_noise(rng, cells):
    """Smooth value noise that repeats across the texture edges."""
    grid = rng.random((cells, cells))
    axis = np.arange(SIZE) / SIZE * cells
    low = axis.astype(int)
    fraction = axis - low
    fade = fraction * fraction * (3 - 2 * fraction)
    high = (low + 1) % cells
    rows = grid[low][:, low] * (1 - fade)[None, :] + grid[low][:, high] * fade[None, :]
    rows_next = grid[high][:, low] * (1 - fade)[None, :] + grid[high][:, high] * fade[None, :]
    return rows * (1 - fade)[:, None] + rows_next * fade[:, None]


def layered(rng, *cells):
    total = sum(tile_noise(rng, c) / (i + 1) for i, c in enumerate(cells))
    total -= total.min()
    return total / total.max()


def ramp(values, colors):
    """Blend smoothly through a list of colours."""
    stops = np.linspace(0, 1, len(colors))
    table = np.array([rgb(c) for c in colors], dtype=float)
    channels = [np.interp(values, stops, table[:, i]) for i in range(3)]
    return np.stack(channels, axis=-1).astype(np.uint8)


def save(name, pixels):
    TEXTURES.mkdir(parents=True, exist_ok=True)
    Image.fromarray(pixels, "RGB").save(TEXTURES / f"{name}.png")


def speckle(rng, pixels, color, chance):
    mask = rng.random((SIZE, SIZE)) < chance
    pixels[mask] = rgb(color)


def grass(rng, name, colors):
    values = layered(rng, 4, 8, 16, 32, 64, 128)
    pixels = ramp(values, colors)
    blades(rng, pixels, colors[0], colors[-1])
    save(name, pixels)


def dirt(rng):
    pixels = ramp(layered(rng, 4, 16, 32, 64, 128), ["5A3A30", "8A5A44", "A9765A", "C0926E"])
    pebbles(rng, pixels, 240, ["6E5A4C", "8F7A66", "B8A58A"])
    save("dirt", pixels)


def rock(rng):
    values = layered(rng, 4, 8, 16, 32, 64)
    cracks = np.abs(layered(rng, 8, 16) - 0.5) < 0.012
    pixels = ramp(values, ["4A4C58", "666A78", "85899A", "A8ABB8"])
    pixels[cracks] = rgb("3A3C48")
    save("rock", pixels)


def blades(rng, pixels, dark, light):
    """Short diagonal strokes that read as grass from a distance."""
    dark_rgb, light_rgb = np.array(rgb(dark)), np.array(rgb(light))
    for _ in range(5200):
        x, y = rng.integers(0, SIZE, 2)
        tone = dark_rgb if rng.random() < 0.55 else light_rgb
        lean = rng.integers(-1, 2)
        for step in range(rng.integers(3, 8)):
            px, py = (x + step * lean // 2) % SIZE, (y - step) % SIZE
            pixels[py, px] = (pixels[py, px] * 0.45 + tone * 0.55).astype(np.uint8)


def pebbles(rng, pixels, count, colors):
    for _ in range(count):
        x, y = rng.integers(0, SIZE, 2)
        radius = rng.integers(1, 4)
        tone = np.array(rgb(colors[rng.integers(0, len(colors))]))
        for dy in range(-radius, radius + 1):
            for dx in range(-radius, radius + 1):
                if dx * dx + dy * dy <= radius * radius:
                    pixels[(y + dy) % SIZE, (x + dx) % SIZE] = tone


def detail(rng, name, values):
    """A light grey texture that multiplies a material colour."""
    grey = (170 + values * 85).astype(np.uint8)
    save(name, np.stack([grey, grey, grey], axis=-1))


def planks(rng):
    """Boards with long grain, a knot here and there, and dark gaps between them."""
    grain = layered(rng, 4, 8, 16)
    lines = np.sin((np.arange(SIZE)[:, None] * 0.9 + layered(rng, 2, 4) * 40) * 1.3) * 0.5 + 0.5
    values = 0.62 + grain * 0.22 + lines * 0.1
    for board in range(0, SIZE, 32):
        values[board:board + 32, :] *= rng.uniform(0.88, 1.06)
        for _ in range(2):
            kx, ky = int(rng.integers(0, SIZE)), board + int(rng.integers(8, 24))
            for dy in range(-3, 4):
                for dx in range(-5, 6):
                    if dx * dx / 25 + dy * dy / 9 <= 1:
                        values[(ky + dy) % SIZE, (kx + dx) % SIZE] *= 0.72
        # Butt joints, staggered from board to board.
        joint = int(rng.integers(0, SIZE))
        values[board:board + 32, joint:joint + 2] = 0.3
    values[::32, :] = 0.22
    values[1::32, :] = 0.4
    grey = (np.clip(values, 0, 1) * 255).astype(np.uint8)
    save("planks", np.stack([grey, grey, grey], axis=-1))


def shingles(rng):
    values = layered(rng, 8, 32) * 0.6 + 0.4
    for row in range(0, SIZE, 32):
        values[row:row + 2, :] = 0.0
        offset = 32 if (row // 32) % 2 else 0
        values[row:row + 32, offset::64] = 0.1
    detail(rng, "shingles", values)


def masonry(rng):
    """Dressed stone laid in courses: each block has its own tone, a lit top-left edge, a shaded
    lower edge and a recessed mortar joint, so walls read as built rather than as noise."""
    courses = [44, 40, 46, 38, 48, 40]
    grain = layered(rng, 16, 64, 128)
    values = np.full((SIZE, SIZE), 0.32)
    top = 0
    for height in courses:
        x = int(rng.integers(0, 40))
        start = x
        while x < start + SIZE:
            width = int(rng.integers(44, 96))
            if start + SIZE - (x + width) < 40:
                width = start + SIZE - x
            tone = rng.uniform(0.62, 0.92)
            for row in range(top + 3, top + height - 1):
                for column in range(x + 3, x + width - 1):
                    u, v = column % SIZE, row
                    edge = 0.0
                    if row < top + 6 or column < x + 6:
                        edge = 0.1
                    elif row > top + height - 5 or column > x + width - 5:
                        edge = -0.12
                    values[v, u] = tone + edge + (grain[v, u] - 0.5) * 0.3
            # A few chips and pits on the face.
            for _ in range(int(width * height / 700)):
                cx, cy = int(rng.integers(x + 6, x + width - 6)) % SIZE, int(rng.integers(top + 6, top + height - 5))
                values[cy, cx] -= 0.16
            x += width
        top += height
    grey = (np.clip(values, 0, 1) * 255).astype(np.uint8)
    save("stone", np.stack([grey, grey, grey], axis=-1))


def plaster(rng):
    """Lime plaster: smooth, with faint trowel sweeps and a little grime."""
    values = layered(rng, 4, 16, 64) * 0.25 + 0.72
    sweeps = layered(rng, 2, 8)
    values += np.sin(sweeps * 9) * 0.012
    for _ in range(900):
        x, y = rng.integers(0, SIZE, 2)
        values[y, x] -= rng.uniform(0.04, 0.1)
    grey = (np.clip(values, 0, 1) * 255).astype(np.uint8)
    save("plaster", np.stack([grey, grey, grey], axis=-1))


def thatch(rng):
    """Bundles of straw laid in overlapping courses, the stems running down the slope."""
    values = np.full((SIZE, SIZE), 0.7)
    for _ in range(5200):
        x, y = rng.integers(0, SIZE, 2)
        length = int(rng.integers(10, 26))
        tone = rng.uniform(0.55, 1.0)
        for step in range(length):
            values[(y + step) % SIZE, x] = values[(y + step) % SIZE, x] * 0.35 + tone * 0.65
    for row in range(0, SIZE, 32):
        values[row:row + 3, :] *= 0.62
        values[row + 3:row + 5, :] *= 0.85
    grey = (np.clip(values, 0, 1) * 255).astype(np.uint8)
    save("thatch", np.stack([grey, grey, grey], axis=-1))


def glass(rng):
    """Window glass by day: dark, with a soft sky reflection and a couple of bright diagonal glints."""
    yy, xx = np.mgrid[0:SIZE, 0:SIZE]
    values = 0.62 + 0.18 * (1 - yy / SIZE)
    diagonal = (xx + yy) % SIZE
    values[(diagonal > 40) & (diagonal < 52)] += 0.3
    values[(diagonal > 60) & (diagonal < 66)] += 0.22
    values[(diagonal > 170) & (diagonal < 178)] += 0.15
    grey = (np.clip(values, 0, 1) * 255).astype(np.uint8)
    save("glass", np.stack([grey, grey, grey], axis=-1))


def merged_palette():
    colours = []
    for name in ("endesga-32", "resurrect-64"):
        for line in (ROOT / "palettes" / f"{name}.gpl").read_text().splitlines():
            match = re.match(r"\s*(\d+)\s+(\d+)\s+(\d+)", line)
            if match:
                value = "%02x%02x%02x" % tuple(int(v) for v in match.groups())
                if value not in colours:
                    colours.append(value)
    body = ",\n".join(f'        "{c}"' for c in colours)
    (ROOT / "client" / "src" / "PaletteData.cs").write_text(
        "namespace PixelCreate;\n\n"
        "/// <summary>Generated by asset-pipeline/generate_textures.py: Endesga 32 and Resurrect 64.</summary>\n"
        "public static class PaletteData\n{\n    public static readonly string[] Colors =\n    {\n"
        f"{body},\n    }};\n}}\n")
    return len(colours)


def bitmap_font():
    """Writes Pillow's built-in bitmap font as a BMFont file, so the UI text stays pixel sharp."""
    UI.mkdir(parents=True, exist_ok=True)
    font = ImageFont.load_default_imagefont()
    glyphs = [chr(c) for c in range(32, 127)]
    cell_w, cell_h, columns = 8, 12, 16
    sheet = Image.new("RGBA", (cell_w * columns, cell_h * 6), (0, 0, 0, 0))
    draw = ImageDraw.Draw(sheet)
    lines = []
    for i, glyph in enumerate(glyphs):
        x, y = (i % columns) * cell_w, (i // columns) * cell_h
        draw.text((x, y), glyph, font=font, fill=(255, 255, 255, 255))
        width = max(int(draw.textlength(glyph, font=font)), 3)
        lines.append(f"char id={ord(glyph)} x={x} y={y} width={cell_w} height={cell_h} "
                     f"xoffset=0 yoffset=0 xadvance={width} page=0 chnl=15")
    sheet.save(UI / "pixel_font.png")
    header = [
        'info face="Pixel" size=11 bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0',
        f"common lineHeight={cell_h} base=10 scaleW={sheet.width} scaleH={sheet.height} pages=1 packed=0",
        'page id=0 file="pixel_font.png"',
        f"chars count={len(glyphs)}",
    ]
    (UI / "pixel_font.fnt").write_text("\n".join(header + lines) + "\n")


def main():
    rng = np.random.default_rng(7)
    grass(rng, "grass", ["2E5F3C", "3E7F44", "4C9149", "5EA650", "7AB95C"])
    grass(rng, "grass_dry", ["4F8444", "6E9A4C", "8FAE58", "B0BE6C"])
    dirt(rng)
    rock(rng)
    detail(rng, "leaves", layered(rng, 16, 32, 64, 128))
    plaster(rng)
    masonry(rng)
    thatch(rng)
    glass(rng)
    planks(rng)
    shingles(rng)
    # Natural stone keeps the soft weathered noise; built stone uses the coursed masonry above.
    detail(rng, "rockface", layered(rng, 8, 16, 64))
    print(f"palette colours: {merged_palette()}")
    bitmap_font()


main()
