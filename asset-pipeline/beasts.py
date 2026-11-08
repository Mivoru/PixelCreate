"""Animals for build_meshes.py: smooth bodies built from elliptic sections, split into jointed parts.

Each animal is one .glb named beast_<id>. Its objects are named so the game can move them: body
(the root, at the middle of the trunk), head and tail under the body, ears under the head, and
legs or wings under the body. Every part has its origin at its joint. Blender's +Y is the animal's
front, which glTF turns into Godot's -Z.
"""
import math

import bmesh
import bpy
import mathutils


def section_tube(name, points, widths, heights, material, add_object, smooth, sides=14, caps=True):
    """A tube through points whose sections are ellipses: width across, height up and down."""
    bm = bmesh.new()
    vectors = [mathutils.Vector(p) for p in points]
    rings = []
    for index, point in enumerate(vectors):
        ahead = vectors[min(index + 1, len(vectors) - 1)] - vectors[max(index - 1, 0)]
        axis = ahead.normalized()
        across = axis.cross(mathutils.Vector((0, 0, 1)))
        if across.length < 1e-4:
            across = mathutils.Vector((1, 0, 0))
        across.normalize()
        up = across.cross(axis).normalized()
        ring = [bm.verts.new(point + across * math.cos(i / sides * math.tau) * widths[index]
                             + up * math.sin(i / sides * math.tau) * heights[index]) for i in range(sides)]
        rings.append(ring)
    for r in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i]))
    if caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object(name, mesh, material)


def ellipsoid(name, center, radii, material, add_object, smooth, subdivisions=3):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1)
    for vert in bm.verts:
        vert.co = mathutils.Vector((vert.co.x * radii[0], vert.co.y * radii[1], vert.co.z * radii[2])) + mathutils.Vector(center)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object(name, mesh, material)


class Builder:
    """Collects the objects made while building one part and joins them into a named part."""

    def __init__(self, add_object, smooth):
        self.add_object = add_object
        self.smooth = smooth
        self.parts = {}

    def tube(self, points, widths, heights, material, sides=14, caps=True):
        section_tube("piece", points, widths, heights, material, self.add_object, self.smooth, sides, caps)

    def round(self, center, radii, material, subdivisions=3):
        ellipsoid("piece", center, radii, material, self.add_object, self.smooth, subdivisions)

    def limb(self, points, radius, material, sides=9):
        self.tube(points, radius, radius, material, sides)

    def part(self, name, pivot, build, parent=None):
        before = set(bpy.context.scene.objects)
        build()
        made = [o for o in bpy.context.scene.objects if o not in before and o.type == "MESH"]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in made:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = made[0]
        if len(made) > 1:
            bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
        obj.name = name
        obj.data.name = name
        pivot = mathutils.Vector(pivot)
        obj.data.transform(mathutils.Matrix.Translation(obj.location - pivot))
        obj.location = pivot
        if parent is not None:
            bpy.context.view_layer.update()
            obj.parent = self.parts[parent]
            obj.matrix_parent_inverse = self.parts[parent].matrix_world.inverted()
        self.parts[name] = obj
        return obj


