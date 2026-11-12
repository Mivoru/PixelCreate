"""Interior pieces for build_meshes.py: the common room of Hearthmere's tavern, the Bent Nail.

The room stands off the map on a platform (x 420, z 60 in the game), behind the tavern's door. Like the
dungeons it comes in two parts: a shell (floor, ceiling, beams and what hangs from them) that a walker
passes through, listed in Zone.NonSolid with the floor as a platform, and the room itself (walls, hearth,
bar and stools) whose collision is measured from the model. Tables, benches and barrels are the zone's
own props. Blender's +Y is the back of the room (the game's -Z), -Y the front with the door; up is z.
Each builder uses the helpers of build_meshes.py, passed in as h.
"""
import math
import random

COLORS = {"ale": "B8742A", "bottle_green": "3E6B48", "bottle_brown": "6B4426", "ham": "9A5A44"}

# The room inside the walls: 16 m wide (x), 12 m deep (y), 4.2 m to the ceiling.
HALF_W, HALF_D, HEIGHT = 8.0, 6.0, 4.2
THICK = 0.5


def inn_shell(h):
    """Plank floor and timbered ceiling, two wheel chandeliers on chains, hams and bunches of herbs hung
    from the beam over the bar, and a hide rug before the hearth."""
    rng = random.Random(801)
    h.box("floor_base", (0, 0, -0.15), (2 * HALF_W + 2, 2 * HALF_D + 2, 0.3), "wood_dark")
    y = -HALF_D
    while y < HALF_D - 0.01:
        width = 0.36
        tone = rng.choice(("wood", "wood", "wood", "bark"))
        h.box("plank", (0, y + width / 2, 0.005), (2 * HALF_W, width - 0.03, 0.01), tone)
        y += width
    for i in range(10):
        # Board ends, staggered, so the floor reads as boards and not as stripes.
        x, row = rng.uniform(-HALF_W + 0.5, HALF_W - 0.5), rng.randrange(int(2 * HALF_D / 0.36))
        h.box("board_end", (x, -HALF_D + row * 0.36 + 0.18, 0.012), (0.03, 0.33, 0.01), "wood_dark")
    for i in range(8):
        h.box("stain", (rng.uniform(-6, 7), rng.uniform(-5, 3), 0.014), (rng.uniform(0.3, 0.7), rng.uniform(0.2, 0.5), 0.004), "bark_dark")
    # A hide rug before the hearth on the west wall.
    h.box("rug", (-6.3, -1.0, 0.016), (1.6, 2.4, 0.02), "hide")
    h.box("rug_edge", (-6.3, -1.0, 0.012), (1.75, 2.55, 0.02), "hide_dark")

    h.box("ceiling", (0, 0, HEIGHT + 0.15), (2 * HALF_W + 2, 2 * HALF_D + 2, 0.3), "wood_dark")
    for k in range(-7, 8):
        h.box("joist", (k * 1.05, 0, HEIGHT - 0.08), (0.16, 2 * HALF_D, 0.16), "wood")
    for y in (-2.5, 2.5):
        h.box("beam", (0, y, HEIGHT - 0.3), (2 * HALF_W, 0.34, 0.34), "wood_dark")

    for cx in (-3.0, 3.0):
        hang = 3.05
        for k in range(3):
            a = k * math.tau / 3
            h.timber("chain", (cx, -1.0, HEIGHT - 0.1), (cx + math.cos(a) * 0.6, -1.0 + math.sin(a) * 0.6, hang + 0.05), 0.02, "iron")
        for k in range(10):
            a0, a1 = k * math.tau / 10, (k + 1) * math.tau / 10
            h.timber("wheel", (cx + math.cos(a0) * 0.65, -1.0 + math.sin(a0) * 0.65, hang),
                     (cx + math.cos(a1) * 0.65, -1.0 + math.sin(a1) * 0.65, hang), 0.08, "wood_dark")
        for k in range(5):
            a = k * math.tau / 5 + 0.4
            px, py = cx + math.cos(a) * 0.65, -1.0 + math.sin(a) * 0.65
            h.loft("candle", [(hang + 0.04, 0.035, 0.035), (hang + 0.22, 0.03, 0.03)], "paper", 6, (px, py, 0))
            h.loft("flame", [(hang + 0.23, 0.018, 0.018), (hang + 0.27, 0.03, 0.03), (hang + 0.34, 0.0, 0.0)], "window", 6, (px, py, 0))
            h.loft("drip", [(hang - 0.02, 0.03, 0.03), (hang + 0.04, 0.04, 0.04)], "paper", 6, (px, py, 0))

    # Over the bar: hams, sausages and herbs drying from pegs in the beam.
    for k, x in enumerate((2.6, 3.4, 4.3, 5.1, 6.0, 6.8)):
        top = HEIGHT - 0.47
        if k % 2 == 0:
            h.timber("cord", (x, 2.5, top), (x, 2.5, top - 0.5), 0.015, "sack")
            h.blob("ham", (x, 2.5, top - 0.78), 0.2, "ham", 810 + k, 1.5, 2)
            h.box("ham_tie", (x, 2.5, top - 0.53), (0.12, 0.12, 0.05), "sack")
        else:
            h.timber("cord", (x, 2.5, top), (x, 2.5, top - 0.35), 0.015, "sack")
            for j in range(5):
                a = j * math.tau / 5
                h.timber("herb", (x, 2.5, top - 0.35), (x + math.cos(a) * 0.1, 2.5 + math.sin(a) * 0.1, top - 0.85), 0.035,
                         "grass_dark" if j % 2 else "hay")


