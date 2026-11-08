"""Werewolf parts for build_meshes.py: a wolf's head on a hunched, shaggy giant.

The werewolf stands on the same jointed rig as goblins and orcs (PlayerRig), so each part has its
origin at its joint: the head at the neck, the torso at the waist, a hand at the wrist, a paw at the
ankle and the tail at the hips. Blender's +Y is the front. The coat uses gob_skin and gob_skin_dark,
so the game colours each kind's fur; were_eye glows.
"""
import math
import random

from goblins import Kit

COLORS = {
    "were_eye": "FEE761", "were_mouth": "6A1A24", "were_gum": "A83A44", "were_fang": "F2EAD6", "were_nose": "141218",
    "were_claw": "2A2224", "were_fur_pale": "8A8478", "were_cloth": "4A4E5A", "were_cloth_dark": "2E3038",
}


def tufts(k, rng, centres, length, width, colours, outward, count):
    """Shaggy fur: tapering tufts that stick out from points, leaning the given way."""
    for _ in range(count):
        cx, cy, cz = rng.choice(centres)
        x, y, z = cx + rng.uniform(-0.05, 0.05), cy + rng.uniform(-0.05, 0.05), cz + rng.uniform(-0.05, 0.05)
        ox, oy, oz = outward(x, y, z)
        l = length * rng.uniform(0.7, 1.3)
        k.tube([(x, y, z), (x + ox * l * 0.6, y + oy * l * 0.6, z + oz * l * 0.6), (x + ox * l, y + oy * l, z + oz * l - l * 0.15)],
               [width, width * 0.6, 0.003], [width * 0.7, width * 0.45, 0.003], rng.choice(colours), 5)


def head(k):
    """A wolf's head, bigger and crueller: a long wrinkled muzzle, the jaws open on fangs, small
    glowing eyes under a heavy brow, tall ears laid back, and a shaggy mane round the neck."""
    rng = random.Random(3)
    k.round((0, 0.02, 0.13), (0.13, 0.15, 0.12), "gob_skin", 3)
    # The upper jaw and the muzzle, ridged where it wrinkles in a snarl.
    k.tube([(0, 0.1, 0.13), (0, 0.22, 0.11), (0, 0.34, 0.085), (0, 0.4, 0.075)], [0.085, 0.07, 0.048, 0.035], [0.07, 0.058, 0.04, 0.03], "gob_skin", 12)
    for i in range(3):
        y = 0.2 + i * 0.045
        k.tube([(-0.05, y, 0.14 - i * 0.008), (0, y + 0.01, 0.155 - i * 0.008), (0.05, y, 0.14 - i * 0.008)], [0.008] * 3, [0.006] * 3, "gob_skin_dark", 5)
    k.round((0, 0.405, 0.09), (0.028, 0.022, 0.022), "were_nose", 2)
    # The open mouth: a red gape, gums, and the lower jaw dropped.
    k.round((0, 0.28, 0.035), (0.05, 0.11, 0.035), "were_mouth", 2)
    k.tube([(0, 0.06, 0.03), (0, 0.2, -0.02), (0, 0.34, -0.03)], [0.07, 0.055, 0.03], [0.035, 0.03, 0.02], "gob_skin", 10)
    for side in (-1, 1):
        k.tube([(side * 0.045, 0.16, 0.06), (side * 0.04, 0.36, 0.055)], [0.01, 0.008], [0.012, 0.01], "were_gum", 5)
        # Fangs: long canines above, shorter below, and a row of teeth between.
        k.tube([(side * 0.035, 0.355, 0.06), (side * 0.035, 0.36, 0.0)], [0.011, 0.002], [0.011, 0.002], "were_fang", 5)
        k.tube([(side * 0.03, 0.32, -0.03), (side * 0.03, 0.325, 0.02)], [0.008, 0.002], [0.008, 0.002], "were_fang", 5)
        for i in range(3):
            y = 0.2 + i * 0.045
            k.tube([(side * 0.045, y, 0.055), (side * 0.045, y + 0.004, 0.035)], [0.006, 0.002], [0.006, 0.002], "were_fang", 4)
        # Eyes: small, slanted and glowing, under a heavy brow.
        k.round((side * 0.062, 0.135, 0.17), (0.026, 0.012, 0.013), "were_eye", 1)
        k.tube([(side * 0.02, 0.15, 0.2), (side * 0.065, 0.14, 0.195), (side * 0.1, 0.11, 0.2)], [0.02, 0.022, 0.015], [0.014, 0.016, 0.01], "gob_skin_dark", 6)
        # Tall ears laid back.
        k.tube([(side * 0.08, -0.02, 0.2), (side * 0.11, -0.07, 0.3), (side * 0.12, -0.12, 0.39)], [0.045, 0.03, 0.004], [0.018, 0.014, 0.004], "gob_skin_dark", 7)
        k.tube([(side * 0.082, -0.01, 0.21), (side * 0.105, -0.06, 0.29)], [0.025, 0.006], [0.008, 0.004], "were_gum", 5)
    # The mane: long fur standing out round the back of the head and down the neck.
    centres = [(math.sin(a) * 0.12, -0.04 - abs(math.cos(a)) * 0.04, 0.05 + math.cos(a) * 0.1) for a in [i / 10 * math.tau for i in range(10)]]
    centres += [(0, -0.1, 0.0), (0.06, -0.08, -0.06), (-0.06, -0.08, -0.06)]
    tufts(k, rng, centres, 0.16, 0.035, ["gob_skin", "gob_skin_dark", "gob_skin_dark"],
          lambda x, y, z: (x * 3.5, -0.7, 0.2 + z), 46)
    k.tube([(0, -0.04, -0.12), (0, 0.0, 0.05)], [0.12, 0.11], [0.12, 0.11], "gob_skin_dark", 12)