def quadruped(b, s):
    """A four-legged animal from a spec: a trunk of sections, a neck, a head, ears, horns, tail and
    legs in two parts each (upper leg and shin), so knees and hocks can bend as it walks."""
    trunk = s["trunk"]
    middle = (0, sum(p[0] for p in trunk) / len(trunk), s["height"])

    def body():
        b.tube([(0, y, z) for y, z, _, _ in trunk], [w for _, _, w, _ in trunk], [h for _, _, _, h in trunk], s["coat"], 20)
        for center, radii, material in s.get("patches", []):
            b.round(center, radii, material)
        for extra in s.get("body_extras", []):
            extra(b)

    b.part("body", middle, body)

    neck_points = s["neck"]

    def neck():
        b.tube([(0, y, z) for y, z, _, _ in neck_points], [w for _, _, w, _ in neck_points], [h for _, _, _, h in neck_points], s.get("neck_coat", s["coat"]), 16)
        for extra in s.get("neck_extras", []):
            extra(b)

    b.part("neck", (0, neck_points[0][0], neck_points[0][1]), neck, "body")

    r = s["leg_radius"]
    for side, (x, y, top) in (("fl", s["front"]), ("fr", (-s["front"][0], s["front"][1], s["front"][2])),
                              ("bl", s["back"]), ("br", (-s["back"][0], s["back"][1], s["back"][2]))):
        front = side[0] == "f"
        joint_z = top * (0.46 if front else 0.5)
        joint_y = y + (0.02 if front else -0.1 * top)
        coat = s.get("leg_coat", s["coat"])

        def upper(x=x, y=y, top=top, front=front, joint_y=joint_y, joint_z=joint_z, coat=coat):
            if front:
                b.tube([(x, y, top + 0.05), (x, y + 0.01, top * 0.75), (x, joint_y, joint_z)], [r * 2.0, r * 1.5, r * 1.0], [r * 2.6, r * 1.7, r * 1.05], coat, 10)
            else:
                b.tube([(x, y + 0.04, top + 0.06), (x, y - 0.02, top * 0.78), (x, joint_y, joint_z)], [r * 2.4, r * 1.7, r * 0.95], [r * 3.4, r * 2.1, r * 1.05], coat, 10)
            b.round((x, joint_y, joint_z), (r * 1.05, r * 1.15, r * 1.1), coat, 2)

        b.part("leg_" + side, (x, y, top), upper, "body")

        def shin(x=x, joint_y=joint_y, joint_z=joint_z, front=front, coat=coat):
            fetlock = (x, joint_y + (0.0 if front else 0.06), 0.11)
            hoof = (x, fetlock[1] + 0.035, 0.035)
            b.tube([(x, joint_y, joint_z), (x, (joint_y + fetlock[1]) / 2, (joint_z + 0.11) / 2), fetlock], [r * 0.95, r * 0.72, r * 0.78], [r * 1.0, r * 0.78, r * 0.85], s.get("shin_coat", coat), 9)
            b.tube([fetlock, hoof], [r * 0.8, r * 0.95], [r * 0.85, r * 0.6], s.get("shin_coat", coat), 8)
            b.round((x, hoof[1] + 0.012, 0.035), (r * 1.0, r * 1.25, 0.04), s["hoof"], 2)

        b.part("shin_" + side, (x, joint_y, joint_z), shin, "leg_" + side)

    head = s["head"]

    def build_head():
        b.tube([(0, y, z) for y, z, _, _ in head], [w for _, _, w, _ in head], [h for _, _, _, h in head], s["face"], 16)
        my, mz, mr = s["muzzle"]
        b.round((0, my, mz), (mr * 1.05, mr * 0.9, mr * 0.85), s["nose"])
        for side in (-1, 1):
            ex, ey, ez = s["eyes"]
            b.round((side * ex, ey, ez), (0.022, 0.03, 0.032), s.get("eye", "eye_black"), 2)
            if s.get("eye", "eye_black") != "eye_black":
                b.round((side * (ex + 0.004), ey + 0.016, ez), (0.008, 0.02, 0.022), "eye_black", 1)
            b.round((side * (ex + 0.006), ey + 0.012, ez + 0.012), (0.007, 0.007, 0.007), "eye_glint", 1)
            b.round((side * mr * 0.4, my + mr * 0.82, mz + mr * 0.1), (mr * 0.13, mr * 0.1, mr * 0.17), "eye_black", 1)
            for horn in s.get("horns", []):
                pts = [(side * px, py, pz) for px, py, pz in horn["points"]]
                b.tube(pts, horn["radii"], horn["radii"], horn["material"], 8)
        for extra in s.get("head_extras", []):
            extra(b)

    hy, hz = s["head_pivot"]
    b.part("head", (0, hy, hz), build_head, "neck")

    if "jaw" in s:
        jaw_spec = s["jaw"]

        def jaw():
            b.tube([(0, y, z) for y, z, _, _ in jaw_spec["sections"]], [w for _, _, w, _ in jaw_spec["sections"]],
                   [h for _, _, _, h in jaw_spec["sections"]], s["face"], 12)
            for extra in jaw_spec.get("extras", []):
                extra(b)

        b.part("jaw", jaw_spec["pivot"], jaw, "head")

    for side, name in ((1, "ear_l"), (-1, "ear_r")):
        ex, ey, ez, length, width, droop = s["ear"]

        def ear(side=side, ex=ex, ey=ey, ez=ez, length=length, width=width, droop=droop):
            tip = (side * (ex + length * math.cos(droop)), ey - 0.03, ez + length * math.sin(droop))
            mid = ((side * ex + tip[0]) / 2, ey - 0.01, (ez + tip[2]) / 2)
            b.tube([(side * ex, ey, ez), mid, tip], [width * 0.45, width, width * 0.15], [width * 0.3, width * 0.32, width * 0.1], s.get("ear_coat", s["face"]), 8)
            b.tube([(side * (ex + 0.01), ey + 0.012, ez), (mid[0], ey + 0.004, mid[2]), (tip[0] * 0.95, ey - 0.02, tip[2])],
                   [width * 0.3, width * 0.65, width * 0.08], [width * 0.12, width * 0.14, width * 0.05], s.get("ear_inner", "ear_pink"), 6)

        b.part(name, (side * ex, ey, ez), ear, "head")

    ty, tz, length, tuft = s["tail"]

    def tail():
        if "brush" in s:
            # A bushy tail that hangs back and down, thickest past the middle, with a coloured tip.
            widths, droop = s["brush"]
            points = [(0, ty - math.sin(droop) * length * f, tz - math.cos(droop) * length * f) for f in (0.0, 0.2, 0.45, 0.7, 0.88, 1.0)]
            b.tube(points, widths, [w * 1.15 for w in widths], s.get("tail_coat", s["coat"]), 10)
            if tuft:
                tip = points[-1]
                b.round((0, tip[1] + math.sin(droop) * 0.03, tip[2] + math.cos(droop) * 0.03), (widths[-2] * 0.8, widths[-2] * 0.8, widths[-2] * 1.1), tuft, 2)
            return
        b.tube([(0, ty, tz), (0, ty - 0.06, tz - length * 0.45), (0, ty - 0.07, tz - length)], [0.03, 0.022, 0.016], [0.035, 0.024, 0.016], s.get("tail_coat", s["coat"]), 7)
        if tuft:
            b.round((0, ty - 0.07, tz - length - 0.04), (0.045, 0.045, 0.1), tuft, 2)

    b.part("tail", (0, ty, tz), tail, "body")


