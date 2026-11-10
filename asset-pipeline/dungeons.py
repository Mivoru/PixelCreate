"""Dungeon pieces for build_meshes.py, put together in the zone like building blocks.

The Sunken Archive: an Auren library under Silver Falls, its white stone gone grey and wet, azure runes
still glowing in the walls, shelves of rotting books and a channel of black water down the middle of
the hall. Floor and ceiling are non-solid (Zone.NonSolid; the floor is a platform); walls, shelves and
pillars are solid and their collision is measured from the model like any other prop.
Blender's +Y is the back of a piece, -Y its front; up is z. Each builder uses the helpers of
build_meshes.py, passed in as h.
"""
import math
import random

COLORS = {
    "auren": "B8B8C4", "auren_dark": "8A8C9A", "auren_wet": "6E7484", "deep_water": "1E3A52", "page": "D8CCA8",
    "book_green": "3E5A3A", "book_blue": "2C3C66", "rot": "4A4434",
}

FLOOR_W, FLOOR_D = 50.0, 36.0
HALL_H = 6.5


def books_row(h, x0, x1, y, z, rng, name="book"):
    """A row of book spines of different heights and colours, a few leaning or missing."""
    x = x0
    while x < x1:
        width = rng.uniform(0.08, 0.18)
        height = rng.uniform(0.38, 0.6)
        if rng.random() > 0.15:
            color = rng.choice(("cloth_red", "book_green", "book_blue", "rot", "wood_dark", "page", "book_blue"))
            h.box(name, (x + width / 2, y, z + height / 2), (width * 0.9, 0.42, height), color)
        x += width


def archive_floor(h):
    """The hall's floor: worn slabs in a grid with dark joints, a sunken channel of black water down the
    middle with a kerb either side, puddles, loose pages and rubble."""
    rng = random.Random(501)
    h.box("base", (0, 0, -0.25), (FLOOR_W, FLOOR_D, 0.5), "auren_dark")
    for i in range(int(FLOOR_W / 2)):
        for j in range(int(FLOOR_D / 2)):
            x = -FLOOR_W / 2 + 1 + i * 2
            y = -FLOOR_D / 2 + 1 + j * 2
            if abs(y) < 2.4:
                continue
            tone = rng.choice(("auren", "auren", "auren_dark", "auren_wet"))
            h.box("slab", (x, y, 0.0), (1.92, 1.92, 0.04), tone)
    # The channel: dark water a hand below the floor, a kerb of dressed stone on each side.
    h.box("water", (0, 0, 0.004), (FLOOR_W - 2, 3.6, 0.01), "deep_water")
    for side in (-1, 1):
        h.box("kerb", (0, side * 2.0, 0.05), (FLOOR_W - 2, 0.4, 0.14), "auren_dark")
    for i in range(14):
        x, y = rng.uniform(-23, 23), rng.choice((-1, 1)) * rng.uniform(3, 16)
        h.box("puddle", (x, y, 0.025), (rng.uniform(0.8, 2.2), rng.uniform(0.6, 1.6), 0.01), "deep_water")
    for i in range(30):
        x, y = rng.uniform(-23, 23), rng.choice((-1, 1)) * rng.uniform(2.6, 17)
        h.box("page", (x, y, 0.035), (0.3, 0.22, 0.01), "page")
    for i in range(10):
        x, y = rng.uniform(-23, 23), rng.choice((-1, 1)) * rng.uniform(3, 17)
        h.blob("rubble", (x, y, 0.05), rng.uniform(0.15, 0.35), "auren_wet", 520 + i, 0.6, 1)


def archive_ceiling(h):
    """A dark vault overhead: a slab carried on heavy ribs, with roots hanging through the cracks."""
    rng = random.Random(502)
    h.box("vault", (0, 0, HALL_H + 0.1), (FLOOR_W + 4, FLOOR_D + 4, 1.0), "auren_wet")
    for i in range(9):
        x = -FLOOR_W / 2 + 3 + i * (FLOOR_W - 6) / 8
        h.box("rib", (x, 0, HALL_H - 0.2), (0.7, FLOOR_D, 0.5), "auren_dark")
    for j in range(3):
        h.box("rib_long", (0, -12 + j * 12, HALL_H - 0.15), (FLOOR_W, 0.6, 0.4), "auren_dark")
    for i in range(16):
        x, y = rng.uniform(-22, 22), rng.uniform(-15, 15)
        length = rng.uniform(0.8, 2.4)
        h.timber("root", (x, y, HALL_H), (x + rng.uniform(-0.3, 0.3), y + rng.uniform(-0.3, 0.3), HALL_H - length), 0.06, "bark_dark")