def torso(k):
    """A hunched giant's chest: a deep barrel with a hump of muscle over the shoulders, ribs under
    paler fur on the front, shaggy fur all over the back, and the rags of trousers at the waist."""
    rng = random.Random(9)
    rings = [(-0.05, 0.0, 0.17, 0.14), (0.08, 0.02, 0.2, 0.17), (0.22, 0.04, 0.3, 0.24), (0.38, 0.0, 0.36, 0.27), (0.5, -0.05, 0.31, 0.24), (0.57, -0.08, 0.17, 0.15)]
    k.tube([(0, y, z) for z, y, _, _ in rings], [w for _, _, w, _ in rings], [d for _, _, _, d in rings], "gob_skin", 18)
    k.round((0, -0.12, 0.47), (0.28, 0.16, 0.14), "gob_skin", 2)
    k.round((0, 0.13, 0.3), (0.2, 0.12, 0.17), "were_fur_pale", 2)
    for i in range(4):
        z = 0.14 + i * 0.05
        for side in (-1, 1):
            k.tube([(side * 0.05, 0.2, z), (side * 0.17, 0.17, z - 0.03)], [0.008, 0.006], [0.005, 0.004], "gob_skin_dark", 4)
    # Shaggy fur over the shoulders, the hump and down the back.
    centres = [(x, y, z) for x in (-0.26, -0.13, 0.0, 0.13, 0.26) for y, z in ((-0.15, 0.5), (-0.2, 0.36), (-0.17, 0.2), (0.02, 0.56))]
    tufts(k, rng, centres, 0.18, 0.045, ["gob_skin", "gob_skin_dark"], lambda x, y, z: (x * 1.2, -0.8, -0.3), 70)
    tufts(k, rng, [(s * 0.32, 0.0, 0.48) for s in (-1, 1)], 0.16, 0.04, ["gob_skin_dark"], lambda x, y, z: (x * 2.5, 0.0, -0.4), 16)
    # The waistband and the rags of trousers torn to strips.
    k.tube([(0, 0.0, -0.05), (0, 0.0, 0.03)], [0.185, 0.18], [0.15, 0.145], "were_cloth_dark", 16)
    for i in range(12):
        a = i / 12 * math.tau
        x, y = math.sin(a) * 0.19, math.cos(a) * 0.155
        length = 0.16 + rng.uniform(0, 0.14)
        k.tube([(x, y, -0.04), (x * 1.1, y * 1.1, -0.04 - length * 0.6), (x * 1.08, y * 1.08, -0.04 - length)],
               [0.06, 0.05, 0.02], [0.012, 0.01, 0.006], "were_cloth" if i % 2 else "were_cloth_dark", 5)


