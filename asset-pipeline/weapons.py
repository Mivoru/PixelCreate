"""Weapons held in the hand, for build_meshes.py. Each item in world/items.json that is a weapon
has a model named weapon_<item id>; bows are the exception, as the game bends them itself.

The rig places a weapon in the fist, so the origin is the middle of the grip. Blender's +Y runs
from the grip to the point (the game's -Z, along the blade), +X is across the blade and +Z is its
flat side. Blades are faceted, not smoothed, so the toon shading gives them hard bevels, and each
has a bright band along its cutting edge.
"""
import math

import bmesh
import bpy
import mathutils

from beasts import ellipsoid, section_tube

COLORS = {
    "wpn_steel": "C0CBDC", "wpn_steel_dark": "8B9BB4", "wpn_steel_deep": "5A6988", "wpn_edge": "F2F5FA",
    "wpn_leather": "733E39", "wpn_leather_dark": "3E2731", "wpn_wrap": "B8924E", "wpn_gold": "FEAE34",
    "wpn_gold_dark": "C27A2C", "wpn_rust": "9E5A3A", "wpn_rust_dark": "6B3A2E", "wpn_rag": "8A7E6A",
    "wpn_wood": "8A5A3C", "wpn_wood_light": "C28569", "wpn_wood_pale": "E4A672", "wpn_wood_dark": "4A3028",
    "wpn_bone": "EAD4AA", "wpn_bone_dark": "B89E7A", "wpn_iron": "4E4E5A", "wpn_ember": "F77622",
    "wpn_red": "E43B44", "wpn_feather": "3A4466", "wpn_gem": "2CE8F5",
    "wpn_marble": "C8C8D0", "wpn_marble_dark": "8E909C", "wpn_bronze": "B8963C",
}


