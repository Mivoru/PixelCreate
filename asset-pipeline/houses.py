"""Village houses: timber-framed walls laid out around their openings, framed casement windows,
thick pitched roofs with framed gables, and a lean-to. Used by build_meshes.py.

The frame follows the openings, so no brace ever crosses a window, and every window and shutter is
checked to fit inside its wall. Blender axes: the front of a house faces -y, up is z.
"""
import math

import mathutils

# build_meshes.py hands over its drawing helpers through bind().
box = timber = blob = add_object = mesh_from = round_log = None


def bind(helpers):
    globals().update(helpers)


def wall_point(facing, centre, u, n, z):
    """A point on a wall: u runs along the wall (left to right seen from outside), n points out of it."""
    cx, cy = centre
    if facing == "-y":
        return (cx + u, cy - n, z)
    if facing == "+y":
        return (cx - u, cy + n, z)
    if facing == "-x":
        return (cx - n, cy - u, z)
    return (cx + n, cy + u, z)


def wall_box(name, facing, centre, u, n, z, size, material):
    """A box set against a wall; size is (along the wall, out of the wall, up)."""
    along, out, up = size
    dims = (along, out, up) if facing in ("-y", "+y") else (out, along, up)
    box(name, wall_point(facing, centre, u, n, z), dims, material)


SHUTTER = 0.42
FRAME = 0.1


def window_half(width, shutters):
    """Half the wall a window takes, measured along the wall, with its frame and shutters."""
    return width / 2 + FRAME + (SHUTTER + 0.06 if shutters else 0.0)


def window(facing, centre, u, z, width=0.9, height=1.0, shutters=True, planter=False, stone=False):
    """A framed casement window: glass just in front of the wall behind a timber frame, a cross of
    mullion and transom, a sill below and a lintel above, with optional shutters and a flower box.
    centre is a point on the wall face; u and z place the middle of the glass."""
    wall_box("glass", facing, centre, u, 0.02, z, (width, 0.03, height), "pane")
    for side in (-1, 1):
        wall_box("frame", facing, centre, u + side * (width + FRAME) / 2, 0.05, z, (FRAME, 0.1, height + FRAME * 2), "wood_dark")
    wall_box("frame", facing, centre, u, 0.05, z + (height + FRAME) / 2, (width, 0.1, FRAME), "wood_dark")
    wall_box("mullion", facing, centre, u, 0.05, z, (0.05, 0.07, height), "wood_dark")
    wall_box("transom", facing, centre, u, 0.05, z + height * 0.12, (width, 0.07, 0.05), "wood_dark")
    trim = "stone_dark" if stone else "wood_dark"
    wall_box("sill", facing, centre, u, 0.1, z - height / 2 - FRAME - 0.05, (width + 0.34, 0.2, 0.1 if stone else 0.08), trim)
    wall_box("lintel", facing, centre, u, 0.06, z + height / 2 + FRAME + 0.08, (width + 0.4, 0.12, 0.16), trim)
    if shutters:
        for side in (-1, 1):
            offset = u + side * (width / 2 + FRAME + 0.03 + SHUTTER / 2)
            wall_box("shutter", facing, centre, offset, 0.06, z, (SHUTTER, 0.05, height + 0.1), "shutter")
            for k in (-1, 1):
                wall_box("shutter_batten", facing, centre, offset, 0.09, z + k * height * 0.3, (SHUTTER - 0.04, 0.03, 0.07), "wood_dark")
    if planter:
        wall_box("planter", facing, centre, u, 0.26, z - height / 2 - 0.3, (width + 0.16, 0.3, 0.22), "wood")
        for k, offset in enumerate((-0.32, -0.1, 0.12, 0.32)):
            blob(f"bloom{k}", wall_point(facing, centre, u + offset * width / 0.9, 0.26, z - height / 2 - 0.13), 0.11,
                 "flower" if k % 2 else "flower_b", k + int(u * 10) + 40, 0.8, 1)


