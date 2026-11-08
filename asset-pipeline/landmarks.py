"""Village landmarks and ruins: the tower mill, the well, the market stall, the watchtower and the
old stonework. Used by build_meshes.py, which hands over its drawing helpers through bind().

Blender axes: the front faces -y, up is z. Each model sits on the ground at z 0 and keeps the
footprint its collision in shared-core/Zone.cs expects.
"""
import math
import random

import bpy
import mathutils

import houses

box = timber = blob = loft = add_object = mesh_from = None


def bind(helpers):
    globals().update(helpers)


def parented(parent_name, location, build):
    """Builds some objects and hangs them under an empty, so the game can move them as one piece
    (the mill's sails turn). Objects are drawn around the origin and the empty carries them."""
    before = set(bpy.context.scene.objects)
    build()
    empty = bpy.data.objects.new(parent_name, None)
    empty.location = location
    bpy.context.collection.objects.link(empty)
    for obj in set(bpy.context.scene.objects) - before:
        if obj is not empty:
            obj.parent = empty


def coursed_block(name, centre, size, rng, material="stone"):
    """One dressed block, a little uneven, so laid courses never look machine cut."""
    x, y, z = centre
    w, d, h = size
    box(name, (x + rng.uniform(-0.02, 0.02), y + rng.uniform(-0.03, 0.03), z),
        (w * rng.uniform(0.94, 1.0), d * rng.uniform(0.94, 1.02), h * rng.uniform(0.92, 1.0)), material)


# --- The tower mill ------------------------------------------------------------------------------

def windmill():
    """A tower mill: a stone footing, a tapering plastered tower banded in stone, a framed door with
    a step, small windows up the stair, a domed cap with a tail pole, and four lattice sails on a
    windshaft. The sails hang under an empty called "sails" so the game can turn them."""
    sides = 16
    loft("footing", [(-0.4, 2.25, 2.25), (1.1, 2.15, 2.15), (1.25, 2.05, 2.05)], "stone", sides)
    tower = [(1.2, 2.0), (7.6, 1.55)]

    def radius(z):
        t = (z - tower[0][0]) / (tower[1][0] - tower[0][0])
        return tower[0][1] + (tower[1][1] - tower[0][1]) * t

    loft("tower", [(z, radius(z), radius(z)) for z in (1.2, 4.4, 7.6)], "wall", sides)
    for z in (1.25, 4.4, 7.55):
        r = radius(z) + 0.06
        loft("band", [(z - 0.1, r, r), (z + 0.1, r, r)], "stone_dark", sides)
    # The face of a 16-sided tower is a little inside its corner radius.
    face = math.cos(math.pi / sides)

    def front(z):
        return (0, -radius(z) * face)

    # Door with a frame, iron straps and a stone step.
    houses.wall_box("door", "-y", front(1.2), 0, 0.02, 2.25, (1.0, 0.08, 2.1), "wood")
    for k in (-1, 1):
        houses.wall_box("door_frame", "-y", front(1.2), k * 0.56, 0.06, 2.3, (0.14, 0.14, 2.3), "wood_dark")
    houses.wall_box("door_head", "-y", front(1.2), 0, 0.07, 3.45, (1.4, 0.16, 0.18), "wood_dark")
    for z in (1.7, 2.8):
        houses.wall_box("door_strap", "-y", front(1.2), 0, 0.07, z, (0.85, 0.03, 0.08), "iron")
    box("step", (0, -2.55, 0.15), (1.6, 0.8, 0.3), "stone")
    box("step", (0, -2.35, 0.45), (1.4, 0.5, 0.3), "stone")
    houses.wall_box("handle", "-y", front(1.2), 0.3, 0.1, 2.2, (0.08, 0.06, 0.08), "gold")
    # Small windows light the stair as it winds up; one faces the yard on the side.
    for z, u in ((4.9, -0.55), (6.6, 0.5)):
        houses.window("-y", front(z), u, z, width=0.5, height=0.7, shutters=False)
    houses.window("+x", (radius(5.6) * face, 0), 0, 5.6, width=0.5, height=0.7, shutters=False)

    # The cap: a dome on a timber curb, a ridge finial and a tail pole down to the ground behind.
    loft("curb", [(7.55, 1.85, 1.85), (7.85, 1.85, 1.85)], "wood_dark", sides)
    loft("cap", [(7.85, 1.8, 1.9), (8.6, 1.7, 1.8), (9.3, 1.25, 1.35), (9.75, 0.55, 0.6), (9.9, 0.05, 0.05)], "roof", sides)
    timber("finial", (0, 0, 9.85), (0, 0, 10.4), 0.08, "gold")
    timber("tail_pole", (0, 1.7, 8.0), (0, 4.4, 0.4), 0.18, "wood_dark")
    timber("tail_brace", (0, 1.6, 7.6), (0, 3.3, 3.5), 0.12, "wood_dark")
    for k in range(8):
        a = k / 8 * math.tau
        timber("cart_wheel", (0.35 * math.cos(a), 4.4, 0.4 + 0.35 * math.sin(a)),
               (0.35 * math.cos(a + math.tau / 8), 4.4, 0.4 + 0.35 * math.sin(a + math.tau / 8)), 0.07, "wood_dark")

    # Windshaft out of the cap's front, then the sails on it.
    timber("windshaft", (0, -1.0, 8.35), (0, -2.55, 8.2), 0.32, "wood_dark")

    def sails():
        box("hub", (0, -0.05, 0), (0.6, 0.5, 0.6), "wood_dark")
        for i in range(4):
            a = i * math.pi / 2 + math.pi / 4
            along = mathutils.Vector((math.cos(a), 0, math.sin(a)))
            across = mathutils.Vector((-math.sin(a), 0, math.cos(a)))
            timber("stock", tuple(along * 0.25), tuple(along * 5.6), 0.16, "wood_dark")
            # The lattice: a rail on the trailing side and bars across it, cloth spread on two
            # sails and furled on the other two, as a miller sets them for the wind.
            inner, outer = 1.1, 5.4
            timber("whip", tuple(along * inner + across * 0.95), tuple(along * outer + across * 0.95), 0.07, "wood")
            for k in range(10):
                r = inner + (outer - inner) * k / 9
                timber("bar", tuple(along * r - across * 0.08), tuple(along * r + across * 0.98), 0.05, "wood")
            if i % 2 == 0:
                corners = [along * (inner + 0.05) + across * 0.1, along * (outer - 0.05) + across * 0.1,
                           along * (outer - 0.05) + across * 0.9, along * (inner + 0.05) + across * 0.9]
                cloth = [tuple(c + mathutils.Vector((0, 0.06, 0))) for c in corners]
                add_object("cloth", mesh_from("cloth", cloth, [(0, 1, 2, 3)]), "sack")
            else:
                timber("furled", tuple(along * (inner + 0.1) + across * 0.16), tuple(along * (outer - 0.2) + across * 0.16), 0.14, "sack")

    parented("sails", (0, -2.7, 8.2), sails)