def antlers(b):
    for side in (-1, 1):
        b.limb([(side * 0.05, 0.62, 1.66), (side * 0.12, 0.58, 1.84), (side * 0.19, 0.5, 1.98), (side * 0.23, 0.44, 2.1)],
               [0.034, 0.028, 0.02, 0.009], "antler", 7)
        b.limb([(side * 0.08, 0.61, 1.72), (side * 0.1, 0.7, 1.76), (side * 0.11, 0.76, 1.8)], [0.02, 0.015, 0.006], "antler", 6)
        b.limb([(side * 0.12, 0.58, 1.84), (side * 0.15, 0.66, 1.95), (side * 0.16, 0.7, 2.03)], [0.02, 0.015, 0.005], "antler", 6)
        b.limb([(side * 0.19, 0.5, 1.98), (side * 0.25, 0.56, 2.08), (side * 0.27, 0.58, 2.15)], [0.017, 0.012, 0.005], "antler", 6)


def dewlap(b):
    """The loose fold of skin along the underside of the neck. It belongs to the neck, so it ends
    behind the jaw: a fold reaching under the head stuck out below the mouth when the cow grazed."""
    b.tube([(0, 0.7, 0.99), (0, 0.86, 0.99), (0, 0.98, 1.07)], [0.05, 0.055, 0.03], [0.06, 0.08, 0.04], "cow_white", 8)


def hips(points, material):
    def build(b):
        for x, y, z, rad in points:
            for side in (-1, 1):
                b.round((side * x, y, z), (rad, rad * 1.2, rad * 0.8), material, 2)
    return build


def mane(b):
    for i in range(12):
        y = 0.5 - i * 0.075
        z = 0.98 - abs(i - 2) * 0.012 - max(0, i - 5) * 0.018
        b.tube([(0, y, z - 0.03), (0, y - 0.04, z + 0.07)], [0.012, 0.003], [0.035, 0.01], "boar_dark", 4)


def boar_snout(b):
    b.tube([(0, 0.8, 0.5), (0, 0.9, 0.46), (0, 0.98, 0.43)], [0.075, 0.06, 0.06], [0.07, 0.055, 0.055], "boar_coat", 12)
    b.tube([(0, 0.98, 0.43), (0, 1.005, 0.43)], [0.062, 0.062], [0.055, 0.055], "boar_snout", 14)
    for side in (-1, 1):
        b.round((side * 0.022, 1.008, 0.43), (0.012, 0.006, 0.016), "eye_black", 1)