class Kit:
    def __init__(self, add_object, smooth):
        self.add_object = add_object
        self.smooth = smooth

    def tube(self, points, widths, heights, material, sides=10, caps=True):
        section_tube("piece", points, widths, heights, material, self.add_object, self.smooth, sides, caps)

    def round(self, center, radii, material, subdivisions=2):
        ellipsoid("piece", center, radii, material, self.add_object, self.smooth, subdivisions)

    def faceted(self, verts, faces, material):
        mesh = bpy.data.meshes.new("piece")
        mesh.from_pydata([tuple(v) for v in verts], [], faces)
        mesh.update()
        self.add_object("piece", mesh, material)

    def box(self, center, size, material, tilt=0.0):
        """A box, turned by tilt around the blade's axis (Y)."""
        x, y, z = (s / 2 for s in size)
        rot = mathutils.Matrix.Rotation(tilt, 3, "Y")
        verts = [rot @ mathutils.Vector((sx * x, sy * y, sz * z)) + mathutils.Vector(center)
                 for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        faces = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
        self.faceted(verts, faces, material)

    def cone(self, base, tip, radius, material, sides=6):
        base, tip = mathutils.Vector(base), mathutils.Vector(tip)
        axis = (tip - base).normalized()
        across = axis.cross(mathutils.Vector((0, 0, 1)))
        if across.length < 1e-4:
            across = mathutils.Vector((1, 0, 0))
        across.normalize()
        up = across.cross(axis)
        ring = [base + (across * math.cos(i / sides * math.tau) + up * math.sin(i / sides * math.tau)) * radius for i in range(sides)]
        verts = ring + [tip, base]
        faces = [(i, (i + 1) % sides, sides) for i in range(sides)] + [((i + 1) % sides, i, sides + 1) for i in range(sides)]
        self.faceted(verts, faces, material)

    def blade(self, start, length, left, right, thick, flat, edge, ridge=0.0, band=0.28, curve=None, edges=(True, True)):
        """
        A blade from y = start, length long. left(s) and right(s) give its half width at s from 0 (the
        base) to 1 (the point), thick(s) its half thickness. Faces near each cutting edge take the edge
        material, the rest the flat one. ridge raises a spine down the middle. curve(s) bends the
        blade sideways in X. edges says which side is sharpened; a blunt side keeps the flat colour.
        """
        steps = 18
        flat_faces, edge_faces, verts = [], [], []
        for i in range(steps):
            s = i / (steps - 1) * 0.985
            y = start + s * length
            wl, wr, t = left(s), right(s), thick(s)
            dx = curve(s) if curve else 0.0
            verts += [
                (dx - wl, y, 0.0), (dx - wl * (1 - band), y, t * 0.45), (dx, y, t * (1 + ridge)),
                (dx + wr * (1 - band), y, t * 0.45), (dx + wr, y, 0.0), (dx + wr * (1 - band), y, -t * 0.45),
                (dx, y, -t * (1 + ridge)), (dx - wl * (1 - band), y, -t * 0.45),
            ]
        sharp_sides = {0: edges[0], 3: edges[1], 4: edges[1], 7: edges[0]}
        for i in range(steps - 1):
            a, b = i * 8, (i + 1) * 8
            for k in range(8):
                n = (k + 1) % 8
                face = (a + k, a + n, b + n, b + k)
                (edge_faces if sharp_sides.get(k, False) else flat_faces).append(face)
        tip_y = start + length
        tip_dx = curve(1.0) if curve else 0.0
        verts.append((tip_dx, tip_y, 0.0))
        tip = len(verts) - 1
        last = (steps - 1) * 8
        for k in range(8):
            n = (k + 1) % 8
            (edge_faces if sharp_sides.get(k, False) else flat_faces).append((last + k, last + n, tip))
        flat_faces.append(tuple(reversed(range(8))))
        self.faceted(verts, flat_faces, flat)
        self.faceted(verts, edge_faces, edge)

    def wrap(self, y0, y1, radius, material, dark, turns, flat=1.0):
        """A grip: a core with raised bands of binding wound round it."""
        self.tube([(0, y0, 0), (0, y1, 0)], [radius, radius], [radius * flat, radius * flat], dark, sides=10)
        for i in range(turns):
            y = y0 + (i + 0.5) / turns * (y1 - y0)
            self.tube([(0, y - 0.006, 0.0008 * (i % 2)), (0, y + 0.006, -0.0008 * (i % 2))],
                      [radius * 1.18, radius * 1.18], [radius * 1.18 * flat, radius * 1.18 * flat], material, sides=10)


def twin_daggers(k):
    """A fine steel dagger, one of a matched pair: a leaf blade with a raised spine, a guard whose
    arms sweep towards the point, a wound leather grip and a gold pommel."""
    leaf = lambda s: 0.03 * (1 - s ** 2.4) * (0.82 + 0.38 * math.sin(math.pi * min(1.0, s * 1.35)))
    k.blade(0.062, 0.33, leaf, leaf, lambda s: 0.0075 * (1 - 0.6 * s), "wpn_steel", "wpn_edge", ridge=0.5)
    # A narrow ricasso where blade meets guard.
    k.box((0, 0.057, 0), (0.034, 0.012, 0.014), "wpn_steel_dark")
    guard = [(-0.072, 0.07, 0), (-0.05, 0.054, 0), (0, 0.05, 0), (0.05, 0.054, 0), (0.072, 0.07, 0)]
    k.tube(guard, [0.008, 0.009, 0.011, 0.009, 0.008], [0.008, 0.009, 0.012, 0.009, 0.008], "wpn_steel_dark", sides=8)
    for x in (-0.074, 0.074):
        k.round((x, 0.073, 0), (0.012, 0.012, 0.012), "wpn_gold")
    k.round((0, 0.05, 0), (0.016, 0.01, 0.016), "wpn_gold")
    k.wrap(-0.052, 0.042, 0.0135, "wpn_leather", "wpn_leather_dark", 7, flat=0.85)
    k.round((0, -0.062, 0), (0.02, 0.016, 0.02), "wpn_gold")
    k.round((0, -0.076, 0), (0.008, 0.006, 0.008), "wpn_gold_dark")


def rusty_dagger(k):
    """A goblin's knife: a broad, single-edged blade, rusted brown with only the ground edge bright,
    a notched back, an iron bar for a guard and a grip of filthy rag."""
    def back(s):
        notch = 0.004 if 0.25 < s < 0.6 and int(s * 30) % 2 == 0 else 0.0
        return 0.022 * (1 - s ** 3) - notch
    edge = lambda s: 0.03 * (1 - s ** 1.6) * (0.9 + 0.2 * math.sin(math.pi * s))
    k.blade(0.058, 0.3, back, edge, lambda s: 0.008 * (1 - 0.5 * s), "wpn_rust", "wpn_steel_dark", band=0.22,
            curve=lambda s: 0.012 * s * s, edges=(False, True))
    # Patches of worse rust on both flats.
    for y, x, size in ((0.13, -0.004, 0.022), (0.22, 0.006, 0.016)):
        k.round((x, y, 0.0), (size, size * 1.4, 0.0095), "wpn_rust_dark", subdivisions=1)
    k.box((0.004, 0.052, 0), (0.1, 0.014, 0.02), "wpn_iron", tilt=0.08)
    k.wrap(-0.055, 0.045, 0.015, "wpn_rag", "wpn_leather_dark", 6)
    # A loose end of rag hanging from the pommel.
    k.tube([(0, -0.06, 0), (0.01, -0.09, -0.005), (0.004, -0.12, -0.012)], [0.008, 0.006, 0.004], [0.003, 0.003, 0.002], "wpn_rag", sides=6)
    k.round((0, -0.062, 0), (0.017, 0.014, 0.017), "wpn_iron")


def wooden_sword(k):
    """A training sword cut from one plank: a rounded point, a blunt edge, a peg guard and rope round the grip."""
    width = lambda s: 0.034 * (1 - max(0.0, s - 0.82) ** 2 * 20) if s < 0.97 else 0.01
    k.blade(0.075, 0.72, width, width, lambda s: 0.011, "wpn_wood_light", "wpn_wood_pale", band=0.18)
    # The grain: two darker lines down the flat.
    for side in (-1, 1):
        k.box((0.012 * side, 0.4, 0.0105 * side), (0.003, 0.55, 0.002), "wpn_wood")
    k.box((0, 0.062, 0), (0.15, 0.026, 0.036), "wpn_wood_dark")
    for x in (-0.07, 0.07):
        k.round((x, 0.062, 0), (0.016, 0.016, 0.02), "wpn_wood")
    k.wrap(-0.06, 0.048, 0.016, "wpn_wrap", "wpn_wood_dark", 8)
    k.round((0, -0.072, 0), (0.022, 0.018, 0.022), "wpn_wood")


def hannes_shortsword(k):
    """A finder's short sword: a plain iron blade, worn bright at the edge and brown at the base, a
    straight bar guard, a leather grip and a round pommel. Hanne's, kept oiled."""
    width = lambda s: 0.03 * (1 - max(0.0, s - 0.78) ** 1.6 * 8) if s < 0.98 else 0.006
    k.blade(0.07, 0.6, width, width, lambda s: 0.008, "wpn_steel_dark", "wpn_edge", band=0.22)
    # Brown at the base of the blade, where the oil never quite reached.
    for y, x, size in ((0.11, -0.006, 0.014), (0.16, 0.007, 0.011)):
        k.round((x, y, 0.0), (size, size * 1.3, 0.0085), "wpn_rust", subdivisions=1)
    k.box((0, 0.058, 0), (0.17, 0.02, 0.026), "wpn_iron")
    for x in (-0.085, 0.085):
        k.round((x, 0.058, 0), (0.014, 0.014, 0.016), "wpn_iron")
    k.wrap(-0.058, 0.046, 0.015, "wpn_leather", "wpn_leather_dark", 8)
    k.round((0, -0.066, 0), (0.02, 0.018, 0.02), "wpn_iron")


def spiked_club(k):
    """A knotted log swelling towards its head, studded with iron spikes, with a leather grip."""
    points, radii = [], []
    for i in range(12):
        s = i / 11
        y = -0.08 + s * 1.38
        points.append((0.012 * math.sin(s * 9.0), y, 0.01 * math.cos(s * 7.0)))
        radii.append(0.04 + 0.13 * s ** 1.6 + 0.012 * math.sin(s * 23.0))
    k.tube(points, radii, radii, "wpn_wood", sides=9)
    k.round(points[-1], (radii[-1] * 0.95, 0.05, radii[-1] * 0.95), "wpn_wood_dark")
    # Knots in the wood.
    for y, angle in ((0.45, 0.5), (0.75, 2.6), (0.98, 4.4)):
        r = 0.04 + 0.13 * ((y + 0.08) / 1.38) ** 1.6
        k.round((math.cos(angle) * r, y, math.sin(angle) * r), (0.03, 0.04, 0.03), "wpn_wood_dark", subdivisions=1)
    # Spikes in staggered rings.
    for ring, y in enumerate((0.72, 0.92, 1.1, 1.24)):
        r = 0.04 + 0.13 * ((y + 0.08) / 1.38) ** 1.6
        for j in range(6):
            angle = j / 6 * math.tau + ring * 0.52
            out = mathutils.Vector((math.cos(angle), 0, math.sin(angle)))
            base = mathutils.Vector((0, y, 0)) + out * (r * 0.85)
            k.cone(base, base + out * 0.085 + mathutils.Vector((0, 0.015, 0)), 0.018, "wpn_iron")
    k.wrap(-0.07, 0.17, 0.045, "wpn_leather", "wpn_leather_dark", 7)
    k.round((0, -0.09, 0), (0.05, 0.03, 0.05), "wpn_wood_dark")


def bone_staff(k):
    """A shaman's staff: a crooked pole bound with cord, crowned by a horned skull with ember eyes,
    feathers and beads hanging below it."""
    points, radii = [], []
    for i in range(14):
        s = i / 13
        # Held a little above its middle, so standing its foot nearly touches the ground.
        points.append((0.025 * math.sin(s * 11.0), -0.82 + s * 1.72, 0.02 * math.sin(s * 7.0 + 1.0)))
        radii.append(0.034 + 0.01 * math.sin(s * 17.0) + 0.012 * s)
    k.tube(points, radii, radii, "wpn_wood", sides=8)
    for y in (-0.08, 0.08, 0.66, 0.8):
        k.tube([(0, y - 0.02, 0), (0, y + 0.02, 0)], [0.05, 0.05], [0.05, 0.05], "wpn_wrap", sides=8)
    top = mathutils.Vector(points[-1])
    skull = top + mathutils.Vector((0, 0.17, 0.0))
    k.round(skull, (0.13, 0.155, 0.14), "wpn_bone", subdivisions=3)
    k.round(skull + mathutils.Vector((0, 0.04, 0.1)), (0.095, 0.075, 0.065), "wpn_bone")
    for side in (-1, 1):
        eye = skull + mathutils.Vector((0.052 * side, 0.03, 0.122))
        k.round(eye, (0.034, 0.03, 0.016), "wpn_leather_dark", subdivisions=1)
        k.round(eye + mathutils.Vector((0, 0, 0.008)), (0.016, 0.016, 0.01), "wpn_ember", subdivisions=1)
        horn = [skull + mathutils.Vector((0.1 * side, 0.08, -0.01)), skull + mathutils.Vector((0.2 * side, 0.15, -0.05)),
                skull + mathutils.Vector((0.23 * side, 0.29, -0.02))]
        k.tube(horn, [0.04, 0.024, 0.008], [0.04, 0.024, 0.008], "wpn_bone_dark", sides=7)
    for i in range(3):
        k.box(skull + mathutils.Vector((-0.032 + 0.032 * i, -0.075, 0.125)), (0.018, 0.028, 0.012), "wpn_bone")
    # Charms on cords below the skull.
    for side, colour in ((-1, "wpn_feather"), (1, "wpn_red")):
        hang = top + mathutils.Vector((0.05 * side, -0.02, 0.02))
        k.tube([hang, hang + mathutils.Vector((0.01 * side, -0.14, 0))], [0.004, 0.004], [0.004, 0.004], "wpn_wrap", sides=5)
        k.round(hang + mathutils.Vector((0.01 * side, -0.07, 0)), (0.016, 0.016, 0.016), "wpn_bone", subdivisions=1)
        k.box(hang + mathutils.Vector((0.012 * side, -0.2, 0)), (0.03, 0.1, 0.006), colour, tilt=0.2 * side)


def orc_longsword(k):
    """An orc longsword: a long, heavy, slightly widening blade with a clipped point, a crossguard of
    plain iron bars, a long grip for two hands wound in leather, and a spiked pommel."""
    width = lambda s: 0.034 + 0.012 * s if s < 0.86 else (0.046 * (1 - (s - 0.86) / 0.14) + 0.004)
    k.blade(0.1, 0.98, width, width, lambda s: 0.009 * (1 - 0.4 * s), "wpn_steel_dark", "wpn_steel", ridge=0.3)
    # Dark pitting along the flat, and a notch knocked out of one edge.
    for y in (0.35, 0.62):
        k.round((0.005, y, 0.0), (0.012, 0.03, 0.01), "wpn_steel_deep", subdivisions=1)
    k.box((0, 0.085, 0), (0.24, 0.035, 0.045), "wpn_iron")
    for x in (-0.12, 0.12):
        k.box((x, 0.1, 0), (0.035, 0.06, 0.05), "wpn_iron")
    k.wrap(-0.16, 0.065, 0.019, "wpn_leather", "wpn_leather_dark", 10)
    k.round((0, -0.18, 0), (0.03, 0.03, 0.03), "wpn_iron")
    k.cone((0, -0.2, 0), (0, -0.26, 0), 0.016, "wpn_iron")


def orc_spear(k):
    """An orc spear: a long ash shaft bound with leather and hung with feathers and a bone charm,
    a broad leaf head of dark iron with a bright edge, and an iron butt spike."""
    k.tube([(0, -0.95, 0), (0, 1.25, 0)], [0.022, 0.02], [0.022, 0.02], "wpn_wood", sides=8)
    leaf = lambda s: 0.045 * math.sin(math.pi * min(1.0, s * 1.25)) * (1 - s ** 4) + 0.006
    k.blade(1.3, 0.36, leaf, leaf, lambda s: 0.008, "wpn_steel_deep", "wpn_steel", ridge=0.6)
    k.tube([(0, 1.22, 0), (0, 1.31, 0)], [0.026, 0.02], [0.026, 0.02], "wpn_steel_deep", sides=8)
    for y in (1.12, -0.05, 0.12):
        k.wrap(y - 0.05, y + 0.05, 0.022, "wpn_leather", "wpn_leather_dark", 3)
    for side, colour in ((-1, "wpn_red"), (1, "wpn_feather")):
        k.tube([(0, 1.08, 0), (side * 0.02, 0.98, 0.01)], [0.003, 0.003], [0.003, 0.003], "wpn_wrap", sides=4)
        k.box((side * 0.025, 0.9, 0.01), (0.02, 0.12, 0.004), colour, tilt=0.3 * side)
    k.round((0.02, 0.97, 0.0), (0.014, 0.014, 0.014), "wpn_bone", subdivisions=1)
    k.cone((0, -0.95, 0), (0, -1.08, 0), 0.02, "wpn_iron")


def orc_battle_axe(k):
    """An orc battle axe: a thick haft wound with rags, a heavy crescent blade with a hooked beard on
    one side and a spike on the other, held on with iron bands."""
    k.tube([(0, -0.25, 0), (0, 1.05, 0)], [0.03, 0.026], [0.03, 0.026], "wpn_wood_dark", sides=8)
    k.wrap(-0.22, 0.25, 0.031, "wpn_rag", "wpn_wood_dark", 7)
    # The crescent blade: a fan of tapering facets from the haft out to a curved edge.
    head_y = 0.9
    for i in range(9):
        t = i / 8
        a0, a1 = -1.0 + t * 2.0, -1.0 + (t + 0.125) * 2.0
        r0, r1 = 0.36 + 0.08 * math.cos(a0 * 1.4), 0.36 + 0.08 * math.cos(a1 * 1.4)
        inner = (0.02, head_y + 0.08 * math.sin(a0), 0)
        e0 = (0.02 + r0 * math.cos(a0 * 0.75), head_y + r0 * math.sin(a0 * 0.75), 0)
        e1 = (0.02 + r1 * math.cos(a1 * 0.75), head_y + r1 * math.sin(a1 * 0.75), 0)
        k.faceted([inner, e0, e1, (inner[0], inner[1], 0.022), (e0[0], e0[1], 0.005), (e1[0], e1[1], 0.005)],
                  [(0, 1, 2), (3, 5, 4), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)], "wpn_steel_dark")
        k.faceted([inner, e0, e1, (inner[0], inner[1], -0.022), (e0[0], e0[1], -0.005), (e1[0], e1[1], -0.005)],
                  [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)], "wpn_steel_dark")
        k.box((e0[0], e0[1], 0), (0.02, 0.05, 0.012), "wpn_steel")
    # The bright edge along the curve.
    k.cone((0.02, head_y, 0), (-0.26, head_y + 0.03, 0), 0.035, "wpn_iron")
    for y in (head_y - 0.09, head_y + 0.09):
        k.tube([(0, y - 0.015, 0), (0, y + 0.015, 0)], [0.04, 0.04], [0.04, 0.04], "wpn_iron", sides=8)
    k.cone((0, 1.05, 0), (0, 1.16, 0), 0.026, "wpn_iron")