def roof_base(top, depth, rise, overhang):
    """Where the slopes would meet the wall line: high enough that the eave, hanging past the wall,
    ends just under the wall plate instead of hiding the tops of the windows."""
    return top + overhang * rise / (depth / 2) - 0.15


def gabled_roof(top, width, depth, rise, overhang, material, thickness=0.2):
    """A pitched roof with real thickness and a ridge along x, hanging past every wall."""
    w, d = width / 2 + overhang, depth / 2 + overhang
    slope = math.atan2(rise, depth / 2)
    drop = overhang * math.tan(slope)
    top = roof_base(top, depth, rise, overhang)
    for side in (-1, 1):
        lower = [(x, y, z) for x in (-w, w) for (y, z) in ((0.0, top + rise), (side * d, top - drop))]
        normal = mathutils.Vector((0, side * math.sin(slope), math.cos(slope)))
        upper = [tuple(mathutils.Vector(v) + normal * thickness) for v in lower]
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (1, 5, 7, 3), (0, 2, 6, 4)]
        add_object("roof", mesh_from("roof", lower + upper, faces), material)
        timber("fascia", (-w, side * d, top - drop - 0.04), (w, side * d, top - drop - 0.04), 0.12, "wood_dark")
    timber("ridge", (-w, 0, top + rise + thickness), (w, 0, top + rise + thickness), 0.16, "wood_dark")


def gable_wall(x, depth, top, rise, wall, outward, overhang=0.55):
    """The wall under a gable end, up to the underside of the roof, framed with rafters, a king
    post and a collar."""
    d = depth / 2
    base = roof_base(top, depth, rise, overhang)
    verts = [(x, -d, top), (x, d, top), (x, d, base), (x, 0, base + rise), (x, -d, base)]
    add_object("gable", mesh_from("gable", verts, [(0, 1, 2, 3, 4) if outward > 0 else (4, 3, 2, 1, 0)]), wall)
    face = x + outward * 0.05
    timber("king_post", (face, 0, top), (face, 0, base + rise - 0.1), 0.14)
    timber("collar", (face, -d * 0.5, base + rise * 0.5), (face, d * 0.5, base + rise * 0.5), 0.12)
    timber("rafter", (face, -d, base), (face, 0, base + rise), 0.13)
    timber("rafter", (face, d, base), (face, 0, base + rise), 0.13)


class Wall:
    """One wall of a house: where its openings are, so the frame can be laid out around them."""

    def __init__(self, facing, centre, length):
        self.facing, self.centre, self.length = facing, centre, length
        self.openings = []  # (u, half width, bottom, top)

    def fits(self, u, half, low, high):
        """True when an opening of this half width at u, from low to high, stays on the wall and
        clear of the openings beside, above or below it."""
        if abs(u) + half > self.length / 2 - 0.12:
            return False
        return all(abs(u - other) > half + other_half + 0.2 or high < other_low or low > other_high
                   for other, other_half, other_low, other_high in self.openings)

    def add_window(self, u, z, shutters=True, planter=False, height=1.0):
        low, high = z - height / 2 - 0.45, z + height / 2 + 0.2
        if not self.fits(u, window_half(0.9, shutters), low, high):
            if shutters and self.fits(u, window_half(0.9, False), low, high):
                shutters = False
            else:
                return False
        window(self.facing, self.centre, u, z, shutters=shutters, planter=planter, height=height)
        self.openings.append((u, window_half(0.9, shutters), low, high))
        return True

    def frame(self, base, top, floors):
        """Posts at the corners and beside openings, rails at each floor, braces in empty panels."""
        half = self.length / 2
        posts = {-half, half}
        for u, hw, _, _ in self.openings:
            posts |= {u - hw - 0.08, u + hw + 0.08}
        posts = sorted(p for p in posts if abs(p) <= half)
        height = top - base
        for u in posts:
            wall_box("post", self.facing, self.centre, u, 0.05, base + height / 2, (0.16, 0.1, height), "wood")
        storey = height / floors
        for z in [base + 0.06, top - 0.06] + [base + storey * f for f in range(1, floors)]:
            wall_box("rail", self.facing, self.centre, 0, 0.05, z, (self.length + 0.16, 0.1, 0.15), "wood")
        for floor in range(floors):
            bottom, ceiling = base + floor * storey + 0.1, base + (floor + 1) * storey - 0.1
            for a, b in zip(posts, posts[1:]):
                mid = (a + b) / 2
                blocked = any(abs(mid - u) < hw + 0.1 and low < ceiling and high > bottom for u, hw, low, high in self.openings)
                if b - a < 0.6 or blocked:
                    continue
                # Lean the brace towards the nearer corner, the way a frame is stiffened.
                low_u, high_u = (a, b) if mid < 0 else (b, a)
                timber("brace", wall_point(self.facing, self.centre, low_u, 0.06, bottom),
                       wall_point(self.facing, self.centre, high_u, 0.06, ceiling), 0.11, "wood")