def cow_spots():
    """Irregular black patches: each is a cluster of flat blobs pressed onto the hide."""
    import random as _random
    rng = _random.Random(12)
    spots = []
    for side, y, z in ((1, 0.25, 1.12), (-1, -0.35, 1.06), (1, -0.6, 1.2), (-1, 0.45, 1.16), (0, -0.8, 1.38), (1, 0.75, 1.2)):
        for _ in range(4):
            dy, dz = rng.uniform(-0.14, 0.14), rng.uniform(-0.12, 0.12)
            if side == 0:
                spots.append(((rng.uniform(-0.12, 0.12), y + dy, z + 0.06), (0.12, 0.12, 0.05), "cow_black"))
            else:
                spots.append(((side * 0.4, y + dy, z + dz), (0.05, rng.uniform(0.1, 0.17), rng.uniform(0.09, 0.15)), "cow_black"))
    return spots


def udder(b):
    b.round((0, -0.42, 0.62), (0.13, 0.15, 0.09), "cow_pink", 2)
    for x, y in ((-0.05, -0.37), (0.05, -0.37), (-0.05, -0.48), (0.05, -0.48)):
        b.tube([(x, y, 0.56), (x, y, 0.5)], [0.016, 0.012], [0.016, 0.012], "cow_pink", 6)


def wolf_ruff(coat, pale):
    """The thick fur of a wolf's neck and chest: layered tufts that hide where the neck meets the body."""
    def build(b):
        for ring, (y, z, w, h) in enumerate(((0.5, 0.86, 0.19, 0.22), (0.58, 0.93, 0.17, 0.19), (0.66, 1.0, 0.14, 0.15))):
            for i in range(7):
                # Only round the sides and the throat: tufts along the top read as a spiny crest.
                a = -math.pi * 0.62 + i / 6 * math.pi * 1.24
                x, dz = math.sin(a) * w, -math.cos(a) * h
                colour = pale if dz < -h * 0.4 else coat
                b.tube([(x * 0.85, y, z + dz * 0.85), (x * 1.12, y - 0.07, z + dz * 1.12 - 0.02)], [0.04, 0.004], [0.03, 0.004], colour, 5)
        b.round((0, 0.52, 0.7), (0.12, 0.11, 0.14), pale, 2)
        # The mane: a thick, soft roll of fur over the top of the neck and shoulders.
        b.tube([(0, 0.4, 0.94), (0, 0.52, 1.0), (0, 0.62, 1.06), (0, 0.7, 1.1)], [0.12, 0.14, 0.12, 0.08], [0.07, 0.09, 0.08, 0.05], coat, 12)
    return build


def wolf_brow(coat, dark, pale):
    def build(b):
        for side in (-1, 1):
            # A heavy brow over each eye and pale cheeks that flare back.
            b.tube([(side * 0.04, 0.915, 1.195), (side * 0.085, 0.87, 1.2)], [0.02, 0.014], [0.014, 0.009], dark, 6)
            b.tube([(side * 0.06, 0.95, 1.09), (side * 0.12, 0.82, 1.1), (side * 0.135, 0.75, 1.07)], [0.048, 0.042, 0.007], [0.04, 0.036, 0.007], pale, 6)
        b.tube([(0, 1.0, 1.14), (0, 0.85, 1.22), (0, 0.7, 1.22)], [0.034, 0.06, 0.06], [0.014, 0.024, 0.024], dark, 6)
    return build


def wolf_teeth(b):
    for side in (-1, 1):
        b.tube([(side * 0.03, 1.08, 1.035), (side * 0.032, 1.085, 0.995)], [0.01, 0.001], [0.01, 0.001], "fang", 5)


def wolf_lower_teeth(b):
    for side in (-1, 1):
        b.tube([(side * 0.026, 1.05, 1.005), (side * 0.027, 1.055, 1.035)], [0.008, 0.001], [0.008, 0.001], "fang", 5)


def shadow_wisps(dark, mid):
    """Ragged wisps of shadow that rise off the back and shoulders like black smoke."""
    def build(b):
        for i, y in enumerate((-0.5, -0.36, -0.2, -0.04, 0.12, 0.28, 0.42)):
            for side in (-1, 0, 1):
                if side and i % 2:
                    continue
                # Low, ragged tufts that lie back along the body; the smoke above them is drawn in the game.
                top = 0.91 if y < 0.2 else 0.96
                lean = 0.1 + 0.03 * ((i + side) % 3)
                colour = mid if (i + side) % 3 == 0 else dark
                b.tube([(side * 0.1, y, top - 0.05), (side * 0.15, y - lean, top + 0.02), (side * 0.13, y - lean * 1.9, top + 0.06)],
                       [0.06, 0.035, 0.003], [0.03, 0.02, 0.003], colour, 5)
    return build


