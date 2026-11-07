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
    """A four-legged animal from a spec of body sections, head, ears, horns, legs and tail."""
    trunk = s["trunk"]
    middle = (0, sum(p[0] for p in trunk) / len(trunk), s["height"])

    def body():
        b.tube([(0, y, z) for y, z, _, _ in trunk], [w for _, _, w, _ in trunk], [h for _, _, _, h in trunk], s["coat"], 18)
        for center, radii, material in s.get("patches", []):
            b.round(center, radii, material)

    b.part("body", middle, body)

    # The neck is its own part, hinged at the shoulders, so a grazing animal can reach the ground.
    neck_points = s["neck"]

    def neck():
        b.tube([(0, y, z) for y, z, _, _ in neck_points], [w for _, _, w, _ in neck_points], [h for _, _, _, h in neck_points], s.get("neck_coat", s["coat"]), 14)

    b.part("neck", (0, neck_points[0][0], neck_points[0][1]), neck, "body")

    for side, (x, y, top) in (("fl", s["front"]), ("fr", (-s["front"][0], s["front"][1], s["front"][2])),
                              ("bl", s["back"]), ("br", (-s["back"][0], s["back"][1], s["back"][2]))):
        front = side[0] == "f"
        bend = s["knee"] if front else -s["hock"]

        def leg(x=x, y=y, top=top, front=front, bend=bend):
            r = s["leg_radius"]
            points = [(x, y, top), (x, y - bend * 0.3, top * 0.55), (x, y + bend * 0.4, top * 0.22), (x, y + bend * 0.2, 0.06)]
            b.tube(points, [r * 1.5, r, r * 0.8, r * 0.85], [r * 1.9, r * 1.1, r * 0.85, r * 0.85], s.get("leg_coat", s["coat"]), 9)
            b.round((x, y + bend * 0.2, 0.05), (r * 1.05, r * 1.25, 0.06), s["hoof"], 2)
            if s.get("thigh"):
                b.round((x * 0.92, y, top - 0.02), (r * 2.3, r * 3.0, top * 0.22), s["coat"], 2)

        b.part("leg_" + side, (x, y, top), leg, "body")

    head = s["head"]

    def build_head():
        b.tube([(0, y, z) for y, z, _, _ in head], [w for _, _, w, _ in head], [h for _, _, _, h in head], s["face"], 14)
        my, mz, mr = s["muzzle"]
        b.round((0, my, mz), (mr * 1.05, mr * 0.9, mr * 0.85), s["nose"])
        for side in (-1, 1):
            ex, ey, ez = s["eyes"]
            b.round((side * ex, ey, ez), (0.028, 0.03, 0.036), "eye_black", 2)
            b.round((side * (ex + 0.008), ey + 0.012, ez + 0.012), (0.009, 0.009, 0.009), "eye_glint", 1)
            b.round((side * mr * 0.38, my + mr * 0.8, mz + mr * 0.15), (mr * 0.14, mr * 0.12, mr * 0.18), "eye_black", 1)
            for horn in s.get("horns", []):
                pts = [(side * px, py, pz) for px, py, pz in horn["points"]]
                b.tube(pts, horn["radii"], horn["radii"], horn["material"], 8)
        for extra in s.get("head_extras", []):
            extra(b)

    hy, hz = s["head_pivot"]
    b.part("head", (0, hy, hz), build_head, "neck")

    for side, name in ((1, "ear_l"), (-1, "ear_r")):
        ex, ey, ez, length, width, droop = s["ear"]

        def ear(side=side, ex=ex, ey=ey, ez=ez, length=length, width=width, droop=droop):
            tip = (side * (ex + length * math.cos(droop)), ey - 0.02, ez + length * math.sin(droop))
            b.tube([(side * ex, ey, ez), ((side * ex + tip[0]) / 2, ey, (ez + tip[2]) / 2), tip],
                   [width * 0.5, width, width * 0.2], [width * 0.25, width * 0.4, width * 0.12], s["face"], 8)

        b.part(name, (side * ex, ey, ez), ear, "head")

    ty, tz, length, tuft = s["tail"]

    def tail():
        b.tube([(0, ty, tz), (0, ty - 0.05, tz - length * 0.5), (0, ty - 0.06, tz - length)], [0.035, 0.025, 0.02], [0.035, 0.025, 0.02], s["coat"], 7)
        if tuft:
            b.round((0, ty - 0.06, tz - length - 0.03), (0.05, 0.05, 0.09), tuft, 2)

    b.part("tail", (0, ty, tz), tail, "body")


def antlers(b):
    for side in (-1, 1):
        b.limb([(side * 0.05, 0.6, 1.62), (side * 0.11, 0.56, 1.78), (side * 0.17, 0.5, 1.92), (side * 0.21, 0.45, 2.04)],
               [0.022, 0.018, 0.013, 0.006], "antler", 7)
        b.limb([(side * 0.1, 0.565, 1.76), (side * 0.12, 0.64, 1.86), (side * 0.13, 0.67, 1.95)], [0.013, 0.01, 0.004], "antler", 6)
        b.limb([(side * 0.16, 0.505, 1.9), (side * 0.2, 0.56, 2.0), (side * 0.22, 0.58, 2.08)], [0.011, 0.008, 0.003], "antler", 6)