def archive_wall(h):
    """A six metre stretch of the hall's wall: grey stone courses on a plinth, a niche of shelves with
    books, pilasters at the ends, a band of glowing azure runes high up and moss at its foot."""
    rng = random.Random(503)
    length, thick = 6.0, 0.8
    h.box("plinth", (0, 0, 0.3), (length, thick + 0.2, 0.6), "auren_dark")
    h.box("wall", (0, 0, HALL_H / 2), (length, thick, HALL_H), "auren")
    for k in range(1, 7):
        h.box("course", (0, -thick / 2 - 0.01, k * 0.92), (length, 0.03, 0.06), "auren_dark")
    for x in (-length / 2 + 0.3, length / 2 - 0.3):
        h.box("pilaster", (x, -thick / 2 - 0.15, HALL_H / 2), (0.6, 0.3, HALL_H), "auren_dark")
        h.box("capital", (x, -thick / 2 - 0.2, HALL_H - 0.4), (0.8, 0.4, 0.3), "auren")
    # The niche: a dark recess with three shelves of books.
    h.box("niche", (0, -thick / 2 + 0.02, 2.1), (3.2, 0.06, 2.8), "cave_dark")
    for k, z in enumerate((0.95, 1.85, 2.75)):
        h.box("shelf", (0, -thick / 2 - 0.2, z), (3.3, 0.45, 0.07), "wood_dark")
        books_row(h, -1.55, 1.55, -thick / 2 - 0.2, z + 0.04, rng)
    h.box("niche_head", (0, -thick / 2 - 0.12, 3.65), (3.6, 0.3, 0.3), "auren_dark")
    # Runes: a thin band of azure glyphs that still glows.
    h.box("rune_band", (0, -thick / 2 - 0.03, 4.6), (length - 1.4, 0.04, 0.16), "rune")
    for i in range(7):
        h.box("glyph", (-2.1 + i * 0.7, -thick / 2 - 0.04, 4.85), (0.18, 0.04, 0.26), "rune")
    for i in range(4):
        h.blob("moss", (rng.uniform(-2.6, 2.6), -thick / 2 - 0.1, 0.1), rng.uniform(0.2, 0.4), "moss", 530 + i, 0.4, 1)


def archive_shelf(h):
    """A free-standing bookcase three metres long: dark wood, four shelves full of mouldering books."""
    rng = random.Random(504)
    w, d, hgt = 3.0, 0.7, 2.8
    for x in (-w / 2, w / 2):
        h.box("side", (x, 0, hgt / 2), (0.1, d, hgt), "wood_dark")
    h.box("back", (0, 0, hgt / 2), (w, 0.06, hgt), "wood_dark")
    h.box("top", (0, 0, hgt), (w + 0.2, d + 0.1, 0.1), "wood_dark")
    for k, z in enumerate((0.12, 0.8, 1.48, 2.16)):
        h.box("board", (0, 0, z), (w, d, 0.06), "wood")
        for side in (-1, 1):
            books_row(h, -w / 2 + 0.1, w / 2 - 0.1, side * 0.18, z + 0.03, rng)


def archive_shelf_fallen(h):
    """A bookcase that toppled long ago: lying on its back, its books spilled in a heap before it."""
    rng = random.Random(505)
    w, d, hgt = 3.0, 0.7, 2.8
    h.box("back", (0, 0, 0.06), (w, hgt, 0.08), "wood_dark")
    for x in (-w / 2, w / 2):
        h.box("side", (x, 0, d / 2), (0.1, hgt, d), "wood_dark")
    for y in (-0.7, 0.0, 0.7):
        h.box("board", (0, y, d / 2), (w, 0.06, d), "wood")
    for i in range(26):
        x, y = rng.uniform(-1.6, 1.6), rng.uniform(-2.4, -1.4)
        a = rng.uniform(0, math.pi)
        color = rng.choice(("cloth_red", "book_green", "book_blue", "rot", "page"))
        h.box("book", (x, y, 0.06 + (i % 3) * 0.06), (0.4 * abs(math.cos(a)) + 0.12, 0.4 * abs(math.sin(a)) + 0.12, 0.07), color)