def rune_marks(colour):
    """Thin glowing marks curling over each shoulder and haunch."""
    def build(b):
        for side in (-1, 1):
            b.tube([(side * 0.17, 0.42, 0.9), (side * 0.2, 0.34, 0.82), (side * 0.19, 0.26, 0.86), (side * 0.18, 0.2, 0.78)],
                   [0.012, 0.012, 0.012, 0.004], [0.012, 0.012, 0.012, 0.004], colour, 5)
            b.tube([(side * 0.17, -0.3, 0.9), (side * 0.19, -0.38, 0.8), (side * 0.18, -0.46, 0.84)],
                   [0.011, 0.011, 0.004], [0.011, 0.011, 0.004], colour, 5)
    return build


def wolf_spec(coat, dark, pale, size_note="", eye="wolf_eye", extras=()):
    return dict(
        height=0.76, coat=coat, face=coat, nose="wolf_nose", hoof="wolf_paw", leg_radius=0.03, eye=eye,
        ear_inner="wolf_ear", ear_coat=dark, shin_coat=pale,
        trunk=[(-0.58, 0.75, 0.09, 0.11), (-0.5, 0.77, 0.16, 0.18), (-0.3, 0.77, 0.16, 0.18), (-0.08, 0.75, 0.14, 0.16),
               (0.15, 0.77, 0.16, 0.22), (0.35, 0.79, 0.19, 0.26), (0.5, 0.81, 0.17, 0.24), (0.6, 0.85, 0.09, 0.13)],
        neck=[(0.5, 0.88, 0.15, 0.17), (0.62, 1.0, 0.12, 0.13), (0.72, 1.09, 0.105, 0.11)],
        neck_extras=[wolf_ruff(coat, pale)],
        head_pivot=(0.72, 1.1),
        head=[(0.68, 1.17, 0.1, 0.1), (0.79, 1.17, 0.125, 0.115), (0.9, 1.13, 0.092, 0.085), (1.01, 1.08, 0.058, 0.052), (1.11, 1.06, 0.04, 0.034)],
        muzzle=(1.12, 1.065, 0.034), eyes=(0.066, 0.9, 1.17),
        head_extras=[wolf_brow(coat, dark, pale), wolf_teeth],
        jaw=dict(pivot=(0, 0.84, 1.07), sections=[(0.84, 1.06, 0.085, 0.04), (0.97, 1.03, 0.058, 0.03), (1.08, 1.015, 0.035, 0.02)], extras=[wolf_lower_teeth]),
        ear=(0.065, 0.78, 1.25, 0.13, 0.06, 1.3), front=(0.11, 0.42, 0.74), back=(0.11, -0.46, 0.72),
        tail=(-0.58, 0.78, 0.46, pale), brush=([0.03, 0.05, 0.07, 0.065, 0.045, 0.02], 0.55),
        body_extras=[hips([(0.11, -0.47, 0.86, 0.07)], coat), hips([(0.12, 0.44, 0.86, 0.07)], coat), *extras],
        patches=[((0, 0.12, 0.62), (0.11, 0.38, 0.07), pale)],
    )