def wood_axe(k):
    """A woodcutter's felling axe: a long pale ash haft worn dark where the hands go, a wedge of an iron head
    with a bright edge, and a poll on the back for driving wedges."""
    k.tube([(0, -0.3, 0), (0, 0.85, 0)], [0.024, 0.02], [0.03, 0.026], "wpn_wood_pale", sides=8)
    k.wrap(-0.28, -0.12, 0.026, "wpn_wood_dark", "wpn_wood", 5)
    head_y = 0.78
    # The wedge: thick at the haft, thin and wide at the edge.
    k.faceted([(0.02, head_y - 0.06, -0.03), (0.02, head_y + 0.06, -0.03), (0.02, head_y + 0.06, 0.03), (0.02, head_y - 0.06, 0.03),
               (0.22, head_y - 0.1, -0.006), (0.22, head_y + 0.1, -0.006), (0.22, head_y + 0.1, 0.006), (0.22, head_y - 0.1, 0.006)],
              [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], "wpn_iron")
    k.box((0.225, head_y, 0), (0.012, 0.2, 0.014), "wpn_edge")
    k.box((0, head_y, 0), (0.07, 0.13, 0.065), "wpn_iron")
    k.box((-0.06, head_y, 0), (0.05, 0.09, 0.055), "wpn_steel_deep")


