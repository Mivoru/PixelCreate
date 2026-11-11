"""Auren sentinel parts for build_meshes.py: the guardians the old empire left in its ruins.

A sentinel stands on the same jointed rig as an orc (PlayerRig), at a man's height and broader, but it
is carved: white marble gone grey with age, bronze trim, and azure rune lines that are the mark of
Auren wherever it stood. Each model has its origin at its joint: a head at the neck, a torso at the
waist, a foot at the ankle, a hand at the wrist. Blender's +Y is the front.

The Archive Warden is the rare one in the Sunken Archive: a crested helm and a mantle of rune plates.
"""
import math

from goblins import Kit

COLORS = {
    "aur_marble": "C8C8D0", "aur_marble_shade": "A8AAB6", "aur_marble_dark": "8E909C", "aur_crack": "4A4C58",
    "aur_rune": "2CE8F5", "aur_rune_deep": "1A8A9E", "aur_bronze": "B8963C", "aur_bronze_dark": "7A5E2A",
    "aur_moss": "5E7A44",
}


def head(k, warden=False):
    """A helm with no face: a smooth marble shell, a narrow visor slot that glows, a ridge over the
    crown, cheek guards, and cracks where the centuries got in. The warden's helm has a tall crest."""
    k.round((0, 0.0, 0.14), (0.13, 0.14, 0.15), "aur_marble", 3)
    # Cheek guards running down to a squared jaw.
    for side in (-1, 1):
        k.tube([(side * 0.1, 0.06, 0.16), (side * 0.11, 0.08, 0.06), (side * 0.08, 0.1, 0.0)], [0.03, 0.032, 0.025], [0.05, 0.05, 0.035], "aur_marble_shade", 8)
    k.tube([(0, 0.05, -0.01), (0, 0.11, 0.02)], [0.08, 0.06], [0.03, 0.03], "aur_marble_shade", 10)
    # The visor: a dark band across the face with the rune light inside it.
    k.tube([(-0.1, 0.115, 0.15), (0, 0.142, 0.15), (0.1, 0.115, 0.15)], [0.012, 0.014, 0.012], [0.016, 0.018, 0.016], "aur_crack", 6)
    k.tube([(-0.075, 0.13, 0.15), (0, 0.15, 0.15), (0.075, 0.13, 0.15)], [0.006, 0.007, 0.006], [0.008, 0.009, 0.008], "aur_rune", 6)
    # A ridge from brow to nape, with a bronze band at the brow.
    k.tube([(0, 0.13, 0.22), (0, 0.06, 0.3), (0, -0.06, 0.3), (0, -0.14, 0.2)], [0.018, 0.022, 0.022, 0.016], [0.02, 0.03, 0.03, 0.02], "aur_marble_dark", 8)
    k.tube([(-0.12, 0.08, 0.2), (0, 0.13, 0.22), (0.12, 0.08, 0.2)], [0.012, 0.012, 0.012], [0.015, 0.015, 0.015], "aur_bronze", 6)
    # A rune on the brow and cracks over one side of the helm.
    k.round((0, 0.138, 0.24), (0.014, 0.006, 0.02), "aur_rune", 1)
    k.tube([(0.06, 0.11, 0.27), (0.1, 0.06, 0.22), (0.12, 0.02, 0.24), (0.13, -0.04, 0.18)], [0.004] * 4, [0.004] * 4, "aur_crack", 4)
    if warden:
        # A tall crest of stone plates, each edged with rune light.
        for i in range(6):
            y = 0.1 - i * 0.05
            height = 0.12 + 0.05 * math.sin(i / 5 * math.pi)
            k.tube([(0, y, 0.28), (0, y - 0.01, 0.28 + height)], [0.01, 0.006], [0.026, 0.012], "aur_marble_dark", 6)
            k.round((0, y - 0.01, 0.28 + height), (0.008, 0.012, 0.008), "aur_rune", 1)
        for side in (-1, 1):
            k.tube([(side * 0.13, 0.0, 0.2), (side * 0.2, -0.05, 0.3), (side * 0.22, -0.1, 0.36)], [0.012, 0.008, 0.002], [0.03, 0.02, 0.004], "aur_bronze", 6)