def house(width, depth, height, wall, roof_material, door_x=0.0, floors=1, closed_sides=()):
    """A timber-framed house on a stone footing with a framed door, windows on every free wall and
    a thick roof over framed gables. Returns the height of the wall plate and the roof rise."""
    w, d = width / 2, depth / 2
    base = 0.4
    top = height + base
    box("footing", (0, 0, -0.3), (width + 0.3, depth + 0.3, 1.4), "stone_dark")
    box("walls", (0, 0, height / 2 + base), (width, depth, height), wall)
    storey = height / floors
    door_w = 1.1
    sill = 0.58

    front = Wall("-y", (0, -d), width)
    front.openings.append((door_x, door_w / 2 + 0.1, base, base + 2.3))
    for floor in range(floors):
        z = base + floor * storey + storey * sill
        if floor == 0:
            # A window in the middle of each stretch of wall beside the door.
            for a, b in ((-w, door_x - door_w / 2 - 0.1), (door_x + door_w / 2 + 0.1, w)):
                front.add_window((a + b) / 2, z, planter=True)
        else:
            count = max(1, int(width // 2.4))
            for k in range(count):
                front.add_window(-w + width * (k + 0.5) / count, z, shutters=False)

    # The door: boards in a timber frame, iron straps, a stone step.
    wall_box("door", "-y", front.centre, door_x, 0.03, base + 1.05, (door_w - 0.1, 0.06, 2.1), "wood")
    for k in (-1, 1):
        wall_box("door_frame", "-y", front.centre, door_x + k * door_w / 2, 0.07, base + 1.1, (0.12, 0.12, 2.2), "wood_dark")
    wall_box("door_head", "-y", front.centre, door_x, 0.08, base + 2.24, (door_w + 0.3, 0.14, 0.16), "wood_dark")
    for z in (0.5, 1.6):
        wall_box("door_strap", "-y", front.centre, door_x, 0.07, base + z, (door_w - 0.2, 0.03, 0.08), "iron")
    wall_box("handle", "-y", front.centre, door_x + 0.32, 0.1, base + 1.0, (0.08, 0.06, 0.08), "gold")
    box("step", (door_x, -d - 0.45, 0.16), (1.5, 0.9, 0.32), "stone")

    back = Wall("+y", (0, d), width)
    left = Wall("-x", (-w, 0), depth)
    right = Wall("+x", (w, 0), depth)
    for floor in range(floors):
        z = base + floor * storey + storey * sill
        count = max(1, int(width // 3.2))
        for k in range(count):
            back.add_window(-w + width * (k + 0.5) / count, z, shutters=False)
        for side in (left, right):
            if side.facing not in closed_sides:
                side.add_window(0, z, shutters=False)
    for side in (front, back, left, right):
        side.frame(base, top, floors)

    rise = depth * 0.55
    gabled_roof(top, width, depth, rise, 0.55, roof_material)
    gable_wall(-w, depth, top, rise, wall, -1)
    gable_wall(w, depth, top, rise, wall, 1)
    cx, cy = width * 0.28, depth * 0.18
    peak = roof_base(top, depth, rise, 0.55) + rise
    box("chimney", (cx, cy, (top + peak + 0.9) / 2), (0.8, 0.8, peak + 0.9 - top), "stone")
    box("chimney_cap", (cx, cy, peak + 0.95), (1.0, 1.0, 0.14), "stone_dark")
    return top, rise


def cottage():
    house(6.4, 4.8, 2.6, "wall", "roof")


def cottage_b():
    """A thatched cottage with a plank lean-to against its east gable for tools and firewood."""
    house(5.6, 4.6, 2.5, "wall_b", "thatch", 0.9, closed_sides=("+x",))
    x0, x1 = 2.8, 4.7
    eave, high = 1.95, 2.8
    box("lean_footing", ((x0 + x1) / 2, 0.2, 0.0), (x1 - x0 + 0.2, 3.4, 0.5), "stone_dark")
    # Plank walls on three sides; the front stays open to show the woodpile.
    box("lean_back", ((x0 + x1) / 2, 1.78, 1.2), (x1 - x0, 0.08, 1.9), "wood")
    box("lean_side", (x1 - 0.04, 0.2, 1.2), (0.08, 3.2, 1.9), "wood")
    for y in (-1.38, 1.78):
        timber("lean_post", (x1, y, 0.25), (x1, y, eave), 0.14)
    timber("lean_plate", (x1, -1.55, eave), (x1, 1.95, eave), 0.13)
    timber("lean_plate", (x0 + 0.05, -1.55, high - 0.1), (x0 + 0.05, 1.95, high - 0.1), 0.13)
    # The roof falls away from the house and hangs over the open front.
    verts = [(x0, -1.8, high), (x0, 2.1, high), (x1 + 0.35, 2.1, eave - 0.15), (x1 + 0.35, -1.8, eave - 0.15)]
    top_verts = [(x, y, z + 0.16) for x, y, z in verts]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 3, 7, 4), (1, 5, 6, 2), (0, 4, 5, 1), (3, 2, 6, 7)]
    add_object("lean_roof", mesh_from("lean_roof", verts + top_verts, faces), "thatch")
    # Split logs stacked inside.
    for row in range(3):
        for k in range(5 - row):
            y = -0.9 + 0.16 * row + k * 0.32
            z = 0.4 + row * 0.27
            round_log("log", (x0 + 0.35, y, z), (x1 - 0.35, y, z), 0.13)


def chapel():
    """The village chapel: a stone nave on a footing with buttresses, pointed windows down both
    sides, an arched door and a round window in the front gable, a steep roof and a small timbered
    bell turret on the ridge with its bell. Built with the nave along x, then turned so the door
    faces -y like every other building."""
    import bpy

    length, width, height = 9.0, 5.4, 4.2
    l, w = length / 2, width / 2
    base = 0.4
    top = base + height
    box("footing", (0, 0, -0.3), (length + 0.4, width + 0.4, 1.4), "stone_dark")
    box("walls", (0, 0, base + height / 2), (length, width, height), "stone")
    # Quoins: darker dressed stones up every corner.
    for x in (-l, l):
        for y in (-w, w):
            for k in range(5):
                z = base + 0.35 + k * 0.85
                long = k % 2 == 0
                box("quoin", (x - (0.25 if long else 0.1) * (1 if x > 0 else -1) + (0.04 if x > 0 else -0.04), y + (0.04 if y > 0 else -0.04),
                              z), (0.6 if long else 0.3, 0.3 if long else 0.6, 0.38), "stone_dark")
    # Buttresses between the windows, stepping in near the top.
    for x in (-1.5, 1.5):
        for side in (-1, 1):
            box("buttress", (x, side * (w + 0.3), base + 1.3), (0.55, 0.6, 2.6), "stone_dark")
            box("buttress_top", (x, side * (w + 0.18), base + 3.0), (0.5, 0.36, 1.0), "stone_dark")
    # Pointed windows: glass, a stone surround and a sill, three a side.
    for x in (-3.0, 0.0, 3.0):
        for side in (-1, 1):
            y = side * (w + 0.02)
            bottom, shoulder, peak = base + 1.6, base + 3.0, base + 3.6
            verts = [(x - 0.4, y, bottom), (x + 0.4, y, bottom), (x + 0.4, y, shoulder), (x, y, peak), (x - 0.4, y, shoulder)]
            add_object("glass", mesh_from("glass", verts, [(0, 1, 2, 3, 4) if side < 0 else (4, 3, 2, 1, 0)]), "pane")
            for a, b in ((verts[0], verts[4]), (verts[1], verts[2]), (verts[2], verts[3]), (verts[4], verts[3])):
                timber("surround", (a[0], y + side * 0.04, a[2]), (b[0], y + side * 0.04, b[2]), 0.14, "stone_dark")
            timber("mullion", (x, y + side * 0.03, bottom), (x, y + side * 0.03, peak - 0.1), 0.06, "stone_dark")
            box("sill", (x, y + side * 0.1, bottom - 0.08), (1.1, 0.24, 0.12), "stone_dark")
    # Gable ends of stone up to the roof.
    rise = width * 0.8
    overhang = 0.4
    roof_top = roof_base(top, width, rise, overhang)
    for x, out in ((-l, -1), (l, 1)):
        verts = [(x, -w, top), (x, w, top), (x, w, roof_top), (x, 0, roof_top + rise), (x, -w, roof_top)]
        add_object("gable", mesh_from("gable", verts, [(0, 1, 2, 3, 4) if out > 0 else (4, 3, 2, 1, 0)]), "stone")
    gabled_roof(top, length, width, rise, overhang, "roof_slate", 0.22)
    # The front (-x) gable: an arched door of boards with iron straps, a stone step, a round window.
    fx = -l - 0.03
    box("door", (fx, 0, base + 1.1), (0.06, 1.3, 2.2), "wood")
    for z in (0.6, 1.7):
        box("strap", (fx - 0.04, 0, base + z), (0.03, 1.1, 0.08), "iron")
    box("ring", (fx - 0.06, 0.4, base + 1.1), (0.04, 0.1, 0.1), "iron")
    for k in range(9):
        a = math.pi * k / 8
        blob(f"arch{k}", (fx - 0.06, math.cos(a) * 0.82, base + 2.2 + math.sin(a) * 0.62), 0.17, "stone_dark", 300 + k, 1.0, 1)
    for side in (-1, 1):
        box("jamb", (fx - 0.06, side * 0.82, base + 1.1), (0.16, 0.24, 2.2), "stone_dark")
    box("step", (-l - 0.7, 0, 0.16), (1.0, 1.9, 0.32), "stone")
    box("step2", (-l - 1.3, 0, 0.05), (0.6, 2.2, 0.12), "stone")
    rose_z = top + 1.0
    add_object("rose", mesh_from("rose", [(fx - 0.01, math.cos(a) * 0.5, rose_z + math.sin(a) * 0.5) for a in
                                          [i * math.tau / 12 for i in range(12)]], [tuple(range(11, -1, -1))]), "pane")
    for i in range(12):
        a, b = i * math.tau / 12, (i + 1) * math.tau / 12
        timber("rose_ring", (fx - 0.05, math.cos(a) * 0.56, rose_z + math.sin(a) * 0.56), (fx - 0.05, math.cos(b) * 0.56, rose_z + math.sin(b) * 0.56), 0.1, "stone_dark")
    for i in range(3):
        a = i * math.pi / 3
        timber("rose_bar", (fx - 0.04, math.cos(a) * 0.5, rose_z + math.sin(a) * 0.5), (fx - 0.04, -math.cos(a) * 0.5, rose_z - math.sin(a) * 0.5), 0.05, "stone_dark")
    # The bell turret over the front gable: four posts on the ridge, open sides, a pyramid roof, the bell.
    tx, peak = -l + 0.9, roof_top + rise
    for dx in (-0.45, 0.45):
        for dy in (-0.45, 0.45):
            timber("turret_post", (tx + dx, dy, peak - 0.6), (tx + dx, dy, peak + 1.4), 0.12, "wood_dark")
    for z in (peak + 0.05, peak + 1.4):
        for dy in (-0.45, 0.45):
            timber("turret_rail", (tx - 0.5, dy, z), (tx + 0.5, dy, z), 0.1, "wood_dark")
        for dx in (-0.45, 0.45):
            timber("turret_rail", (tx + dx, -0.5, z), (tx + dx, 0.5, z), 0.1, "wood_dark")
    box("turret_floor", (tx, 0, peak), (1.0, 1.0, 0.12), "wood")
    pyramid = [(tx - 0.75, -0.75, peak + 1.45), (tx + 0.75, -0.75, peak + 1.45), (tx + 0.75, 0.75, peak + 1.45), (tx - 0.75, 0.75, peak + 1.45), (tx, 0, peak + 2.6)]
    add_object("turret_roof", mesh_from("turret_roof", pyramid, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4), (3, 2, 1, 0)]), "roof_slate")
    timber("bell_beam", (tx, -0.45, peak + 1.25), (tx, 0.45, peak + 1.25), 0.08, "wood_dark")
    blob("bell", (tx, 0, peak + 0.85), 0.3, "gold", 320, 1.25, 2)
    timber("cross", (tx, 0, peak + 2.55), (tx, 0, peak + 3.3), 0.08, "iron")
    timber("cross_arm", (tx, -0.22, peak + 3.05), (tx, 0.22, peak + 3.05), 0.07, "iron")
    # A stone cross on the back gable.
    timber("back_cross", (l + 0.1, 0, roof_top + rise + 0.1), (l + 0.1, 0, roof_top + rise + 0.9), 0.16, "stone_dark")
    timber("back_arm", (l + 0.1, -0.28, roof_top + rise + 0.62), (l + 0.1, 0.28, roof_top + rise + 0.62), 0.14, "stone_dark")
    # Turn the door to face -y.
    bpy.context.view_layer.update()  # fresh objects carry stale matrices until then
    turn = mathutils.Matrix.Rotation(math.radians(90), 4, "Z")
    for obj in bpy.context.scene.objects:
        obj.matrix_world = turn @ obj.matrix_world


