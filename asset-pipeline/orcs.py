"""Orc parts for build_meshes.py: four heads, a male and a female torso, and boots.

Orcs stand on the same jointed rig as goblins (PlayerRig), but at a man's height and broader. Each
model has its origin at its joint: a head at the neck, a torso at the waist, a boot at the ankle.
Blender's +Y is the front. Skin uses gob_skin and gob_skin_dark, so the game recolours it per kind,
and the goblin hand fits an orc arm.
"""
import math

from goblins import Kit

COLORS = {
    "orc_tusk": "E8DCC0", "orc_eye": "FEAE34", "orc_mouth": "3A1A1E", "orc_hair": "2A2220", "orc_hair_grey": "B8B4A8",
    "orc_leather": "5A3A2C", "orc_leather_dark": "3A2420", "orc_fur": "8A7A68", "orc_fur_dark": "5A4A3E", "orc_fur_pale": "C8BCA8",
    "orc_iron": "6A6C78", "orc_iron_dark": "3E3E48", "orc_bone": "D8CCB0", "orc_paint": "A82A22", "orc_paint_dark": "5A1A1A",
    "orc_cloth": "6A3A2A", "orc_cloth_dark": "4A2820", "orc_bead": "2CA8B8", "orc_gold": "D8A23C",
}


def orc_face(k, female=False):
    """A heavy orc face: a broad skull, a jutting jaw with tusks pushing up from the lower teeth,
    a flat nose, small yellow eyes deep under the brow, and short ears swept back."""
    jaw = 0.42 if female else 0.62
    k.skull((0, -0.01, 0.16), (0.142 if female else 0.15, 0.155, 0.15 if female else 0.155), "gob_skin", jaw=jaw, snout=0.02)
    # The jaw juts forward under the face.
    w = 0.85 if female else 1.0
    k.tube([(0, 0.0, 0.03), (0, 0.08, 0.015), (0, 0.13, 0.03)], [0.115 * w, 0.105 * w, 0.075 * w], [0.06, 0.055, 0.042], "gob_skin", 12)
    k.tube([(0, -0.02, -0.1), (0, -0.01, 0.0), (0, 0.0, 0.06)], [0.095 * w, 0.09 * w, 0.085 * w], [0.09, 0.085, 0.08], "gob_skin", 12)
    # A heavy brow ridge, lower in the middle so the face always glowers.
    k.tube([(-0.11, 0.105, 0.21), (-0.04, 0.142, 0.188), (0.04, 0.142, 0.188), (0.11, 0.105, 0.21)],
           [0.022, 0.026, 0.026, 0.022], [0.018, 0.022, 0.022, 0.018], "gob_skin_dark", 8)
    # A short, broad nose with flared nostrils.
    k.round((0, 0.15, 0.14), (0.034, 0.03, 0.032), "gob_skin", 2)
    for side in (-1, 1):
        k.round((side * 0.017, 0.168, 0.124), (0.01, 0.006, 0.007), "orc_mouth", 1)
    for side in (-1, 1):
        # Small eyes set deep.
        k.round((side * 0.055, 0.128, 0.168), (0.022, 0.01, 0.013), "orc_eye", 1)
        k.round((side * 0.054, 0.136, 0.168), (0.007, 0.004, 0.011), "eye_black", 1)
        k.round((side * 0.085, 0.11, 0.105), (0.045, 0.035, 0.035), "gob_skin", 2)
        # Short ears swept back.
        k.tube([(side * 0.135, 0.0, 0.155), (side * 0.19, -0.045, 0.19), (side * 0.235, -0.1, 0.235)],
               [0.012, 0.01, 0.003], [0.048, 0.034, 0.004], "gob_skin", 7)
        k.tube([(side * 0.145, 0.01, 0.155), (side * 0.19, -0.035, 0.185)], [0.006, 0.003], [0.03, 0.01], "gob_ear_inner", 5)
        # Tusks from the lower jaw, curving up past the lip.
        tusk = 0.06 if female else 0.095
        r = 0.011 if female else 0.016
        k.tube([(side * 0.045, 0.135, 0.03), (side * 0.052, 0.152, 0.03 + tusk * 0.6), (side * 0.048, 0.148, 0.03 + tusk)],
               [r, r * 0.75, 0.002], [r, r * 0.75, 0.002], "orc_tusk", 6)
    k.round((0, 0.143, 0.065), (0.07, 0.012, 0.01), "orc_mouth", 1)


