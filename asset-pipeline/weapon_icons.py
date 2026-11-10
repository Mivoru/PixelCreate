"""32x32 weapon icons for generate_icons.py, painted from the weapon's profile rather than typed row by row.

Every weapon lies on the icon's diagonal, grip at the bottom left and point at the top right, as is
usual for item icons. A weapon is a list of parts along that axis; each part has a half width that
may change along its length and a ramp from the Endesga 32 palette. A pixel is shaded by where it
sits across the part: the side towards the top left faces the light. Blades add a bright edge and a
ridge down the middle. Bows follow a curve instead of the axis. The outline is added by the caller.
"""
import math

from PIL import Image

SIZE = 32
OUTLINE = "181425"

RAMPS = {
    "steel": ["3a4466", "5a6988", "8b9bb4", "c0cbdc", "ffffff"],
    "steel_dark": ["262b44", "3a4466", "5a6988", "8b9bb4", "c0cbdc"],
    "rust": ["3e2731", "733e39", "be4a2f", "d77643", "e4a672"],
    "wood": ["3e2731", "733e39", "b86f50", "e4a672", "ead4aa"],
    "wood_dark": ["181425", "3e2731", "733e39", "b86f50", "e4a672"],
    "leather": ["181425", "3e2731", "733e39", "b86f50", "c28569"],
    "wrap": ["3e2731", "733e39", "c28569", "e4a672", "ead4aa"],
    "rag": ["262b44", "3a4466", "5a6988", "8b9bb4", "c0cbdc"],
    "gold": ["733e39", "be4a2f", "f77622", "feae34", "fee761"],
    "bone": ["3e2731", "733e39", "c28569", "ead4aa", "ffffff"],
    "iron": ["181425", "262b44", "3a4466", "5a6988", "8b9bb4"],
    "ember": ["a22633", "e43b44", "f77622", "feae34", "fee761"],
}


def rgba(hex_color):
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