SPECS = {
    "cow": dict(
        height=1.0, coat="cow_white", face="cow_white", nose="cow_pink", hoof="hoof", leg_radius=0.062, ear_inner="cow_pink",
        trunk=[(-1.0, 1.02, 0.18, 0.2), (-0.92, 1.06, 0.34, 0.36), (-0.7, 1.06, 0.4, 0.4), (-0.35, 1.0, 0.43, 0.45), (0.0, 0.97, 0.44, 0.48),
               (0.35, 1.0, 0.42, 0.46), (0.62, 1.06, 0.36, 0.44), (0.82, 1.1, 0.27, 0.36), (0.94, 1.14, 0.14, 0.22)],
        neck=[(0.66, 1.18, 0.22, 0.3), (0.86, 1.24, 0.18, 0.25), (1.04, 1.3, 0.15, 0.2)],
        neck_extras=[dewlap],
        head_pivot=(1.04, 1.32),
        head=[(1.0, 1.42, 0.12, 0.13), (1.08, 1.43, 0.15, 0.15), (1.2, 1.37, 0.14, 0.14), (1.34, 1.24, 0.12, 0.12), (1.44, 1.13, 0.115, 0.1)],
        muzzle=(1.47, 1.1, 0.12), eyes=(0.135, 1.15, 1.43),
        horns=[dict(points=[(0.09, 1.04, 1.53), (0.2, 1.03, 1.57), (0.25, 1.07, 1.65)], radii=[0.03, 0.024, 0.007], material="horn_pale")],
        ear=(0.15, 1.06, 1.47, 0.18, 0.08, -0.15), front=(0.24, 0.58, 0.9), back=(0.24, -0.66, 0.92),
        tail=(-1.0, 1.1, 0.68, "cow_black"),
        body_extras=[udder, hips([(0.27, -0.78, 1.28, 0.1)], "cow_white")],
        patches=cow_spots(),
    ),
    "goat": dict(
        height=0.66, coat="goat_coat", face="goat_coat", nose="goat_dark", hoof="hoof", leg_radius=0.036,
        trunk=[(-0.52, 0.68, 0.1, 0.12), (-0.45, 0.71, 0.2, 0.22), (-0.2, 0.7, 0.23, 0.25), (0.1, 0.69, 0.24, 0.26),
               (0.32, 0.72, 0.21, 0.24), (0.46, 0.75, 0.15, 0.19), (0.53, 0.78, 0.08, 0.12)],
        neck=[(0.38, 0.8, 0.12, 0.15), (0.5, 0.92, 0.09, 0.11), (0.58, 1.01, 0.08, 0.09)],
        head_pivot=(0.58, 1.03),
        head=[(0.55, 1.1, 0.07, 0.08), (0.62, 1.1, 0.085, 0.09), (0.72, 1.04, 0.075, 0.075), (0.82, 0.96, 0.055, 0.055), (0.88, 0.92, 0.045, 0.04)],
        muzzle=(0.89, 0.91, 0.05), eyes=(0.075, 0.67, 1.11),
        horns=[dict(points=[(0.04, 0.6, 1.16), (0.06, 0.53, 1.27), (0.08, 0.43, 1.31), (0.1, 0.36, 1.26)], radii=[0.026, 0.021, 0.014, 0.005], material="horn_dark")],
        head_extras=[lambda b: b.tube([(0, 0.82, 0.88), (0, 0.81, 0.8), (0, 0.79, 0.72)], [0.028, 0.022, 0.004], [0.018, 0.018, 0.004], "goat_beard", 6)],
        ear=(0.085, 0.62, 1.08, 0.13, 0.045, -0.2), front=(0.12, 0.33, 0.6), back=(0.12, -0.4, 0.6),
        tail=(-0.53, 0.78, 0.1, None),
        body_extras=[hips([(0.12, -0.42, 0.86, 0.06)], "goat_coat")],
    ),
    "deer": dict(
        height=0.98, coat="deer_coat", face="deer_coat", nose="eye_black", hoof="hoof", leg_radius=0.03, shin_coat="deer_leg",
        trunk=[(-0.62, 1.0, 0.11, 0.13), (-0.55, 1.03, 0.21, 0.24), (-0.3, 1.01, 0.23, 0.26), (0.0, 0.99, 0.23, 0.27),
               (0.25, 1.0, 0.22, 0.27), (0.45, 1.06, 0.18, 0.24), (0.57, 1.12, 0.1, 0.15)],
        neck=[(0.42, 1.14, 0.12, 0.16), (0.53, 1.32, 0.085, 0.11), (0.6, 1.48, 0.075, 0.09)],
        head_pivot=(0.6, 1.5),
        head=[(0.56, 1.58, 0.07, 0.08), (0.65, 1.58, 0.085, 0.085), (0.76, 1.52, 0.065, 0.065), (0.86, 1.45, 0.045, 0.048), (0.93, 1.41, 0.034, 0.036)],
        muzzle=(0.94, 1.4, 0.04), eyes=(0.07, 0.69, 1.58), head_extras=[antlers],
        ear=(0.07, 0.6, 1.63, 0.17, 0.06, 0.4), front=(0.12, 0.42, 0.92), back=(0.12, -0.48, 0.92),
        tail=(-0.63, 1.08, 0.13, "deer_belly"),
        body_extras=[hips([(0.11, -0.5, 1.17, 0.06)], "deer_coat")],
        patches=[((0, -0.05, 0.84), (0.17, 0.42, 0.09), "deer_belly"), ((0, -0.62, 1.0), (0.1, 0.05, 0.13), "deer_belly")],
    ),
    "sheep": dict(
        height=0.62, coat="wool", face="sheep_face", nose="sheep_face", hoof="hoof", leg_radius=0.03, leg_coat="wool", shin_coat="sheep_face", ear_coat="sheep_face",
        trunk=[(-0.48, 0.68, 0.2, 0.2), (-0.4, 0.71, 0.32, 0.31), (-0.15, 0.73, 0.36, 0.33), (0.12, 0.73, 0.36, 0.33),
               (0.34, 0.74, 0.3, 0.3), (0.46, 0.76, 0.2, 0.22), (0.52, 0.78, 0.1, 0.13)],
        neck=[(0.38, 0.8, 0.17, 0.19), (0.5, 0.87, 0.12, 0.13)],
        head_pivot=(0.52, 0.89),
        head=[(0.5, 0.95, 0.085, 0.095), (0.59, 0.94, 0.09, 0.095), (0.7, 0.87, 0.07, 0.075), (0.78, 0.81, 0.05, 0.05)],
        muzzle=(0.79, 0.8, 0.05), eyes=(0.08, 0.63, 0.95),
        head_extras=[lambda b: b.round((0, 0.55, 1.01), (0.09, 0.08, 0.06), "wool", 2)],
        ear=(0.09, 0.56, 0.94, 0.12, 0.05, -0.5), front=(0.14, 0.3, 0.5), back=(0.14, -0.32, 0.5),
        tail=(-0.5, 0.74, 0.12, "wool"),
        patches=[((x * 0.2, y, 0.73 + z), (0.16, 0.16, 0.14), "wool") for x, y, z in
                 ((1, -0.3, 0.2), (-1, -0.3, 0.2), (1, 0.1, 0.23), (-1, 0.1, 0.23), (0, -0.1, 0.29), (0, 0.28, 0.22), (1, 0.32, 0.08), (-1, 0.32, 0.08), (0, -0.4, 0.14))],
    ),
    "pig": dict(
        height=0.66, coat="boar_coat", face="boar_coat", nose="boar_coat", hoof="hoof", leg_radius=0.034, shin_coat="boar_dark", ear_coat="boar_dark", ear_inner="boar_dark",
        trunk=[(-0.55, 0.58, 0.12, 0.14), (-0.48, 0.6, 0.22, 0.24), (-0.25, 0.62, 0.26, 0.29), (0.05, 0.66, 0.29, 0.34),
               (0.3, 0.7, 0.29, 0.36), (0.46, 0.7, 0.24, 0.32), (0.56, 0.66, 0.15, 0.22)],
        neck=[(0.46, 0.72, 0.22, 0.28), (0.6, 0.64, 0.17, 0.21)],
        head_pivot=(0.6, 0.66),
        head=[(0.56, 0.7, 0.15, 0.17), (0.64, 0.64, 0.14, 0.15), (0.72, 0.56, 0.1, 0.11), (0.8, 0.5, 0.075, 0.075)],
        muzzle=(0.82, 0.48, 0.06), eyes=(0.085, 0.68, 0.68),
        horns=[dict(points=[(0.05, 0.86, 0.44), (0.09, 0.88, 0.5), (0.08, 0.84, 0.57)], radii=[0.02, 0.015, 0.004], material="tusk")],
        head_extras=[boar_snout, lambda b: b.tube([(0, 0.48, 1.02), (0, 0.6, 0.92), (0, 0.68, 0.82)], [0.025, 0.04, 0.025], [0.06, 0.07, 0.04], "boar_dark", 8)],
        ear=(0.09, 0.6, 0.8, 0.12, 0.06, 0.75), front=(0.15, 0.32, 0.56), back=(0.14, -0.4, 0.54),
        tail=(-0.56, 0.66, 0.2, "boar_dark"),
        body_extras=[mane],
        patches=[((0, 0.1, 0.48), (0.2, 0.38, 0.12), "boar_belly")],
    ),
}