def smithy():
    """Tess's smithy: an open-fronted workshop under a lean-to roof of planks on heavy posts, a stone
    forge with a glowing hearth and a chimney, an anvil on a stump, a quench barrel, a grindstone and
    tools hung on the back wall. The open side is the front (-y)."""
    import bpy

    w, d = 5.2, 4.0
    box("floor", (0, 0, -0.14), (w + 0.3, d + 0.3, 0.5), "stone_dark")
    low, high = 2.5, 3.4
    for x in (-w / 2, w / 2):
        timber("post_front", (x, -d / 2, 0.1), (x, -d / 2, low), 0.22, "wood_dark")
        timber("post_back", (x, d / 2, 0.1), (x, d / 2, high), 0.22, "wood_dark")
        timber("tie", (x, -d / 2, low - 0.1), (x, d / 2, high - 0.1), 0.16, "wood_dark")
    timber("beam_front", (-w / 2 - 0.2, -d / 2, low), (w / 2 + 0.2, -d / 2, low), 0.2, "wood_dark")
    timber("beam_back", (-w / 2 - 0.2, d / 2, high), (w / 2 + 0.2, d / 2, high), 0.2, "wood_dark")
    # The roof slopes down to the front and hangs over it.
    verts = [(-w / 2 - 0.4, -d / 2 - 0.6, low - 0.05), (w / 2 + 0.4, -d / 2 - 0.6, low - 0.05), (w / 2 + 0.4, d / 2 + 0.3, high + 0.12), (-w / 2 - 0.4, d / 2 + 0.3, high + 0.12)]
    top = [(x, y, z + 0.14) for x, y, z in verts]
    faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    add_object("roof", mesh_from("roof", verts + top, faces), "wood")
    for k in range(7):
        x = -w / 2 + 0.2 + k * (w - 0.4) / 6
        timber("batten", (x, -d / 2 - 0.55, low + 0.12), (x, d / 2 + 0.25, high + 0.29), 0.05, "wood_dark")
    # Back wall of planks and a half wall on the left; the right stays open to the street.
    box("back_wall", (0, d / 2 - 0.05, high / 2), (w, 0.1, high), "wood")
    box("side_wall", (-w / 2 + 0.05, 0.4, 0.6), (0.1, d - 0.8, 1.2), "wood")
    # The forge: stone hearth with embers, a hood and a chimney through the roof.
    fx, fy = -1.4, 1.2
    box("forge", (fx, fy, 0.5), (1.6, 1.2, 1.0), "stone")
    box("hearth", (fx, fy - 0.1, 1.02), (1.0, 0.7, 0.06), "ember")
    for k in range(5):
        blob(f"coal{k}", (fx - 0.3 + k * 0.15, fy - 0.1, 1.07), 0.08, "char" if k % 2 else "ember", 700 + k, 0.7, 1)
    box("hood", (fx, fy + 0.1, 2.0), (1.4, 1.0, 0.5), "stone_dark")
    box("chimney", (fx, fy + 0.2, 3.2), (0.6, 0.6, 2.4), "stone")
    box("chimney_cap", (fx, fy + 0.2, 4.45), (0.8, 0.8, 0.12), "stone_dark")
    timber("bellows", (fx + 1.0, fy, 0.75), (fx + 1.5, fy - 0.3, 0.85), 0.28, "hide")
    # Anvil on a stump, a quench barrel, a grindstone.
    ax, ay = 0.6, -0.6
    timber("stump", (ax, ay, 0.1), (ax, ay, 0.65), 0.5, "wood")
    box("anvil_foot", (ax, ay, 0.72), (0.34, 0.24, 0.14), "iron")
    box("anvil_body", (ax, ay, 0.86), (0.22, 0.18, 0.16), "iron")
    box("anvil_face", (ax, ay, 0.98), (0.62, 0.24, 0.1), "iron")
    blob("anvil_horn", (ax + 0.38, ay, 0.98), 0.1, "iron", 710, 0.6, 1)
    timber("barrel", (1.8, 1.2, 0.1), (1.8, 1.2, 0.8), 0.6, "wood")
    box("water", (1.8, 1.2, 0.78), (0.42, 0.42, 0.02), "water")
    timber("wheel_axle", (1.7, -0.2, 0.7), (2.1, -0.2, 0.7), 0.06, "wood_dark")
    timber("wheel", (1.9, -0.2, 0.35), (1.9, -0.2, 1.05), 0.12, "stone")
    for side in (-1, 1):
        timber("wheel_leg", (1.9 + side * 0.25, -0.35, 0.1), (1.9 + side * 0.25, -0.2, 0.72), 0.06, "wood_dark")
    # Tools on the back wall.
    for k, (x, length) in enumerate(((0.3, 0.7), (0.6, 0.6), (0.9, 0.75), (1.2, 0.5))):
        timber("handle", (x, d / 2 - 0.14, 1.9), (x, d / 2 - 0.14, 1.9 - length), 0.04, "wood_dark")
        box("head", (x, d / 2 - 0.14, 1.9 - length), (0.16, 0.08, 0.08), "iron")
    box("rack", (0.75, d / 2 - 0.12, 1.92), (1.4, 0.06, 0.08), "wood_dark")