# --- The well ------------------------------------------------------------------------------------

def well():
    """A village well: a round stone wall with a coping, a windlass between two posts on stone pads,
    a crank, a rope down to a bucket hanging over the water, and a small boarded roof."""
    loft("well_wall", [(0, 1.0, 1.0), (0.85, 0.95, 0.95)], "stone", 16)
    loft("well_inner", [(0.9, 0.72, 0.72), (0.2, 0.72, 0.72)], "stone_dark", 16)
    loft("coping", [(0.82, 1.06, 1.06), (0.98, 1.04, 1.04)], "stone_dark", 16)
    loft("water", [(0.3, 0.71, 0.71), (0.32, 0.71, 0.71)], "water", 16)
    for side in (-1, 1):
        box("pad", (side * 1.0, 0, 0.98), (0.34, 0.34, 0.1), "stone_dark")
        timber("post", (side * 1.0, 0, 1.0), (side * 1.0, 0, 2.9), 0.16)
        timber("knee", (side * 1.0, -0.35, 2.55), (side * 1.0, 0, 2.85), 0.08)
        timber("knee", (side * 1.0, 0.35, 2.55), (side * 1.0, 0, 2.85), 0.08)
    timber("windlass", (-0.95, 0, 2.15), (0.95, 0, 2.15), 0.16, "wood_dark")
    loft("rope_coil", [(-0.05, 0.11, 0.11), (0.05, 0.11, 0.11)], "sack", 10)
    bpy.data.objects["rope_coil"].rotation_euler = (0, math.pi / 2, 0)
    bpy.data.objects["rope_coil"].location = (0.2, 0, 2.15)
    timber("crank", (1.1, 0, 2.15), (1.1, 0, 1.8), 0.06, "iron")
    timber("crank_grip", (1.1, 0, 1.8), (1.3, 0, 1.8), 0.06, "wood_dark")
    timber("rope", (0.2, -0.1, 2.12), (0.2, -0.1, 1.25), 0.025, "sack")
    loft("bucket", [(0.95, 0.15, 0.15), (1.25, 0.18, 0.18)], "wood", 10, (0.2, -0.1, 0))
    loft("bucket_hoop", [(1.18, 0.185, 0.185), (1.22, 0.185, 0.185)], "iron", 10, (0.2, -0.1, 0))
    timber("ridge_beam", (-1.15, 0, 2.95), (1.15, 0, 2.95), 0.14, "wood_dark")
    houses.gabled_roof(2.85, 2.0, 1.6, 0.75, 0.3, "thatch", 0.14)
    for side in (-1, 1):
        x = side * 1.15
        timber("rafter", (x, -1.1, 2.75), (x, 0, 3.6), 0.09)
        timber("rafter", (x, 1.1, 2.75), (x, 0, 3.6), 0.09)