def head_warrior(k):
    """A veteran: shaved sides, a topknot bound with iron rings, a braided beard and a scar."""
    orc_face(k)
    k.shell((0, -0.02, 0.17), (0.152, 0.157, 0.158), "orc_hair", lambda x, y, z: z > 0.55 and abs(x) < 0.45)
    k.round((0, -0.06, 0.32), (0.05, 0.05, 0.06), "orc_hair", 2)
    k.tube([(0, -0.07, 0.36), (0, -0.14, 0.38), (0, -0.22, 0.3), (0, -0.25, 0.16)], [0.03, 0.025, 0.02, 0.012], [0.03, 0.025, 0.02, 0.012], "orc_hair", 8)
    for z in (0.34, 0.27):
        k.tube([(0, -0.08 - (0.34 - z), z + 0.01), (0, -0.09 - (0.34 - z), z - 0.02)], [0.036, 0.036], [0.036, 0.036], "orc_iron", 8, caps=False)
    k.tube([(0, 0.13, 0.0), (0, 0.15, -0.06), (0, 0.13, -0.13)], [0.03, 0.022, 0.01], [0.02, 0.016, 0.008], "orc_hair", 7)
    k.tube([(0, 0.149, -0.07), (0, 0.145, -0.09)], [0.026, 0.026], [0.02, 0.02], "orc_iron", 8, caps=False)
    k.tube([(0.03, 0.143, 0.24), (0.07, 0.13, 0.16), (0.09, 0.115, 0.1)], [0.005] * 3, [0.005] * 3, "orc_paint_dark", 4)


def head_berserker(k):
    """A berserker: a bare scalp with a crest of stiff hair, red war paint across the eyes, iron rings in the ears."""
    orc_face(k)
    for i in range(9):
        t = i / 8
        y = 0.1 - t * 0.28
        z = 0.31 - abs(t - 0.35) * 0.18
        k.tube([(0, y, z - 0.03), (0, y - 0.03, z + 0.07 - abs(t - 0.4) * 0.05)], [0.022, 0.003], [0.03, 0.004], "orc_hair", 5)
    k.tube([(-0.12, 0.135, 0.17), (-0.05, 0.142, 0.165), (0.05, 0.142, 0.165), (0.12, 0.135, 0.17)], [0.025] * 4, [0.012] * 4, "orc_paint", 6)
    for side in (-1, 1):
        k.tube([(side * 0.06, 0.14, 0.14), (side * 0.07, 0.13, 0.08)], [0.006, 0.004], [0.004, 0.003], "orc_paint", 4)
        for z in (0.2, 0.17):
            k.tube([(side * 0.2, -0.04, z), (side * 0.2, -0.04, z - 0.02)], [0.02, 0.02], [0.02, 0.02], "orc_iron", 8, caps=False)


def head_spearwoman(k):
    """A spearwoman: a long braid down the back bound with bands, beads in her hair, a stripe of paint on the chin."""
    orc_face(k, female=True)
    k.shell((0, -0.02, 0.165), (0.148, 0.157, 0.156), "orc_hair", lambda x, y, z: (z > 0.15 and y < 0.55) or z > 0.62)
    points = [(0, -0.15, 0.2), (0, -0.2, 0.08), (0, -0.21, -0.08), (0, -0.2, -0.24), (0, -0.18, -0.38), (0, -0.17, -0.48)]
    k.tube(points, [0.05, 0.045, 0.04, 0.035, 0.028, 0.012], [0.045, 0.04, 0.035, 0.03, 0.024, 0.01], "orc_hair", 9)
    for z in (0.02, -0.16, -0.32):
        k.tube([(0, -0.205, z + 0.015), (0, -0.2, z - 0.015)], [0.048, 0.048], [0.042, 0.042], "orc_leather", 9, caps=False)
    for side in (-1, 1):
        k.tube([(side * 0.12, 0.06, 0.15), (side * 0.13, 0.07, 0.04)], [0.012, 0.008], [0.012, 0.008], "orc_hair", 6)
        k.round((side * 0.13, 0.07, 0.03), (0.012, 0.012, 0.012), "orc_bead", 1)
    k.tube([(0, 0.15, 0.02), (0, 0.145, -0.03)], [0.01, 0.008], [0.004, 0.004], "orc_paint", 4)