def laundry_line():
    """Washing hung out to dry: two T-shaped posts and a sagging line between, with sheets, shirts and
    a pair of trousers pegged along it."""
    span = 4.0
    for x in (-span / 2, span / 2):
        timber("post", (x, 0, 0), (x, 0, 2.0), 0.1, "wood_dark")
        timber("arm", (x, -0.3, 1.95), (x, 0.3, 1.95), 0.07, "wood_dark")
    for y in (-0.25, 0.25):
        for k in range(6):
            x0 = -span / 2 + k * span / 6
            x1 = x0 + span / 6
            sag = lambda x: 1.95 - 0.18 * (1 - (2 * x / span) ** 2)
            timber("line", (x0, y, sag(x0)), (x1, y, sag(x1)), 0.015, "sack")
    for x, y, width, length, color in ((-1.3, -0.25, 0.8, 0.9, "paper"), (-0.25, -0.25, 0.5, 0.55, "cloth_purple"), (0.6, -0.25, 0.7, 0.9, "cloth_red"),
                                       (-0.8, 0.25, 0.45, 0.6, "sack"), (0.2, 0.25, 0.9, 1.0, "paper"), (1.25, 0.25, 0.4, 0.75, "anime_trousers")):
        z = 1.95 - 0.18 * (1 - (2 * x / span) ** 2)
        box("cloth", (x, y, z - length / 2), (width, 0.02, length), color)