def window(h, side, along, centre_z=2.1):
    """A small-paned window let into the inside face of a wall, with a deep sill: side is the wall's
    facing ('-y', '+y', '-x', '+x') and along the place along it."""
    def put(name, offset, along_off, z, size_along, size_depth, size_z, material):
        if side in ("-y", "+y"):
            sign = -1 if side == "-y" else 1
            h.box(name, (along + along_off, sign * (HALF_D - offset), z), (size_along, size_depth, size_z), material)
        else:
            sign = -1 if side == "-x" else 1
            h.box(name, (sign * (HALF_W - offset), along + along_off, z), (size_depth, size_along, size_z), material)

    put("window_recess", 0.06, 0, centre_z, 1.5, 0.14, 1.5, "wood_dark")
    put("window", 0.13, 0, centre_z, 1.3, 0.04, 1.3, "glass")
    for k in (-0.43, 0.0, 0.43):
        put("window_bar", 0.16, k, centre_z, 0.06, 0.05, 1.3, "wood_dark")
    put("window_bar", 0.16, 0, centre_z, 1.3, 0.05, 0.06, "wood_dark")
    put("window_head", 0.08, 0, centre_z + 0.8, 1.8, 0.18, 0.14, "wood_dark")
    put("window_sill", 0.12, 0, centre_z - 0.8, 1.8, 0.3, 0.08, "wood")


def bottles(h, x0, x1, y, z, rng):
    """A row of bottles, jugs and stoppered flasks on a shelf."""
    x = x0
    while x < x1:
        kind = rng.random()
        colour = rng.choice(("bottle_green", "bottle_brown", "bottle_brown", "stone_dark", "glass"))
        if kind < 0.6:
            h.loft("bottle", [(z, 0.06, 0.06), (z + 0.2, 0.065, 0.065), (z + 0.26, 0.03, 0.03), (z + 0.34, 0.025, 0.025)], colour, 8, (x, y, 0))
            h.loft("cork", [(z + 0.34, 0.025, 0.025), (z + 0.37, 0.022, 0.022)], "hide", 6, (x, y, 0))
            x += 0.16
        else:
            h.loft("jug", [(z, 0.08, 0.08), (z + 0.1, 0.11, 0.11), (z + 0.22, 0.09, 0.09), (z + 0.28, 0.05, 0.05), (z + 0.31, 0.06, 0.06)],
                   "stone_dark" if colour == "glass" else "wall_b", 10, (x + 0.04, y, 0))
            x += 0.26
        x += rng.uniform(0.0, 0.06)