def head_huntress(k):
    """A grey-haired huntress: her hair drawn up into a knot, bone pins through it, dots of paint under the eyes."""
    orc_face(k, female=True)
    k.shell((0, -0.015, 0.165), (0.149, 0.158, 0.158), "orc_hair_grey", lambda x, y, z: z > 0.05 and y < 0.6)
    k.round((0, -0.07, 0.33), (0.065, 0.06, 0.06), "orc_hair_grey", 2)
    k.tube([(0, -0.09, 0.33), (0, -0.16, 0.28), (0, -0.2, 0.18), (0, -0.22, 0.06)], [0.03, 0.026, 0.02, 0.01], [0.026, 0.022, 0.018, 0.008], "orc_hair_grey", 8)
    for side in (-1, 1):
        k.tube([(side * 0.09, -0.04, 0.36), (side * -0.02, -0.1, 0.34)], [0.006, 0.004], [0.006, 0.004], "orc_bone", 4)
        for i in range(3):
            k.round((side * (0.04 + i * 0.016), 0.137, 0.135 - i * 0.004), (0.005, 0.003, 0.005), "orc_paint", 1)
    k.tube([(0, 0.0, -0.07), (0, 0.0, -0.04)], [0.1, 0.1], [0.095, 0.095], "orc_bead", 12, caps=False)


def torso_male(k):
    """A broad, muscled chest bare under a fur mantle, leather straps crossing it, a belt with a skull
    for a buckle, a kilt of fur and leather strips, and one spiked iron pauldron."""
    rings = [(-0.06, 0.0, 0.17, 0.13), (0.06, 0.012, 0.19, 0.14), (0.18, 0.025, 0.23, 0.165), (0.3, 0.025, 0.27, 0.18), (0.4, 0.0, 0.27, 0.165), (0.47, -0.01, 0.18, 0.12)]
    k.tube([(0, y, z) for z, y, _, _ in rings], [w for _, _, w, _ in rings], [d for _, _, _, d in rings], "gob_skin", 18)
    # Pectorals and the line down the belly.
    for side in (-1, 1):
        k.round((side * 0.1, 0.15, 0.3), (0.1, 0.05, 0.075), "gob_skin", 2)
    k.tube([(0, 0.17, 0.25), (0, 0.17, 0.08)], [0.006, 0.006], [0.004, 0.004], "gob_skin_dark", 4)
    # Crossing straps.
    for side in (-1, 1):
        k.tube([(side * 0.24, 0.0, 0.43), (side * 0.06, 0.2, 0.25), (-side * 0.16, 0.12, 0.04)], [0.035, 0.035, 0.035], [0.012, 0.012, 0.012], "orc_leather_dark", 6)
    k.round((0, 0.205, 0.24), (0.035, 0.012, 0.035), "orc_iron", 1)
    # The belt and its skull.
    k.tube([(0, 0.0, -0.04), (0, 0.0, 0.05)], [0.2, 0.195], [0.155, 0.15], "orc_leather", 18)
    k.round((0, 0.16, 0.005), (0.045, 0.03, 0.045), "orc_bone", 2)
    for side in (-1, 1):
        k.round((side * 0.016, 0.188, 0.012), (0.012, 0.006, 0.012), "eye_black", 1)
    # A kilt of fur and leather strips.
    for i in range(12):
        a = i / 12 * math.tau
        x, y = math.sin(a) * 0.2, math.cos(a) * 0.155
        length = 0.32 + (i % 3) * 0.05
        k.tube([(x, y, -0.03), (x * 1.12, y * 1.12, -0.03 - length * 0.5), (x * 1.18, y * 1.18, -0.03 - length)],
               [0.07, 0.065, 0.045], [0.014, 0.014, 0.01], "orc_fur_dark" if i % 2 else "orc_leather", 6)
    # The fur mantle over the shoulders and round the neck.
    for i in range(14):
        a = i / 14 * math.tau
        x, y = math.sin(a) * 0.2, math.cos(a) * 0.14
        k.round((x, y - 0.01, 0.46), (0.09, 0.08, 0.06), "orc_fur" if i % 3 else "orc_fur_pale", 1)
    # A spiked iron pauldron on the left shoulder.
    k.shell((-0.3, 0.0, 0.42), (0.13, 0.13, 0.1), "orc_iron", lambda x, y, z: z > -0.2)
    for i, (dx, dy) in enumerate(((-0.02, 0.06), (-0.06, -0.04), (0.02, -0.07))):
        base = (-0.3 + dx, dy, 0.5)
        k.tube([base, (-0.36 + dx * 2, dy * 1.6, 0.66)], [0.025, 0.002], [0.025, 0.002], "orc_iron_dark", 6)