def fishing_rod(k):
    """A fisherman's rod: a long whippy hazel pole, thicker at the butt, with a cork grip and a line running
    from the tip down to a float."""
    k.tube([(0, -0.3, 0), (0, 2.2, 0)], [0.022, 0.006], [0.022, 0.006], "wpn_wood_light", sides=6)
    k.wrap(-0.28, 0.0, 0.026, "wpn_wood_pale", "wpn_wood", 4)
    k.tube([(0, 2.2, 0), (0.05, 1.2, 0.02)], [0.003, 0.003], [0.003, 0.003], "wpn_edge", sides=4, caps=False)
    k.round((0.05, 1.18, 0.02), (0.025, 0.04, 0.025), "wpn_red")


def shepherd_crook(k):
    """A shepherd's crook: a tall straight staff of pale wood with a hook bent at the top, worn smooth."""
    k.tube([(0, -0.35, 0), (0, 1.35, 0)], [0.022, 0.018], [0.022, 0.018], "wpn_wood_pale", sides=7)
    hook = [(0, 1.35, 0), (0.05, 1.48, 0), (0.15, 1.53, 0), (0.24, 1.48, 0), (0.26, 1.38, 0), (0.22, 1.3, 0)]
    k.tube(hook, [0.018] * len(hook), [0.018] * len(hook), "wpn_wood_pale", sides=7)