class Canvas:
    def __init__(self):
        self.pixels = {}

    def put(self, x, y, color):
        if 0 <= x < SIZE and 0 <= y < SIZE:
            self.pixels[(x, y)] = color

    def image(self):
        image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        for (x, y), color in self.pixels.items():
            image.putpixel((x, y), rgba(color))
        outlined = image.copy()
        for y in range(SIZE):
            for x in range(SIZE):
                if image.getpixel((x, y))[3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < SIZE and 0 <= ny < SIZE and image.getpixel((nx, ny))[3]:
                        outlined.putpixel((x, y), rgba(OUTLINE))
                        break
        return outlined


def shade(ramp, across):
    """across runs from -1 (the shadowed side) to 1 (the lit side)."""
    colors = RAMPS[ramp]
    index = 1 if across < -0.45 else 2 if across < 0.25 else 3
    return colors[index]


class Axis:
    """Positions along the diagonal: t in pixels from the start, d across it, positive towards the top left."""

    def __init__(self, start=(3.5, 28.5), end=(28.5, 3.5)):
        self.start = start
        dx, dy = end[0] - start[0], end[1] - start[1]
        self.length = math.hypot(dx, dy)
        self.along = (dx / self.length, dy / self.length)
        self.normal = (self.along[1], -self.along[0])
        if self.normal[0] + self.normal[1] > 0:
            self.normal = (-self.normal[0], -self.normal[1])

    def locate(self, x, y):
        px, py = x + 0.5 - self.start[0], y + 0.5 - self.start[1]
        return px * self.along[0] + py * self.along[1], px * self.normal[0] + py * self.normal[1]

    def point(self, t, d=0.0):
        return (self.start[0] + self.along[0] * t + self.normal[0] * d, self.start[1] + self.along[1] * t + self.normal[1] * d)


def part(canvas, axis, t0, t1, half, ramp, kind="round", edge=None, ridge=None, stripes=0, notches=False, sharp=(True, True), edge_dark=None):
    """
    Fills the part of the weapon between t0 and t1. half(s) gives its half width for s from 0 to 1.
    kind "round" shades like a rod; "blade" adds bright edges and a ridge; stripes bands a grip.
    """
    for y in range(SIZE):
        for x in range(SIZE):
            t, d = axis.locate(x, y)
            if not t0 <= t <= t1:
                continue
            s = (t - t0) / max(t1 - t0, 1e-6)
            w = half(s)
            if notches and d > 0 and int(t) % 3 == 0 and 0.2 < s < 0.7:
                w -= 0.8
            if w <= 0 or abs(d) > w:
                continue
            across = d / w
            if kind == "blade":
                colors = RAMPS[ramp]
                near_edge = abs(d) > w - 1.0 and w > 1.2
                if near_edge and ((d > 0 and sharp[0]) or (d < 0 and sharp[1])):
                    color = (edge or colors[4]) if d > 0 else (edge_dark or colors[2])
                elif abs(d) < 0.5 and ridge:
                    color = ridge
                else:
                    color = colors[3] if d > 0 else colors[1]
            else:
                color = shade(ramp, across)
                if stripes and int(t / stripes) % 2 == 0:
                    color = RAMPS[ramp][max(0, RAMPS[ramp].index(color) - 1)]
            canvas.put(x, y, color)


def bar(canvas, axis, t, length, thick, ramp):
    """A cross guard: a bar square to the axis."""
    for y in range(SIZE):
        for x in range(SIZE):
            at, d = axis.locate(x, y)
            if abs(at - t) <= thick and abs(d) <= length:
                canvas.put(x, y, shade(ramp, 0.6 if at - t < 0 else -0.2) if abs(d) < length - 0.8 else RAMPS[ramp][3])


def blob(canvas, cx, cy, radius, ramp, squash=1.0):
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = (x + 0.5 - cx) / radius, (y + 0.5 - cy) / (radius * squash)
            if dx * dx + dy * dy <= 1:
                light = -(dx + dy) / 1.4
                canvas.put(x, y, shade(ramp, light))


def line(canvas, a, b, color):
    steps = int(max(abs(b[0] - a[0]), abs(b[1] - a[1])) * 2) + 1
    for i in range(steps + 1):
        f = i / steps
        canvas.put(int(a[0] + (b[0] - a[0]) * f), int(a[1] + (b[1] - a[1]) * f), color)


def dagger(canvas, axis, blade_ramp="steel", grip="leather", guard="steel_dark", pommel="gold", scale=1.0):
    k = scale
    blob(canvas, *axis.point(1.6 * k), 1.7 * k, pommel)
    part(canvas, axis, 2.5 * k, 8.5 * k, lambda s: 1.2, grip, stripes=1.5)
    bar(canvas, axis, 9.3 * k, 3.9 * k, 0.9, guard)
    leaf = lambda s: (1.45 + 0.85 * math.sin(math.pi * min(1.0, s * 1.6))) * (1 - s ** 3) + 0.25
    part(canvas, axis, 10.3 * k, 27.5 * k, leaf, blade_ramp, kind="blade", ridge=RAMPS[blade_ramp][4])


class Layered:
    """Two outlined pictures, one over the other, so crossed blades stay apart."""

    def __init__(self, back, front):
        self.back, self.front = back, front

    def image(self):
        image = self.back.image()
        top = self.front.image()
        image.alpha_composite(top)
        return image


def twin_daggers():
    """Two fine daggers crossed, grips at the bottom corners; the one behind is mirrored and a shade darker."""
    axis = Axis((3.5, 28.5), (27.5, 4.5))
    back, front = Canvas(), Canvas()
    dagger(back, axis)
    dagger(front, axis)
    mirrored = Canvas()
    for (x, y), color in back.pixels.items():
        mirrored.put(SIZE - 1 - x, y, RAMPS["steel_dark"][RAMPS["steel"].index(color)] if color in RAMPS["steel"] else color)
    return Layered(mirrored, front)


def rusty_dagger():
    canvas = Canvas()
    axis = Axis((5.5, 26.5), (28.5, 3.5))
    blob(canvas, *axis.point(1.5), 1.6, "iron")
    part(canvas, axis, 2.4, 9, lambda s: 1.3 - 0.3 * (int(s * 6) % 2), "wrap", stripes=2)
    # A loose end of rag below the pommel.
    line(canvas, axis.point(0.5, -0.5), (2, 30), RAMPS["wrap"][2])
    bar(canvas, axis, 9.8, 3.2, 1.0, "iron")
    # Single edged: a straight back on the lit side, the belly curving up to the point.
    width = lambda s: 2.6 * (1 - s ** 1.3) + 0.35
    part(canvas, axis, 10.8, 30, width, "rust", kind="blade", edge=RAMPS["rust"][3], edge_dark=RAMPS["steel"][3],
         notches=True, sharp=(False, True), ridge=RAMPS["rust"][2])
    # Pits of deeper rust.
    for t, d in ((15, 0.5), (19, -1), (22, 1)):
        x, y = axis.point(t, d)
        canvas.put(int(x), int(y), RAMPS["rust"][0])
    return canvas


def wooden_sword():
    canvas = Canvas()
    axis = Axis((3.5, 28.5), (29.5, 2.5))
    blob(canvas, *axis.point(1.5), 1.9, "wood")
    part(canvas, axis, 2.5, 8.5, lambda s: 1.3, "wrap", stripes=1.5)
    bar(canvas, axis, 9.4, 4.4, 1.0, "wood_dark")
    rounded = lambda s: 2.3 if s < 0.88 else 2.3 * math.sqrt(max(0.0, 1 - ((s - 0.88) / 0.12) ** 2))
    part(canvas, axis, 10.4, 36.5, rounded, "wood", kind="blade", ridge=RAMPS["wood"][2], edge=RAMPS["wood"][4])
    return canvas


def hannes_shortsword():
    canvas = Canvas()
    axis = Axis((4.5, 27.5), (27.5, 4.5))
    blob(canvas, *axis.point(1.2), 1.7, "steel")
    part(canvas, axis, 2.2, 7.6, lambda s: 1.2, "leather", stripes=1.5)
    bar(canvas, axis, 8.4, 4.6, 0.9, "steel")
    taper = lambda s: 2.0 if s < 0.8 else 2.0 * max(0.0, 1 - (s - 0.8) / 0.2)
    part(canvas, axis, 9.3, 31.5, taper, "steel", kind="blade", edge=RAMPS["steel"][4], ridge=RAMPS["steel"][2])
    # Brown near the guard, where the oil never quite reached.
    for t, d in ((11, 0.5), (13, -0.5)):
        x, y = axis.point(t, d)
        canvas.put(int(x), int(y), RAMPS["rust"][1])
    return canvas


def spiked_club():
    canvas = Canvas()
    axis = Axis((4.5, 27.5), (26.5, 5.5))
    swell = lambda s: 1.5 + 3.6 * s ** 1.5
    # Spikes first, so the wood covers their roots.
    for t, side in ((17, 1), (21, -1), (24, 1), (27, -1), (14, -1), (29.5, 1)):
        s = t / 31
        w = swell(s)
        root = axis.point(t, side * (w - 0.5))
        tip = axis.point(t + 0.8, side * (w + 2.4))
        line(canvas, root, tip, RAMPS["steel"][2])
        canvas.put(int(tip[0]), int(tip[1]), RAMPS["steel"][4] if side > 0 else RAMPS["steel"][1])
    part(canvas, axis, 0, 31, swell, "wood")
    blob(canvas, *axis.point(30.4), 4.3, "wood")
    part(canvas, axis, 0.5, 8, lambda s: 1.9, "leather", stripes=1.5)
    # Knots in the wood.
    for t, d in ((19, 0.5), (25, -1.5), (13, 0)):
        x, y = axis.point(t, d)
        canvas.put(int(x), int(y), RAMPS["wood"][0])
    return canvas


def bone_staff():
    canvas = Canvas()
    axis = Axis((1.5, 30.5), (22.5, 9.5))
    part(canvas, axis, 0, 30, lambda s: 1.15, "wood")
    for t in (8, 15, 25):
        part(canvas, axis, t, t + 1.4, lambda s: 1.8, "wrap")
    cx, cy = 23.5, 8.5
    # Horns sweeping back from the skull.
    for side in (-1, 1):
        line(canvas, (cx + 3 * side, cy - 2), (cx + 5 * side, cy - 6), RAMPS["bone"][2])
        canvas.put(int(cx + 5 * side), int(cy - 7), RAMPS["bone"][3])
    blob(canvas, cx, cy, 5.2, "bone", squash=0.95)
    blob(canvas, cx + 0.5, cy + 3.5, 3.0, "bone", squash=0.8)
    for ex in (-2, 1):
        canvas.put(int(cx + ex), int(cy), RAMPS["ember"][0])
        canvas.put(int(cx + ex + 1), int(cy), RAMPS["ember"][0])
        canvas.put(int(cx + ex), int(cy + 1), RAMPS["ember"][0])
        canvas.put(int(cx + ex + 1), int(cy + 1), RAMPS["ember"][3])
    for tx in (-1, 0, 1, 2):
        canvas.put(int(cx + tx), int(cy + 4), RAMPS["bone"][4] if tx % 2 == 0 else RAMPS["bone"][1])
    # Charms hanging below.
    line(canvas, (cx - 4, cy + 4), (cx - 5, cy + 9), RAMPS["wrap"][2])
    canvas.put(int(cx - 5), int(cy + 10), RAMPS["ember"][1])
    canvas.put(int(cx - 6), int(cy + 10), RAMPS["ember"][1])
    return canvas


def bow(length, bulge, recurve, wood, grip, string, tips):
    """A bow along the diagonal, its back bowing towards the top left and the string straight."""
    canvas = Canvas()
    margin = (SIZE - length) / 2
    a = (margin + 1, SIZE - margin - 1)
    b = (SIZE - margin - 1, margin + 1)
    axis = Axis(a, b)
    samples = []
    for i in range(241):
        s = i / 240
        d = bulge * math.sin(math.pi * s) - recurve * (math.sin(math.pi * s * 3) if s < 1 / 6 or s > 5 / 6 else 0)
        samples.append((s, axis.point(s * axis.length, d)))
    line(canvas, axis.point(0.6), axis.point(axis.length - 0.6), string)
    for y in range(SIZE):
        for x in range(SIZE):
            best, at = 99.0, 0.0
            px, py = x + 0.5, y + 0.5
            for s, (qx, qy) in samples:
                dist = math.hypot(px - qx, py - qy)
                if dist < best:
                    best, at = dist, s
            half = 0.65 + 1.0 * math.sin(math.pi * at) ** 2
            if best <= half:
                qx, qy = samples[int(at * 240)][1]
                side = (px - qx) * axis.normal[0] + (py - qy) * axis.normal[1]
                ramp = grip if 0.42 < at < 0.58 else wood
                canvas.put(x, y, shade(ramp, side / half))
    for end in (samples[0][1], samples[-1][1]):
        canvas.put(int(end[0]), int(end[1]), RAMPS[tips][3])
    return canvas


def goblin_shortbow():
    return bow(24, 6.0, 0.0, "wood", "wrap", RAMPS["wrap"][2], "bone")


def hunters_longbow():
    return bow(30, 6.5, 1.2, "wood_dark", "leather", RAMPS["bone"][3], "bone")


PAINTERS = {
    "twin_daggers": twin_daggers,
    "rusty_dagger": rusty_dagger,
    "wooden_sword": wooden_sword,
    "hannes_shortsword": hannes_shortsword,
    "spiked_club": spiked_club,
    "bone_staff": bone_staff,
    "goblin_shortbow": goblin_shortbow,
    "hunters_longbow": hunters_longbow,
}


def paint(icon):
    return PAINTERS[icon]().image()
