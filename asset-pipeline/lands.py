"""Props for the lands beyond the old valley edge, for build_meshes.py.

Greyfang Forest: the wolves' den and an abandoned hunters' camp. More lands add their props here.
Blender's +Y is the front of a prop. Each builder uses the helpers of build_meshes.py, passed in as h.
"""
import math
import random

COLORS = {
    "cave_dark": "141218", "den_rock": "6A6C78", "den_rock_dark": "4A4C58", "lichen": "8A9A5A",
    "pelt_grey": "7E8494", "pelt_pale": "D6D2C8", "branch": "5A3E30", "bone_old": "D8CCB0",
}


def wolf_den(h):
    """A mound of tumbled boulders on a rise, a black cave mouth at its front, moss on its back and
    gnawed bones scattered before the entrance."""
    rng = random.Random(41)
    # The mound: big boulders leaning together, smaller ones at their feet.
    for i, (x, y, z, r) in enumerate(((0, -0.6, 1.4, 2.6), (-2.2, -0.2, 1.0, 1.9), (2.3, -0.4, 1.1, 2.0), (-1.2, -2.4, 1.2, 2.0),
                                      (1.4, -2.3, 1.3, 2.1), (0.2, -3.2, 1.6, 1.8), (-3.4, 0.9, 0.5, 1.1), (3.5, 0.6, 0.5, 1.2))):
        h.blob(f"boulder{i}", (x, y, z), r, "den_rock" if i % 3 else "den_rock_dark", 60 + i, 0.72, 2)
    # The cave mouth: a dark hollow framed by two leaning stones and a lintel.
    h.blob("mouth", (0, 1.15, 0.85), 1.05, "cave_dark", 7, 1.1, 2)
    h.blob("jamb_l", (-1.35, 1.2, 1.0), 0.75, "den_rock_dark", 8, 1.6, 2)
    h.blob("jamb_r", (1.35, 1.2, 1.0), 0.75, "den_rock_dark", 9, 1.6, 2)
    h.blob("lintel", (0, 1.0, 2.15), 1.3, "den_rock", 10, 0.45, 2)
    # Moss and lichen in the hollows of the top.
    for i in range(7):
        a = rng.uniform(0, math.tau)
        h.blob(f"moss{i}", (math.cos(a) * 1.8, -1.2 + math.sin(a) * 1.5, 2.6 + rng.uniform(-0.3, 0.3)), rng.uniform(0.4, 0.7),
               "moss" if i % 2 else "lichen", 70 + i, 0.3, 1)
    # Bones before the entrance: long bones, a skull, ribs.
    for i in range(6):
        x, y = rng.uniform(-2.0, 2.0), rng.uniform(2.0, 3.4)
        a = rng.uniform(0, math.pi)
        h.timber(f"bone{i}", (x - math.cos(a) * 0.3, y - math.sin(a) * 0.3, 0.06), (x + math.cos(a) * 0.3, y + math.sin(a) * 0.3, 0.06), 0.07, "bone_old")
    h.blob("skull", (1.2, 2.6, 0.14), 0.18, "bone_old", 11, 0.85, 2)
    h.blob("eye", (1.2, 2.77, 0.16), 0.06, "cave_dark", 12, 1.0, 1)
    for i in range(4):
        h.timber(f"rib{i}", (-1.4 + i * 0.12, 2.4, 0.04), (-1.5 + i * 0.12, 2.4, 0.32), 0.035, "bone_old")


def hunters_lean_to(h):
    """An abandoned hunters' shelter: a lean-to of poles and branches, a hide half torn off it, a
    drying rack with an old pelt, and a ring of cold stones where the fire was."""
    # Two forked posts and a ridge pole.
    for x in (-1.3, 1.3):
        h.timber(f"post{x}", (x, 0.6, 0), (x, 0.6, 1.7), 0.1, "branch")
        h.timber(f"fork{x}", (x, 0.6, 1.5), (x + 0.15, 0.6, 1.9), 0.06, "branch")
    h.timber("ridge", (-1.6, 0.6, 1.72), (1.6, 0.6, 1.72), 0.09, "branch")
    # The roof: a close row of branches laid from the ridge pole down to the ground behind, with
    # a few cross sticks tying them, and a hide thrown over the left half.
    for i in range(17):
        x = -1.55 + i * 0.194
        h.timber(f"branch{i}", (x, 0.66, 1.78), (x + 0.03 * (i % 3 - 1), -1.25, 0.02), 0.11, "branch" if i % 4 else "wood_dark")
    for t in (0.3, 0.65):
        h.timber(f"tie{t}", (-1.6, 0.66 - 1.91 * t, 1.78 * (1 - t) + 0.1), (1.6, 0.66 - 1.91 * t, 1.78 * (1 - t) + 0.1), 0.05, "wood_dark")
    for i in range(6):
        x = -1.5 + i * 0.2
        h.timber(f"hide{i}", (x, 0.7, 1.86), (x, -0.95, 0.38), 0.21, "hide")
    # Pine boughs piled along the ridge to keep the rain out.
    for i in range(5):
        h.timber(f"bough{i}", (-1.5 + i * 0.7, 0.75, 1.9), (-1.1 + i * 0.7, 0.2, 1.62), 0.16, "foliage_pine")
    # The drying rack with a grey pelt.
    for x in (2.2, 3.4):
        h.timber(f"rack{x}", (x, 0.3, 0), (x, 0.3, 1.5), 0.06, "branch")
    h.timber("rackbar", (2.1, 0.3, 1.45), (3.5, 0.3, 1.45), 0.05, "branch")
    h.box("pelt", (2.8, 0.3, 1.0), (1.0, 0.04, 0.85), "pelt_grey")
    h.box("pelt_belly", (2.8, 0.31, 0.8), (0.5, 0.04, 0.4), "pelt_pale")
    # The cold fire ring.
    for i in range(8):
        a = i / 8 * math.tau
        h.blob(f"ring{i}", (math.cos(a) * 0.55, 2.0 + math.sin(a) * 0.55, 0.08), 0.15, "rock_dark", 90 + i, 0.7, 1)
    h.blob("ash", (0, 2.0, 0.02), 0.4, "iron", 99, 0.1, 1)


BUILDERS = {
    "wolf_den": wolf_den,
    "hunters_lean_to": hunters_lean_to,
}


def models(helpers):
    """Model name to builder, ready for build_meshes.MODELS."""
    return {name: (lambda build: lambda: build(helpers))(build) for name, build in BUILDERS.items()}