# --- The market stall ----------------------------------------------------------------------------

def basket(name, centre, radius, fruit, seed):
    """A woven basket heaped with produce."""
    x, y, z = centre
    loft(name, [(z, radius * 0.85, radius * 0.85), (z + 0.2, radius, radius)], "hay", 12, (x, y, 0))
    rng = random.Random(seed)
    for k in range(7):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0, radius * 0.6)
        blob(f"{name}_item{k}", (x + math.cos(a) * r, y + math.sin(a) * r, z + 0.2 + rng.uniform(0.0, 0.08)),
             radius * 0.32, fruit, seed * 10 + k, 0.85, 1)


def stall():
    """A market stall: posts on stone pads, a plank counter with an apron, baskets and crates of
    produce, a striped awning with a scalloped valance, and a painted board."""
    for x, y, height in ((-1.5, -1.0, 2.2), (1.5, -1.0, 2.2), (-1.5, 1.0, 2.6), (1.5, 1.0, 2.6)):
        box("pad", (x, y, 0.06), (0.3, 0.3, 0.12), "stone_dark")
        timber("post", (x, y, 0.1), (x, y, height), 0.13)
    box("counter", (0, -0.7, 0.92), (3.25, 0.95, 0.1), "wood")
    box("apron", (0, -1.12, 0.48), (3.15, 0.06, 0.8), "wood_dark")
    for x in (-1.0, 0.0, 1.0):
        box("apron_batten", (x, -1.16, 0.48), (0.1, 0.03, 0.78), "wood")
    box("shelf", (0, 0.75, 0.6), (3.0, 0.5, 0.08), "wood")
    box("back_board", (0, 1.0, 1.3), (3.0, 0.06, 1.0), "wood_dark")
    for i, (x, fruit) in enumerate(((-1.15, "flower"), (-0.4, "leaf"), (0.4, "flower_b"), (1.15, "cloth_red"))):
        basket(f"basket{i}", (x, -0.75, 0.97), 0.26, fruit, i + 3)
    for x in (-0.9, 0.9):
        box("crate", (x, 0.6, 0.85), (0.6, 0.4, 0.4), "wood")
        timber("crate_band", (x - 0.3, 0.39, 0.97), (x + 0.3, 0.39, 0.97), 0.05, "wood_dark")
    # Spare stock waits on the ground behind the counter, inside the stall.
    blob("sack", (-1.05, 0.15, 0.3), 0.32, "sack", 3, 0.95, 2)
    blob("sack", (-0.55, 0.2, 0.28), 0.29, "sack", 4, 0.95, 2)
    # Awning: alternating red and cream strips from the back beam to the front beam, then a
    # scalloped valance along the front.
    timber("beam", (-1.6, -1.0, 2.1), (1.6, -1.0, 2.1), 0.12)
    timber("beam", (-1.6, 1.0, 2.6), (1.6, 1.0, 2.6), 0.12)
    strips = 8
    for k in range(strips):
        x0 = -1.75 + 3.5 * k / strips
        x1 = -1.75 + 3.5 * (k + 1) / strips
        verts = [(x0, -1.45, 2.12), (x1, -1.45, 2.12), (x1, 1.15, 2.72), (x0, 1.15, 2.72)]
        top = [(x, y, z + 0.03) for x, y, z in verts]
        faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        add_object("awning", mesh_from("awning", verts + top, faces), "cloth_red" if k % 2 == 0 else "paper")
        flap = [(x0, -1.46, 2.12), (x1, -1.46, 2.12), (x1, -1.46, 1.95), ((x0 + x1) / 2, -1.46, 1.84), (x0, -1.46, 1.95)]
        add_object("valance", mesh_from("valance", flap, [(0, 1, 2, 3, 4)]), "cloth_red" if k % 2 == 0 else "paper")
    box("sign", (0, -1.08, 2.0), (1.4, 0.06, 0.32), "wood_dark")
    box("sign_face", (0, -1.12, 2.0), (1.2, 0.03, 0.2), "gold")


