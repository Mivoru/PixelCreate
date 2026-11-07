"""Goblin parts for build_meshes.py: heads with gear for each kind, two torsos, a clawed hand and foot.

The rig in the game (PlayerRig) places these on its joints, so each model has its origin where its
joint is: a head at the neck, a torso at the waist, a hand at the wrist and a foot at the ankle.
Blender's +Y is the front. Material names starting with gob_ are recoloured by the game: gob_skin
takes the goblin's skin colour and gob_cloth its clothes.
"""
import math

import bmesh
import bpy
import mathutils

from beasts import ellipsoid, section_tube

COLORS = {
    "gob_skin": "7FB04A", "gob_skin_dark": "5E8A36", "gob_cloth": "3E7A48", "gob_cloth_dark": "2E5A36",
    "gob_eye": "FEE761", "gob_mouth": "4A1A22", "gob_tooth": "EAD4AA", "gob_claw": "3E2731",
    "gob_ear_inner": "C8907A", "gob_leather": "733E39", "gob_leather_dark": "4A2A26", "gob_iron": "8B9BB4",
    "gob_iron_dark": "5A6988", "gob_bone": "EAD4AA", "gob_ember": "F77622", "gob_feather_a": "8B9BB4",
    "gob_feather_b": "C28569", "gob_red": "E43B44", "gob_gold": "FEAE34", "gob_hat": "3E2731", "gob_rope": "B8924E",
}


class Kit:
    def __init__(self, add_object, smooth):
        self.add_object = add_object
        self.smooth = smooth

    def tube(self, points, widths, heights, material, sides=12, caps=True):
        section_tube("piece", points, widths, heights, material, self.add_object, self.smooth, sides, caps)

    def round(self, center, radii, material, subdivisions=3):
        ellipsoid("piece", center, radii, material, self.add_object, self.smooth, subdivisions)

    def shell(self, center, radii, material, keep, subdivisions=3):
        """Part of an ellipsoid surface: keep(x, y, z) on the unit sphere says which faces stay."""
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1)
        doomed = [f for f in bm.faces if not keep(*f.calc_center_median())]
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
        for vert in bm.verts:
            vert.co = mathutils.Vector((vert.co.x * radii[0], vert.co.y * radii[1], vert.co.z * radii[2])) + mathutils.Vector(center)
        mesh = bpy.data.meshes.new("shell")
        bm.to_mesh(mesh)
        bm.free()
        self.smooth(mesh)
        self.add_object("shell", mesh, material)

    def skull(self, center, radii, material, jaw=0.25, snout=0.06):
        """A goblin cranium: broad jaw, sloping forehead, a bulge at the back."""
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1)
        for vert in bm.verts:
            x, y, z = vert.co
            if z < 0:
                x *= 1 + jaw * -z
                y += snout / radii[1] * -z * max(0.0, y)
            if z > 0 and y > 0:
                y *= 1 - 0.22 * z
            if y < 0:
                y *= 1.08
            vert.co = mathutils.Vector((x * radii[0], y * radii[1], z * radii[2])) + mathutils.Vector(center)
        mesh = bpy.data.meshes.new("skull")
        bm.to_mesh(mesh)
        bm.free()
        self.smooth(mesh)
        self.add_object("skull", mesh, material)