def archive_pillar(h):
    """A square column from floor to vault, with a stepped base, a capital and a ring of glowing runes."""
    h.box("base", (0, 0, 0.25), (1.6, 1.6, 0.5), "auren_dark")
    h.box("shaft", (0, 0, HALL_H / 2), (1.1, 1.1, HALL_H), "auren")
    h.box("capital", (0, 0, HALL_H - 0.35), (1.5, 1.5, 0.4), "auren_dark")
    for side in range(4):
        a = side * math.pi / 2
        h.box("rune", (math.cos(a) * 0.56, math.sin(a) * 0.56, 2.6), (0.5 if side % 2 == 0 else 0.04, 0.04 if side % 2 == 0 else 0.5, 0.5), "rune")
        h.box("rune", (math.cos(a) * 0.56, math.sin(a) * 0.56, 2.6), (0.04 if side % 2 == 0 else 0.5, 0.5 if side % 2 == 0 else 0.04, 0.5), "rune")
    h.blob("moss", (0.4, -0.6, 0.3), 0.3, "moss", 540, 0.5, 1)


def archive_lectern(h):
    """A reading desk at the end of the hall with a great book left open, and a rune stone glowing over it."""
    h.box("foot", (0, 0, 0.1), (1.0, 0.8, 0.2), "auren_dark")
    h.box("post", (0, 0, 0.6), (0.4, 0.4, 1.0), "auren")
    h.box("desk", (0, -0.05, 1.15), (1.1, 0.8, 0.1), "auren_dark")
    for side in (-1, 1):
        h.box("page", (side * 0.26, -0.05, 1.23), (0.48, 0.62, 0.04), "page")
    h.box("spine", (0, -0.05, 1.22), (0.06, 0.64, 0.06), "cloth_red")
    h.blob("orb", (0, 0.2, 1.9), 0.22, "rune", 541, 1.0, 2)


def archive_cave(h):
    """The way in behind Silver Falls: a dark cave mouth in wet rock, framed inside by a doorway of the
    old white stone with a line of azure runes across its lintel."""
    rng = random.Random(506)
    for i, (x, y, z, r) in enumerate(((-3.4, 1.4, 1.4, 1.7), (3.4, 1.4, 1.4, 1.7), (0, 2.2, 3.9, 2.3), (-4.4, 1.8, 0.6, 1.3),
                                      (4.5, 1.6, 0.6, 1.4), (-1.9, 2.4, 3.0, 1.5), (2.0, 2.5, 3.1, 1.5))):
        h.blob(f"rock{i}", (x, y, z), r, "den_rock" if i % 2 else "den_rock_dark", 550 + i, 0.8, 2)
    h.box("mouth", (0, 0.6, 1.3), (2.6, 1.6, 2.6), "cave_dark")
    h.box("back_rock", (0, 1.6, 1.6), (4.6, 1.0, 3.4), "den_rock_dark")
    for side in (-1, 1):
        h.box("jamb", (side * 1.5, -0.2, 1.3), (0.45, 0.6, 2.6), "auren")
        h.box("jamb_base", (side * 1.5, -0.2, 0.15), (0.65, 0.8, 0.3), "auren_dark")
    h.box("lintel", (0, -0.2, 2.8), (3.6, 0.6, 0.45), "auren")
    for i in range(6):
        h.box("glyph", (-1.0 + i * 0.4, -0.52, 2.8), (0.14, 0.04, 0.22), "rune")
    for i in range(5):
        h.blob(f"moss{i}", (rng.choice((-1, 1)) * rng.uniform(2.2, 3.8), 0.4, rng.uniform(0.1, 3.0)), rng.uniform(0.3, 0.5), "moss", 560 + i, 0.4, 1)


BUILDERS = {
    "archive_floor": archive_floor,
    "archive_ceiling": archive_ceiling,
    "archive_wall": archive_wall,
    "archive_shelf": archive_shelf,
    "archive_shelf_fallen": archive_shelf_fallen,
    "archive_pillar": archive_pillar,
    "archive_lectern": archive_lectern,
    "archive_cave": archive_cave,
}


def models(helpers):
    """Model name to builder, ready for build_meshes.MODELS."""
    return {name: (lambda build: lambda: build(helpers))(build) for name, build in BUILDERS.items()}
