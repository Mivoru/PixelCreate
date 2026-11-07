"""Build low-poly game assets with headless Blender.

Usage: blender -b --factory-startup -P asset-pipeline/build_meshes.py
Writes one .glb per model into client/assets. Colours come from the Endesga 32 palette.
"""
import math
import random
from pathlib import Path

import bmesh
import bpy
import mathutils

OUT = Path(__file__).resolve().parent.parent / "client" / "assets"

COLORS = {
    "grass": "3E8948", "grass_dark": "265C42", "leaf": "63C74D", "path": "D77643",
    "wall": "E8D4B0", "wall_b": "D8C2A0", "roof": "B04A32", "thatch": "B8924E", "wood": "8A5A3C", "wood_dark": "4A3028",
    "stone": "9A9CA8", "stone_dark": "6A6C78", "window": "FEE761", "tunic": "0099DB", "shutter": "3E6A7A",
    "skin": "E0AC8C", "hat": "E4A672", "hair": "5A3A28", "gold": "D8A23C", "water": "2F6F9F",
    "flower": "E8506A", "flower_b": "F0C040", "hay": "D8B85A", "sack": "C8B48C", "cloth_red": "B84040",
    "anime_skin": "F6D8C4", "anime_hair": "2B3044", "anime_shirt": "D8D0C2", "anime_dark": "23252E",
    "anime_trousers": "2E3040", "anime_leather": "5A3A2C", "anime_cloak": "2F4A3C", "anime_gold": "D8A23C",
}
MATERIALS = {}


def linear(channel):
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def make_material(name, hex_color):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    rgb = [linear(int(hex_color[i:i + 2], 16) / 255) for i in (0, 2, 4)]
    shader.inputs["Base Color"].default_value = (*rgb, 1)
    shader.inputs["Roughness"].default_value = 1
    return material


def add_object(name, mesh, material, location=(0, 0, 0)):
    mesh.materials.append(MATERIALS[material])
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    bpy.context.collection.objects.link(obj)


def mesh_from(name, verts, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    return mesh


def box(name, center, size, material):
    x, y, z = (s / 2 for s in size)
    verts = [(sx * x, sy * y, sz * z) for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
    faces = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
    add_object(name, mesh_from(name, verts, faces), material, center)


def roof(name, center, width, depth, height, material):
    w, d = width / 2, depth / 2
    verts = [(-w, -d, 0), (w, -d, 0), (w, d, 0), (-w, d, 0), (-w, 0, height), (w, 0, height)]
    faces = [(0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5), (0, 3, 2, 1)]
    add_object(name, mesh_from(name, verts, faces), material, center)


def blob(name, center, radius, material, seed, squash=1.0, subdivisions=3):
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)
    for vert in bm.verts:
        vert.co *= 1 + rng.uniform(-0.1, 0.1)
        vert.co.z *= squash
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    add_object(name, mesh, material, center)


def cylinder(name, center, radius, height, material, sides=7):
    ring = [(math.cos(i / sides * math.tau), math.sin(i / sides * math.tau)) for i in range(sides)]
    verts = [(x * radius * 1.3, y * radius * 1.3, -height / 2) for x, y in ring]
    verts += [(x * radius, y * radius, height / 2) for x, y in ring]
    faces = [(i, (i + 1) % sides, (i + 1) % sides + sides, i + sides) for i in range(sides)]
    add_object(name, mesh_from(name, verts, faces), material, center)


def ground():
    rng = random.Random(3)
    cells = 28
    size = 44.0
    step = size / cells
    verts = [(x * step - size / 2, y * step - size / 2, rng.uniform(-0.09, 0.09))
             for y in range(cells + 1) for x in range(cells + 1)]
    row = cells + 1
    faces = [(y * row + x, y * row + x + 1, (y + 1) * row + x + 1, (y + 1) * row + x)
             for y in range(cells) for x in range(cells)]
    add_object("ground", mesh_from("ground", verts, faces), "grass")


def tree(prefix, x, y, scale, seed):
    cylinder(f"{prefix}_trunk", (x, y, 1.3 * scale), 0.38 * scale, 2.6 * scale, "wood")
    blob(f"{prefix}_a", (x, y, 3.5 * scale), 1.9 * scale, "grass_dark", seed)
    blob(f"{prefix}_b", (x - 0.8 * scale, y + 0.3 * scale, 4.3 * scale), 1.3 * scale, "grass", seed + 1)
    blob(f"{prefix}_c", (x + 0.9 * scale, y - 0.5 * scale, 4.0 * scale), 1.1 * scale, "leaf", seed + 2)


def start_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATERIALS.clear()
    MATERIALS.update({name: make_material(name, value) for name, value in COLORS.items()})


def export(name):
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(OUT / f"{name}.glb"), export_format="GLB", export_apply=True)