def bottle(name, x, y, z, radius, colour, tall=1.0):
    """A glass potion bottle: a round belly of coloured liquid, a narrow neck and a cork."""
    h = radius * 2.4 * tall
    loft(name, [(z, radius * 0.7, radius * 0.7), (z + h * 0.15, radius, radius), (z + h * 0.55, radius, radius),
                (z + h * 0.7, radius * 0.4, radius * 0.4), (z + h * 0.92, radius * 0.32, radius * 0.32)], colour, 10, (x, y, 0))
    loft(name + "_cork", [(z + h * 0.92, radius * 0.3, radius * 0.3), (z + h * 1.05, radius * 0.34, radius * 0.34)], "wood", 8, (x, y, 0))


def alchemy_stall():
    """The alchemist's stall: a market stall under a purple and cream awning, its counter and two
    shelves crowded with potion bottles, a bubbling cauldron on embers at one side, bundles of
    drying herbs from the beam, and a board with a painted flask."""
    for x, y, height in ((-1.5, -1.0, 2.2), (1.5, -1.0, 2.2), (-1.5, 1.0, 2.6), (1.5, 1.0, 2.6)):
        box("pad", (x, y, 0.06), (0.3, 0.3, 0.12), "stone_dark")
        timber("post", (x, y, 0.1), (x, y, height), 0.13)
    box("counter", (0, -0.7, 0.92), (3.25, 0.95, 0.1), "wood")
    box("apron", (0, -1.12, 0.48), (3.15, 0.06, 0.8), "wood_dark")
    box("apron_cloth", (0, -1.16, 0.62), (2.2, 0.03, 0.45), "cloth_purple")
    for z in (0.7, 1.35):
        box("shelf", (0, 0.8, z), (3.0, 0.4, 0.07), "wood")
    box("back_board", (0, 1.0, 1.25), (3.0, 0.06, 1.3), "wood_dark")
    rng = random.Random(7)
    colours = ("potion_red", "potion_red", "potion_blue", "potion_green", "cloth_purple", "potion_red")
    for z in (0.74, 1.39):
        for k in range(11):
            x = -1.3 + k * 0.26 + rng.uniform(-0.04, 0.04)
            bottle("shelf_bottle", x, 0.8, z, rng.uniform(0.06, 0.08), colours[rng.randrange(len(colours))], rng.uniform(0.85, 1.3))
    for k, (x, colour) in enumerate(((-1.1, "potion_red"), (-0.75, "potion_red"), (-0.4, "potion_red"), (0.5, "potion_blue"), (0.85, "potion_green"))):
        bottle("counter_bottle", x, -0.7, 0.97, 0.1, colour, 1.1)
    # A pestle and mortar and an open book of recipes on the counter.
    loft("mortar", [(0.97, 0.12, 0.12), (1.1, 0.14, 0.14), (1.14, 0.11, 0.11)], "stone", 10, (1.25, -0.65, 0))
    timber("pestle", (1.25, -0.65, 1.05), (1.33, -0.6, 1.25), 0.04, "wood_dark")
    box("book", (0.05, -0.65, 0.99), (0.5, 0.36, 0.04), "paper")
    box("book_spine", (0.05, -0.65, 1.01), (0.04, 0.36, 0.03), "cloth_red")
    # The cauldron, off to the side on a ring of embers.
    for k in range(6):
        a = k * math.tau / 6
        blob("cauldron_stone", (2.35 + math.cos(a) * 0.55, -0.3 + math.sin(a) * 0.55, 0.1), 0.16, "stone_dark", k, 0.9, 1)
    blob("embers", (2.35, -0.3, 0.06), 0.32, "ember", 5, 0.4, 1)
    loft("cauldron", [(0.15, 0.2, 0.2), (0.3, 0.42, 0.42), (0.6, 0.5, 0.5), (0.85, 0.44, 0.44), (0.9, 0.47, 0.47)], "iron", 14, (2.35, -0.3, 0))
    loft("brew", [(0.82, 0.42, 0.42), (0.84, 0.42, 0.42)], "potion_green", 14, (2.35, -0.3, 0))
    for k in range(3):
        blob("bubble", (2.25 + k * 0.12, -0.25 + (k % 2) * 0.1, 0.86), 0.05, "potion_green", k, 1, 1)
    # Herbs drying from the front beam.
    timber("beam", (-1.6, -1.0, 2.1), (1.6, -1.0, 2.1), 0.12)
    timber("beam", (-1.6, 1.0, 2.6), (1.6, 1.0, 2.6), 0.12)
    for k, x in enumerate((-1.1, -0.6, 0.6, 1.1)):
        timber("herb_cord", (x, -0.95, 2.05), (x, -0.95, 1.8), 0.015, "wood_dark")
        blob("herbs", (x, -0.95, 1.7), 0.12, "leaf" if k % 2 == 0 else "moss", k + 11, 0.8, 1)
    strips = 8
    for k in range(strips):
        x0 = -1.75 + 3.5 * k / strips
        x1 = -1.75 + 3.5 * (k + 1) / strips
        verts = [(x0, -1.45, 2.12), (x1, -1.45, 2.12), (x1, 1.15, 2.72), (x0, 1.15, 2.72)]
        top = [(x, y, z + 0.03) for x, y, z in verts]
        faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        add_object("awning", mesh_from("awning", verts + top, faces), "cloth_purple" if k % 2 == 0 else "paper")
        flap = [(x0, -1.46, 2.12), (x1, -1.46, 2.12), (x1, -1.46, 1.95), ((x0 + x1) / 2, -1.46, 1.84), (x0, -1.46, 1.95)]
        add_object("valance", mesh_from("valance", flap, [(0, 1, 2, 3, 4)]), "cloth_purple" if k % 2 == 0 else "paper")
    # The board: a red flask painted on gold.
    box("sign", (0, -1.08, 2.0), (1.4, 0.06, 0.32), "wood_dark")
    box("sign_face", (0, -1.12, 2.0), (1.2, 0.03, 0.2), "gold")
    box("sign_flask", (0, -1.14, 1.97), (0.12, 0.02, 0.1), "potion_red")
    box("sign_neck", (0, -1.14, 2.06), (0.04, 0.02, 0.07), "potion_red")