SPECS = {
    "cow": dict(
        height=1.05, coat="cow_white", face="cow_white", nose="cow_pink", hoof="hoof", leg_radius=0.07, knee=0.04, hock=0.08, thigh=True,
        trunk=[(-0.98, 1.0, 0.2, 0.22), (-0.86, 1.05, 0.36, 0.38), (-0.45, 1.04, 0.43, 0.43), (0.05, 1.0, 0.44, 0.45),
               (0.5, 1.04, 0.41, 0.42), (0.78, 1.1, 0.3, 0.36), (0.9, 1.16, 0.16, 0.22)],
        neck=[(0.62, 1.14, 0.24, 0.3), (0.86, 1.26, 0.19, 0.24), (1.02, 1.36, 0.16, 0.19)],
        head_pivot=(1.0, 1.4),
        head=[(0.98, 1.46, 0.14, 0.15), (1.1, 1.44, 0.17, 0.17), (1.26, 1.32, 0.15, 0.14), (1.38, 1.2, 0.13, 0.11)],
        muzzle=(1.42, 1.15, 0.13), eyes=(0.15, 1.15, 1.47),
        horns=[dict(points=[(0.1, 1.06, 1.57), (0.22, 1.06, 1.63), (0.27, 1.1, 1.72)], radii=[0.035, 0.028, 0.008], material="horn_pale")],
        ear=(0.16, 1.06, 1.5, 0.19, 0.07, -0.25), front=(0.24, 0.55, 0.92), back=(0.24, -0.62, 0.92),
        tail=(-0.98, 1.08, 0.62, "cow_black"),
        patches=[((0.3, 0.2, 1.22), (0.2, 0.32, 0.22), "cow_black"), ((-0.33, -0.45, 1.08), (0.16, 0.3, 0.24), "cow_black"),
                 ((0.05, -0.75, 1.24), (0.22, 0.18, 0.12), "cow_black"), ((0, -0.38, 0.62), (0.12, 0.15, 0.08), "cow_pink")],
    ),
    "goat": dict(
        height=0.66, coat="goat_coat", face="goat_coat", nose="goat_dark", hoof="hoof", leg_radius=0.04, knee=0.03, hock=0.05,
        trunk=[(-0.52, 0.66, 0.12, 0.14), (-0.44, 0.7, 0.22, 0.24), (-0.1, 0.7, 0.25, 0.26), (0.25, 0.7, 0.24, 0.26),
               (0.44, 0.74, 0.18, 0.22), (0.52, 0.78, 0.1, 0.14)],
        neck=[(0.36, 0.78, 0.13, 0.16), (0.5, 0.92, 0.1, 0.12), (0.58, 1.02, 0.09, 0.1)],
        head_pivot=(0.58, 1.04),
        head=[(0.55, 1.1, 0.08, 0.09), (0.64, 1.09, 0.1, 0.1), (0.76, 1.0, 0.08, 0.08), (0.84, 0.93, 0.06, 0.06)],
        muzzle=(0.86, 0.91, 0.065), eyes=(0.085, 0.68, 1.11),
        horns=[dict(points=[(0.05, 0.6, 1.17), (0.07, 0.52, 1.28), (0.09, 0.42, 1.31), (0.1, 0.36, 1.25)], radii=[0.03, 0.025, 0.016, 0.006], material="horn_dark")],
        head_extras=[lambda b: b.tube([(0, 0.8, 0.88), (0, 0.79, 0.8), (0, 0.77, 0.72)], [0.03, 0.025, 0.005], [0.02, 0.02, 0.005], "goat_beard", 6)],
        ear=(0.09, 0.62, 1.08, 0.14, 0.05, -0.15), front=(0.13, 0.32, 0.6), back=(0.13, -0.38, 0.6),
        tail=(-0.52, 0.76, 0.12, None),
    ),
    "deer": dict(
        height=0.98, coat="deer_coat", face="deer_coat", nose="eye_black", hoof="hoof", leg_radius=0.033, knee=0.03, hock=0.06,
        trunk=[(-0.62, 0.98, 0.13, 0.15), (-0.52, 1.02, 0.23, 0.26), (-0.15, 1.0, 0.25, 0.27), (0.22, 1.0, 0.24, 0.27),
               (0.45, 1.05, 0.2, 0.24), (0.56, 1.1, 0.12, 0.16)],
        neck=[(0.4, 1.1, 0.13, 0.17), (0.55, 1.3, 0.09, 0.11), (0.62, 1.45, 0.085, 0.095)],
        head_pivot=(0.62, 1.47),
        head=[(0.58, 1.54, 0.08, 0.09), (0.68, 1.54, 0.095, 0.095), (0.8, 1.46, 0.07, 0.07), (0.9, 1.4, 0.045, 0.05)],
        muzzle=(0.92, 1.39, 0.05), eyes=(0.08, 0.72, 1.56), head_extras=[antlers],
        ear=(0.08, 0.62, 1.6, 0.17, 0.06, 0.45), front=(0.13, 0.4, 0.9), back=(0.13, -0.45, 0.9),
        tail=(-0.62, 1.06, 0.14, "deer_belly"),
        patches=[((0, -0.05, 0.86), (0.18, 0.42, 0.1), "deer_belly"), ((0, -0.6, 1.0), (0.1, 0.06, 0.12), "deer_belly"),
                 ((0.18, 0.05, 1.12), (0.08, 0.36, 0.06), "deer_spots")],
    ),
    "sheep": dict(
        height=0.62, coat="wool", face="sheep_face", nose="sheep_face", hoof="hoof", leg_radius=0.032, knee=0.02, hock=0.04, leg_coat="sheep_face",
        trunk=[(-0.48, 0.66, 0.2, 0.2), (-0.4, 0.7, 0.33, 0.32), (-0.05, 0.72, 0.37, 0.34), (0.28, 0.72, 0.35, 0.33),
               (0.44, 0.74, 0.26, 0.27), (0.5, 0.76, 0.15, 0.18)],
        neck=[(0.36, 0.78, 0.18, 0.2), (0.5, 0.86, 0.12, 0.13)],
        head_pivot=(0.52, 0.88),
        head=[(0.5, 0.94, 0.09, 0.1), (0.6, 0.92, 0.1, 0.1), (0.72, 0.84, 0.07, 0.075), (0.78, 0.8, 0.05, 0.05)],
        muzzle=(0.79, 0.79, 0.05), eyes=(0.085, 0.64, 0.95),
        head_extras=[lambda b: b.round((0, 0.56, 1.0), (0.1, 0.09, 0.07), "wool", 2)],
        ear=(0.1, 0.56, 0.93, 0.13, 0.05, -0.55), front=(0.14, 0.3, 0.5), back=(0.14, -0.32, 0.5),
        tail=(-0.5, 0.72, 0.12, "wool"),
        patches=[((x * 0.2, y, 0.72 + z), (0.17, 0.17, 0.15), "wool") for x, y, z in
                 ((1, -0.3, 0.2), (-1, -0.3, 0.2), (1, 0.1, 0.24), (-1, 0.1, 0.24), (0, -0.1, 0.3), (0, 0.3, 0.22), (1, 0.3, 0.1), (-1, 0.3, 0.1), (0, -0.4, 0.15))],
    ),
    "pig": dict(
        height=0.62, coat="boar_coat", face="boar_coat", nose="boar_snout", hoof="hoof", leg_radius=0.045, knee=0.02, hock=0.04, thigh=True,
        trunk=[(-0.55, 0.56, 0.16, 0.18), (-0.46, 0.6, 0.28, 0.3), (-0.1, 0.6, 0.31, 0.34), (0.25, 0.64, 0.32, 0.37),
               (0.45, 0.66, 0.27, 0.32), (0.55, 0.64, 0.18, 0.22)],
        neck=[(0.45, 0.66, 0.24, 0.28), (0.58, 0.62, 0.18, 0.2)],
        head_pivot=(0.58, 0.64),
        head=[(0.55, 0.66, 0.15, 0.17), (0.66, 0.6, 0.14, 0.15), (0.78, 0.5, 0.09, 0.09), (0.86, 0.44, 0.07, 0.07)],
        muzzle=(0.86, 0.44, 0.075), eyes=(0.1, 0.7, 0.66),
        horns=[dict(points=[(0.06, 0.82, 0.42), (0.1, 0.84, 0.48), (0.09, 0.8, 0.55)], radii=[0.022, 0.016, 0.004], material="tusk")],
        head_extras=[lambda b: b.tube([(0, 0.4, 0.98), (0, 0.55, 0.86), (0, 0.66, 0.78)], [0.03, 0.05, 0.03], [0.07, 0.09, 0.05], "boar_dark", 8)],
        ear=(0.1, 0.6, 0.76, 0.12, 0.06, 0.6), front=(0.17, 0.3, 0.52), back=(0.17, -0.38, 0.52),
        tail=(-0.56, 0.62, 0.16, "boar_dark"),
        patches=[((0, 0.05, 0.9), (0.06, 0.45, 0.07), "boar_dark")],
    ),
}


SPECS["doe"] = dict(SPECS["deer"], head_extras=[], height=0.94)


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
    "eye_black": "15161C", "eye_glint": "FFFFFF", "antler": "D8C8A8",
}

NAMES = ["cow", "goat", "deer", "doe", "sheep", "pig", "chicken"]