def sentinel_blade(k):
    """An Auren sentinel's greatsword: a broad, straight blade of grey marble longer than a man's arm,
    a rune channel glowing down its middle, a bronze guard like spread wings and a long bronze-bound grip."""
    width = lambda s: 0.07 if s < 0.8 else 0.07 * (1 - (s - 0.8) / 0.2) + 0.006
    k.blade(0.12, 1.25, width, width, lambda s: 0.016 * (1 - 0.3 * s), "wpn_marble_dark", "wpn_marble", ridge=0.2)
    for side in (1, -1):
        k.box((0, 0.7, side * 0.02), (0.016, 1.0, 0.006), "wpn_gem")
    for side in (-1, 1):
        k.tube([(0, 0.1, 0), (side * 0.12, 0.13, 0), (side * 0.2, 0.2, 0)], [0.03, 0.025, 0.01], [0.03, 0.025, 0.01], "wpn_bronze", sides=6)
    k.round((0, 0.1, 0), (0.05, 0.04, 0.04), "wpn_bronze")
    k.wrap(-0.22, 0.07, 0.024, "wpn_bronze", "wpn_marble_dark", 8)
    k.round((0, -0.25, 0), (0.04, 0.04, 0.04), "wpn_gem")


BUILDERS = {
    "sentinel_blade": sentinel_blade,
    "orc_longsword": orc_longsword,
    "orc_spear": orc_spear,
    "orc_battle_axe": orc_battle_axe,
    "wood_axe": wood_axe,
    "fishing_rod": fishing_rod,
    "shepherd_crook": shepherd_crook,
    "twin_daggers": twin_daggers,
    "rusty_dagger": rusty_dagger,
    "wooden_sword": wooden_sword,
    "hannes_shortsword": hannes_shortsword,
    "spiked_club": spiked_club,
    "bone_staff": bone_staff,
}


def models(add_object, smooth):
    """Model name to builder, ready for build_meshes.MODELS."""
    k = Kit(add_object, smooth)
    return {f"weapon_{item}": (lambda build: lambda: build(k))(build) for item, build in BUILDERS.items()}