def storage_chest():
    """A sturdy chest for an adventurer's things: oak planks, a rounded lid, iron bands and corners,
    and a heavy lock."""
    box("chest_body", (0, 0, 0.3), (1.1, 0.62, 0.6), "wood")
    for x in (-0.36, 0.0, 0.36):
        box("plank_line", (x, -0.315, 0.3), (0.02, 0.01, 0.58), "wood_dark")
    for k in range(9):
        x0 = -0.55 + 1.1 * k / 9
        box("lid", (x0 + 0.061, 0, 0.66), (0.122, 0.62, 0.12), "wood")
    box("lid_top", (0, 0, 0.75), (1.1, 0.44, 0.08), "wood")
    for x in (-0.42, 0.42):
        box("band", (x, 0, 0.45), (0.06, 0.66, 0.66), "iron")
        box("band_top", (x, 0, 0.8), (0.06, 0.46, 0.06), "iron")
    for x in (-0.55, 0.55):
        for y in (-0.31, 0.31):
            box("corner", (x, y, 0.06), (0.08, 0.08, 0.12), "iron")
    box("lock", (0, -0.33, 0.55), (0.14, 0.04, 0.16), "gold")
    box("keyhole", (0, -0.355, 0.54), (0.03, 0.01, 0.06), "iron")


# --- The watchtower ------------------------------------------------------------------------------