def inn_walls(h):
    """Walls of plaster between dark timbers over a board wainscot, small-paned windows, and the door in
    the front wall with hooks for cloaks beside it. Built as two models (front and back, the two sides):
    a closed ring measured as one would be filled solid, room and all (collision.py fills enclosed cells)."""
    outer_w, outer_d = HALF_W + THICK / 2, HALF_D + THICK / 2
    for sign in (-1, 1):
        h.box("wall", (0, sign * outer_d, HEIGHT / 2), (2 * HALF_W + 2 * THICK, THICK, HEIGHT), "wall")
        h.box("wall", (sign * outer_w, 0, HEIGHT / 2), (THICK, 2 * HALF_D, HEIGHT), "wall")
    # Wainscot: upright boards to hip height with a capping rail.
    for sign in (-1, 1):
        h.box("wainscot", (0, sign * (HALF_D - 0.03), 0.55), (2 * HALF_W, 0.06, 1.1), "wood")
        h.box("wainscot", (sign * (HALF_W - 0.03), 0, 0.55), (0.06, 2 * HALF_D, 1.1), "wood")
        h.box("rail", (0, sign * (HALF_D - 0.07), 1.12), (2 * HALF_W, 0.12, 0.07), "wood_dark")
        h.box("rail", (sign * (HALF_W - 0.07), 0, 1.12), (0.12, 2 * HALF_D, 0.07), "wood_dark")
    for k in range(-31, 32):
        for sign in (-1, 1):
            h.box("board_line", (k * 0.25, sign * (HALF_D - 0.065), 0.55), (0.02, 0.02, 1.06), "wood_dark")
    for k in range(-23, 24):
        for sign in (-1, 1):
            h.box("board_line", (sign * (HALF_W - 0.065), k * 0.25, 0.55), (0.02, 0.02, 1.06), "wood_dark")
    # Timber posts up the walls, a plate along the top, a few braces.
    for x in (-HALF_W + 0.12, -4.0, 0.0, 4.0, HALF_W - 0.12):
        for sign in (-1, 1):
            if x == 0.0 and sign == -1:
                continue  # The door stands here.
            h.box("post", (x, sign * (HALF_D - 0.08), HEIGHT / 2), (0.24, 0.16, HEIGHT), "wood_dark")
    for y in (-3.0, 3.0):
        for sign in (-1, 1):
            h.box("post", (sign * (HALF_W - 0.08), y, HEIGHT / 2), (0.16, 0.24, HEIGHT), "wood_dark")
    for sign in (-1, 1):
        h.box("plate", (0, sign * (HALF_D - 0.09), HEIGHT - 0.12), (2 * HALF_W, 0.18, 0.24), "wood_dark")
        h.box("plate", (sign * (HALF_W - 0.09), 0, HEIGHT - 0.12), (0.18, 2 * HALF_D, 0.24), "wood_dark")

    # Windows: either side of the door, two in the back wall west of the bar, one in the east wall.
    for x in (-6.0, 6.0):
        window(h, "-y", x)
    for x in (-6.0, -2.0):
        window(h, "+y", x)
    window(h, "+x", -4.5)

    # The door: boards in a heavy frame, iron straps, a lamp over it and a mat before it.
    door_y = -HALF_D + 0.04
    h.box("door_frame", (0, door_y, 1.2), (1.6, 0.1, 2.4), "wood_dark")
    h.box("door", (0, door_y + 0.04, 1.12), (1.2, 0.06, 2.2), "wood")
    for k in range(-2, 3):
        h.box("door_board", (k * 0.24, door_y + 0.075, 1.12), (0.02, 0.02, 2.2), "wood_dark")
    for z in (0.5, 1.75):
        h.box("door_strap", (0, door_y + 0.08, z), (1.1, 0.03, 0.08), "iron")
    h.box("door_ring", (0.4, door_y + 0.1, 1.05), (0.12, 0.04, 0.12), "iron")
    h.box("door_head", (0, door_y + 0.06, 2.45), (1.9, 0.16, 0.18), "wood_dark")
    h.box("door_lamp", (0, door_y + 0.2, 2.85), (0.26, 0.26, 0.34), "window")
    h.box("door_lamp_cap", (0, door_y + 0.2, 3.06), (0.34, 0.34, 0.06), "iron")
    h.box("door_mat", (0, -HALF_D + 0.8, 0.02), (1.6, 1.0, 0.03), "sack")
    # Hooks by the door with a cloak and a hat.
    for x in (-1.4, -1.9):
        h.timber("hook", (x, door_y + 0.02, 1.85), (x, door_y + 0.16, 1.9), 0.03, "iron")
    h.box("cloak", (-1.4, door_y + 0.12, 1.3), (0.4, 0.08, 1.1), "hide")
    h.box("cloak_hood", (-1.4, door_y + 0.14, 1.78), (0.3, 0.1, 0.2), "hide_dark")
    h.blob("hat", (-1.9, door_y + 0.15, 1.82), 0.16, "hide_dark", 820, 0.5, 2)