SPECS["doe"] = dict(SPECS["deer"], head_extras=[], height=0.94)
SPECS["wolf"] = wolf_spec("wolf_coat", "wolf_dark", "wolf_pale")
# The pack leader: a darker, heavier wolf with a grizzled ruff.
SPECS["alpha_wolf"] = wolf_spec("alpha_coat", "alpha_dark", "alpha_pale")
# Mirrorfang: a dark grey wolf with amber marks over its shoulders; its clones copy this body.
SPECS["mirror_wolf"] = wolf_spec("mirror_coat", "mirror_dark", "mirror_pale", extras=[rune_marks("mirror_rune")])
# Shadowstalker: black as soot, with white eyes and shadow rising off its back like smoke.
SPECS["shadow_wolf"] = wolf_spec("shadow_coat", "shadow_dark", "shadow_pale", eye="shadow_eye",
                                 extras=[shadow_wisps("shadow_dark", "shadow_coat")])


def chicken(b):
    """A plump hen: body, head with comb and beak, two wings and two legs."""
    def body():
        b.round((0, 0, 0.36), (0.15, 0.2, 0.16), "chicken_white")
        b.tube([(0, -0.14, 0.4), (0, -0.24, 0.52), (0, -0.26, 0.6)], [0.09, 0.08, 0.02], [0.05, 0.09, 0.04], "chicken_white", 8)
        b.tube([(0, 0.1, 0.42), (0, 0.15, 0.52), (0, 0.16, 0.58)], [0.07, 0.06, 0.05], [0.08, 0.06, 0.05], "chicken_white", 10)

    b.part("body", (0, 0, 0.36), body)

    def head():
        b.round((0, 0.17, 0.63), (0.055, 0.065, 0.06), "chicken_white", 2)
        b.tube([(0, 0.21, 0.63), (0, 0.27, 0.61)], [0.022, 0.003], [0.016, 0.003], "beak", 6)
        for k in range(3):
            b.round((0, 0.13 + k * 0.035, 0.69 - abs(k - 1) * 0.01), (0.012, 0.022, 0.03), "comb", 1)
        b.round((0, 0.215, 0.585), (0.012, 0.015, 0.025), "comb", 1)
        for side in (-1, 1):
            b.round((side * 0.045, 0.2, 0.645), (0.012, 0.012, 0.014), "eye_black", 1)

    b.part("head", (0, 0.15, 0.58), head, "body")
    for side, name in ((1, "wing_l"), (-1, "wing_r")):
        def wing(side=side):
            b.round((side * 0.14, -0.02, 0.38), (0.035, 0.15, 0.09), "chicken_wing", 2)
        b.part(name, (side * 0.12, 0.06, 0.42), wing, "body")
    for side, name in ((1, "leg_fl"), (-1, "leg_fr")):
        def leg(side=side):
            b.limb([(side * 0.06, 0, 0.24), (side * 0.06, 0.01, 0.12), (side * 0.06, 0.02, 0.02)], [0.016, 0.012, 0.012], "chicken_leg", 6)
            for toe in (-0.5, 0, 0.5):
                b.limb([(side * 0.06, 0.02, 0.015), (side * 0.06 + math.sin(toe) * 0.07, 0.02 + math.cos(toe) * 0.07, 0.01)], [0.01, 0.006], "chicken_leg", 5)
        b.part(name, (side * 0.06, 0, 0.24), leg, "body")