def face(k, brute=False, ears=True):
    """Everything a goblin face has: nose, brow, eyes, grin, teeth, cheeks and long ears."""
    k.skull((0, 0, 0.17), (0.25, 0.24, 0.23), "gob_skin", jaw=0.4 if brute else 0.25, snout=0.08 if brute else 0.05)
    k.tube([(0, -0.03, -0.14), (0, -0.01, -0.02), (0, 0.01, 0.06)], [0.1, 0.09, 0.09], [0.09, 0.085, 0.08], "gob_skin", 10)
    # A long hooked nose with a wart.
    k.tube([(0, 0.2, 0.22), (0, 0.29, 0.19), (0, 0.35, 0.13), (0, 0.34, 0.09)], [0.05, 0.045, 0.03, 0.014], [0.06, 0.05, 0.035, 0.014], "gob_skin", 10)
    k.round((0.03, 0.31, 0.17), (0.014, 0.014, 0.014), "gob_skin_dark", 1)
    # A heavy brow that dips towards the nose, so the face always scowls.
    k.tube([(-0.18, 0.15, 0.28), (-0.07, 0.225, 0.235), (0.07, 0.225, 0.235), (0.18, 0.15, 0.28)], [0.03, 0.035, 0.035, 0.03], [0.026, 0.03, 0.03, 0.026], "gob_skin_dark", 8)
    for side in (-1, 1):
        k.round((side * 0.092, 0.2, 0.195), (0.05, 0.022, 0.034), "gob_eye", 2)
        k.round((side * 0.09, 0.219, 0.195), (0.011, 0.006, 0.028), "eye_black", 1)
        k.round((side * 0.15, 0.15, 0.1), (0.075, 0.065, 0.05), "gob_skin", 2)
        if ears:
            k.tube([(side * 0.2, 0.0, 0.2), (side * 0.36, -0.04, 0.25), (side * 0.54, -0.09, 0.31), (side * 0.7, -0.14, 0.37)],
                   [0.03, 0.025, 0.018, 0.004], [0.11, 0.09, 0.055, 0.006], "gob_skin", 8)
            k.tube([(side * 0.24, 0.016, 0.2), (side * 0.38, -0.02, 0.25), (side * 0.53, -0.07, 0.3)],
                   [0.012, 0.01, 0.004], [0.07, 0.055, 0.01], "gob_ear_inner", 6)
    # A wide grin with a row of uneven teeth, and two tusks pushing up from the lower jaw.
    k.round((0, 0.205, 0.065), (0.13, 0.035, 0.026), "gob_mouth", 2)
    for i, x in enumerate((-0.075, -0.035, 0.0, 0.04, 0.08)):
        length = 0.035 if i % 2 else 0.025
        k.tube([(x, 0.235, 0.085), (x, 0.24, 0.085 - length)], [0.014, 0.004], [0.01, 0.003], "gob_tooth", 5)
    tusk = 0.09 if brute else 0.05
    for side in (-1, 1):
        k.tube([(side * 0.085, 0.22, 0.04), (side * 0.095, 0.235, 0.04 + tusk * 0.6), (side * 0.09, 0.23, 0.04 + tusk)],
               [0.022 if brute else 0.014, 0.014, 0.003], [0.022 if brute else 0.014, 0.014, 0.003], "gob_tooth", 6)
    k.round((0, 0.16, -0.02), (0.1, 0.075, 0.055), "gob_skin", 2)


def head_skirmisher(k):
    face(k)
    # A pointed hood that leaves the face open and lets the ears through.
    k.shell((0, -0.02, 0.2), (0.29, 0.28, 0.27), "gob_cloth", lambda x, y, z: not (y > 0.15 and z < 0.75))
    k.tube([(0, -0.15, 0.4), (0, -0.3, 0.46), (0, -0.44, 0.38), (0, -0.5, 0.26)], [0.12, 0.08, 0.04, 0.01], [0.1, 0.07, 0.035, 0.01], "gob_cloth", 8)
    k.tube([(0, -0.02, -0.08), (0, -0.01, 0.02)], [0.22, 0.24], [0.2, 0.22], "gob_cloth_dark", 14)


def head_archer(k):
    face(k)
    k.shell((0, 0, 0.17), (0.265, 0.255, 0.245), "gob_cloth", lambda x, y, z: 0.35 < z < 0.65)
    for side in (-1, 1):
        k.tube([(side * 0.03, -0.25, 0.27), (side * 0.07, -0.36, 0.2), (side * 0.06, -0.42, 0.1)], [0.012, 0.01, 0.006], [0.04, 0.035, 0.03], "gob_cloth", 6)
    k.round((0, -0.255, 0.27), (0.04, 0.03, 0.035), "gob_cloth_dark", 2)
    k.tube([(-0.06, 0.24, 0.24), (0.06, 0.23, 0.15)], [0.008, 0.008], [0.008, 0.008], "gob_skin_dark", 4)


