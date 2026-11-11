"""Props for the lands beyond the old valley edge, for build_meshes.py.

Greyfang Forest: the wolves' den and an abandoned hunters' camp. More lands add their props here.
Blender's +Y is the front of a prop. Each builder uses the helpers of build_meshes.py, passed in as h.
"""
import math
import random

COLORS = {
    "cave_dark": "141218", "den_rock": "6A6C78", "den_rock_dark": "4A4C58", "lichen": "8A9A5A",
    "pelt_grey": "7E8494", "pelt_pale": "D6D2C8", "branch": "5A3E30", "bone_old": "D8CCB0",
    "char": "2A2224", "stone_old": "7A7884", "thatch_rot": "6A5A3E", "shutter_old": "3A4A50", "dirt_dark": "4A3E34", "bronze": "8A6A3A",
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


def tube3(h, points, widths, heights, material, sides=8):
    """A bent tube through points; build_meshes.tube is round, so the widths give its radius."""
    h.tube("piece", [tuple(p) for p in points], widths, material, sides)


def quad(h, name, points, material):
    """A flat panel through four or more points, seen from both sides."""
    mesh = h.mesh_from(name, [tuple(p) for p in points], [tuple(range(len(points)))])
    h.add_object(name, mesh, material)


def orc_tent(h):
    """A big orc tent: a cone of hides stitched together over poles that cross above the smoke hole,
    tusks lashed where they meet, a dark doorway with its flap thrown back, and guy ropes to stakes."""
    h.loft("cone", [(0, 2.7, 2.6), (0.6, 2.35, 2.25), (1.6, 1.7, 1.65), (2.7, 0.95, 0.92), (3.6, 0.25, 0.25)], "hide", 12)
    rng = random.Random(5)
    # Patches of darker hide stitched over the cone.
    for i in range(9):
        a = rng.uniform(0, math.tau)
        z = rng.uniform(0.4, 2.6)
        r = 2.7 - z * 0.68
        h.blob(f"patch{i}", (math.cos(a) * r * 0.97, math.sin(a) * r * 0.97, z), rng.uniform(0.35, 0.6), "hide_dark", 20 + i, 0.25, 1)
    # Poles crossing above the top, and two great tusks bound where they meet.
    for i in range(5):
        a = i / 5 * math.tau + 0.3
        h.timber(f"pole{i}", (math.cos(a) * 2.5, math.sin(a) * 2.4, 0.1), (-math.cos(a) * 0.55, -math.sin(a) * 0.55, 4.6), 0.09, "wood_dark")
    for side in (-1, 1):
        tube3(h, [(side * 0.15, 0.1, 3.7), (side * 0.6, 0.2, 4.3), (side * 0.75, 0.1, 5.0), (side * 0.55, -0.05, 5.4)],
               [0.12, 0.1, 0.06, 0.01], [0.12, 0.1, 0.06, 0.01], "orc_tusk", 8)
    h.loft("binding", [(3.6, 0.3, 0.3), (3.85, 0.32, 0.32)], "orc_leather_dark", 10)
    # The doorway at the front, its flap tied back to one side.
    quad(h, "door", [(-0.75, 2.42, 0.02), (0.75, 2.42, 0.02), (0.25, 2.0, 1.75), (-0.25, 2.0, 1.75)], "cave_dark")
    quad(h, "flap", [(0.75, 2.45, 0.02), (1.45, 2.2, 0.1), (0.95, 1.8, 1.45), (0.25, 2.02, 1.75)], "hide_dark")
    # Guy ropes out to stakes.
    for i in range(4):
        a = i / 4 * math.tau + math.pi / 4
        stake = (math.cos(a) * 3.8, math.sin(a) * 3.7, 0.0)
        h.timber(f"stake{i}", stake, (stake[0], stake[1], 0.45), 0.07, "wood_dark")
        h.timber(f"rope{i}", (stake[0], stake[1], 0.4), (math.cos(a) * 1.9, math.sin(a) * 1.85, 1.6), 0.025, "sack")


def orc_longhouse(h):
    """The chieftain's hall: walls of stacked logs, a steep roof of hides over rafters whose ends cross
    above the gables like horns, a doorway framed by two huge tusks, and a skull over the door."""
    length, half = 12.0, 3.3
    # Log walls along both sides and across the back.
    for side in (-1, 1):
        for i in range(6):
            z = 0.2 + i * 0.4
            h.timber(f"log{side}{i}", (side * half, -length / 2, z), (side * half, length / 2, z), 0.4, "bark" if i % 2 else "bark_dark")
    for i in range(6):
        z = 0.2 + i * 0.4
        h.timber(f"back{i}", (-half, -length / 2, z), (half, -length / 2, z), 0.4, "bark" if i % 2 else "bark_dark")
    # The front wall, with a doorway in its middle.
    for side in (-1, 1):
        for i in range(6):
            z = 0.2 + i * 0.4
            h.timber(f"front{side}{i}", (side * half, length / 2, z), (side * 1.0, length / 2, z), 0.4, "bark" if i % 2 else "bark_dark")
    h.timber("lintel", (-1.3, length / 2, 2.3), (1.3, length / 2, 2.3), 0.45, "bark_dark")
    quad(h, "doorway", [(-1.0, length / 2 + 0.21, 0.0), (1.0, length / 2 + 0.21, 0.0), (1.0, length / 2 + 0.21, 2.1), (-1.0, length / 2 + 0.21, 2.1)], "cave_dark")
    # The roof: two steep slopes of hide over close rafters, their ends crossing above the gables.
    ridge = 6.2
    for side in (-1, 1):
        quad(h, f"roof{side}", [(side * (half + 0.6), -length / 2 - 0.6, 2.2), (side * (half + 0.6), length / 2 + 0.6, 2.2),
                                (0, length / 2 + 0.6, ridge), (0, -length / 2 - 0.6, ridge)], "hide")
        quad(h, f"roof_under{side}", [(side * (half + 0.6), -length / 2 - 0.6, 2.15), (0, -length / 2 - 0.6, ridge - 0.05),
                                      (0, length / 2 + 0.6, ridge - 0.05), (side * (half + 0.6), length / 2 + 0.6, 2.15)], "hide_dark")
    for i in range(7):
        y = -length / 2 - 0.3 + i * (length + 0.6) / 6
        for side in (-1, 1):
            h.timber(f"rafter{i}{side}", (side * (half + 0.7), y, 2.15), (-side * 0.5, y, ridge + 0.35), 0.16, "wood_dark")
    # Gable ends of planks.
    for end in (-1, 1):
        for i in range(7):
            x = -half + 0.3 + i * (2 * half - 0.6) / 6
            top = 2.4 + (ridge - 2.4) * (1 - abs(x) / (half + 0.6))
            h.timber(f"gable{end}{i}", (x, end * length / 2, 2.4), (x, end * length / 2, top - 0.1), 0.3, "wood")
    # Tusks framing the door, a skull above it, and torches either side.
    for side in (-1, 1):
        tube3(h, [(side * 1.4, length / 2 + 0.6, 0.0), (side * 1.9, length / 2 + 1.4, 1.4), (side * 1.7, length / 2 + 1.6, 2.8), (side * 1.0, length / 2 + 1.2, 3.6)],
               [0.28, 0.24, 0.16, 0.03], [0.28, 0.24, 0.16, 0.03], "orc_tusk", 10)
        h.timber(f"torch{side}", (side * 2.2, length / 2 + 0.5, 0), (side * 2.2, length / 2 + 0.5, 2.2), 0.1, "wood_dark")
        h.blob(f"flame{side}", (side * 2.2, length / 2 + 0.5, 2.35), 0.18, "ember", 40 + side, 1.4, 1)
    h.blob("skull", (0, length / 2 + 0.3, 2.85), 0.45, "orc_bone", 30, 0.9, 2)
    for side in (-1, 1):
        h.blob(f"socket{side}", (side * 0.16, length / 2 + 0.68, 2.92), 0.12, "cave_dark", 31, 1.0, 1)
        tube3(h, [(side * 0.35, length / 2 + 0.3, 3.05), (side * 0.85, length / 2 + 0.35, 3.35), (side * 1.0, length / 2 + 0.3, 3.8)],
               [0.12, 0.08, 0.02], [0.12, 0.08, 0.02], "orc_bone", 8)


def war_banner(h):
    """A tall war banner: a pole with a skull on top, a crossbar, a long red cloth with a ragged foot
    and a black tusked mark painted on it, and bones hanging from the bar's ends."""
    h.timber("pole", (0, 0, 0), (0, 0, 6.2), 0.14, "wood_dark")
    h.timber("bar", (-0.9, 0, 5.6), (0.9, 0, 5.6), 0.09, "wood_dark")
    quad(h, "cloth", [(-0.8, 0.03, 5.55), (0.8, 0.03, 5.55), (0.75, 0.03, 2.6), (0.4, 0.03, 2.95), (0.05, 0.03, 2.35), (-0.35, 0.03, 2.9), (-0.8, 0.03, 2.5)], "orc_paint")
    quad(h, "cloth_back", [(-0.8, -0.03, 5.55), (-0.8, -0.03, 2.5), (-0.35, -0.03, 2.9), (0.05, -0.03, 2.35), (0.4, -0.03, 2.95), (0.75, -0.03, 2.6), (0.8, -0.03, 5.55)], "orc_paint")
    # The mark: a black fist between two tusks.
    h.blob("fist", (0, 0.06, 4.35), 0.32, "orc_hair", 3, 1.2, 1)
    for side in (-1, 1):
        tube3(h, [(side * 0.45, 0.06, 3.7), (side * 0.6, 0.06, 4.3), (side * 0.35, 0.06, 4.9)], [0.07, 0.06, 0.01], [0.02, 0.02, 0.01], "orc_bone", 6)
        h.timber(f"cord{side}", (side * 0.88, 0, 5.55), (side * 0.88, 0, 4.9), 0.02, "sack")
        h.timber(f"bone{side}", (side * 0.88 - 0.15, 0, 4.85), (side * 0.88 + 0.15, 0, 4.8), 0.05, "orc_bone")
    h.blob("skull", (0, 0, 6.42), 0.24, "orc_bone", 4, 0.9, 2)
    h.blob("jaw", (0, 0.1, 6.24), 0.15, "orc_bone", 5, 0.6, 1)
    for side in (-1, 1):
        h.blob(f"eye{side}", (side * 0.08, 0.19, 6.46), 0.06, "cave_dark", 6, 1.0, 1)


def skull_pikes(h):
    """Three sharpened stakes leaning out of a cairn, each crowned with a skull."""
    for i, (x, y, lean) in enumerate(((0, 0, 0.0), (0.7, 0.3, 0.12), (-0.6, 0.35, -0.15))):
        top = (x + lean * 2.2, y, 2.4 + (i % 2) * 0.4)
        h.timber(f"stake{i}", (x, y, 0), top, 0.1, "wood_dark")
        h.blob(f"skull{i}", (top[0], top[1], top[2] + 0.12), 0.2, "orc_bone", 10 + i, 0.92, 2)
        for side in (-1, 1):
            h.blob(f"eye{i}{side}", (top[0] + side * 0.07, top[1] + 0.16, top[2] + 0.15), 0.05, "cave_dark", 14 + i, 1.0, 1)
    for i in range(7):
        a = i / 7 * math.tau
        h.blob(f"stone{i}", (math.cos(a) * 0.6, 0.15 + math.sin(a) * 0.55, 0.15), 0.28, "rock_dark" if i % 2 else "rock", 20 + i, 0.7, 1)


def war_drum(h):
    """A great war drum on a frame of crossed logs: a deep body bound in hide, a pale drum skin laced
    with cord, and two bone beaters resting on top."""
    for side in (-1, 1):
        h.timber(f"leg_a{side}", (side * 1.0, -0.8, 0), (side * 1.0, 0.8, 1.7), 0.14, "wood_dark")
        h.timber(f"leg_b{side}", (side * 1.0, 0.8, 0), (side * 1.0, -0.8, 1.7), 0.14, "wood_dark")
    body = [(-0.85, 0.85, 0.85), (-0.4, 0.95, 0.95), (0.4, 0.95, 0.95), (0.85, 0.85, 0.85)]
    h.loft("body", [(y, r, r, 0, 0) for y, r, _ in body], "hide_dark", 16, (0, 0, 0))
    # The drum lies on its side across the frame, so turn the loft by building it along x instead.
    for obj in list(h.bpy.context.scene.objects):
        if obj.name.startswith("body"):
            obj.rotation_euler = (0, math.pi / 2, 0)
            obj.location = (0, 0, 1.7)
    for side in (-1, 1):
        h.blob(f"skin{side}", (side * 0.88, 0, 1.7), 0.84, "orc_fur_pale", 7 + side, 0.12, 2)
        for obj in list(h.bpy.context.scene.objects):
            if obj.name.startswith(f"skin{side}"):
                obj.rotation_euler = (0, math.pi / 2, 0)
    for i in range(10):
        a = i / 10 * math.tau
        h.timber(f"lace{i}", (-0.8, math.cos(a) * 0.88, 1.7 + math.sin(a) * 0.88), (0.8, math.cos(a + 0.3) * 0.88, 1.7 + math.sin(a + 0.3) * 0.88), 0.025, "sack")
    for side in (-1, 1):
        h.timber(f"beater{side}", (-0.3, side * 0.3, 2.6), (0.4, side * 0.45, 2.62), 0.05, "orc_bone")
        h.blob(f"knob{side}", (0.42, side * 0.45, 2.62), 0.09, "orc_bone", 20, 1.0, 1)


def orc_forge(h):
    """The warcamp's forge: a squat stone furnace glowing at its mouth with a chimney, an anvil on a
    stump, a quench barrel, bellows of hide, and blades set to cool against it."""
    h.loft("furnace", [(0, 1.1, 1.0), (1.2, 1.0, 0.95), (1.8, 0.7, 0.65), (3.4, 0.35, 0.35), (3.6, 0.38, 0.38)], "rock_dark", 10)
    for i in range(12):
        a = i / 12 * math.tau
        h.blob(f"stone{i}", (math.cos(a) * 1.02, math.sin(a) * 0.96, 0.3 + (i % 3) * 0.35), 0.28, "rock", 50 + i, 0.6, 1)
    quad(h, "mouth", [(-0.4, 1.0, 0.25), (0.4, 1.0, 0.25), (0.3, 0.96, 0.9), (-0.3, 0.96, 0.9)], "ember")
    h.blob("glow", (0, 0.85, 0.45), 0.35, "orc_gold", 60, 0.8, 1)
    # The anvil on its stump.
    h.loft("stump", [(0, 0.38, 0.38, 2.0, 0.6), (0.7, 0.34, 0.34, 2.0, 0.6)], "bark", 9)
    h.box("anvil_foot", (2.0, 0.6, 0.78), (0.3, 0.22, 0.16), "orc_iron_dark")
    h.box("anvil", (2.0, 0.6, 0.92), (0.75, 0.28, 0.14), "orc_iron")
    h.box("horn", (2.48, 0.6, 0.94), (0.22, 0.16, 0.08), "orc_iron")
    h.box("hammer", (1.85, 0.62, 1.04), (0.18, 0.1, 0.1), "orc_iron_dark")
    h.timber("handle", (1.85, 0.62, 1.04), (1.5, 0.75, 1.04), 0.04, "wood_dark")
    # The quench barrel and the bellows.
    h.loft("barrel", [(0, 0.38, 0.38, -1.7, 0.7), (0.45, 0.43, 0.43, -1.7, 0.7), (0.9, 0.38, 0.38, -1.7, 0.7)], "wood", 10)
    h.loft("water", [(0.86, 0.35, 0.35, -1.7, 0.7), (0.87, 0.35, 0.35, -1.7, 0.7)], "pane", 10)
    h.blob("bellows", (-1.0, -0.9, 0.6), 0.5, "orc_leather", 61, 0.45, 2)
    h.timber("nozzle", (-0.7, -0.6, 0.6), (-0.3, -0.2, 0.5), 0.07, "orc_iron_dark")
    for i in range(3):
        h.timber(f"blade{i}", (0.9 + i * 0.25, -0.95, 0.05), (0.75 + i * 0.25, -0.75, 1.5), 0.06, "orc_iron")


def weapon_rack(h):
    """A rack of crossed poles with spears and axes leaning in it."""
    for side in (-1, 1):
        h.timber(f"a{side}", (side * 1.4, -0.5, 0), (side * 1.4, 0.0, 1.4), 0.08, "wood_dark")
        h.timber(f"b{side}", (side * 1.4, 0.5, 0), (side * 1.4, 0.0, 1.4), 0.08, "wood_dark")
    h.timber("bar", (-1.5, 0.0, 1.3), (1.5, 0.0, 1.3), 0.07, "wood_dark")
    # Heavy sills along the ground hold the frame and keep walkers out of it.
    for y in (-0.45, 0.45):
        h.timber(f"sill{y}", (-1.6, y, 0.15), (1.6, y, 0.15), 0.3, "bark_dark")
    for i in range(4):
        x = -1.0 + i * 0.65
        h.timber(f"spear{i}", (x, 0.4, 0), (x, -0.25, 2.4), 0.04, "wood")
        h.timber(f"head{i}", (x, -0.25, 2.4), (x, -0.3, 2.65), 0.06, "orc_iron")
    for i, x in enumerate((-0.7, 0.6)):
        h.timber(f"haft{i}", (x, -0.4, 0), (x + 0.1, 0.05, 1.5), 0.05, "wood_dark")
        h.box(f"axe{i}", (x + 0.25, 0.08, 1.45), (0.38, 0.04, 0.3), "orc_iron")


def broken_wall(h, name, x0, y0, x1, y1, height, seed, material="stone_old", gap=None):
    """A stone wall from one point to another, its top broken to an uneven line, with an optional
    gap (a door or a breach) between two shares of its length."""
    rng = random.Random(seed)
    length = math.hypot(x1 - x0, y1 - y0)
    blocks = max(2, int(length / 0.7))
    for i in range(blocks):
        t0, t1 = i / blocks, (i + 1) / blocks
        if gap and gap[0] < (t0 + t1) / 2 < gap[1]:
            continue
        top = height * rng.uniform(0.35, 1.0) if rng.random() < 0.6 else height * rng.uniform(0.85, 1.05)
        cx, cy = x0 + (x1 - x0) * (t0 + t1) / 2, y0 + (y1 - y0) * (t0 + t1) / 2
        step = length / blocks * 1.02
        along_x = abs(x1 - x0) >= abs(y1 - y0)
        size = (step, 0.62, top) if along_x else (0.62, step, top)
        h.box(f"{name}{i}", (cx, cy, top / 2), size, material if i % 3 else "stone_dark")
        # Courses of masonry: a slightly proud band every so often, so the wall reads as laid stone.
        for course in range(1, int(top / 0.55)):
            band = (step * 0.98, 0.66, 0.08) if along_x else (0.66, step * 0.98, 0.08)
            h.box(f"{name}{i}_course{course}", (cx, cy, course * 0.55), band, "stone_dark")
    # A course of rubble along its foot.
    for i in range(blocks // 2):
        t = rng.uniform(0, 1)
        h.blob(f"{name}_rubble{i}", (x0 + (x1 - x0) * t + rng.uniform(-0.6, 0.6), y0 + (y1 - y0) * t + rng.uniform(-0.6, 0.6), 0.08),
               rng.uniform(0.15, 0.3), "stone_dark", seed * 10 + i, 0.6, 1)


def ruined_cottage(h, seed=21, standing=0.3):
    """A cottage burnt to its stones: broken walls, a doorway, charred posts still standing, a roof
    beam fallen across the floor, blackened boards and rubble, and moss in the corners."""
    rng = random.Random(seed)
    w, d = 3.2, 2.4
    broken_wall(h, "wall_n", -w, d, w, d, 2.4, seed + 1)
    broken_wall(h, "wall_s", -w, -d, w, -d, 2.4, seed + 2, gap=(0.42, 0.58))
    broken_wall(h, "wall_e", w, -d, w, d, 2.4, seed + 3)
    broken_wall(h, "wall_w", -w, -d, -w, d, 2.4, seed + 4, gap=(0.55, 0.8))
    for x, y in ((-w + 0.4, d - 0.4), (w - 0.4, -d + 0.4), (w - 0.4, d - 0.4)):
        top = rng.uniform(1.6, 3.4) if rng.random() > standing else 0.8
        h.timber(f"post{x}{y}", (x, y, 0), (x + rng.uniform(-0.2, 0.2), y, top), 0.22, "char")
    h.timber("fallen_beam", (-w + 0.6, -d + 0.5, 0.15), (w - 0.8, d - 0.6, 1.4), 0.26, "char")
    for i in range(8):
        x, y = rng.uniform(-w + 0.6, w - 0.6), rng.uniform(-d + 0.6, d - 0.6)
        a = rng.uniform(0, math.pi)
        h.timber(f"board{i}", (x - math.cos(a) * 0.7, y - math.sin(a) * 0.7, 0.04), (x + math.cos(a) * 0.7, y + math.sin(a) * 0.7, 0.07), 0.12, "char" if i % 2 else "wood_dark")
    for i in range(5):
        h.blob(f"moss{i}", (rng.choice((-1, 1)) * (w - 0.5), rng.choice((-1, 1)) * (d - 0.5), 0.1), rng.uniform(0.3, 0.5), "moss", seed + 40 + i, 0.25, 1)
    # The old hearth against the north wall.
    h.box("hearth", (1.6, d - 0.55, 0.5), (1.1, 0.7, 1.0), "stone_dark")
    h.box("hearth_mouth", (1.6, d - 0.92, 0.35), (0.6, 0.05, 0.45), "cave_dark")


def ruined_cottage_b(h):
    """A cottage that kept half its roof: two walls whole, the roof's ridge sagging over them, its
    thatch rotted through to the rafters, and a shutter hanging by one hinge."""
    ruined_cottage(h, seed=37, standing=0.8)
    for i in range(6):
        x = -2.8 + i * 0.6
        h.timber(f"rafter_n{i}", (x, 2.5, 2.3), (x, 0.2, 3.9 - (i % 2) * 0.15), 0.12, "char")
    h.timber("ridge", (-3.2, 0.2, 3.85), (0.4, 0.2, 3.6), 0.18, "char")
    quad(h, "thatch", [(-3.4, 2.7, 2.25), (0.2, 2.7, 2.25), (0.2, 0.3, 3.65), (-3.4, 0.3, 3.85)], "thatch_rot")
    quad(h, "thatch_under", [(-3.4, 2.7, 2.2), (-3.4, 0.3, 3.8), (0.2, 0.3, 3.6), (0.2, 2.7, 2.2)], "thatch_rot")
    quad(h, "shutter", [(3.25, 0.5, 1.0), (3.3, 1.2, 0.8), (3.3, 1.25, 1.6), (3.25, 0.55, 1.8)], "shutter_old")


def gravestones(h):
    """A row of old graves: leaning headstones with rounded tops, a stone cross, sunken mounds."""
    rng = random.Random(51)
    for i in range(5):
        x = -3.2 + i * 1.6 + rng.uniform(-0.2, 0.2)
        lean = rng.uniform(-0.25, 0.25)
        height = rng.uniform(0.7, 1.1)
        if i == 2:
            h.timber("cross", (x, 0, 0), (x + lean * 0.3, 0, 1.6), 0.2, "stone_old")
            h.timber("arm", (x - 0.45, 0, 1.15), (x + 0.45, 0, 1.15), 0.18, "stone_old")
        else:
            h.timber(f"stone{i}", (x, 0, 0), (x + lean * 0.4, -lean * 0.2, height), 0.55, "stone_old" if i % 2 else "stone_dark")
            h.blob(f"top{i}", (x + lean * 0.4, -lean * 0.2, height), 0.28, "stone_old" if i % 2 else "stone_dark", 60 + i, 0.5, 1)
        h.blob(f"mound{i}", (x, -1.1, 0.0), 0.65, "dirt_dark", 70 + i, 0.25, 1)
        if rng.random() < 0.5:
            h.blob(f"moss{i}", (x, 0.05, 0.12), 0.25, "moss", 80 + i, 0.4, 1)


def grave_fence(h):
    """A stretch of wrought iron fence round a graveyard: spiked bars between two rails, a few bent."""
    rng = random.Random(53)
    for x in (-3.0, 3.0):
        h.timber(f"pillar{x}", (x, 0, 0), (x, 0, 1.5), 0.3, "stone_old")
        h.box(f"cap{x}", (x, 0, 1.55), (0.4, 0.4, 0.12), "stone_dark")
    for z in (0.3, 1.1):
        h.timber(f"rail{z}", (-2.85, 0, z), (2.85, 0, z), 0.05, "iron")
    for i in range(14):
        x = -2.6 + i * 0.4
        bend = rng.uniform(-0.25, 0.25) if i % 5 == 3 else 0.0
        h.timber(f"bar{i}", (x, 0, 0), (x + bend, bend * 0.5, 1.35), 0.035, "iron")
        h.blob(f"spike{i}", (x + bend, bend * 0.5, 1.4), 0.06, "iron", 90 + i, 1.8, 1)


def chapel_ruin(h):
    """A roofless stone chapel: tall walls with pointed window gaps, an arched door at the front,
    a few rafters left against the sky, and a broken bell tower with its bell still hanging."""
    w, d = 3.6, 7.0
    broken_wall(h, "nave_e", w, -d, w, d, 6.0, 101, gap=None)
    broken_wall(h, "nave_w", -w, -d, -w, d, 6.0, 102, gap=(0.35, 0.45))
    broken_wall(h, "apse", -w, -d, w, -d, 6.5, 103)
    broken_wall(h, "front_l", -w, d, -1.0, d, 7.0, 104)
    broken_wall(h, "front_r", 1.0, d, w, d, 7.0, 105)
    # The arch over the door.
    for i in range(9):
        a = math.pi * i / 8
        h.blob(f"arch{i}", (math.cos(a) * 1.1, d, 3.0 + math.sin(a) * 1.1), 0.32, "stone_old", 110 + i, 1.0, 1)
    quad(h, "door", [(-1.0, d + 0.02, 0.0), (1.0, d + 0.02, 0.0), (1.0, d + 0.02, 3.0), (-1.0, d + 0.02, 3.0)], "cave_dark")
    # Window gaps: dark pointed openings in the long walls.
    for y in (-4.0, 0.0, 3.5):
        for side in (-1, 1):
            quad(h, f"window{y}{side}", [(side * (w + 0.32), y - 0.5, 2.6), (side * (w + 0.32), y + 0.5, 2.6),
                                         (side * (w + 0.32), y + 0.5, 4.0), (side * (w + 0.32), y, 4.7), (side * (w + 0.32), y - 0.5, 4.0)], "cave_dark")
    # The last rafters.
    for y in (-5.0, -2.5, 4.5):
        for side in (-1, 1):
            h.timber(f"rafter{y}{side}", (side * (w + 0.2), y, 6.0), (0, y, 8.4), 0.18, "char")
    # The bell tower beside the door, broken off, its bell still under a beam.
    for x, y in ((w + 0.4, d - 0.4), (w + 2.6, d - 0.4), (w + 0.4, d - 2.6), (w + 2.6, d - 2.6)):
        h.timber(f"tower{x}{y}", (x, y, 0), (x, y, 7.5 if x > w + 1 else 9.0), 0.7, "stone_old")
    h.timber("bell_beam", (w + 0.4, d - 1.5, 8.2), (w + 2.6, d - 1.5, 7.4), 0.22, "char")
    h.loft("bell", [(6.9, 0.5, 0.5, w + 1.5, d - 1.5), (7.4, 0.32, 0.32, w + 1.5, d - 1.5), (7.7, 0.2, 0.2, w + 1.5, d - 1.5)], "bronze", 12)


def moon_altar(h):
    """Where the pack gathers under the full moon: a slab of dark stone on two blocks, scored with
    claw marks, a ring of standing stones round it, bones heaped at its foot."""
    h.box("slab", (0, 0, 1.0), (2.6, 1.4, 0.35), "stone_dark")
    for x in (-0.9, 0.9):
        h.box(f"block{x}", (x, 0, 0.42), (0.6, 1.1, 0.84), "stone_old")
    for i in range(4):
        h.box(f"claw{i}", (-0.5 + i * 0.25, 0.71, 1.0), (0.05, 0.02, 0.3), "cave_dark")
    for i in range(7):
        a = i / 7 * math.tau
        x, y = math.cos(a) * 4.2, math.sin(a) * 4.2
        h.loft(f"menhir{i}", [(0, 0.45, 0.35, x, y), (1.6 + (i % 3) * 0.5, 0.32, 0.25, x, y), (2.4 + (i % 3) * 0.5, 0.06, 0.06, x, y)], "stone_old" if i % 2 else "stone_dark", 6)
    rng = random.Random(61)
    for i in range(9):
        x, y = rng.uniform(-1.2, 1.2), rng.uniform(0.9, 1.7)
        a = rng.uniform(0, math.pi)
        h.timber(f"bone{i}", (x - math.cos(a) * 0.3, y - math.sin(a) * 0.3, 0.05), (x + math.cos(a) * 0.3, y + math.sin(a) * 0.3, 0.07), 0.07, "bone_old")
    h.blob("skull", (0.5, 1.3, 0.15), 0.2, "bone_old", 62, 0.85, 2)


def rockfall(h):
    """A slide of boulders across a road: big blocks heaped in the middle, smaller ones spilling out to
    both ends, rubble at the foot, and a pine the slide brought down lying over it."""
    rng = random.Random(83)
    for i in range(15):
        x = -6.5 + i * 0.93 + rng.uniform(-0.3, 0.3)
        middle = 1.0 - abs(x) / 8.5
        size = 0.9 + middle * 1.3 + rng.uniform(-0.15, 0.25)
        h.blob(f"boulder{i}", (x, rng.uniform(-1.3, 1.3), size * 0.55), size, "den_rock" if i % 3 else "den_rock_dark", 90 + i, rng.uniform(0.7, 0.95), 2)
    for i in range(6):
        x = rng.uniform(-4.0, 4.0)
        h.blob(f"top{i}", (x, rng.uniform(-0.6, 0.6), 2.2 + (1.0 - abs(x) / 5.0) * 1.2), rng.uniform(0.8, 1.15), "den_rock_dark" if i % 2 else "den_rock", 120 + i, 0.8, 2)
    for i in range(16):
        h.blob(f"rubble{i}", (rng.uniform(-8.0, 8.0), rng.choice((-1, 1)) * rng.uniform(2.0, 3.4), 0.12), rng.uniform(0.22, 0.45), "den_rock", 140 + i, 0.7, 1)
    h.tube("trunk", [(-5.5, 2.6, 0.5), (-2.0, 1.2, 2.4), (1.5, 0.2, 3.9), (4.2, -0.8, 4.6)], [0.3, 0.26, 0.2, 0.08], "branch")
    for i, t in enumerate((0.45, 0.62, 0.78)):
        x, y, z = -5.5 + 9.7 * t, 2.6 - 3.4 * t, 0.5 + 4.1 * t
        h.timber(f"stub{i}", (x, y, z), (x + 0.3, y + (0.9 if i % 2 else -0.9), z + 0.5), 0.07, "branch")
    for i in range(3):
        h.blob(f"lichen{i}", (-3.0 + i * 3.1, -1.5, 1.3 + (i % 2) * 0.5), 0.5, "lichen", 170 + i, 0.25, 1)


BUILDERS = {
    "rockfall": rockfall,
    "wolf_den": wolf_den,
    "hunters_lean_to": hunters_lean_to,
    "orc_tent": orc_tent,
    "orc_longhouse": orc_longhouse,
    "war_banner": war_banner,
    "skull_pikes": skull_pikes,
    "war_drum": war_drum,
    "orc_forge": orc_forge,
    "arms_rack": weapon_rack,
    "ruined_cottage": ruined_cottage,
    "ruined_cottage_b": ruined_cottage_b,
    "gravestones": gravestones,
    "grave_fence": grave_fence,
    "chapel_ruin": chapel_ruin,
    "moon_altar": moon_altar,
}


def models(helpers):
    """Model name to builder, ready for build_meshes.MODELS."""
    return {name: (lambda build: lambda: build(helpers))(build) for name, build in BUILDERS.items()}