def village_graves():
    """A tended row of graves in the churchyard: upright headstones and wooden crosses, each with a
    low kerb of stones, and flowers laid on a few."""
    import random
    rng = random.Random(71)
    for i in range(5):
        x = -3.2 + i * 1.6
        if i in (1, 4):
            timber(f"cross{i}", (x, 0, 0), (x, 0, 1.2), 0.1, "wood_dark")
            timber(f"arm{i}", (x - 0.32, 0, 0.88), (x + 0.32, 0, 0.88), 0.08, "wood_dark")
        else:
            height = rng.uniform(0.75, 1.0)
            box(f"stone{i}", (x, 0, height / 2), (0.62, 0.14, height), "stone" if i % 2 else "stone_dark")
            blob(f"top{i}", (x, 0, height), 0.31, "stone" if i % 2 else "stone_dark", 400 + i, 0.45, 1)
        for side in (-1, 1):
            box(f"kerb{i}", (x + side * 0.4, -0.95, 0.06), (0.1, 1.6, 0.12), "stone_dark")
        box(f"kerb_end{i}", (x, -1.75, 0.06), (0.9, 0.1, 0.12), "stone_dark")
        box(f"bed{i}", (x, -0.95, 0.05), (0.7, 1.5, 0.1), "dirt_dark")
        if rng.random() < 0.6:
            for k in range(3):
                blob(f"bloom{i}{k}", (x - 0.15 + k * 0.15, -0.4 - rng.uniform(0, 0.3), 0.14), 0.09, "flower" if k % 2 else "flower_b", 410 + i * 3 + k, 0.8, 1)


def tavern():
    house(9.2, 6, 4.6, "wall", "roof", 0.0, 2)
    # The sign hangs between the ground floor window and the corner, below the upper windows.
    timber("sign_arm", (4.2, -3.05, 3.1), (4.2, -4.1, 3.1), 0.1, "wood_dark")
    box("sign", (4.2, -3.85, 2.65), (0.08, 0.9, 0.7), "wood_dark")
    for side in (-1, 1):
        box("sign_face", (4.2 + side * 0.05, -3.85, 2.65), (0.03, 0.7, 0.5), "gold")