def village():
    ground()
    cottage()
    box("path", (0, -7.6, 0.06), (1.7, 11, 0.12), "path")
    tree("oak", -7.2, 1.8, 1.25, 11)
    tree("birch", 7.4, -2.2, 0.95, 21)
    tree("elm", 6.2, 5.8, 1.1, 31)

    rng = random.Random(9)
    for i in range(9):
        x, y = rng.uniform(-14, 14), rng.uniform(-13, -3)
        if abs(x) > 1.6:
            blob(f"rock{i}", (x, y, 0.16), rng.uniform(0.3, 0.7), "stone_dark", i, 0.7, 1)

    # A figure the height of the player, to judge scale.
    box("legs", (1.6, -4.4, 0.4), (0.42, 0.34, 0.8), "stone_dark")
    box("body", (1.6, -4.4, 1.05), (0.56, 0.42, 0.7), "tunic")
    box("head", (1.6, -4.4, 1.6), (0.46, 0.46, 0.46), "skin")
    box("hat", (1.6, -4.4, 1.9), (0.84, 0.84, 0.1), "hat")


def rock():
    blob("rock", (0, 0, 0.2), 0.6, "stone_dark", 5, 0.7, 1)


def bush():
    blob("bush", (0, 0, 0.35), 0.6, "grass_dark", 8, 0.7, 1)
    blob("bush_top", (0.1, 0.1, 0.55), 0.35, "leaf", 9, 0.8, 1)


def smooth(mesh):
    for polygon in mesh.polygons:
        polygon.use_smooth = True


def loft(name, rings, material, sides=14, location=(0, 0, 0)):
    """A smooth tube through ellipses given as (z, radius x, radius y, centre x, centre y)."""
    verts = []
    for ring in rings:
        z, rx, ry = ring[0], ring[1], ring[2]
        cx = ring[3] if len(ring) > 3 else 0
        cy = ring[4] if len(ring) > 4 else 0
        verts += [(cx + math.cos(i / sides * math.tau) * rx, cy + math.sin(i / sides * math.tau) * ry, z) for i in range(sides)]
    faces = []
    for r in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((r * sides + i, (r + 1) * sides + i, (r + 1) * sides + j, r * sides + j))
    faces.append(tuple(range(sides - 1, -1, -1)))
    top = (len(rings) - 1) * sides
    faces.append(tuple(top + i for i in range(sides)))
    mesh = mesh_from(name, verts, faces)
    smooth(mesh)
    add_object(name, mesh, material, location)


def anime_head():
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1)
    for vert in bm.verts:
        x, y, z = vert.co
        taper = max(0.0, -z)
        vert.co = (x * 0.128 * (1 - 0.62 * taper ** 1.7),
                   y * 0.135 * (1 - 0.3 * taper ** 1.4) + 0.012 * max(0.0, y) * math.exp(-(x / 0.2) ** 2 - ((z + 0.3) / 0.2) ** 2),
                   z * (0.15 if z > 0 else 0.178))
    mesh = bpy.data.meshes.new("head")
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object("head", mesh, "anime_skin")
    loft("neck", [(-0.27, 0.048, 0.05, 0, -0.01), (-0.12, 0.04, 0.042, 0, -0.012)], "anime_skin", 10)