def torso_female(k):
    """A leather corset laced up the front over a narrower frame, a collar of fur, a belt with pouches
    and a long skirt of leather strips slit for running."""
    rings = [(-0.06, 0.0, 0.15, 0.12), (0.06, 0.008, 0.14, 0.11), (0.18, 0.02, 0.17, 0.13), (0.3, 0.03, 0.2, 0.15), (0.4, 0.0, 0.21, 0.14), (0.47, -0.01, 0.15, 0.1)]
    k.tube([(0, y, z) for z, y, _, _ in rings], [w for _, _, w, _ in rings], [d for _, _, _, d in rings], "orc_leather", 18)
    for side in (-1, 1):
        k.round((side * 0.075, 0.13, 0.3), (0.075, 0.06, 0.065), "orc_leather", 2)
    # Bare shoulders and collar bones above the corset.
    k.tube([(0, 0.0, 0.38), (0, -0.005, 0.47)], [0.2, 0.15], [0.135, 0.1], "gob_skin", 16)
    for i in range(5):
        z = 0.08 + i * 0.05
        k.tube([(-0.03, 0.168 + (z - 0.08) * 0.08, z), (0.03, 0.168 + (z - 0.08) * 0.08, z + 0.03)], [0.004] * 2, [0.004] * 2, "orc_leather_dark", 4)
    # The fur collar.
    for i in range(12):
        a = i / 12 * math.tau
        k.round((math.sin(a) * 0.16, math.cos(a) * 0.105 - 0.01, 0.46), (0.07, 0.06, 0.05), "orc_fur_pale" if i % 2 else "orc_fur", 1)
    # Belt, pouches and the skirt.
    k.tube([(0, 0.0, -0.04), (0, 0.0, 0.03)], [0.17, 0.165], [0.135, 0.13], "orc_leather_dark", 16)
    for side in (-1, 1):
        k.round((side * 0.14, 0.09, -0.04), (0.04, 0.03, 0.05), "orc_leather", 1)
        k.round((side * 0.14, 0.12, -0.02), (0.012, 0.006, 0.012), "orc_gold", 1)
    for i in range(14):
        a = i / 14 * math.tau
        if abs(math.sin(a)) > 0.92:
            continue
        x, y = math.sin(a) * 0.17, math.cos(a) * 0.135
        length = 0.42 + (i % 2) * 0.06
        k.tube([(x, y, -0.03), (x * 1.15, y * 1.15, -0.03 - length * 0.5), (x * 1.25, y * 1.25, -0.03 - length)],
               [0.06, 0.055, 0.045], [0.012, 0.012, 0.01], "orc_cloth" if i % 2 else "orc_cloth_dark", 6)


def boot(k):
    """A heavy boot of leather wrapped with straps, with a fur cuff."""
    k.round((0, 0.07, -0.04), (0.075, 0.14, 0.05), "orc_leather_dark", 2)
    k.tube([(0, 0.0, -0.06), (0, 0.0, 0.18)], [0.07, 0.07], [0.075, 0.075], "orc_leather", 10)
    for z in (0.02, 0.09):
        k.tube([(0, 0.0, z), (0, 0.0, z + 0.025)], [0.075, 0.075], [0.08, 0.08], "orc_leather_dark", 10, caps=False)
    k.tube([(0, 0.0, 0.17), (0, 0.0, 0.22)], [0.085, 0.09], [0.09, 0.095], "orc_fur", 10)


HEADS = {"warrior": head_warrior, "berserker": head_berserker, "spearwoman": head_spearwoman, "huntress": head_huntress}


def models(add_object, smooth):
    """Model name to builder, ready for build_meshes.MODELS."""
    k = Kit(add_object, smooth)
    found = {f"orc_head_{kind}": (lambda build: lambda: build(k))(build) for kind, build in HEADS.items()}
    found["orc_torso_m"] = lambda: torso_male(k)
    found["orc_torso_f"] = lambda: torso_female(k)
    found["orc_boot"] = lambda: boot(k)
    return found