def head_sharpshooter(k):
    face(k)
    k.round((0, -0.01, 0.35), (0.36, 0.34, 0.022), "gob_hat", 3)
    k.tube([(0, -0.01, 0.34), (0, -0.02, 0.47), (0, -0.04, 0.53)], [0.2, 0.17, 0.1], [0.19, 0.16, 0.09], "gob_hat", 14)
    k.tube([(0, -0.015, 0.36), (0, -0.015, 0.4)], [0.205, 0.2], [0.195, 0.19], "gob_red", 14)
    k.tube([(0.14, -0.06, 0.4), (0.21, -0.16, 0.56), (0.24, -0.3, 0.7), (0.23, -0.42, 0.76)], [0.006, 0.006, 0.005, 0.002], [0.035, 0.05, 0.04, 0.01], "gob_red", 6)
    # A patch over one eye: this one has seen a lot of arrows.
    k.round((-0.092, 0.215, 0.195), (0.058, 0.02, 0.045), "gob_leather_dark", 2)
    k.tube([(-0.2, 0.12, 0.3), (-0.09, 0.22, 0.24), (0.1, 0.21, 0.27), (0.24, 0.05, 0.28)], [0.008] * 4, [0.008] * 4, "gob_leather_dark", 4)


def head_brute(k):
    face(k, brute=True)
    k.shell((0, -0.01, 0.2), (0.28, 0.27, 0.27), "gob_iron", lambda x, y, z: z > 0.3)
    k.tube([(0, -0.01, 0.27), (0, -0.01, 0.3)], [0.285, 0.285], [0.275, 0.275], "gob_iron_dark", 16)
    k.tube([(0, 0.255, 0.31), (0, 0.29, 0.22), (0, 0.3, 0.17)], [0.025, 0.02, 0.012], [0.02, 0.016, 0.01], "gob_iron_dark", 6)
    for side in (-1, 1):
        k.tube([(side * 0.2, 0.0, 0.36), (side * 0.32, 0.02, 0.44), (side * 0.38, 0.06, 0.58), (side * 0.36, 0.1, 0.66)],
               [0.05, 0.04, 0.025, 0.004], [0.05, 0.04, 0.025, 0.004], "gob_bone", 8)
    k.tube([(0.03, 0.2, 0.3), (0.12, 0.21, 0.2), (0.17, 0.18, 0.12)], [0.006] * 3, [0.006] * 3, "gob_mouth", 4)
    for side in (-1, 1):
        k.tube([(side * 0.6, -0.1, 0.33), (side * 0.6, -0.1, 0.29)], [0.03, 0.03], [0.03, 0.03], "gob_gold", 8, caps=False)


def head_shaman(k):
    face(k)
    # An animal skull worn as a mask, with burning eyes and a crown of feathers.
    k.shell((0, 0.06, 0.24), (0.26, 0.22, 0.2), "gob_bone", lambda x, y, z: y > -0.1 and z > -0.35)
    for side in (-1, 1):
        k.round((side * 0.09, 0.265, 0.25), (0.05, 0.02, 0.045), "eye_black", 2)
        k.round((side * 0.09, 0.282, 0.25), (0.018, 0.006, 0.018), "gob_ember", 1)
        k.tube([(side * 0.16, 0.12, 0.38), (side * 0.22, 0.1, 0.5), (side * 0.2, 0.06, 0.58)], [0.03, 0.02, 0.004], [0.03, 0.02, 0.004], "gob_bone", 6)
    k.tube([(0, 0.27, 0.2), (0, 0.29, 0.14)], [0.03, 0.014], [0.02, 0.01], "eye_black", 5)
    for i in range(7):
        angle = (i - 3) * 0.32
        base = (math.sin(angle) * 0.14, -0.12, 0.34)
        tip = (math.sin(angle) * 0.42, -0.22, 0.34 + math.cos(angle) * 0.42)
        mid = tuple((a + b) / 2 for a, b in zip(base, tip))
        k.tube([base, mid, tip], [0.006, 0.006, 0.002], [0.035, 0.05, 0.012], "gob_feather_a" if i % 2 == 0 else "gob_feather_b", 6)
    for i in range(5):
        a = (i - 2) * 0.35
        k.round((math.sin(a) * 0.17, 0.12 + math.cos(a) * 0.05, -0.1 - abs(i - 2) * 0.02), (0.025, 0.025, 0.025), "gob_red" if i % 2 else "gob_bone", 1)