def strand(bm, start, end, bend, width, sides=5, steps=6):
    start, end, bend = (mathutils.Vector(v) for v in (start, end, bend))
    axis = (end - start).normalized()
    side = axis.cross(mathutils.Vector((0.3, 0.2, 1))).normalized()
    other = axis.cross(side).normalized()
    rings = []
    for step in range(steps + 1):
        t = step / steps
        centre = start.lerp(end, t) + bend * math.sin(t * math.pi)
        radius = width * (1 - t) ** 0.75 + 0.0008
        ring = []
        for i in range(sides):
            angle = i / sides * math.tau
            ring.append(bm.verts.new(centre + side * math.cos(angle) * radius + other * math.sin(angle) * radius * 0.55))
        rings.append(ring)
    for r in range(steps):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((rings[r][i], rings[r + 1][i], rings[r + 1][j], rings[r][j]))


def anime_hair():
    """A cap of hair and many sharp clumps that fall around the face and over the brow."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < (0.18 if v.co.y > 0.2 else -0.42)], context="VERTS")
    for vert in bm.verts:
        vert.co = (vert.co.x * 0.139, vert.co.y * 0.146, vert.co.z * 0.164 + 0.008)
    rng = random.Random(14)
    for index in range(11):
        # The fringe: uneven points over the forehead.
        x = -0.115 + index * 0.023
        length = rng.uniform(0.075, 0.125)
        strand(bm, (x, 0.118, 0.1), (x * 1.1 + rng.uniform(-0.02, 0.02), 0.14, 0.1 - length), (0, 0.016, 0), 0.024)
    for index in range(10):
        # Long locks that frame the sides of the face.
        side = -1 if index % 2 else 1
        y = rng.uniform(-0.02, 0.09)
        drop = rng.uniform(0.15, 0.24)
        strand(bm, (side * 0.118, y, 0.08), (side * rng.uniform(0.13, 0.155), y + 0.01, -drop + 0.03), (side * 0.024, 0, 0), 0.027)
    for index in range(26):
        # The messy back and crown, pointing out and down.
        angle = rng.uniform(0.15, math.pi - 0.15)
        height = rng.uniform(-0.1, 0.13)
        x, y = math.cos(angle) * 0.13, -math.sin(angle) * 0.13
        length = rng.uniform(0.09, 0.17)
        strand(bm, (x, y, height), (x * 1.5, y * 1.5, height - length), (x * 0.25, y * 0.25, 0.015), 0.034)
    for index in range(9):
        # Tufts that stick out at the crown.
        angle = rng.uniform(0, math.tau)
        x, y = math.cos(angle) * 0.075, math.sin(angle) * 0.075 - 0.02
        strand(bm, (x, y, 0.15), (x * 2.1, y * 2.1 - 0.02, 0.2 + rng.uniform(-0.02, 0.05)), (0, 0, 0.012), 0.024)
    strand(bm, (0.0, 0.03, 0.17), (0.05, 0.08, 0.26), (0.03, -0.02, 0.02), 0.012)
    mesh = bpy.data.meshes.new("hair")
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object("hair", mesh, "anime_hair")


def anime_torso():
    """Lean shoulders, a shirt tucked at the belt and a soft collar."""
    loft("shirt", [(-0.12, 0.138, 0.095), (0, 0.118, 0.082), (0.14, 0.12, 0.085), (0.3, 0.15, 0.092), (0.385, 0.17, 0.088),
                   (0.44, 0.14, 0.07), (0.47, 0.07, 0.06)], "anime_shirt", 18)
    loft("collar", [(0.44, 0.075, 0.07), (0.5, 0.058, 0.058), (0.54, 0.062, 0.062)], "anime_dark", 14)
    loft("belt", [(-0.03, 0.128, 0.09), (0.03, 0.123, 0.087)], "anime_leather", 18)
    box("buckle", (0, 0.092, 0.0), (0.052, 0.012, 0.05), "anime_gold")
    loft("sash", [(-0.32, 0.03, 0.012, 0.09, 0.082), (-0.03, 0.04, 0.014, 0.09, 0.09)], "anime_cloak", 8)


def anime_pelvis():
    loft("pelvis", [(-0.14, 0.13, 0.095), (-0.04, 0.142, 0.1), (0.1, 0.125, 0.09)], "anime_trousers", 16)


def anime_cloak():
    """A short mantle over the shoulders that falls to a ragged hem behind the legs."""
    bm = bmesh.new()
    rings = []
    rows, columns = 9, 22
    rng = random.Random(6)
    hem = [rng.uniform(-0.05, 0.06) for _ in range(columns + 1)]
    for row in range(rows):
        t = row / (rows - 1)
        ring = []
        for column in range(columns + 1):
            u = column / columns
            angle = math.pi * (-0.14 + 1.28 * u)
            width = 0.17 + 0.08 * t
            z = 0.0 - t * (0.88 + hem[column])
            fold = 0.012 * math.sin(u * 22) * t
            ring.append(bm.verts.new((math.cos(angle) * (width + fold), -math.sin(angle) * (0.1 + 0.11 * t) + 0.0, z)))
        rings.append(ring)
    for row in range(rows - 1):
        for column in range(columns):
            bm.faces.new((rings[row][column], rings[row + 1][column], rings[row + 1][column + 1], rings[row][column + 1]))
    mesh = bpy.data.meshes.new("cloak")
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object("cloak", mesh, "anime_cloak")
    loft("mantle", [(0.0, 0.205, 0.135), (0.05, 0.19, 0.125), (0.1, 0.12, 0.1)], "anime_cloak", 18)


def anime_upper_arm():
    loft("upper", [(-0.3, 0.036, 0.036), (-0.16, 0.044, 0.044), (-0.02, 0.05, 0.05), (0.03, 0.03, 0.03)], "anime_shirt", 12)


def anime_forearm():
    loft("sleeve", [(-0.14, 0.034, 0.034), (0, 0.036, 0.036)], "anime_shirt", 12)
    loft("glove", [(-0.255, 0.031, 0.029), (-0.2, 0.031, 0.03), (-0.13, 0.04, 0.04), (-0.1, 0.042, 0.042)], "anime_dark", 12)
    loft("hand", [(-0.345, 0.022, 0.016), (-0.3, 0.036, 0.019), (-0.255, 0.033, 0.022)], "anime_dark", 10)
    loft("thumb", [(-0.31, 0.009, 0.009, 0.032, 0.016), (-0.27, 0.013, 0.013, 0.03, 0.012)], "anime_dark", 8)


def anime_thigh():
    loft("thigh", [(-0.47, 0.054, 0.058), (-0.25, 0.07, 0.074), (-0.06, 0.086, 0.09), (0.04, 0.07, 0.07)], "anime_trousers", 14)


def anime_shin():
    loft("calf", [(-0.27, 0.042, 0.044), (-0.13, 0.055, 0.058, 0, -0.012), (0.0, 0.054, 0.056)], "anime_trousers", 14)
    loft("boot", [(-0.45, 0.054, 0.08, 0, 0.02), (-0.4, 0.051, 0.075, 0, 0.018), (-0.33, 0.046, 0.052), (-0.2, 0.054, 0.056), (-0.17, 0.064, 0.066)], "anime_leather", 14)
    loft("toe", [(-0.46, 0.05, 0.1, 0, 0.09), (-0.4, 0.044, 0.08, 0, 0.08)], "anime_leather", 12)
    loft("cuff", [(-0.2, 0.066, 0.068), (-0.155, 0.068, 0.07)], "anime_dark", 14)


def timber(name, start, end, size, material="wood"):
    start, end = mathutils.Vector(start), mathutils.Vector(end)
    centre = (start + end) / 2
    length = (end - start).length
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for vert in bm.verts:
        vert.co = (vert.co.x * size, vert.co.y * size, vert.co.z * length)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(MATERIALS[material])
    obj = bpy.data.objects.new(name, mesh)
    obj.location = centre
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = (end - start).to_track_quat("Z", "Y")
    bpy.context.collection.objects.link(obj)


def house(width, depth, height, wall, roof_material, door_x=0.0, floors=1):
    """A timber-framed house on a stone footing, with an overhanging roof, shutters and a chimney."""
    w, d = width / 2, depth / 2
    box("footing", (0, 0, 0.22), (width + 0.25, depth + 0.25, 0.44), "stone_dark")
    box("walls", (0, 0, height / 2 + 0.4), (width, depth, height), wall)
    top = height + 0.4
    for x in (-w, w, -w / 3, w / 3):
        timber("post", (x, -d - 0.04, 0.4), (x, -d - 0.04, top), 0.17)
        timber("post", (x, d + 0.04, 0.4), (x, d + 0.04, top), 0.17)
    for y in (-d, d):
        timber("post", (-w - 0.04, y, 0.4), (-w - 0.04, y, top), 0.17)
        timber("post", (w + 0.04, y, 0.4), (w + 0.04, y, top), 0.17)
    for z in [0.45, top - 0.05] + ([height * 0.52 + 0.4] if floors > 1 else []):
        timber("beam", (-w - 0.08, -d - 0.05, z), (w + 0.08, -d - 0.05, z), 0.15)
        timber("beam", (-w - 0.08, d + 0.05, z), (w + 0.08, d + 0.05, z), 0.15)
        timber("beam", (-w - 0.05, -d, z), (-w - 0.05, d, z), 0.15)
        timber("beam", (w + 0.05, -d, z), (w + 0.05, d, z), 0.15)
    timber("brace", (-w, -d - 0.06, 0.5), (-w / 3, -d - 0.06, top - 0.1), 0.11)
    timber("brace", (w, -d - 0.06, 0.5), (w / 3, -d - 0.06, top - 0.1), 0.11)
    rise = width * 0.42
    roof("roof", (0, 0, top), width + 1.1, depth + 1.0, rise, roof_material)
    timber("ridge", (-width / 2 - 0.55, 0, top + rise + 0.03), (width / 2 + 0.55, 0, top + rise + 0.03), 0.14, "wood_dark")
    box("chimney", (width * 0.25, depth * 0.1, top + rise * 0.8), (0.75, 0.75, rise * 1.2 + 0.4), "stone")
    box("chimney_cap", (width * 0.25, depth * 0.1, top + rise * 1.42), (0.95, 0.95, 0.14), "stone_dark")
    box("door", (door_x, -d - 0.05, 1.46), (1.15, 0.1, 2.1), "wood")
    timber("lintel", (door_x - 0.7, -d - 0.12, 2.55), (door_x + 0.7, -d - 0.12, 2.55), 0.16)
    box("step", (door_x, -d - 0.45, 0.14), (1.6, 0.9, 0.28), "stone")
    box("handle", (door_x + 0.42, -d - 0.13, 1.35), (0.09, 0.06, 0.09), "gold")
    for x in (door_x - w * 0.62, door_x + w * 0.62):
        box("frame", (x, -d - 0.06, 1.95), (1.15, 0.08, 1.15), "wood_dark")
        box("glass", (x, -d - 0.1, 1.95), (0.9, 0.04, 0.9), "window")
        timber("mullion", (x, -d - 0.13, 1.5), (x, -d - 0.13, 2.4), 0.05, "wood_dark")
        timber("mullion", (x - 0.45, -d - 0.13, 1.95), (x + 0.45, -d - 0.13, 1.95), 0.05, "wood_dark")
        for side in (-1, 1):
            box("shutter", (x + side * 0.82, -d - 0.08, 1.95), (0.42, 0.06, 1.1), "shutter")
        box("planter", (x, -d - 0.26, 1.3), (1.15, 0.3, 0.2), "wood")
        for offset in (-0.4, -0.15, 0.12, 0.38):
            blob(f"bloom{offset}", (x + offset, -d - 0.26, 1.46), 0.13, "flower" if offset < 0 else "flower_b", int(offset * 10 + 50), 0.8, 1)


def cottage():
    house(6.4, 4.8, 2.5, "wall", "roof")


def cottage_b():
    house(5.6, 4.6, 2.4, "wall_b", "thatch", 0.8)
    box("lean_to", (3.7, 0.2, 1.1), (1.8, 3.2, 2.2), "wood")
    roof("lean_roof", (3.7, 0.2, 2.2), 2.4, 3.8, 0.9, "thatch")


def tavern():
    house(9.2, 6, 4.4, "wall", "roof", 0.0, 2)
    timber("sign_arm", (5.4, -3.2, 3.2), (4.3, -3.2, 3.2), 0.1)
    box("sign", (5.2, -3.2, 2.7), (0.9, 0.08, 0.7), "wood_dark")
    box("sign_face", (5.2, -3.26, 2.7), (0.7, 0.05, 0.5), "gold")


def well():
    loft("well_wall", [(0, 1.0, 1.0), (0.85, 0.92, 0.92), (0.95, 1.0, 1.0)], "stone", 14)
    loft("water", [(0.8, 0.78, 0.78), (0.82, 0.78, 0.78)], "water", 14)
    for side in (-1, 1):
        timber("post", (side * 0.95, 0, 0.8), (side * 0.95, 0, 2.9), 0.15)
    timber("axle", (-1, 0, 2.3), (1, 0, 2.3), 0.09, "wood_dark")
    roof("well_roof", (0, 0, 2.8), 2.7, 2.1, 0.9, "thatch")
    loft("bucket", [(1.2, 0.18, 0.18), (1.55, 0.22, 0.22)], "wood", 9)


def stall():
    for x, y in ((-1.5, -1), (1.5, -1), (-1.5, 1), (1.5, 1)):
        timber("post", (x, y, 0), (x, y, 2.5 if y > 0 else 2.15), 0.11)
    box("counter", (0, -0.65, 0.9), (3.2, 0.9, 0.12), "wood")
    box("front", (0, -1.05, 0.45), (3.1, 0.08, 0.9), "wood_dark")
    awning = [(-1.75, -1.45, 2.05), (1.75, -1.45, 2.05), (1.75, 1.2, 2.6), (-1.75, 1.2, 2.6)]
    add_object("awning", mesh_from("awning", awning, [(0, 1, 2, 3)]), "cloth_red")
    for i in range(6):
        x = -1.2 + i * 0.48
        blob(f"goods{i}", (x, -0.65, 1.05), 0.15, ("flower", "leaf", "flower_b", "gold")[i % 4], i, 0.9, 1)


def barrel():
    loft("barrel", [(0, 0.34, 0.34), (0.35, 0.42, 0.42), (0.7, 0.42, 0.42), (1.05, 0.34, 0.34)], "wood", 12)
    for z in (0.15, 0.9):
        loft("hoop", [(z, 0.39, 0.39), (z + 0.07, 0.39, 0.39)], "stone_dark", 12)


def crate():
    box("crate", (0, 0, 0.45), (0.9, 0.9, 0.9), "wood")
    for z in (0.06, 0.84):
        timber("band", (-0.47, -0.47, z), (0.47, -0.47, z), 0.1, "wood_dark")
        timber("band", (-0.47, 0.47, z), (0.47, 0.47, z), 0.1, "wood_dark")
    timber("cross", (-0.42, -0.47, 0.08), (0.42, -0.47, 0.82), 0.09, "wood_dark")


def sacks():
    blob("sack_a", (0, 0, 0.35), 0.42, "sack", 3, 0.95, 2)
    blob("sack_b", (0.5, 0.2, 0.3), 0.35, "sack", 4, 0.9, 2)
    blob("sack_c", (0.2, 0.05, 0.75), 0.32, "sack", 5, 0.9, 2)


def cart():
    box("bed", (0, 0, 0.9), (2.6, 1.4, 0.14), "wood")
    for side in (-1, 1):
        box("rail", (0, side * 0.7, 1.15), (2.6, 0.08, 0.4), "wood_dark")
        loft("wheel", [(-0.07, 0.62, 0.62), (0.07, 0.62, 0.62)], "wood_dark", 14, (0, 0, 0))
    for obj in [o for o in bpy.context.collection.objects if o.name.startswith("wheel")]:
        obj.rotation_euler = (math.pi / 2, 0, 0)
    for index, obj in enumerate(o for o in bpy.context.collection.objects if o.name.startswith("wheel")):
        obj.location = (-0.4, -0.8 if index == 0 else 0.8, 0.62)
    timber("shaft", (1.3, -0.3, 0.9), (2.9, -0.3, 0.5), 0.09)
    timber("shaft", (1.3, 0.3, 0.9), (2.9, 0.3, 0.5), 0.09)
    blob("hay_load", (-0.1, 0, 1.3), 0.75, "hay", 6, 0.6, 2)


def bench():
    box("seat", (0, 0, 0.5), (1.9, 0.5, 0.1), "wood")
    for x in (-0.75, 0.75):
        box("leg", (x, 0, 0.24), (0.14, 0.42, 0.48), "wood_dark")


def lantern_post():
    timber("pole", (0, 0, 0), (0, 0, 2.9), 0.13, "wood_dark")
    timber("arm", (0, 0, 2.75), (0.6, 0, 2.75), 0.08, "wood_dark")
    box("lamp_top", (0.6, 0, 2.55), (0.3, 0.3, 0.08), "stone_dark")
    box("lamp", (0.6, 0, 2.36), (0.2, 0.2, 0.3), "window")
    box("lamp_base", (0.6, 0, 2.18), (0.26, 0.26, 0.06), "stone_dark")


def signpost():
    timber("pole", (0, 0, 0), (0, 0, 2.3), 0.13)
    box("board_a", (0.4, 0, 2.0), (1.1, 0.08, 0.28), "wood_dark")
    box("board_b", (-0.3, 0, 1.6), (1.0, 0.08, 0.26), "wood")


def hay():
    blob("hay", (0, 0, 0.6), 0.95, "hay", 12, 0.75, 3)
    blob("hay_top", (0.2, 0.1, 1.1), 0.5, "hay", 13, 0.8, 2)


def log_pile():
    for row, count in enumerate((4, 3, 2)):
        for i in range(count):
            y = (i - (count - 1) / 2) * 0.42
            timber("log", (-1.1, y, 0.2 + row * 0.36), (1.1, y, 0.2 + row * 0.36), 0.36)


def fence():
    for x in (-1.5, 0, 1.5):
        timber("post", (x, 0, 0), (x, 0, 1.05), 0.12)
    for z in (0.45, 0.85):
        timber("rail", (-1.6, 0, z), (1.6, 0, z), 0.08)


def stone_wall():
    rng = random.Random(4)
    for i in range(9):
        x = -1.8 + i * 0.45
        blob(f"stone{i}", (x, rng.uniform(-0.1, 0.1), 0.26), 0.3, "stone_dark" if i % 3 else "stone", i, 0.8, 2)
        blob(f"cap{i}", (x + 0.2, rng.uniform(-0.08, 0.08), 0.62), 0.24, "stone", i + 20, 0.75, 2)


def flowers():
    rng = random.Random(21)
    for i in range(9):
        x, y = rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8)
        timber(f"stem{i}", (x, y, 0), (x, y, 0.28), 0.025, "grass")
        blob(f"bloom{i}", (x, y, 0.32), 0.08, ("flower", "flower_b", "window")[i % 3], i, 0.8, 1)


def rock_b():
    blob("big", (0, 0, 0.5), 1.0, "stone_dark", 15, 0.8, 3)
    blob("side", (0.9, 0.3, 0.24), 0.48, "stone", 16, 0.75, 2)


def rock_c():
    for i, (x, y, r) in enumerate(((0, 0, 0.42), (0.55, 0.2, 0.28), (-0.4, 0.4, 0.26), (0.1, -0.5, 0.2))):
        blob(f"stone{i}", (x, y, r * 0.5), r, "stone" if i % 2 else "stone_dark", 30 + i, 0.72, 2)


def mushroom():
    for i, (x, y, h) in enumerate(((0, 0, 0.3), (0.18, 0.1, 0.2), (-0.14, 0.12, 0.16))):
        timber(f"stalk{i}", (x, y, 0), (x, y, h), 0.06, "wall_b")
        blob(f"cap{i}", (x, y, h), 0.13 + h * 0.2, "flower_b" if i else "cloth_red", i, 0.55, 2)


def camp_set():
    box("mat", (0, 0, 0.03), (1.5, 0.9, 0.05), "cloth_red")
    timber("tripod", (-0.5, -1.1, 0), (0, -1.5, 1.3), 0.07, "wood_dark")
    timber("tripod", (0.5, -1.1, 0), (0, -1.5, 1.3), 0.07, "wood_dark")
    timber("tripod", (0, -2, 0), (0, -1.5, 1.3), 0.07, "wood_dark")
    loft("pot", [(0.45, 0.18, 0.18), (0.62, 0.28, 0.28), (0.85, 0.25, 0.25)], "stone_dark", 10, (0, -1.5, 0))
    blob("pack", (1, 0.7, 0.3), 0.35, "sack", 3, 0.95, 2)


def tent():
    roof("canvas", (0, 0, 0), 3.6, 4.2, 2.8, "wall")
    box("pole", (0, 0, 1.4), (0.12, 0.12, 2.8), "wood_dark")


def camp_fire():
    for index, angle in enumerate((0.0, 1.2, 2.3)):
        cylinder(f"log{index}", (math.cos(angle) * 0.25, math.sin(angle) * 0.25, 0.2), 0.11, 0.9, "wood_dark")
    blob("embers", (0, 0, 0.32), 0.3, "window", 4, 1.3, 1)


def palisade():
    for index in range(9):
        x = -3.9 + index * 0.98
        cylinder(f"stake{index}", (x, 0, 1.4), 0.33, 2.9 + (index % 3) * 0.25, "wood")


def ruin():
    box("base", (0, 0, 0.25), (2.3, 2.3, 0.5), "stone")
    cylinder("column", (0, 0, 2.2), 0.72, 3.5, "stone", 8)
    blob("rubble", (1.4, 0.8, 0.2), 0.55, "stone_dark", 2, 0.7, 1)


MODELS = {
    "village": village,
    "cottage": cottage,
    "tree_a": lambda: tree("a", 0, 0, 1.0, 11),
    "tree_b": lambda: tree("b", 0, 0, 1.0, 21),
    "rock": rock,
    "bush": bush,
    "anime_head": anime_head,
    "anime_hair": anime_hair,
    "anime_torso": anime_torso,
    "anime_pelvis": anime_pelvis,
    "anime_cloak": anime_cloak,
    "anime_upper_arm": anime_upper_arm,
    "anime_forearm": anime_forearm,
    "anime_thigh": anime_thigh,
    "anime_shin": anime_shin,
    "cottage_b": cottage_b,
    "tavern": tavern,
    "well": well,
    "stall": stall,
    "barrel": barrel,
    "crate": crate,
    "sacks": sacks,
    "cart": cart,
    "bench": bench,
    "lantern_post": lantern_post,
    "signpost": signpost,
    "hay": hay,
    "log_pile": log_pile,
    "fence": fence,
    "stone_wall": stone_wall,
    "flowers": flowers,
    "rock_b": rock_b,
    "rock_c": rock_c,
    "mushroom": mushroom,
    "camp_set": camp_set,
    "tent": tent,
    "camp_fire": camp_fire,
    "palisade": palisade,
    "ruin": ruin,
}

for model_name, build in MODELS.items():
    start_scene()
    build()
    export(model_name)