def inn_fittings(h):
    """A stone hearth on the west wall, the bar in the back east corner with its kegs and shelves of
    bottles, low stools before it, and a corner where a minstrel left a lute and a drum."""
    rng = random.Random(802)
    # The hearth, in the middle of the west wall: two stone cheeks, a lintel, a chimney breast to the
    # ceiling, a mantel with jugs and a pair of tankards, logs and embers in the fire box.
    hx, hy = -HALF_W, -1.0
    for side in (-1, 1):
        h.box("cheek", (hx + 0.5, hy + side * 1.05, 0.65), (0.9, 0.4, 1.3), "stone")
    h.box("lintel", (hx + 0.5, hy, 1.42), (0.95, 2.5, 0.3), "stone_dark")
    h.box("breast", (hx + 0.35, hy, (1.57 + HEIGHT) / 2), (0.7, 2.3, HEIGHT - 1.57), "stone")
    for k in range(5):
        h.box("course", (hx + 0.71, hy, 1.9 + k * 0.45), (0.02, 2.3, 0.04), "stone_dark")
    h.box("fire_back", (hx + 0.08, hy, 0.65), (0.1, 1.7, 1.3), "rock_dark")
    h.box("fire_floor", (hx + 0.5, hy, 0.04), (0.9, 1.7, 0.08), "stone_dark")
    h.box("hearthstone", (hx + 1.25, hy, 0.03), (0.6, 2.6, 0.06), "stone")
    h.box("mantel", (hx + 0.8, hy, 1.62), (0.4, 2.7, 0.1), "wood_dark")
    for k, colour in enumerate(("wall_b", "iron", "stone_dark", "iron", "wall_b")):
        y = hy - 1.1 + k * 0.55
        h.loft("mantel_jar", [(1.67, 0.07, 0.07), (1.82, 0.08, 0.08), (1.9, 0.05, 0.05)], colour, 8, (hx + 0.8, y, 0))
    for k in range(3):
        a = 0.4 + k * 0.5
        h.timber("log", (hx + 0.3 + math.cos(a) * 0.1, hy - 0.5, 0.18 + k * 0.08), (hx + 0.55, hy + 0.5, 0.15 + (k % 2) * 0.1), 0.11, "bark_dark")
    h.box("embers", (hx + 0.45, hy, 0.1), (0.6, 1.2, 0.06), "ember")
    h.timber("poker", (hx + 0.95, hy + 1.35, 0.05), (hx + 0.85, hy + 1.4, 0.95), 0.025, "iron")
    # A stack of split wood beside it.
    for row in range(3):
        for k in range(4 - row):
            y = hy - 2.2 + row * 0.12 + k * 0.24
            h.timber("firewood", (hx + 0.15, y, 0.12 + row * 0.2), (hx + 0.85, y, 0.12 + row * 0.2), 0.1, "bark" if k % 2 else "bark_dark")
    # A boar's head over the mantel.
    h.box("trophy_board", (hx + 0.72, hy, 2.9), (0.06, 0.9, 1.0), "wood_dark")
    h.blob("boar_head", (hx + 0.98, hy, 2.85), 0.3, "hide_dark", 821, 0.95, 2)
    h.timber("boar_snout", (hx + 1.12, hy, 2.8), (hx + 1.46, hy, 2.74), 0.2, "hide")
    h.box("boar_nose", (hx + 1.47, hy, 2.74), (0.03, 0.16, 0.13), "anime_dark")
    for side in (-1, 1):
        h.blob("boar_eye", (hx + 1.18, hy + side * 0.17, 2.98), 0.035, "anime_dark", 823, 1, 1)
    for side in (-1, 1):
        h.tube("tusk", [(hx + 1.42, hy + side * 0.1, 2.68), (hx + 1.5, hy + side * 0.15, 2.82)], [0.03, 0.008], "paper", 6)
        h.blob("ear", (hx + 0.95, hy + side * 0.26, 3.12), 0.09, "hide_dark", 822, 0.5, 1)

    # The bar, in the back east corner: a counter with a return, a foot rail, and a way in at its east end.
    bar_y, bar_x0, bar_x1 = 2.8, 1.6, 6.6
    h.box("counter", ((bar_x0 + bar_x1) / 2, bar_y, 0.52), (bar_x1 - bar_x0, 0.6, 1.04), "wood")
    h.box("counter_top", ((bar_x0 + bar_x1) / 2, bar_y - 0.05, 1.07), (bar_x1 - bar_x0 + 0.2, 0.8, 0.07), "wood_dark")
    h.box("counter", (bar_x0, bar_y + 0.85, 0.52), (0.6, 1.1, 1.04), "wood")
    h.box("counter_top", (bar_x0, bar_y + 0.8, 1.07), (0.8, 1.4, 0.07), "wood_dark")
    for k in range(5):
        x = bar_x0 + 0.5 + k * 1.0
        h.box("counter_panel", (x, bar_y - 0.31, 0.5), (0.8, 0.03, 0.7), "wood_dark")
    h.timber("foot_rail", (bar_x0, bar_y - 0.48, 0.2), (bar_x1, bar_y - 0.48, 0.2), 0.035, "iron")
    for x in (bar_x0 + 0.4, (bar_x0 + bar_x1) / 2, bar_x1 - 0.2):
        h.timber("rail_post", (x, bar_y - 0.48, 0.2), (x, bar_y - 0.3, 0.2), 0.03, "iron")
    # On the counter: tankards, a tray, a coin box, a candle and a cask with a tap.
    for x, y in ((2.2, 2.65), (2.5, 2.75), (4.9, 2.6), (5.8, 2.7)):
        h.loft("tankard", [(1.1, 0.065, 0.065), (1.27, 0.07, 0.07)], "wood_dark", 8, (x, y, 0))
        h.loft("foam", [(1.27, 0.07, 0.07), (1.31, 0.055, 0.055)], "paper", 8, (x, y, 0))
        h.loft("hoop", [(1.24, 0.075, 0.075), (1.26, 0.075, 0.075)], "iron", 8, (x, y, 0))
    h.box("tray", (3.4, 2.75, 1.115), (0.6, 0.4, 0.03), "iron")
    h.box("coin_box", (6.1, 3.0, 1.17), (0.35, 0.25, 0.14), "wood_dark")
    h.box("coins", (6.1, 3.0, 1.245), (0.2, 0.12, 0.02), "gold")
    h.loft("candle", [(1.1, 0.04, 0.04), (1.28, 0.035, 0.035)], "paper", 6, (4.3, 2.95, 0))
    h.loft("flame", [(1.29, 0.02, 0.02), (1.33, 0.032, 0.032), (1.4, 0.0, 0.0)], "window", 6, (4.3, 2.95, 0))
    # Low stools before the bar.
    for x in (2.4, 3.6, 4.8, 6.0):
        h.disc("stool", (x, bar_y - 0.95, 0.46), 0.22, 0.07, "wood", 10)
        for leg in range(3):
            a = leg * math.tau / 3 + 0.5
            h.timber("stool_leg", (x + math.cos(a) * 0.17, bar_y - 0.95 + math.sin(a) * 0.17, 0),
                     (x + math.cos(a) * 0.1, bar_y - 0.95 + math.sin(a) * 0.1, 0.46), 0.045, "wood_dark")

    # Behind the bar: kegs on a rack under shelves of bottles and jugs, tankards on pegs.
    for k, x in enumerate((2.6, 3.7, 4.8)):
        h.box("rack", (x, HALF_D - 0.55, 0.22), (0.9, 0.8, 0.44), "wood_dark")
        h.timber("keg", (x, HALF_D - 0.95, 0.85), (x, HALF_D - 0.15, 0.85), 0.36, "wood")
        for t in (0.15, 0.5, 0.85):
            yy = HALF_D - 0.95 + t * 0.8
            h.timber("keg_hoop", (x, yy - 0.02, 0.85), (x, yy + 0.02, 0.85), 0.38, "iron")
        h.timber("tap", (x, HALF_D - 0.97, 0.75), (x, HALF_D - 1.12, 0.75), 0.03, "gold")
        h.box("drip_tray", (x, HALF_D - 1.1, 0.46), (0.3, 0.2, 0.04), "iron")
    h.box("shelf_back", (4.2, HALF_D - 0.06, 2.55), (4.0, 0.06, 2.0), "wood_dark")
    for z in (1.75, 2.35, 2.95, 3.5):
        h.box("shelf", (4.2, HALF_D - 0.2, z), (4.0, 0.3, 0.05), "wood")
        bottles(h, 2.35, 6.05, HALF_D - 0.2, z + 0.025, rng)
    for x in (2.2, 6.2):
        h.box("shelf_side", (x, HALF_D - 0.2, 2.6), (0.06, 0.32, 1.9), "wood")
    for k in range(6):
        z = 1.55 if k < 3 else 1.95
        h.timber("peg", (HALF_W - 0.05, HALF_D - 1.0 - (k % 3) * 0.35, z + 0.25), (HALF_W - 0.2, HALF_D - 1.0 - (k % 3) * 0.35, z + 0.25), 0.02, "wood_dark")
        h.loft("tankard_peg", [(z, 0.06, 0.06), (z + 0.17, 0.065, 0.065)], "wood_dark", 8, (HALF_W - 0.22, HALF_D - 1.0 - (k % 3) * 0.35, 0))
    # A sign over the bar with the tavern's mark, a bent nail, in gold.
    h.box("bar_sign", (4.1, HALF_D - 0.05, 3.85), (1.8, 0.06, 0.42), "wood_dark")
    h.box("sign_nail", (3.7, HALF_D - 0.09, 3.85), (0.5, 0.03, 0.07), "gold")
    h.box("sign_nail_bend", (4.0, HALF_D - 0.09, 3.93), (0.07, 0.03, 0.2), "gold")
    h.box("sign_rim", (4.1, HALF_D - 0.04, 3.85), (1.9, 0.04, 0.5), "wood")
    # The bar's own lamp on a bracket.
    h.timber("lamp_arm", (HALF_W - 0.1, 3.4, 2.5), (HALF_W - 0.6, 3.4, 2.5), 0.05, "iron")
    h.box("bar_lamp", (HALF_W - 0.62, 3.4, 2.3), (0.22, 0.22, 0.3), "window")

    # The minstrel's corner, in the back west: a stool with a lute leaning on it and a hand drum.
    mx, my = -6.8, 4.6
    h.disc("stool", (mx, my, 0.46), 0.24, 0.07, "wood", 10)
    for leg in range(3):
        a = leg * math.tau / 3
        h.timber("stool_leg", (mx + math.cos(a) * 0.18, my + math.sin(a) * 0.18, 0), (mx + math.cos(a) * 0.1, my + math.sin(a) * 0.1, 0.46), 0.045, "wood_dark")
    h.blob("lute_body", (mx + 0.45, my + 0.1, 0.32), 0.24, "wood", 830, 0.45, 2)
    h.timber("lute_neck", (mx + 0.45, my + 0.1, 0.5), (mx + 0.3, my + 0.12, 1.1), 0.05, "wood_dark")
    h.box("lute_head", (mx + 0.28, my + 0.12, 1.16), (0.08, 0.06, 0.14), "wood_dark")
    h.loft("drum", [(0, 0.26, 0.26), (0.3, 0.28, 0.28)], "hide_dark", 12, (mx - 0.2, my - 0.6, 0))
    h.loft("drum_skin", [(0.3, 0.28, 0.28), (0.32, 0.26, 0.26)], "paper", 12, (mx - 0.2, my - 0.6, 0))
    # On the back wall above: a pair of crossed oars from Mirror Lake and a round shield.
    h.timber("oar", (-5.0, HALF_D - 0.08, 2.0), (-3.0, HALF_D - 0.08, 3.4), 0.05, "wood")
    h.timber("oar", (-5.0, HALF_D - 0.08, 3.4), (-3.0, HALF_D - 0.08, 2.0), 0.05, "wood")
    for x, z in ((-5.0, 2.0), (-3.0, 3.4), (-5.0, 3.4), (-3.0, 2.0)):
        h.box("oar_blade", (x, HALF_D - 0.09, z), (0.2, 0.04, 0.4), "wood_dark")
    h.disc_y("shield", (-4.0, HALF_D - 0.12, 2.7), 0.45, 0.06, "cloth_red", 16)
    h.disc_y("shield_boss", (-4.0, HALF_D - 0.16, 2.7), 0.12, 0.05, "iron", 10)