def torso(k, warden=False):
    """A carved breastplate on a broad marble chest: a bronze collar, a rune channel down the middle
    that glows, pauldrons of layered plates, a belt of bronze and a short kilt of stone strips."""
    rings = [(-0.06, 0.0, 0.17, 0.13), (0.08, 0.01, 0.18, 0.14), (0.22, 0.02, 0.23, 0.16), (0.36, 0.02, 0.27, 0.17), (0.46, 0.0, 0.26, 0.15), (0.52, -0.01, 0.15, 0.1)]
    k.tube([(0, y, z) for z, y, _, _ in rings], [w for _, _, w, _ in rings], [d for _, _, _, d in rings], "aur_marble", 18)
    # The breastplate's two halves, a little proud of the chest.
    for side in (-1, 1):
        k.round((side * 0.1, 0.12, 0.3), (0.11, 0.06, 0.12), "aur_marble_shade", 2)
    # The rune channel down the middle and a ring of runes over the heart.
    k.tube([(0, 0.175, 0.44), (0, 0.18, 0.28), (0, 0.16, 0.1)], [0.012, 0.012, 0.01], [0.01, 0.01, 0.008], "aur_rune", 6)
    k.round((0, 0.18, 0.33), (0.045, 0.012, 0.045), "aur_rune_deep", 2)
    k.round((0, 0.186, 0.33), (0.025, 0.01, 0.025), "aur_rune", 2)
    # A bronze collar.
    k.tube([(0, 0.0, 0.47), (0, 0.0, 0.53)], [0.22, 0.17], [0.15, 0.11], "aur_bronze", 16)
    # Pauldrons: three layered plates on each shoulder.
    for side in (-1, 1):
        for i in range(3):
            z = 0.47 - i * 0.06
            k.round((side * (0.25 + i * 0.02), 0.0, z), (0.11 - i * 0.012, 0.11 - i * 0.01, 0.045), "aur_marble_dark" if i % 2 else "aur_marble", 2)
        k.round((side * 0.25, 0.08, 0.49), (0.02, 0.01, 0.02), "aur_rune", 1)
    # Belt and kilt.
    k.tube([(0, 0.0, -0.05), (0, 0.0, 0.03)], [0.19, 0.185], [0.145, 0.14], "aur_bronze_dark", 16)
    for i in range(12):
        a = i / 12 * math.tau
        x, y = math.sin(a) * 0.19, math.cos(a) * 0.145
        length = 0.3 + (i % 2) * 0.05
        k.tube([(x, y, -0.04), (x * 1.12, y * 1.12, -0.04 - length)], [0.055, 0.05], [0.016, 0.014], "aur_marble_shade" if i % 2 else "aur_marble_dark", 6)
    # Cracks and a patch of moss from standing out in the weather.
    k.tube([(-0.16, 0.12, 0.42), (-0.12, 0.15, 0.3), (-0.15, 0.13, 0.2)], [0.005] * 3, [0.005] * 3, "aur_crack", 4)
    k.round((0.17, -0.1, 0.12), (0.06, 0.04, 0.05), "aur_moss", 1)
    if warden:
        # A mantle of rune plates hanging from the shoulders down the back.
        for i in range(5):
            x = -0.2 + i * 0.1
            k.tube([(x, -0.14, 0.48), (x * 1.1, -0.2, 0.2), (x * 1.15, -0.22, -0.1)], [0.05, 0.05, 0.045], [0.012, 0.012, 0.01], "aur_marble_dark", 6)
            k.round((x * 1.1, -0.215, 0.18), (0.012, 0.006, 0.03), "aur_rune", 1)


def foot(k):
    """A heavy sandal-boot of stone: a thick sole, a plated top and a bronze greave edge."""
    k.round((0, 0.07, -0.04), (0.08, 0.15, 0.05), "aur_marble_dark", 2)
    k.tube([(0, 0.0, -0.06), (0, 0.0, 0.2)], [0.075, 0.07], [0.08, 0.075], "aur_marble", 10)
    k.tube([(0, 0.0, 0.18), (0, 0.0, 0.22)], [0.085, 0.085], [0.09, 0.09], "aur_bronze", 10)
    k.round((0, 0.075, 0.08), (0.012, 0.006, 0.03), "aur_rune", 1)


def hand(k):
    """A gauntlet of stone: a squared palm, thick jointed fingers curled to grip, and a bronze cuff."""
    k.tube([(0, 0.0, 0.02), (0, 0.0, -0.04)], [0.06, 0.062], [0.065, 0.065], "aur_bronze", 10)
    k.round((0, 0.0, -0.09), (0.052, 0.04, 0.06), "aur_marble", 2)
    for i in range(4):
        x = -0.033 + i * 0.022
        k.tube([(x, 0.02, -0.13), (x, 0.045, -0.16), (x, 0.03, -0.185)], [0.012, 0.011, 0.01], [0.012, 0.011, 0.01], "aur_marble_shade", 6)
    k.tube([(0.05, 0.02, -0.08), (0.06, 0.05, -0.12)], [0.013, 0.011], [0.013, 0.011], "aur_marble_shade", 6)


def models(add_object, smooth):
    """Model name to builder, ready for build_meshes.MODELS."""
    k = Kit(add_object, smooth)
    return {
        "sentinel_head": lambda: head(k),
        "sentinel_head_warden": lambda: head(k, warden=True),
        "sentinel_torso": lambda: torso(k),
        "sentinel_torso_warden": lambda: torso(k, warden=True),
        "sentinel_foot": lambda: foot(k),
        "sentinel_hand": lambda: hand(k),
    }