def torso(k, brute):
    """A hunched chest over a round belly, a belt with rags hanging from it, or for a brute bare skin and a hide pauldron."""
    width = 1.3 if brute else 1.0
    belly = "gob_skin" if brute else "gob_cloth"
    rings = [(-0.1, 0.0, 0.2, 0.17), (0.04, 0.035, 0.26, 0.24), (0.18, 0.04, 0.28, 0.26), (0.32, 0.0, 0.27, 0.21), (0.42, -0.03, 0.22, 0.16), (0.5, -0.05, 0.12, 0.1)]
    k.tube([(0, y, z) for z, y, _, _ in rings], [w * width for _, _, w, _ in rings], [d * width for _, _, _, d in rings], belly, 16)
    if not brute:
        k.round((0, 0.13, 0.14), (0.17, 0.16, 0.16), "gob_skin", 2)
        k.tube([(0, 0.0, 0.33), (0, -0.02, 0.38)], [0.24, 0.2], [0.18, 0.16], "gob_cloth_dark", 14)
    k.tube([(0, 0.01, -0.02), (0, 0.01, 0.06)], [0.215 * width, 0.21 * width], [0.2 * width, 0.19 * width], "gob_leather", 16)
    k.round((0, 0.21 * width, 0.02), (0.05, 0.02, 0.045), "gob_iron", 1)
    for i in range(9):
        angle = i / 9 * math.tau
        x, y = math.sin(angle) * 0.19 * width, math.cos(angle) * 0.18 * width
        length = 0.26 + (i % 3) * 0.06
        k.tube([(x, y, -0.02), (x * 1.1, y * 1.1, -0.02 - length * 0.5), (x * 1.15, y * 1.15, -0.02 - length)],
               [0.07, 0.065, 0.04], [0.012, 0.012, 0.008], "gob_cloth" if i % 2 else "gob_cloth_dark", 6)
    k.tube([(-0.22 * width, 0.0, 0.42), (0.0, 0.2 * width, 0.2), (0.2 * width, 0.05, 0.0)], [0.04, 0.04, 0.04], [0.015, 0.015, 0.015], "gob_leather", 6)
    if brute:
        for side in (-1, 1):
            k.shell((side * 0.36, 0, 0.42), (0.17, 0.17, 0.12), "gob_leather", lambda x, y, z: z > -0.2)
            k.round((side * 0.36, 0.0, 0.5), (0.03, 0.03, 0.05), "gob_bone", 1)
        k.tube([(0.1, 0.27, 0.3), (0.18, 0.25, 0.2)], [0.006, 0.006], [0.006, 0.006], "gob_mouth", 4)


def hand(k):
    k.round((0, 0.0, -0.06), (0.06, 0.045, 0.065), "gob_skin", 2)
    for i, x in enumerate((-0.035, 0.0, 0.035)):
        k.tube([(x, 0.01, -0.1), (x, 0.03, -0.16), (x, 0.05, -0.19)], [0.017, 0.015, 0.012], [0.017, 0.015, 0.012], "gob_skin", 6)
        k.tube([(x, 0.05, -0.19), (x, 0.08, -0.2)], [0.009, 0.002], [0.009, 0.002], "gob_claw", 5)
    k.tube([(0.055, 0.03, -0.05), (0.07, 0.07, -0.1), (0.06, 0.09, -0.12)], [0.018, 0.015, 0.006], [0.018, 0.015, 0.006], "gob_skin", 6)


def foot(k):
    k.round((0, 0.06, -0.035), (0.1, 0.17, 0.05), "gob_skin", 2)
    k.round((0, -0.06, -0.02), (0.075, 0.07, 0.06), "gob_skin", 2)
    for x in (-0.06, 0.0, 0.06):
        k.round((x, 0.2, -0.045), (0.035, 0.05, 0.035), "gob_skin", 1)
        k.tube([(x, 0.24, -0.05), (x, 0.29, -0.07)], [0.016, 0.003], [0.012, 0.003], "gob_claw", 5)
    k.tube([(0, 0.0, 0.0), (0, 0.0, 0.08)], [0.075, 0.08], [0.07, 0.075], "gob_skin", 10)


HEADS = {"skirmisher": head_skirmisher, "archer": head_archer, "sharpshooter": head_sharpshooter, "brute": head_brute, "shaman": head_shaman}


def models(add_object, smooth):
    """Model name to builder, ready for build_meshes.MODELS."""
    k = Kit(add_object, smooth)
    found = {f"gob_head_{kind}": (lambda build: lambda: build(k))(build) for kind, build in HEADS.items()}
    found["gob_torso_lean"] = lambda: torso(k, False)
    found["gob_torso_brute"] = lambda: torso(k, True)
    found["gob_hand"] = lambda: hand(k)
    found["gob_foot"] = lambda: foot(k)
    return found