def build(name, add_object, smooth):
    b = Builder(add_object, smooth)
    if name == "chicken":
        chicken(b)
    else:
        quadruped(b, SPECS[name])


COLORS = {
    "cow_white": "F2EEE6", "cow_black": "2A2628", "cow_pink": "E8A8A0", "hoof": "2A2224", "horn_pale": "E8DCC0",
    "goat_coat": "DCD6CC", "goat_dark": "8A7A70", "goat_beard": "ECE8E0", "horn_dark": "6A5A4A",
    "deer_coat": "A8703C", "deer_belly": "EADCC8", "deer_spots": "C8905A",
    "wool": "F6F2EA", "sheep_face": "2A2628",
    "boar_coat": "5A4038", "boar_dark": "3A2A24", "boar_snout": "C08A80", "tusk": "F0E8D8",
    "chicken_white": "F4F0E8", "chicken_wing": "E4DCCE", "comb": "E43B44", "beak": "FEAE34", "chicken_leg": "F7A52A",
    "wolf_coat": "7E8494", "wolf_dark": "3A4466", "wolf_pale": "D6D2C8", "wolf_nose": "181425", "wolf_paw": "8B8A94",
    "wolf_eye": "FEAE34", "wolf_ear": "5A4A50", "fang": "F4EEDC",
    "alpha_coat": "3E3A48", "alpha_dark": "1E1C26", "alpha_pale": "9A98A0",
    "mirror_coat": "5A6170", "mirror_dark": "2E3240", "mirror_pale": "A8ACB8", "mirror_rune": "F77622",
    "shadow_coat": "1C1A28", "shadow_dark": "0E0D14", "shadow_pale": "2E3048", "shadow_eye": "FFFFFF",
    "eye_black": "15161C", "eye_glint": "FFFFFF", "antler": "D8C8A8", "ear_pink": "D89A92", "deer_leg": "8A5A34", "deer_dark": "7A4A26", "boar_belly": "7A5A4C",
}

NAMES = ["cow", "goat", "deer", "doe", "sheep", "pig", "chicken", "wolf", "alpha_wolf", "mirror_wolf", "shadow_wolf"]