class _Part:
    """Passes on to the helpers only the pieces of one wall pair: 'ns' the front and back, 'ew' the sides."""

    def __init__(self, h, part):
        self.h, self.part = h, part

    def _keep(self, x, y):
        front_back = abs(y) > HALF_D - 0.45
        return front_back if self.part == "ns" else (not front_back and abs(x) > HALF_W - 0.45)

    def box(self, name, centre, *rest):
        if self._keep(centre[0], centre[1]):
            self.h.box(name, centre, *rest)

    def timber(self, name, start, end, *rest):
        if self._keep((start[0] + end[0]) / 2, (start[1] + end[1]) / 2):
            self.h.timber(name, start, end, *rest)

    def blob(self, name, centre, *rest):
        if self._keep(centre[0], centre[1]):
            self.h.blob(name, centre, *rest)


BUILDERS = {
    "inn_shell": inn_shell,
    "inn_walls_ns": lambda h: inn_walls(_Part(h, "ns")),
    "inn_walls_ew": lambda h: inn_walls(_Part(h, "ew")),
    "inn_fittings": inn_fittings,
}


def models(helpers):
    """Model name to builder, ready for build_meshes.MODELS."""
    return {name: (lambda build: lambda: build(helpers))(build) for name, build in BUILDERS.items()}
