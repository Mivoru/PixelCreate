"""Village houses: timber-framed walls laid out around their openings, framed casement windows,
thick pitched roofs with framed gables, and a lean-to. Used by build_meshes.py.

The frame follows the openings, so no brace ever crosses a window, and every window and shutter is
checked to fit inside its wall. Blender axes: the front of a house faces -y, up is z.
"""
import math

import mathutils

# build_meshes.py hands over its drawing helpers through bind().
box = timber = blob = add_object = mesh_from = None


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
        wall_box("door_strap", "-y", front.centre, door_x, 0.07, base + z, (door_w - 0.2, 0.03, 0.08), "stone_dark")
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
            timber("log", (x0 + 0.35, y, z), (x1 - 0.35, y, z), 0.26, "bark")


def tavern():
    house(9.2, 6, 4.6, "wall", "roof", 0.0, 2)
    # The sign hangs between the ground floor window and the corner, below the upper windows.
    timber("sign_arm", (4.2, -3.05, 3.1), (4.2, -4.1, 3.1), 0.1, "wood_dark")
    box("sign", (4.2, -3.85, 2.65), (0.08, 0.9, 0.7), "wood_dark")
    for side in (-1, 1):
        box("sign_face", (4.2 + side * 0.05, -3.85, 2.65), (0.03, 0.7, 0.5), "gold")