def watchtower():
    """A timber watchtower: four splayed legs on stone footings, braced in two stages, a boarded
    platform with a plank parapet, a ladder through a hatch, and a thick hipped roof."""
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    for sx, sy in corners:
        box("footing", (sx * 1.95, sy * 1.95, 0.1), (0.6, 0.6, 0.4), "stone")
        timber("leg", (sx * 1.95, sy * 1.95, 0.2), (sx * 1.5, sy * 1.5, 6.4), 0.3)
    for z0, z1, inset in ((0.7, 3.4, 1.85), (3.5, 6.1, 1.62)):
        for (ax, ay), (bx, by) in zip(corners, corners[1:] + corners[:1]):
            timber("brace", (ax * inset, ay * inset, z0), (bx * (inset - 0.12), by * (inset - 0.12), z1), 0.13, "wood_dark")
            timber("brace", (bx * inset, by * inset, z0), (ax * (inset - 0.12), ay * (inset - 0.12), z1), 0.13, "wood_dark")
            timber("girt", (ax * (inset - 0.1), ay * (inset - 0.1), z1 - 0.05), (bx * (inset - 0.1), by * (inset - 0.1), z1 - 0.05), 0.14)
    box("deck", (0, 0, 6.42), (4.0, 4.0, 0.18), "wood")
    for k in range(-1, 2):
        timber("joist", (-1.9, k * 1.3, 6.25), (1.9, k * 1.3, 6.25), 0.18, "wood_dark")
    # Plank parapet on three sides, open where the ladder arrives.
    for side, (dx, dy) in enumerate(((0, -1), (1, 0), (0, 1), (-1, 0))):
        if side == 3:
            continue
        for k in range(13):
            u = -1.9 + 3.8 * k / 12
            x, y = (u, dy * 1.95) if dx == 0 else (dx * 1.95, u)
            box("paling", (x, y, 6.95), (0.26, 0.06, 0.9) if dx == 0 else (0.06, 0.26, 0.9), "wood")
        a, b = ((-2.0, dy * 1.98), (2.0, dy * 1.98)) if dx == 0 else ((dx * 1.98, -2.0), (dx * 1.98, 2.0))
        timber("parapet_rail", (a[0], a[1], 7.4), (b[0], b[1], 7.4), 0.12, "wood_dark")
    timber("parapet_rail", (-1.98, -2.0, 7.4), (-1.98, -0.6, 7.4), 0.12, "wood_dark")
    for sx, sy in corners:
        timber("post", (sx * 1.9, sy * 1.9, 6.5), (sx * 1.9, sy * 1.9, 9.0), 0.16)
    # Ladder up the open west side to a hatch.
    for y in (0.0, 0.55):
        timber("ladder_rail", (-2.9, y, 0.0), (-1.95, y, 6.5), 0.08, "wood_dark")
    for k in range(17):
        t = (k + 0.5) / 17
        timber("rung", (-2.9 + 0.95 * t, -0.02, 6.5 * t), (-2.9 + 0.95 * t, 0.57, 6.5 * t), 0.05, "wood")
    # Hipped roof with real thickness, four slopes meeting at a peak.
    peak = (0, 0, 10.6)
    eaves = [(-2.5, -2.5, 8.85), (2.5, -2.5, 8.85), (2.5, 2.5, 8.85), (-2.5, 2.5, 8.85)]
    for a, b in zip(eaves, eaves[1:] + eaves[:1]):
        lower = [a, b, peak]
        upper = [(x, y, z + 0.18) for x, y, z in lower]
        add_object("roof", mesh_from("roof", lower + upper, [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)]), "thatch")
        timber("hip", a, peak, 0.12, "wood_dark")
    timber("finial", (0, 0, 10.7), (0, 0, 11.3), 0.07, "wood_dark")


# --- Ruins ---------------------------------------------------------------------------------------