def hand(k):
    """A great clawed hand: a broad palm, four long fingers and a thumb, each ending in a black hook."""
    k.round((0, 0.0, -0.06), (0.07, 0.05, 0.075), "gob_skin", 2)
    for i, x in enumerate((-0.045, -0.015, 0.015, 0.045)):
        length = 0.13 if i in (1, 2) else 0.11
        k.tube([(x, 0.0, -0.11), (x, 0.02, -0.11 - length * 0.55), (x, 0.05, -0.11 - length)], [0.017, 0.015, 0.012], [0.017, 0.015, 0.012], "gob_skin", 6)
        k.tube([(x, 0.05, -0.11 - length), (x, 0.1, -0.11 - length - 0.02), (x, 0.13, -0.11 - length + 0.01)], [0.011, 0.006, 0.001], [0.011, 0.006, 0.001], "were_claw", 5)
    k.tube([(0.065, 0.02, -0.05), (0.085, 0.07, -0.1), (0.075, 0.1, -0.12)], [0.02, 0.016, 0.006], [0.02, 0.016, 0.006], "gob_skin", 6)
    k.tube([(0.075, 0.1, -0.12), (0.075, 0.14, -0.13)], [0.009, 0.001], [0.009, 0.001], "were_claw", 5)
    tufts(k, random.Random(4), [(0, -0.02, 0.0)], 0.07, 0.025, ["gob_skin_dark"], lambda x, y, z: (0, -0.6, 0.3), 6)


def paw(k):
    """A wolf's hind paw at the end of a long ankle: four toes with claws and fur at the hock."""
    k.round((0, 0.08, -0.04), (0.08, 0.13, 0.05), "gob_skin", 2)
    for x in (-0.05, -0.017, 0.017, 0.05):
        k.round((x, 0.2, -0.05), (0.025, 0.04, 0.03), "gob_skin", 1)
        k.tube([(x, 0.23, -0.05), (x, 0.28, -0.075)], [0.012, 0.002], [0.01, 0.002], "were_claw", 5)
    k.tube([(0, 0.0, 0.0), (0, 0.02, 0.12)], [0.07, 0.075], [0.07, 0.075], "gob_skin", 10)
    tufts(k, random.Random(6), [(0, -0.05, 0.1)], 0.1, 0.03, ["gob_skin_dark"], lambda x, y, z: (0, -0.8, 0.2), 8)


def tail(k):
    """A long, shaggy tail that hangs back and down from the hips."""
    points = [(0, -0.12, 0.0), (0, -0.3, -0.12), (0, -0.44, -0.34), (0, -0.5, -0.58), (0, -0.48, -0.74)]
    k.tube(points, [0.05, 0.08, 0.09, 0.07, 0.02], [0.05, 0.08, 0.09, 0.07, 0.02], "gob_skin", 10)
    rng = random.Random(8)
    tufts(k, rng, points[1:4], 0.12, 0.04, ["gob_skin", "gob_skin_dark"], lambda x, y, z: (rng.uniform(-0.8, 0.8), -0.6, -0.2), 26)


def limb_fur(k, length, radius, seed):
    """Shaggy fur round a limb from its joint down, longest at the back, to hang on a thigh or an arm."""
    rng = random.Random(seed)
    centres = [(math.sin(a) * radius, math.cos(a) * radius, -length * t) for a in [i / 8 * math.tau for i in range(8)] for t in (0.15, 0.45, 0.75)]
    tufts(k, rng, centres, 0.12, 0.035, ["gob_skin", "gob_skin_dark"], lambda x, y, z: (x * 3.0, y * 3.0 - 0.5, -0.6), 40)


def models(add_object, smooth):
    """Model name to builder, ready for build_meshes.MODELS."""
    k = Kit(add_object, smooth)
    return {
        "were_head": lambda: head(k),
        "were_torso": lambda: torso(k),
        "were_hand": lambda: hand(k),
        "were_paw": lambda: paw(k),
        "were_tail": lambda: tail(k),
        "were_thigh_fur": lambda: limb_fur(k, 0.5, 0.1, 11),
        "were_arm_fur": lambda: limb_fur(k, 0.32, 0.09, 12),
    }