def ruin_arch():
    """A broken arch of the old road: two piers of coursed blocks, a ring of voussoirs with a gap
    where three stones beside the crown have fallen, the stones lying where they dropped, and moss."""
    rng = random.Random(14)
    for x in (-2.6, 2.6):
        box("plinth", (x, 0, 0.2), (1.9, 1.9, 0.5), "stone_dark")
        courses = 9 if x < 0 else 8
        for k in range(courses):
            z = 0.45 + k * 0.45 + 0.22
            long_x = k % 2 == 0
            for j in (-1, 1):
                if long_x:
                    coursed_block("pier", (x, j * 0.36, z), (1.5, 0.72, 0.42), rng)
                else:
                    coursed_block("pier", (x + j * 0.38, 0, z), (0.74, 1.46, 0.42), rng)
        box("impost", (x, 0, 0.45 + courses * 0.45 + 0.1), (1.7, 1.7, 0.22), "stone_dark")
    # The ring springs from the imposts at 4.6 m and would close at the crown.
    span, centre_z, r = 2.6, 4.6, 2.6
    stones = 13
    for k in range(stones):
        a = math.pi - math.pi * (k + 0.5) / stones
        if k in (7, 8, 9):
            continue  # three stones beside the crown have fallen; the stubs still stand
        inner = (math.cos(a) * (r - 0.55), 0, centre_z + math.sin(a) * (r - 0.55))
        outer = (math.cos(a) * (r + 0.3), 0, centre_z + math.sin(a) * (r + 0.3))
        timber("voussoir", inner, outer, 0.62 if k % 2 else 0.56, "stone")
    for k, (x, y, yaw) in enumerate(((3.9, 1.2, 0.4), (4.6, -0.6, 1.2), (3.3, -1.5, 2.0), (1.6, 1.6, 0.8))):
        timber("fallen", (x - 0.4 * math.cos(yaw), y - 0.4 * math.sin(yaw), 0.3), (x + 0.4 * math.cos(yaw), y + 0.4 * math.sin(yaw), 0.3 + k * 0.03), 0.55, "stone")
    for x, z in ((-2.6, 4.84), (-1.9, 6.6), (2.6, 4.44)):
        blob("moss", (x, 0, z), 0.4, "moss", int(z * 10), 0.3, 1)
    blob("rubble", (2.0, -1.2, 0.15), 0.45, "stone_dark", 5, 0.5, 1)


def ruin_wall():
    """A stretch of fallen wall: courses that break off unevenly, a window opening with its sill, and
    blocks lying at its foot."""
    rng = random.Random(23)
    heights = [5, 5, 4, 4, 4, 3, 3, 2, 2, 1, 1, 1]
    for column, height in enumerate(heights):
        for row in range(height):
            x = -2.75 + column * 0.5 + (0.25 if row % 2 else 0)
            if x > 2.9:
                continue
            if 1 <= row <= 2 and -1.4 <= x <= -0.8:
                continue  # the window opening
            coursed_block("block", (x, 0, 0.32 + row * 0.62), (0.5, 0.8, 0.6), rng, "stone" if rng.random() > 0.25 else "stone_dark")
    box("sill", (-1.1, 0, 0.62), (0.9, 0.95, 0.12), "stone_dark")
    box("lintel_stub", (-1.4, 0, 1.92), (0.5, 0.85, 0.2), "stone_dark")
    for k, (x, y) in enumerate(((1.8, 0.9), (2.5, 1.3), (0.9, -1.0), (2.2, -0.8))):
        coursed_block("fallen", (x, y, 0.25), (0.5, 0.75, 0.5), rng)
    for x in (-2.4, -1.6):
        blob("moss", (x, 0, 3.15), 0.35, "moss", int(x * -10), 0.3, 1)


def ruin():
    """The stump of an old round tower: courses of blocks that end raggedly, with fallen stones."""
    rng = random.Random(31)
    loft("ruin_footing", [(-0.3, 1.25, 1.25), (0.3, 1.2, 1.2)], "stone_dark", 14)
    stones = 10
    for row in range(8):
        top_missing = row - 4
        for k in range(stones):
            if top_missing > 0 and (k * 7 + row * 3) % 10 < top_missing * 2.5:
                continue
            a = (k + (0.5 if row % 2 else 0)) / stones * math.tau
            x, y, z = math.cos(a), math.sin(a), 0.55 + row * 0.42
            # Each block lies along the curve of the wall.
            tx, ty = -math.sin(a) * 0.32, math.cos(a) * 0.32
            timber("course", (x - tx, y - ty, z + rng.uniform(-0.02, 0.02)), (x + tx, y + ty, z), 0.4 * rng.uniform(0.94, 1.0),
                   "stone" if rng.random() > 0.2 else "stone_dark")
    loft("ruin_core", [(0.3, 0.8, 0.8), (2.4, 0.8, 0.8)], "stone_dark", 12)
    for k, (x, y) in enumerate(((1.6, 0.6), (1.3, -0.9), (-1.5, 1.0))):
        coursed_block("fallen", (x, y, 0.22), (0.6, 0.45, 0.42), rng)
    blob("moss", (0.4, 0.6, 3.5), 0.4, "moss", 9, 0.3, 1)
