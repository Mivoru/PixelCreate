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
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import beasts  # noqa: E402

COLORS = {
    "grass": "3E8948", "grass_dark": "265C42", "leaf": "63C74D", "path": "D77643",
    "wall": "E8D4B0", "wall_b": "D8C2A0", "roof": "B04A32", "thatch": "B8924E", "wood": "8A5A3C", "wood_dark": "4A3028",
    "stone": "9A9CA8", "stone_dark": "6A6C78", "window": "FEE761", "tunic": "0099DB", "shutter": "3E6A7A",
    "skin": "E0AC8C", "hat": "E4A672", "hair": "5A3A28", "gold": "D8A23C", "water": "2F6F9F",
    "flower": "E8506A", "flower_b": "F0C040", "hay": "D8B85A", "sack": "C8B48C", "cloth_red": "B84040",
    "anime_skin": "F6D8C4", "anime_hair": "2B3044", "anime_shirt": "D8D0C2", "anime_dark": "23252E",
    "anime_trousers": "2E3040", "anime_leather": "5A3A2C", "anime_cloak": "2F4A3C", "anime_gold": "D8A23C",
    "foliage_oak": "5F9A3C", "foliage_oak_b": "6FA63E", "foliage_birch": "A8C850", "foliage_blossom": "F4A8C0", "foliage_pine": "2F6A58",
    "foliage_willow": "8FB84A", "foliage_bush": "4F8E42", "foliage_jungle": "3F9048", "foliage_core": "1F4A3A",
    "bark": "6E4E3C", "bark_dark": "4A3634", "bark_birch": "E0DACC", "bark_grey": "8A8078", "reed": "9AA850", "grass_tall": "7FBF52",
    "hide": "8A6A4A", "hide_dark": "4A3428", "sand": "D8C49A", "crop": "D8B85A", "paper": "EEE4C8", "glass": "CFE6F2", "ember": "B03A1E", "anime_glove": "23252E", "anime_vest": "2F4070", "anime_accent": "C84848", "anime_armor": "B8C4D4", "anime_armor_dark": "7A889C",
}
COLORS.update(beasts.COLORS)
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


FOLIAGE_CELLS = {"broad": (0.0, 0.0), "small": (0.5, 0.0), "needles": (0.0, 0.5), "strands": (0.5, 0.5)}


def tube(name, points, radii, material, sides=7):
    """A bent tube through points, narrowing along the given radii."""
    bm = bmesh.new()
    rings = []
    vectors = [mathutils.Vector(p) for p in points]
    for index, point in enumerate(vectors):
        ahead = vectors[min(index + 1, len(vectors) - 1)] - vectors[max(index - 1, 0)]
        axis = ahead.normalized()
        side = axis.cross(mathutils.Vector((0.31, 0.17, 0.93))).normalized()
        other = axis.cross(side).normalized()
        ring = []
        for i in range(sides):
            angle = i / sides * math.tau
            ring.append(bm.verts.new(point + (side * math.cos(angle) + other * math.sin(angle)) * radii[index]))
        rings.append(ring)
    for r in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i]))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object(name, mesh, material)


def leaf_cards(name, clumps, material, cell, cards, size, seed, droop=0.0):
    """Cut-out leaf cards on the surface of each clump, all shading as one soft ball."""
    rng = random.Random(seed)
    u0, v0 = FOLIAGE_CELLS[cell]
    verts, faces, uvs, normals = [], [], [], []
    for centre, radius in clumps:
        centre = mathutils.Vector(centre)
        for _ in range(cards):
            direction = mathutils.Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1) * 0.8 + 0.25)).normalized()
            spot = centre + direction * radius * rng.uniform(0.55, 1.0)
            facing = (direction + mathutils.Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6)))).normalized()
            right = facing.cross(mathutils.Vector((0.21, 0.13, 0.97))).normalized()
            up = right.cross(facing).normalized()
            up = (up + mathutils.Vector((0, 0, -droop))).normalized()
            half = size * rng.uniform(0.75, 1.2)
            base = len(verts)
            for corner_x, corner_y, u, v in ((-1, -1, 0, 0.5), (1, -1, 0.5, 0.5), (1, 1, 0.5, 0), (-1, 1, 0, 0)):
                point = spot + right * corner_x * half + up * corner_y * half
                verts.append(tuple(point))
                uvs.append((u0 + u * 0.995, 1 - (v0 + v * 0.995)))
                normals.append(tuple((point - centre).normalized()))
            faces.append((base, base + 1, base + 2, base + 3))
    mesh = mesh_from(name, verts, faces)
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    mesh.normals_split_custom_set_from_vertices(normals)
    add_object(name, mesh, material)


def crown_clumps(rng, base, width, height, count):
    clumps = []
    for _ in range(count):
        a = rng.uniform(0, math.tau)
        t = rng.random()
        reach = width * (1 - t ** 1.6) * rng.uniform(0.35, 1)
        clumps.append(((math.cos(a) * reach, math.sin(a) * reach, base + t * height), rng.uniform(0.95, 1.5) * width * 0.42))
    return clumps


def broad_tree(seed, material, trunk_height, width, crown_height, bark="bark", lean=0.3, cell="broad", cards=26, size=0.62):
    """A trunk that flares at the roots, branches that reach into every clump, and a leafy crown."""
    rng = random.Random(seed)
    tip = (rng.uniform(-lean, lean), rng.uniform(-lean, lean), trunk_height)
    tube("trunk", [(0, 0, -0.2), (0, 0, 0.3), (tip[0] * 0.4, tip[1] * 0.4, trunk_height * 0.5), tip],
         [0.5, 0.33, 0.27, 0.2], bark, 8)
    for i in range(5):
        a = i * 1.257 + rng.uniform(-0.3, 0.3)
        tube(f"root{i}", [(0, 0, 0.5), (math.cos(a) * 0.5, math.sin(a) * 0.5, 0.12), (math.cos(a) * 0.95, math.sin(a) * 0.95, -0.1)], [0.15, 0.1, 0.03], bark, 5)
    clumps = crown_clumps(rng, trunk_height + 0.5, width, crown_height, 9)
    for index, (centre, radius) in enumerate(clumps):
        start = (tip[0] * 0.8, tip[1] * 0.8, trunk_height * rng.uniform(0.62, 1))
        middle = ((start[0] + centre[0]) / 2, (start[1] + centre[1]) / 2, (start[2] + centre[2]) / 2 + 0.25)
        tube(f"branch{index}", [start, middle, centre], [0.13, 0.08, 0.02], bark, 5)
        blob(f"core{index}", centre, radius * 0.6, "foliage_core", seed + index, 0.85, 1)
    leaf_cards("leaves", clumps, material, cell, cards, size, seed)


def oak():
    broad_tree(11, "foliage_oak", 3.0, 3.1, 3.2)


def oak_b():
    broad_tree(21, "foliage_oak_b", 2.6, 2.7, 3.6, lean=0.5)


def birch():
    rng = random.Random(31)
    broad_tree(31, "foliage_birch", 4.6, 1.7, 3.8, "bark_birch", 0.25, "small", 30, 0.5)
    for i in range(9):
        a = rng.uniform(0, math.tau)
        z = rng.uniform(0.5, 4)
        box(f"mark{i}", (math.cos(a) * 0.24, math.sin(a) * 0.24, z), (0.2, 0.2, 0.05), "wood_dark")


def blossom():
    broad_tree(41, "foliage_blossom", 2.4, 2.5, 2.6, "bark", 0.5, "small", 30, 0.55)


def apple():
    broad_tree(51, "foliage_oak_b", 2.1, 2.1, 2.3, "bark", 0.3, "broad", 22, 0.5)
    rng = random.Random(52)
    for i in range(24):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.9, 2.1)
        blob(f"fruit{i}", (math.cos(a) * r, math.sin(a) * r, rng.uniform(2.2, 4.2)), 0.1, "cloth_red", i, 1, 1)


def pine_at(seed, height, width):
    """Tiers of drooping boughs around a straight trunk, narrowing to a point."""
    rng = random.Random(seed)
    tube("trunk", [(0, 0, -0.2), (0, 0, height * 0.5), (0, 0, height)], [0.34, 0.2, 0.03], "bark_dark", 7)
    tiers = 8
    verts_clumps = []
    for tier in range(tiers):
        t = tier / (tiers - 1)
        z = height * (0.22 + 0.76 * t)
        reach = width * (1 - t) ** 0.85 + 0.25
        for i in range(9 - tier // 2):
            a = i / (9 - tier // 2) * math.tau + rng.uniform(-0.25, 0.25) + tier
            tip = (math.cos(a) * reach, math.sin(a) * reach, z - reach * 0.42)
            tube(f"bough{tier}_{i}", [(0, 0, z), tip], [0.05, 0.015], "bark_dark", 4)
            verts_clumps.append((tip, reach, a, z))
    cards = []
    verts, faces, uvs, normals = [], [], [], []
    u0, v0 = FOLIAGE_CELLS["needles"]
    for tip, reach, a, z in verts_clumps:
        out = mathutils.Vector((math.cos(a), math.sin(a), -0.42)).normalized()
        side = mathutils.Vector((-math.sin(a), math.cos(a), 0))
        root = mathutils.Vector((math.cos(a) * reach * 0.08, math.sin(a) * reach * 0.08, z + 0.08))
        half = max(reach * 0.58, 0.45)
        end = root + out * reach * 1.15
        base = len(verts)
        for point, u, v in ((root - side * half * 0.3, 0, 0), (root + side * half * 0.3, 0.5, 0), (end + side * half, 0.5, 0.5), (end - side * half, 0, 0.5)):
            verts.append(tuple(point))
            uvs.append((u0 + u * 0.995, 1 - (v0 + v * 0.995)))
            normal = mathutils.Vector((point.x, point.y, 0.6 * reach + 0.5)).normalized()
            normals.append(tuple(normal))
        faces.append((base, base + 1, base + 2, base + 3))
    mesh = mesh_from("needles", verts, faces)
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    mesh.normals_split_custom_set_from_vertices(normals)
    add_object("needles", mesh, "foliage_pine")


def pine():
    pine_at(61, 9.5, 2.5)


def pine_b():
    pine_at(62, 6.8, 2.1)


def willow():
    rng = random.Random(71)
    tube("trunk", [(0, 0, -0.2), (0, 0, 0.4), (0.3, 0.1, 2.2), (0.2, 0.2, 3.4)], [0.55, 0.4, 0.3, 0.2], "bark", 8)
    clumps = crown_clumps(rng, 3.8, 2.9, 1.7, 8)
    for index, (centre, radius) in enumerate(clumps):
        tube(f"branch{index}", [(0.2, 0.2, 3.1), ((centre[0] + 0.2) / 2, (centre[1] + 0.2) / 2, centre[2] + 0.7), centre], [0.12, 0.07, 0.02], "bark", 5)
        blob(f"core{index}", centre, radius * 0.5, "foliage_core", index, 0.7, 1)
    leaf_cards("leaves", clumps, "foliage_willow", "broad", 16, 0.56, 72)
    # Curtains of hanging twigs around the crown.
    verts, faces, uvs, normals = [], [], [], []
    u0, v0 = FOLIAGE_CELLS["strands"]
    for index in range(46):
        a = index / 46 * math.tau + rng.uniform(-0.1, 0.1)
        r = rng.uniform(1.8, 3.5)
        top = 5.4 - r * 0.42 + rng.uniform(-0.3, 0.3)
        length = rng.uniform(2.5, 3.9)
        side = mathutils.Vector((-math.sin(a), math.cos(a), 0)) * rng.uniform(0.6, 0.95)
        centre = mathutils.Vector((math.cos(a) * r, math.sin(a) * r, 0))
        base = len(verts)
        for point, u, v in ((centre - side + mathutils.Vector((0, 0, top)), 0, 0), (centre + side + mathutils.Vector((0, 0, top)), 0.5, 0),
                            (centre + side + mathutils.Vector((0, 0, top - length)), 0.5, 0.5), (centre - side + mathutils.Vector((0, 0, top - length)), 0, 0.5)):
            verts.append(tuple(point))
            uvs.append((u0 + u * 0.995, 1 - (v0 + v * 0.995)))
            normals.append(tuple(mathutils.Vector((point.x, point.y, 1.4)).normalized()))
        faces.append((base, base + 1, base + 2, base + 3))
    mesh = mesh_from("strands", verts, faces)
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    mesh.normals_split_custom_set_from_vertices(normals)
    add_object("strands", mesh, "foliage_willow")


def dead_tree():
    rng = random.Random(81)
    tube("trunk", [(0, 0, -0.2), (0.1, 0, 1.8), (0.3, 0.2, 4)], [0.42, 0.26, 0.06], "bark_grey", 7)
    for i in range(6):
        a = rng.uniform(0, math.tau)
        z = rng.uniform(1.6, 3.6)
        tip = (math.cos(a) * rng.uniform(1, 2), math.sin(a) * rng.uniform(1, 2), z + rng.uniform(0.3, 1.5))
        tube(f"branch{i}", [(0.1, 0.05, z), ((tip[0]) * 0.6, (tip[1]) * 0.6, z + 0.2), tip], [0.11, 0.06, 0.01], "bark_grey", 5)


def stump():
    tube("stump", [(0, 0, -0.1), (0, 0, 0.2), (0, 0, 0.5)], [0.6, 0.45, 0.42], "bark", 9)
    disc_top = [(math.cos(i / 9 * math.tau) * 0.42, math.sin(i / 9 * math.tau) * 0.42, 0.5) for i in range(9)]
    add_object("top", mesh_from("top", disc_top, [tuple(range(9))]), "sand")
    for i in range(5):
        a = i * 1.26
        tube(f"root{i}", [(0, 0, 0.25), (math.cos(a) * 0.65, math.sin(a) * 0.65, 0.02), (math.cos(a) * 0.95, math.sin(a) * 0.95, -0.1)], [0.14, 0.09, 0.03], "bark", 5)


def fallen_log():
    tube("log", [(-2.3, 0, 0.32), (-0.6, 0.1, 0.36), (1.2, -0.1, 0.32), (2.5, 0.05, 0.26)], [0.36, 0.38, 0.34, 0.28], "bark", 8)
    for i, x in enumerate((-1.7, -0.4, 1.3)):
        blob(f"moss{i}", (x, 0, 0.6), 0.34, "grass", i, 0.5, 1)
    tube("twig", [(0.4, 0, 0.5), (0.9, 0.4, 1.1)], [0.07, 0.015], "bark", 5)


def twigs():
    rng = random.Random(83)
    for i in range(7):
        a = rng.uniform(0, math.tau)
        c = (rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), 0.05)
        tube(f"twig{i}", [(c[0] - math.cos(a) * 0.6, c[1] - math.sin(a) * 0.6, 0.04), c, (c[0] + math.cos(a) * 0.7, c[1] + math.sin(a) * 0.7, 0.1)], [0.04, 0.03, 0.01], "bark", 4)


def start_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATERIALS.clear()
    MATERIALS.update({name: make_material(name, value) for name, value in COLORS.items()})


def join_all():
    objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(objects) < 2:
        return
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()


def export(name):
    OUT.mkdir(parents=True, exist_ok=True)
    if not name.startswith(("anime_", "beast_")):
        join_all()
    bpy.ops.export_scene.gltf(filepath=str(OUT / f"{name}.glb"), export_format="GLB", export_apply=True)


def rock():
    blob("rock", (0, 0, 0.2), 0.6, "stone_dark", 5, 0.7, 1)


def bush():
    rng = random.Random(8)
    clumps = [((rng.uniform(-0.45, 0.45), rng.uniform(-0.45, 0.45), rng.uniform(0.35, 0.75)), 0.55) for _ in range(5)]
    for i, (c, r) in enumerate(clumps):
        blob(f"core{i}", c, r * 0.65, "foliage_core", i, 0.8, 1)
    leaf_cards("leaves", clumps, "foliage_bush", "broad", 16, 0.34, 8)


def berry_bush():
    rng = random.Random(18)
    clumps = [((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0.3, 0.7)), 0.5) for _ in range(5)]
    for i, (c, r) in enumerate(clumps):
        blob(f"core{i}", c, r * 0.65, "foliage_core", i, 0.8, 1)
    leaf_cards("leaves", clumps, "foliage_oak_b", "small", 20, 0.28, 18)
    for i in range(22):
        a = rng.uniform(0, math.tau)
        blob(f"berry{i}", (math.cos(a) * rng.uniform(0.4, 0.85), math.sin(a) * rng.uniform(0.4, 0.85), rng.uniform(0.35, 1)), 0.05, "flower", i, 1, 1)


def tall_bush():
    rng = random.Random(28)
    clumps = [((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0.6 + i * 0.42), 0.5 - i * 0.04) for i in range(5)]
    tube("stem", [(0, 0, 0), (0, 0, 2)], [0.09, 0.02], "bark", 5)
    for i, (c, r) in enumerate(clumps):
        blob(f"core{i}", c, r * 0.6, "foliage_core", i, 0.9, 1)
    leaf_cards("leaves", clumps, "foliage_birch", "small", 18, 0.3, 28)


def reeds():
    rng = random.Random(38)
    for i in range(16):
        x, y = rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8)
        h = rng.uniform(1, 1.9)
        tube(f"reed{i}", [(x, y, -0.1), (x + rng.uniform(-0.1, 0.1), y + rng.uniform(-0.1, 0.1), h)], [0.025, 0.01], "reed", 4)
        if i % 3 == 0:
            tube(f"head{i}", [(x, y, h - 0.32), (x, y, h)], [0.05, 0.05], "wood_dark", 5)


def grass_tuft():
    rng = random.Random(48)
    for i in range(14):
        a = rng.uniform(0, math.tau)
        tube(f"blade{i}", [(0, 0, 0), (math.cos(a) * 0.14, math.sin(a) * 0.14, 0.5), (math.cos(a) * 0.5, math.sin(a) * 0.5, rng.uniform(0.7, 1.05))],
             [0.05, 0.035, 0.005], "grass_tall", 3)


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


def hair_cap(bm, back=-0.42):
    """The solid part of the hair: a shell over the skull, open over the face."""
    made = bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1)["verts"]
    bmesh.ops.delete(bm, geom=[v for v in made if v.co.z < (0.18 if v.co.y > 0.2 else back)], context="VERTS")
    for vert in bm.verts:
        vert.co = (vert.co.x * 0.139, vert.co.y * 0.146, vert.co.z * 0.164 + 0.008)


def finish_hair(bm, name="hair"):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    smooth(mesh)
    add_object(name, mesh, "anime_hair")


def anime_hair_hero():
    """A cap of hair and many sharp clumps that fall around the face and over the brow."""
    bm = bmesh.new()
    hair_cap(bm)
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
    for index in range(6):
        # Short tufts at the crown, swept back so the outline stays calm.
        angle = rng.uniform(0, math.tau)
        x, y = math.cos(angle) * 0.075, math.sin(angle) * 0.07 - 0.03
        strand(bm, (x, y, 0.15), (x * 1.5, y * 1.4 - 0.05, 0.17 + rng.uniform(-0.01, 0.025)), (0, 0, 0.01), 0.026)
    finish_hair(bm)


def anime_hair_mira():
    """Soft bangs swept to one side, with long locks in front of the ears."""
    bm = bmesh.new()
    hair_cap(bm, -0.6)
    rng = random.Random(31)
    for index in range(13):
        x = -0.125 + index * 0.021
        drop = 0.1 + 0.05 * math.sin(index * 0.55) + rng.uniform(-0.008, 0.008)
        strand(bm, (x - 0.03, 0.118, 0.11), (x + 0.03, 0.142, 0.1 - drop), (0.012, 0.016, 0), 0.026, steps=7)
    for side in (-1, 1):
        for index in range(4):
            y = 0.05 + index * 0.012
            strand(bm, (side * 0.118, y, 0.07), (side * (0.135 + index * 0.004), y + 0.01, -0.22 - index * 0.03), (side * 0.016, 0, 0), 0.026, steps=7)
    finish_hair(bm)


def anime_hair_rhea():
    """Short and sharp, pulled away from the face, with a band at the back of the head."""
    bm = bmesh.new()
    hair_cap(bm, -0.55)
    rng = random.Random(52)
    for index in range(8):
        x = -0.11 + index * 0.031
        strand(bm, (x, 0.12, 0.11), (x * 1.3, 0.15, 0.06 - rng.uniform(0.01, 0.05)), (0, 0.014, 0.01), 0.03)
    for index in range(14):
        angle = rng.uniform(0.2, math.pi - 0.2)
        x, y = math.cos(angle) * 0.125, -math.sin(angle) * 0.125
        strand(bm, (x, y, 0.06), (x * 1.3, y * 1.3, -0.06 - rng.uniform(0, 0.05)), (x * 0.2, y * 0.2, 0), 0.03)
    for side in (-1, 1):
        strand(bm, (side * 0.118, 0.06, 0.07), (side * 0.14, 0.07, -0.12), (side * 0.012, 0, 0), 0.024)
    finish_hair(bm)


def anime_hair_bram():
    """Thin white hair around a bald crown."""
    bm = bmesh.new()
    rng = random.Random(61)
    for index in range(20):
        angle = rng.uniform(-0.25, math.pi + 0.25)
        x, y = math.cos(angle) * 0.13, -math.sin(angle) * 0.13
        strand(bm, (x, y, 0.05), (x * 1.22, y * 1.22, -0.09 - rng.uniform(0, 0.04)), (x * 0.12, y * 0.12, 0), 0.028)
    finish_hair(bm)


def anime_hair_lina():
    """A neat bob: straight bangs and a sleek curtain that ends at the jaw, curling in."""
    bm = bmesh.new()
    hair_cap(bm, -0.62)
    for index in range(13):
        x = -0.12 + index * 0.02
        strand(bm, (x, 0.118, 0.11), (x * 1.02, 0.143, 0.035), (0, 0.012, 0), 0.026)
    for index in range(26):
        angle = -0.25 + index / 25 * (math.pi + 0.5)
        x, y = math.cos(angle) * 0.13, -math.sin(angle) * 0.13
        if y > 0.06:
            continue
        strand(bm, (x, y, 0.06), (x * 1.18, y * 1.18, -0.115), (x * 0.18, y * 0.18, 0.0), 0.034)
        strand(bm, (x * 1.18, y * 1.18, -0.11), (x * 0.98, y * 0.98, -0.128), (0, 0, -0.01), 0.022, steps=3)
    finish_hair(bm)


def anime_hair_spiky():
    """Bold spikes that sweep up and back, with a sharp fringe."""
    bm = bmesh.new()
    hair_cap(bm, -0.45)
    rng = random.Random(91)
    for index in range(9):
        x = -0.11 + index * 0.0275
        strand(bm, (x, 0.12, 0.12), (x * 1.3, 0.16, 0.02 - rng.uniform(0, 0.04)), (0, 0.02, 0.02), 0.03)
    for index in range(22):
        angle = rng.uniform(0, math.tau)
        x, y = math.cos(angle) * 0.1, math.sin(angle) * 0.1 - 0.03
        z = rng.uniform(0.06, 0.15)
        strand(bm, (x, y, z), (x * 2.1, y * 2.1 - 0.06, z + rng.uniform(0.05, 0.12)), (0, -0.02, 0.02), 0.036)
    finish_hair(bm)


def anime_hair_short():
    """Short, tidy hair parted at one side."""
    bm = bmesh.new()
    hair_cap(bm, -0.5)
    rng = random.Random(93)
    for index in range(10):
        x = -0.12 + index * 0.025
        strand(bm, (x - 0.02, 0.12, 0.12), (x + 0.03, 0.142, 0.06 - rng.uniform(0, 0.02)), (0.01, 0.014, 0), 0.026)
    for index in range(18):
        angle = rng.uniform(0.1, math.pi - 0.1)
        x, y = math.cos(angle) * 0.13, -math.sin(angle) * 0.13
        strand(bm, (x, y, 0.05), (x * 1.12, y * 1.12, -0.06 - rng.uniform(0, 0.03)), (x * 0.1, y * 0.1, 0), 0.03)
    finish_hair(bm)


def anime_hair_sora():
    """Soft parted bangs that frame the face, for long wavy hair."""
    bm = bmesh.new()
    hair_cap(bm, -0.55)
    for side in (-1, 1):
        for index in range(6):
            x = side * (0.012 + index * 0.02)
            strand(bm, (x * 0.6, 0.12, 0.12), (x * 1.25, 0.143, 0.02 - index * 0.012), (side * 0.016, 0.014, 0), 0.026, steps=7)
        for index in range(3):
            strand(bm, (side * 0.12, 0.06 + index * 0.01, 0.06), (side * 0.15, 0.08, -0.3 - index * 0.04), (side * 0.025, 0, 0), 0.026, steps=8)
    finish_hair(bm)


def anime_hair_wavy():
    """Long waves down the back."""
    bm = bmesh.new()
    rng = random.Random(95)
    for index in range(24):
        angle = rng.uniform(0.05, math.pi - 0.05)
        x, y = math.cos(angle) * 0.128, -math.sin(angle) * 0.128
        end_z = rng.uniform(-0.6, -0.48)
        rings = 9
        points = []
        for step in range(rings + 1):
            t = step / rings
            wave = math.sin(t * 9 + index) * 0.022 * t
            points.append((x * (1 + 0.25 * t) + wave, y * (1 + 0.4 * t), 0.08 + (end_z - 0.08) * t))
        for step in range(rings):
            strand(bm, points[step], points[step + 1], (0, 0, 0), 0.042 * (1 - step / rings * 0.6), steps=2)
    finish_hair(bm, "wavy")


def anime_hair_elda():
    """Hair pulled smoothly back from the face, held in a bun."""
    bm = bmesh.new()
    hair_cap(bm, -0.5)
    for side in (-1, 1):
        strand(bm, (side * 0.11, 0.08, 0.07), (side * 0.13, 0.07, -0.08), (side * 0.01, 0, 0), 0.02)
    finish_hair(bm)


def anime_bun():
    bm = bmesh.new()
    made = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.075)["verts"]
    for vert in made:
        vert.co = (vert.co.x, vert.co.y - 0.11, vert.co.z + 0.15)
    strand(bm, (0, -0.06, 0.1), (0, -0.1, 0.16), (0, 0, 0), 0.04)
    finish_hair(bm, "bun")


def anime_hair_pip():
    """A messy mop, sticking out every way."""
    bm = bmesh.new()
    hair_cap(bm, -0.48)
    rng = random.Random(97)
    for index in range(30):
        angle = rng.uniform(0, math.tau)
        x, y = math.cos(angle) * 0.12, math.sin(angle) * 0.12
        z = rng.uniform(-0.02, 0.15)
        strand(bm, (x * 0.8, y * 0.8, z), (x * 1.55, y * 1.55, z - rng.uniform(0.02, 0.08)), (0, 0, 0.02), 0.03)
    finish_hair(bm)


def anime_headband():
    loft("band", [(0.085, 0.142, 0.15), (0.115, 0.146, 0.154)], "anime_accent", 18, (0, 0, 0.3))


def anime_beard():
    bm = bmesh.new()
    rng = random.Random(62)
    for index in range(16):
        x = -0.085 + index * 0.0113
        strand(bm, (x, 0.09 - abs(x) * 0.4, -0.085 + abs(x) * 0.4), (x * 0.5, 0.125, -0.27 - rng.uniform(0, 0.05) + abs(x) * 0.5), (0, 0.016, 0), 0.026)
    for side in (-1, 1):
        strand(bm, (side * 0.012, 0.13, -0.075), (side * 0.07, 0.112, -0.11), (0, 0.008, -0.01), 0.016)
    finish_hair(bm, "beard")


def anime_hair_tess():
    """Straight bangs and a parting, with hair drawn back to the two braids."""
    bm = bmesh.new()
    hair_cap(bm, -0.62)
    for index in range(12):
        x = -0.118 + index * 0.0215
        strand(bm, (x, 0.118, 0.11), (x, 0.142, 0.03 - 0.008 * (index % 3)), (0, 0.014, 0), 0.026)
    finish_hair(bm)


def anime_braids():
    bm = bmesh.new()
    for side in (-1, 1):
        for index in range(8):
            z = -0.03 - index * 0.055
            radius = 0.033 - index * 0.002
            sway = 0.008 * (1 if index % 2 else -1)
            sphere = bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=radius)["verts"]
            for vert in sphere:
                vert.co = (vert.co.x + side * (0.135 + index * 0.002) + sway, vert.co.y + 0.02, vert.co.z * 1.25 + z)
    finish_hair(bm, "braids")


def anime_ponytail():
    bm = bmesh.new()
    rng = random.Random(53)
    for index in range(12):
        x = rng.uniform(-0.03, 0.03)
        strand(bm, (x * 0.7, -0.15, 0.11), (x * 2.6, -0.2 - abs(x), -0.34 - rng.uniform(0, 0.09)), (x, -0.05, 0.02), 0.03, steps=8)
    finish_hair(bm, "tail")
    loft("band", [(0.09, 0.034, 0.03, 0, -0.15), (0.13, 0.034, 0.03, 0, -0.15)], "anime_accent", 8)


def anime_torso():
    """Lean shoulders, a shirt tucked at the belt and a soft collar."""
    loft("shirt", [(-0.12, 0.138, 0.095), (0, 0.118, 0.082), (0.14, 0.122, 0.087), (0.26, 0.146, 0.098), (0.34, 0.168, 0.1), (0.4, 0.168, 0.086),
                   (0.44, 0.128, 0.07), (0.47, 0.066, 0.056)], "anime_shirt", 22)
    loft("collar", [(0.44, 0.075, 0.07), (0.5, 0.058, 0.058), (0.54, 0.062, 0.062)], "anime_dark", 14)
    loft("belt", [(-0.03, 0.128, 0.09), (0.03, 0.123, 0.087)], "anime_leather", 18)
    box("buckle", (0, 0.092, 0.0), (0.052, 0.012, 0.05), "anime_gold")
    loft("sash", [(-0.32, 0.03, 0.012, 0.09, 0.082), (-0.03, 0.04, 0.014, 0.09, 0.09)], "anime_cloak", 8)


def anime_torso_f():
    """A narrow waist, a bust and sloping shoulders."""
    loft("shirt", [(-0.12, 0.135, 0.095), (-0.03, 0.112, 0.082), (0.06, 0.092, 0.072), (0.14, 0.098, 0.075, 0, 0.004), (0.22, 0.118, 0.098, 0, 0.022),
                   (0.28, 0.126, 0.1, 0, 0.024), (0.34, 0.136, 0.082, 0, 0.006), (0.395, 0.142, 0.072), (0.44, 0.11, 0.06), (0.47, 0.058, 0.05)], "anime_shirt", 22)
    loft("collar", [(0.44, 0.066, 0.06), (0.5, 0.05, 0.05), (0.535, 0.054, 0.054)], "anime_dark", 14)
    loft("belt", [(-0.03, 0.118, 0.087), (0.02, 0.108, 0.081)], "anime_leather", 18)
    box("buckle", (0, 0.088, -0.005), (0.04, 0.012, 0.038), "anime_gold")


def anime_vest():
    """A fitted waistcoat over the blouse, open at the neck, with gold buttons."""
    loft("vest", [(-0.04, 0.122, 0.09), (0.06, 0.1, 0.08), (0.14, 0.106, 0.083, 0, 0.004), (0.22, 0.126, 0.106, 0, 0.022), (0.3, 0.134, 0.108, 0, 0.022)], "anime_vest", 22)
    for z in (0.02, 0.09, 0.16):
        box("button", (0, 0.086 + (z - 0.02) * 0.04, z), (0.014, 0.012, 0.014), "anime_gold")


def anime_bow():
    """A small ribbon at the collar, with the Guild pin."""
    for side in (-1, 1):
        loft("loop", [(0.0, 0.006, 0.006, side * 0.008, 0), (0.0, 0.03, 0.012, side * 0.038, 0)], "anime_accent", 6, (0, 0.074, 0.42))
        loft("tail", [(-0.07, 0.016, 0.004, side * 0.022, 0), (0, 0.006, 0.004, side * 0.006, 0)], "anime_accent", 6, (0, 0.074, 0.415))
    box("knot", (0, 0.078, 0.42), (0.018, 0.012, 0.022), "anime_accent")
    box("pin", (0.07, 0.112, 0.25), (0.022, 0.008, 0.026), "anime_gold")


def anime_armor():
    """A breastplate and rounded pauldrons, worn over a dark tunic."""
    loft("plate", [(0.04, 0.114, 0.09), (0.14, 0.11, 0.09, 0, 0.004), (0.22, 0.13, 0.11, 0, 0.022), (0.3, 0.14, 0.112, 0, 0.022), (0.38, 0.145, 0.086), (0.42, 0.11, 0.07)], "anime_armor", 22)
    box("ridge", (0, 0.122, 0.22), (0.012, 0.01, 0.22), "anime_armor_dark")
    for side in (-1, 1):
        blob("pauldron", (side * 0.195, 0, 0.405), 0.075, "anime_armor", 7, 0.8, 2)
        blob("rim", (side * 0.2, 0, 0.375), 0.07, "anime_armor_dark", 8, 0.45, 2)


def anime_apron():
    loft("apron", [(-0.46, 0.13, 0.028, 0, 0.17), (-0.2, 0.115, 0.024, 0, 0.142), (0.04, 0.09, 0.018, 0, 0.106)], "anime_vest", 12)
    loft("waist", [(0.03, 0.116, 0.088), (0.07, 0.11, 0.084)], "anime_vest", 18)


def anime_robe():
    """A long coat that falls past the knees."""
    loft("robe", [(-0.74, 0.17, 0.15), (-0.4, 0.16, 0.135), (-0.1, 0.15, 0.115), (0.1, 0.14, 0.1)], "anime_cloak", 18)
    loft("sash", [(0.0, 0.15, 0.116), (0.05, 0.146, 0.112)], "anime_leather", 18)


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
    loft("upper", [(-0.3, 0.036, 0.036), (-0.16, 0.044, 0.044), (-0.03, 0.054, 0.054), (0.02, 0.054, 0.054), (0.055, 0.04, 0.04), (0.07, 0.016, 0.016)], "anime_shirt", 14)


def anime_forearm():
    loft("sleeve", [(-0.14, 0.034, 0.034), (0, 0.036, 0.036)], "anime_shirt", 12)
    loft("glove", [(-0.255, 0.031, 0.029), (-0.2, 0.031, 0.03), (-0.13, 0.04, 0.04), (-0.1, 0.042, 0.042)], "anime_glove", 12)
    loft("hand", [(-0.345, 0.022, 0.016), (-0.3, 0.036, 0.019), (-0.255, 0.033, 0.022)], "anime_glove", 10)
    loft("thumb", [(-0.31, 0.009, 0.009, 0.032, 0.016), (-0.27, 0.013, 0.013, 0.03, 0.012)], "anime_glove", 8)


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


def anime_hair_long():
    """Long hair falling down the back, added to the cap."""
    bm = bmesh.new()
    rng = random.Random(23)
    for index in range(26):
        angle = rng.uniform(0.05, math.pi - 0.05)
        x, y = math.cos(angle) * 0.128, -math.sin(angle) * 0.128
        strand(bm, (x, y, 0.08), (x * 1.15, y * 1.35, rng.uniform(-0.56, -0.44)), (x * 0.28, y * 0.2, 0), 0.042, steps=8)
    finish_hair(bm, "long")


def anime_skirt():
    loft("skirt", [(-0.5, 0.23, 0.2), (-0.2, 0.205, 0.165), (0.02, 0.185, 0.135), (0.08, 0.15, 0.105)], "anime_cloak", 18)


def guild_hall():
    """The largest building in the valley: stone below, timber above, with a gabled porch and banners."""
    box("base", (0, 0, 2.2), (15, 11, 4.4), "stone")
    box("upper", (0, 0, 6.2), (15.8, 11.8, 3.8), "wall")
    for x in (-7.9, -3.9, 0, 3.9, 7.9):
        timber("post", (x, -5.95, 4.3), (x, -5.95, 8.1), 0.28)
        timber("post", (x, 5.95, 4.3), (x, 5.95, 8.1), 0.28)
    for z in (4.4, 8.1):
        timber("beam", (-8, -6, z), (8, -6, z), 0.26)
        timber("beam", (-8, 6, z), (8, 6, z), 0.26)
        timber("beam", (-8, -6, z), (-8, 6, z), 0.26)
        timber("beam", (8, -6, z), (8, 6, z), 0.26)
    roof("roof", (0, 0, 8.1), 17.6, 13.4, 5.4, "roof")
    timber("ridge", (-8.8, 0, 13.55), (8.8, 0, 13.55), 0.24, "wood_dark")
    for x in (-5.8, 5.8):
        box("tower", (x, -5.2, 7), (3.2, 3.2, 14), "stone")
        roof("tower_roof", (x, -5.2, 14), 4, 4, 2.6, "roof")
        box("slit", (x, -6.84, 9), (0.4, 0.08, 1.5), "window")
    box("porch_floor", (0, -6.6, 0.2), (5.4, 3, 0.4), "stone")
    for x in (-2.4, 2.4):
        timber("column", (x, -7.8, 0.4), (x, -7.8, 4.4), 0.34, "stone")
    roof("porch_roof", (0, -6.7, 4.4), 6.2, 3.8, 1.8, "roof")
    box("doors", (0, -5.56, 1.9), (2.4, 0.12, 3), "wood")
    box("door_line", (0, -5.64, 1.9), (0.08, 0.05, 3), "wood_dark")
    for x in (-0.3, 0.3):
        box("ring", (x, -5.66, 1.8), (0.14, 0.05, 0.14), "gold")
    for x in (-3.9, 3.9):
        box("banner", (x, -6.04, 6.4), (1.2, 0.05, 2.6), "cloth_red")
        box("emblem", (x, -6.08, 6.7), (0.6, 0.04, 0.6), "gold")
    for x in (-2, 2):
        box("window", (x, -6, 6.2), (1.5, 0.1, 1.7), "window")
    box("sign", (0, -8.2, 3.4), (2.8, 0.1, 0.7), "wood_dark")
    box("sign_face", (0, -8.27, 3.4), (2.4, 0.05, 0.42), "gold")


def disc(name, center, radius, height, material, sides=32):
    ring = [(math.cos(i / sides * math.tau) * radius, math.sin(i / sides * math.tau) * radius) for i in range(sides)]
    verts = [(x, y, 0) for x, y in ring] + [(x, y, height) for x, y in ring]
    faces = [tuple(range(sides, 2 * sides))]
    faces += [(i, (i + 1) % sides, (i + 1) % sides + sides, i + sides) for i in range(sides)]
    add_object(name, mesh_from(name, verts, faces), material, center)


def books(x0, x1, y, z, rng):
    """A row of book spines of different heights and colours."""
    x = x0
    while x < x1:
        width = rng.uniform(0.09, 0.2)
        height = rng.uniform(0.5, 0.82)
        if rng.random() > 0.1:
            color = rng.choice(("cloth_red", "shutter", "gold", "sack", "wood_dark", "stone_dark", "shutter", "cloth_red"))
            box("book", (x + width / 2, y, z + height / 2), (width * 0.92, 0.3, height), color)
        x += width


def guild_interior():
    rng = random.Random(77)
    box("floor", (0, 0, -0.05), (26, 22, 0.1), "wood")
    box("ceiling", (0, 0, 7), (26, 22, 0.2), "wood_dark")
    for y, width in ((-9.5, 24), (9.5, 24)):
        box("wall", (0, y, 3.5), (width, 1, 7), "wall")
    for x in (-11.5, 11.5):
        box("wall", (x, 0, 3.5), (1, 20, 7), "wall")
    box("panel", (0, 8.94, 0.7), (22, 0.1, 1.4), "wood_dark")
    box("panel", (0, -8.94, 0.7), (22, 0.1, 1.4), "wood_dark")
    for x in (-10.94, 10.94):
        box("panel", (x, 0, 0.7), (0.1, 18, 1.4), "wood_dark")
    for x in (-8, -3, 3, 8):
        timber("pillar", (x, -8.8, 0), (x, -8.8, 7), 0.35)
        timber("pillar", (x, 8.8, 0), (x, 8.8, 7), 0.35)
        timber("rafter", (x, -9, 6.7), (x, 9, 6.7), 0.3, "wood_dark")
    for y in (-5, 0, 5):
        timber("beam", (-11, y, 6.45), (11, y, 6.45), 0.22, "wood_dark")
    # Upright boards along the lower walls.
    for k in range(-21, 22):
        box("wainscot_line", (k * 0.5, 8.88, 0.7), (0.03, 0.02, 1.36), "bark_dark")
    for k in range(-17, 18):
        for x in (-10.88, 10.88):
            box("wainscot_line", (x, k * 0.5, 0.7), (0.02, 0.03, 1.36), "bark_dark")
    box("wainscot_cap", (0, 8.86, 1.42), (22, 0.16, 0.08), "wood")
    for x in (-10.86, 10.86):
        box("wainscot_cap", (x, 0, 1.42), (0.16, 18, 0.08), "wood")
    for y in (-5.5, 0, 5.5):
        for x in (-10.92, 10.92):
            box("sill", (x * 0.985, y, 2.45), (0.3, 1.9, 0.08), "wood")
            box("window", (x, y, 3.6), (0.08, 1.7, 2.2), "glass")
            box("frame", (x * 0.995, y, 3.05), (0.1, 1.8, 0.06), "wood_dark")
            box("frame", (x * 0.995, y, 4.15), (0.1, 1.8, 0.06), "wood_dark")
            box("frame", (x * 0.995, y, 3.6), (0.1, 0.12, 2.3), "wood_dark")
            box("frame", (x * 0.995, y, 3.6), (0.1, 1.8, 0.12), "wood_dark")
    box("carpet", (0, 1, 0.02), (4, 15, 0.04), "cloth_red")
    box("carpet_edge", (0, 1, 0.03), (3.4, 14.4, 0.04), "gold")
    box("carpet_top", (0, 1, 0.04), (3.1, 14.1, 0.04), "cloth_red")

    # The reception desk, with what a clerk needs on it.
    box("counter", (0, 4.8, 0.43), (14, 1.2, 0.86), "wood")
    box("counter_top", (0, 4.8, 0.9), (14.4, 1.4, 0.1), "wood_dark")
    for x in (-6, -3, 3, 6):
        box("panel", (x, 4.18, 0.45), (2.3, 0.05, 0.6), "wood_dark")
    box("ledger", (-1.3, 4.7, 1.0), (0.8, 0.6, 0.1), "cloth_red")
    box("page", (-1.3, 4.7, 1.06), (0.74, 0.54, 0.03), "paper")
    box("papers", (1.5, 4.75, 0.98), (0.5, 0.65, 0.06), "paper")
    loft("inkwell", [(0.95, 0.06, 0.06), (1.05, 0.05, 0.05), (1.09, 0.03, 0.03)], "stone_dark", 8, (-0.5, 4.5, 0))
    timber("quill", (-0.5, 4.5, 1.05), (-0.36, 4.42, 1.35), 0.02, "paper")
    loft("bell", [(0.95, 0.09, 0.09), (1.03, 0.07, 0.07), (1.09, 0.02, 0.02)], "gold", 10, (0.4, 4.45, 0))
    timber("scale_post", (3.2, 4.7, 0.95), (3.2, 4.7, 1.55), 0.05, "gold")
    timber("scale_arm", (2.8, 4.7, 1.52), (3.6, 4.7, 1.52), 0.03, "gold")
    for x in (2.8, 3.6):
        timber("cord", (x, 4.7, 1.52), (x, 4.7, 1.2), 0.012, "gold")
        loft("pan", [(1.18, 0.14, 0.14), (1.2, 0.15, 0.15)], "gold", 10, (x, 4.7, 0))
    blob("crystal", (2.8, 4.7, 1.26), 0.07, "tunic", 3, 1.4, 1)

    # Shelves behind the desk.
    box("shelf_back", (0, 8.72, 2.1), (11, 0.2, 4.2), "wood_dark")
    for x in (-5.5, -2.75, 0, 2.75, 5.5):
        box("shelf_side", (x, 8.5, 2.1), (0.14, 0.5, 4.2), "wood")
    for row in range(4):
        z = 0.15 + row * 1.0
        box("shelf_board", (0, 8.5, z), (11.1, 0.5, 0.08), "wood")
        books(-5.4, 5.3, 8.5, z + 0.04, rng)
    box("shelf_top", (0, 8.5, 4.2), (11.3, 0.56, 0.12), "wood")

    # The Guild crest with banners either side.
    box("crest_back", (0, 8.9, 5.5), (2.2, 0.08, 1.6), "wood_dark")
    box("crest", (0, 8.85, 5.5), (1.6, 0.06, 1.1), "gold")
    timber("crest_sword", (-0.5, 8.8, 5.1), (0.5, 8.8, 5.9), 0.07, "stone")
    timber("crest_sword", (0.5, 8.8, 5.1), (-0.5, 8.8, 5.9), 0.07, "stone")
    for x in (-8.4, 8.4):
        box("banner", (x, 8.92, 4.2), (1.4, 0.05, 3.6), "cloth_red")
        box("banner_trim", (x, 8.88, 5.9), (1.4, 0.05, 0.14), "gold")
        box("banner_mark", (x, 8.88, 4.2), (0.6, 0.05, 0.6), "gold")

    # The quest board, with notices held by pins.
    box("board", (-11.0, 2, 2.2), (0.12, 4.6, 2.6), "wood")
    box("board_rim", (-10.98, 2, 3.56), (0.16, 4.8, 0.14), "wood_dark")
    box("board_rim", (-10.98, 2, 0.84), (0.16, 4.8, 0.14), "wood_dark")
    for i, (y, z) in enumerate(((0.6, 2.7), (1.6, 3.0), (2.7, 2.6), (3.6, 2.9), (0.9, 1.6), (2.0, 1.7), (3.2, 1.5))):
        box("notice", (-10.92, y, z), (0.03, 0.72, 0.9), "paper")
        box("line", (-10.9, y, z + 0.18), (0.02, 0.5, 0.05), "wood_dark")
        box("line", (-10.9, y, z - 0.02), (0.02, 0.5, 0.04), "stone_dark")
        box("pin", (-10.89, y, z + 0.36), (0.05, 0.06, 0.06), "cloth_red")
    box("board_sign", (-10.9, 2, 3.85), (0.1, 2, 0.36), "gold")

    for x, y in ((-6.5, -4.5), (6.5, -4.5), (-6.5, 0.5)):
        disc("rug", (x, y, 0.0), 2.1, 0.03, "anime_vest", 24)
        disc("rug_rim", (x, y, 0.005), 1.9, 0.03, "gold", 24)
        disc("rug_core", (x, y, 0.01), 1.82, 0.03, "anime_vest", 24)
        disc("table", (x, y, 0.88), 1.0, 0.12, "wood", 14)
        loft("table_foot", [(0, 0.34, 0.34), (0.1, 0.2, 0.2), (0.8, 0.14, 0.14), (0.9, 0.24, 0.24)], "wood_dark", 10, (x, y, 0))
        for dx, dy in ((1.3, 0), (-1.3, 0), (0, 1.3), (0, -1.3)):
            disc("stool", (x + dx, y + dy, 0.46), 0.25, 0.08, "wood", 10)
            for leg in range(3):
                a = leg * math.tau / 3
                timber("stool_leg", (x + dx + math.cos(a) * 0.17, y + dy + math.sin(a) * 0.17, 0),
                       (x + dx + math.cos(a) * 0.1, y + dy + math.sin(a) * 0.1, 0.46), 0.05, "wood_dark")
        loft("mug", [(0.94, 0.09, 0.09), (1.18, 0.1, 0.1)], "wood_dark", 8, (x + 0.3, y, 0))
        loft("foam", [(1.18, 0.1, 0.1), (1.22, 0.08, 0.08)], "paper", 8, (x + 0.3, y, 0))
        box("plate", (x - 0.3, y + 0.2, 1.0), (0.34, 0.34, 0.03), "stone")
        loft("bread", [(1.01, 0.1, 0.07), (1.08, 0.09, 0.06), (1.1, 0.05, 0.03)], "hat", 8, (x - 0.3, y + 0.2, 0))

    # The appraiser's stall: a U-shaped counter where Hollis weighs what adventurers bring back.
    box("stall_front", (8.4, 1.0, 0.45), (0.6, 3.2, 0.9), "wood")
    box("stall_top", (8.4, 1.0, 0.93), (0.8, 3.4, 0.08), "wood_dark")
    for y in (-0.6, 2.6):
        box("stall_end", (9.65, y, 0.45), (2.5, 0.5, 0.9), "wood")
        box("stall_end_top", (9.65, y, 0.93), (2.7, 0.7, 0.08), "wood_dark")
    for y in (0.0, 1.0, 2.0):
        box("stall_panel", (8.08, y, 0.45), (0.04, 0.8, 0.6), "wood_dark")
    timber("scale_post", (8.4, 1.6, 0.97), (8.4, 1.6, 1.62), 0.05, "gold")
    timber("scale_arm", (8.4, 1.15, 1.6), (8.4, 2.05, 1.6), 0.03, "gold")
    for y in (1.15, 2.05):
        timber("cord", (8.4, y, 1.6), (8.4, y, 1.25), 0.012, "gold")
        loft("pan", [(1.22, 0.15, 0.15), (1.25, 0.16, 0.16)], "gold", 10, (8.4, y, 0))
    loft("tusk_on_pan", [(1.25, 0.03, 0.03), (1.36, 0.02, 0.02)], "paper", 6, (8.4, 1.15, 0))
    for i, (y, colour) in enumerate(((0.0, "leaf"), (0.32, "tunic"), (0.64, "flower"))):
        loft("jar", [(0.97, 0.09, 0.09), (1.15, 0.1, 0.1), (1.22, 0.06, 0.06), (1.26, 0.07, 0.07)], colour, 10, (8.35, y, 0))
        loft("lid", [(1.26, 0.075, 0.075), (1.29, 0.075, 0.075)], "wood_dark", 10, (8.35, y, 0))
    for k, colour in enumerate(("hide", "hide_dark", "wall_b", "hide")):
        box("pelt", (9.7, 2.6, 0.99 + k * 0.06), (1.3 - k * 0.12, 0.6 - k * 0.05, 0.06), colour)
    for k in range(3):
        tube("horn", [(9.3 + k * 0.4, -0.6, 1.0), (9.4 + k * 0.4, -0.75, 1.12), (9.5 + k * 0.4, -0.7, 1.26)], [0.06, 0.04, 0.01], "paper" if k != 1 else "bark", 6)
    box("ledger", (10.4, -0.6, 1.0), (0.5, 0.4, 0.06), "cloth_red")
    for y, z in ((0.3, 0), (1.6, 0), (0.95, 0.6)):
        box("crate", (10.5, y, 0.3 + z), (0.6, 0.6, 0.6), "wood")
        box("crate_band", (10.5, y, 0.3 + z), (0.62, 0.62, 0.08), "wood_dark")

    # Trophies between the windows over the stall: a boar's head between two sets of antlers.
    box("trophy_board", (10.9, 2.75, 3.4), (0.1, 1.3, 1.5), "wood_dark")
    box("trophy_plate", (10.86, 2.75, 3.4), (0.08, 1.1, 1.3), "wood")
    blob("boar_head", (10.55, 2.75, 3.35), 0.38, "hide_dark", 5, 0.95, 2)
    loft("boar_snout", [(0, 0.17, 0.14), (0.28, 0.15, 0.12), (0.34, 0.1, 0.08)], "hide", 10, (10.25, 2.75, 3.25))
    for side in (-1, 1):
        tube("boar_tusk", [(10.05, 2.75 + side * 0.12, 3.15), (9.95, 2.75 + side * 0.18, 3.3), (9.98, 2.75 + side * 0.2, 3.42)], [0.035, 0.025, 0.008], "paper", 6)
        blob("boar_ear", (10.6, 2.75 + side * 0.32, 3.7), 0.11, "hide_dark", 2, 0.5, 1)
        blob("boar_eye", (10.27, 2.75 + side * 0.17, 3.48), 0.035, "anime_dark", 1, 1, 1)
    for y in (1.6, 3.9):
        box("antler_plate", (10.9, y, 3.6), (0.1, 0.5, 0.5), "wood")
        for side in (-1, 1):
            tube("antler", [(10.85, y, 3.6), (10.7, y + side * 0.25, 3.9), (10.6, y + side * 0.45, 4.3)], [0.04, 0.03, 0.015], "paper", 6)
            tube("tine", [(10.7, y + side * 0.25, 3.9), (10.55, y + side * 0.2, 4.15)], [0.025, 0.01], "paper", 5)
            tube("tine", [(10.65, y + side * 0.36, 4.1), (10.5, y + side * 0.3, 4.4)], [0.02, 0.008], "paper", 5)

    # Swords and spears on a rack by the door, for adventurers who broke their own.
    box("rack_base", (-8.0, -8.05, 0.1), (2.6, 0.9, 0.2), "wood_dark")
    for x in (-9.2, -6.8):
        timber("rack_post", (x, -8.3, 0), (x, -8.3, 2.0), 0.12, "wood")
    timber("rack_bar", (-9.3, -8.3, 1.6), (-6.7, -8.3, 1.6), 0.1, "wood")
    for i, x in enumerate((-8.8, -8.4, -8.0, -7.6, -7.2)):
        if i % 2 == 0:
            timber("rack_spear", (x, -7.9, 0.2), (x, -8.2, 2.5), 0.05, "wood")
            loft("spear_head", [(0, 0.05, 0.02), (0.28, 0.0, 0.0)], "stone", 4, (x, -8.22, 2.5))
        else:
            box("rack_blade", (x, -8.0, 1.05), (0.09, 0.03, 1.3), "stone")
            box("rack_guard", (x, -8.03, 1.72), (0.3, 0.08, 0.06), "wood_dark")
            timber("rack_grip", (x, -8.05, 1.74), (x, -8.1, 2.0), 0.05, "anime_leather")

    # A hearth with a stone hood, and barrels beside it.
    box("hearth", (11, -3, 1.2), (0.8, 3, 2.4), "stone_dark")
    loft("hood", [(2.4, 0.7, 1.6, 0, 0), (3.3, 0.55, 1.15, 0.1, 0), (4.4, 0.45, 0.8, 0.15, 0), (7.0, 0.45, 0.8, 0.15, 0)], "stone", 4, (10.8, -3, 0))
    box("mantel", (10.35, -3, 2.45), (0.6, 2.8, 0.14), "wood")
    for k in range(5):
        loft("mantel_jar", [(2.52, 0.06, 0.06), (2.7, 0.07, 0.07), (2.75, 0.04, 0.04)], ("gold", "flower", "leaf", "tunic", "paper")[k], 8, (10.35, -4.1 + k * 0.55, 0))
    box("logs", (10.6, -3, 0.25), (0.5, 1.5, 0.3), "wood_dark")
    box("embers", (10.6, -3, 0.12), (0.5, 1.8, 0.1), "ember")
    for y in (-0.8, -5.2):
        loft("barrel", [(0, 0.34, 0.34), (0.5, 0.42, 0.42), (1, 0.34, 0.34)], "wood", 10, (10.2, y, 0))
    for x, y in ((-10.2, -8.2), (10.2, 8.2), (-10.2, 8.2)):
        disc("pot", (x, y, 0), 0.34, 0.5, "cloth_red", 10)
        for i in range(6):
            a = i * 1.05
            blob("plant", (x + math.cos(a) * 0.2, y + math.sin(a) * 0.2, 0.9 + (i % 3) * 0.25), 0.3, "grass" if i % 2 else "leaf", i, 1, 1)
    # Wheel chandeliers with candles hang where the lights are.
    for x, y in ((-4, -2), (4, -2), (-4, 4), (4, 4)):
        for k in range(3):
            a = k * math.tau / 3
            timber("chain", (x, y, 7), (x + math.cos(a) * 0.75, y + math.sin(a) * 0.75, 5.25), 0.025, "stone_dark")
        for k in range(10):
            a0, a1 = k * math.tau / 10, (k + 1) * math.tau / 10
            timber("wheel", (x + math.cos(a0) * 0.8, y + math.sin(a0) * 0.8, 5.2), (x + math.cos(a1) * 0.8, y + math.sin(a1) * 0.8, 5.2), 0.09, "wood_dark")
        for k in range(6):
            a = k * math.tau / 6 + 0.3
            cx, cy = x + math.cos(a) * 0.8, y + math.sin(a) * 0.8
            loft("candle", [(5.24, 0.04, 0.04), (5.44, 0.035, 0.035)], "paper", 6, (cx, cy, 0))
            loft("flame", [(5.45, 0.02, 0.02), (5.5, 0.035, 0.035), (5.58, 0.0, 0.0)], "window", 6, (cx, cy, 0))
        timber("hub", (x, y, 5.12), (x, y, 5.3), 0.14, "stone_dark")


def trial_arena():
    box("floor", (0, 0, -0.05), (56, 52, 0.1), "sand")
    for x, y, w, d in ((0, 21, 46, 1), (0, -21, 46, 1), (-22, 0, 1, 42), (22, 0, 1, 42)):
        box("wall", (x, y, 3), (w, d, 6), "stone")
    disc("ring", (0, 0, 0), 11.4, 0.04, "stone", 40)
    disc("ring_inner", (0, 0, 0.01), 11, 0.05, "sand", 40)
    for i in range(10):
        a = i / 10 * math.tau
        timber("post", (math.cos(a) * 12, math.sin(a) * 12, 0), (math.cos(a) * 12, math.sin(a) * 12, 1.4), 0.2, "wood")
    for x in range(-21, 22, 6):
        for y in (20.6, -20.6):
            box("merlon", (x, y, 6.5), (2.6, 1, 1), "stone")
    for side, x in ((-1, -21.2), (1, 21.2)):
        for row in range(3):
            box("seat", (x - side * (row * 0.8), 0, 0.3 + row * 0.6), (0.8, 30, 0.6 + row * 0.6), "wood_dark" if row % 2 else "wood")
    for x in (-18, -9, 0, 9, 18):
        box("banner", (x, 20.4, 4), (2, 0.06, 3), "cloth_red" if int(x / 9) % 2 == 0 else "shutter")
        box("banner_rod", (x, 20.3, 5.55), (2.4, 0.1, 0.1), "wood_dark")
    box("gate", (0, -20.3, 1.8), (4, 0.3, 3.6), "wood")
    for x in (-2.3, 2.3):
        box("gate_post", (x, -20.2, 2.1), (0.6, 0.6, 4.2), "stone_dark")
    box("gate_top", (0, -20.2, 4.3), (5.4, 0.6, 0.6), "stone_dark")
    for x in (-1.2, 0, 1.2):
        timber("bar", (x, -20.1, 0), (x, -20.1, 3.5), 0.1, "stone_dark")
    for x in (-15, 15):
        timber("rack", (x - 1.2, 18.8, 1.3), (x + 1.2, 18.8, 1.3), 0.1)
        for i in range(3):
            timber("weapon", (x - 0.8 + i * 0.8, 18.7, 0), (x - 0.8 + i * 0.8, 18.7, 1.9), 0.07, "stone")


def bridge():
    box("deck", (0, 0, -0.08), (19, 6.4, 0.16), "wood")
    for i in range(-8, 9):
        timber("plank", (i, -3.1, 0.02), (i, 3.1, 0.02), 0.16, "wood_dark")
    for y in (-3.1, 3.1):
        for x in (-8.5, -4.2, 0, 4.2, 8.5):
            timber("post", (x, y, -2.6), (x, y, 1.2), 0.3)
        timber("rail", (-9, y, 1.15), (9, y, 1.15), 0.14)


def cliff():
    crag("cliff", 11, 9, 11, 4, 40)


def crag(name, width, depth, height, levels, seed, material="stone_dark"):
    """Layered rock with a flat foot and top. Each layer steps in, like weathered strata."""
    rng = random.Random(seed)
    sides = 18
    wobble = [rng.uniform(0.84, 1.14) for _ in range(sides)]
    bm = bmesh.new()
    rings = []
    for level in range(levels + 1):
        t = level / levels
        shrink = 1 - 0.34 * t ** 1.6
        jag = [w * rng.uniform(0.93, 1.05) for w in wobble]
        for lift in (0.0, height / levels * 0.86):
            if level == levels and lift > 0:
                continue
            z = height * t + lift
            ring = []
            for i in range(sides):
                a = i / sides * math.tau
                inset = 0.965 if lift else 1.0
                ring.append(bm.verts.new((math.cos(a) * width / 2 * shrink * jag[i] * inset,
                                          math.sin(a) * depth / 2 * shrink * jag[i] * inset, z)))
            rings.append(ring)
    for r in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    add_object(name, mesh, material)


def falls_cliff():
    crag("wall", 50, 18, 16, 6, 71, "stone")
    crag("left", 18, 12, 21, 7, 72, "stone_dark")
    bpy.data.objects["left"].location = (-19, 3, 0)
    crag("right", 16, 12, 19, 6, 73, "stone_dark")
    bpy.data.objects["right"].location = (20, 3, 0)
    for i, (x, y, z, s) in enumerate(((-12, -5.5, 15.5, 1.4), (10, -5.5, 15.5, 1.1), (15, -5, 15, 1.7), (-22, -5, 15, 1.2),
                                      (-9, -7.5, 8, 0.9), (9, -7.6, 6, 1.1), (-15, -7, 3, 1.2), (13, -7, 11, 0.9))):
        blob(f"moss{i}", (x, y, z), 1.9 * s, "grass" if i % 2 else "grass_dark", i, 0.45, 2)


def windmill():
    cylinder("tower", (0, 0, 3.6), 1.75, 7.2, "wall", 10)
    loft("cap", [(7.1, 1.95, 1.95), (8, 1.5, 1.5), (8.9, 0.15, 0.15)], "roof", 10)
    box("door", (0, -2.2, 1.1), (0.9, 0.3, 2.1), "wood")
    box("window", (0, -1.92, 4.6), (0.6, 0.2, 0.8), "shutter")
    timber("axle", (0, -1.5, 7.2), (0, -2.8, 7.2), 0.22, "wood_dark")
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        tip = (math.cos(a) * 5, -2.7, 7.2 + math.sin(a) * 5)
        timber(f"arm{i}", (0, -2.7, 7.2), tip, 0.14, "wood_dark")
        side = (-math.sin(a) * 0.45, 0, math.cos(a) * 0.45)
        verts = [(math.cos(a) * 1.2, -2.75, 7.2 + math.sin(a) * 1.2), tip,
                 (tip[0] + side[0] * 2, -2.75, tip[2] + side[2] * 2),
                 (math.cos(a) * 1.2 + side[0] * 2, -2.75, 7.2 + math.sin(a) * 1.2 + side[2] * 2)]
        add_object(f"sail{i}", mesh_from(f"sail{i}", verts, [(0, 1, 2, 3)]), "wall_b")


def watchtower():
    for x, y in ((-1.7, -1.7), (1.7, -1.7), (1.7, 1.7), (-1.7, 1.7)):
        timber("leg", (x * 1.15, y * 1.15, 0), (x * 0.85, y * 0.85, 6.4), 0.3)
    for z in (2.2, 4.4):
        for (a, b) in (((-1.7, -1.7), (1.7, -1.7)), ((1.7, -1.7), (1.7, 1.7)), ((1.7, 1.7), (-1.7, 1.7)), ((-1.7, 1.7), (-1.7, -1.7))):
            timber("brace", (a[0], a[1], z), (b[0], b[1], z + 1.6), 0.14, "wood_dark")
    box("deck", (0, 0, 6.4), (4.2, 4.2, 0.24), "wood_dark")
    for x in (-2, 2):
        box("rail", (x, 0, 7.1), (0.14, 4.1, 1.1), "wood")
        box("rail", (0, x, 7.1), (4.1, 0.14, 1.1), "wood")
    for x, y in ((-1.9, -1.9), (1.9, -1.9), (1.9, 1.9), (-1.9, 1.9)):
        timber("post", (x, y, 6.4), (x, y, 9), 0.16)
    roof("roof", (0, 0, 9), 5, 5, 1.6, "thatch")


def field():
    box("soil", (0, 0, 0.03), (12, 8, 0.12), "wood_dark")
    rng = random.Random(4)
    for row in range(7):
        y = -3.3 + row * 1.1
        for i in range(24):
            x = -5.6 + i * 0.49 + rng.uniform(-0.1, 0.1)
            box(f"crop{row}_{i}", (x, y, 0.42), (0.16, 0.2, 0.8 + rng.uniform(-0.15, 0.15)), "crop")


def pier():
    box("deck", (0, 0, 0.5), (10, 2.4, 0.14), "wood")
    for x in range(-4, 6, 2):
        for y in (-1.1, 1.1):
            timber("pile", (x, y, -1.6), (x, y, 0.9), 0.2, "wood_dark")
    for x in range(-5, 5):
        timber("plank", (x + 0.5, -1.2, 0.58), (x + 0.5, 1.2, 0.58), 0.1, "wood_dark")
    box("barrel", (3.6, 0.7, 0.9), (0.6, 0.6, 0.7), "wood")


def standing_stone():
    crag("stone", 1.2, 0.9, 3, 3, 81, "stone")


def stone_wall():
    rng = random.Random(21)
    for row in range(3):
        for i in range(7 - row % 2):
            x = -1.95 + i * 0.65 + (0.32 if row % 2 else 0)
            size = (0.6 + rng.uniform(-0.06, 0.06), 0.62, 0.26 + rng.uniform(-0.02, 0.05))
            box(f"stone{row}_{i}", (x, rng.uniform(-0.04, 0.04), 0.14 + row * 0.27), size, "stone" if (i + row) % 3 else "stone_dark")


def ruin_arch():
    for x in (-2.6, 2.6):
        box("base", (x, 0, 0.3), (1.8, 1.8, 0.6), "stone_dark")
        cylinder(f"column{x}", (x, 0, 2.6), 0.6, 4.2, "stone", 8)
    for i in range(7):
        a = i / 6 * math.pi
        blob(f"arch{i}", (math.cos(a) * 2.7, 0, 4.7 + math.sin(a) * 1.5), 0.62, "stone", i, 0.9, 1)
    blob("fallen", (3.6, 1.8, 0.4), 0.8, "stone_dark", 3, 0.6, 2)


def ruin_wall():
    rng = random.Random(11)
    for row in range(5):
        for i in range(8 - row):
            x = -2.6 + i * 0.75 + row * 0.15
            box(f"block{row}_{i}", (x, rng.uniform(-0.05, 0.05), 0.35 + row * 0.7), (0.7, 0.8, 0.64), "stone" if (i + row) % 3 else "stone_dark")


def jungle_tree():
    """A giant with buttress roots, a flat high canopy and hanging vines."""
    rng = random.Random(61)
    tube("trunk", [(0, 0, -0.2), (0, 0, 3), (0.2, 0.1, 6), (0.1, 0.2, 9)], [0.9, 0.52, 0.42, 0.28], "bark_dark", 9)
    for i in range(6):
        a = i * 1.05
        tube(f"root{i}", [(0, 0, 2.2), (math.cos(a) * 0.9, math.sin(a) * 0.9, 0.7), (math.cos(a) * 2, math.sin(a) * 2, -0.1)], [0.26, 0.2, 0.05], "bark_dark", 5)
    clumps = []
    for i in range(10):
        a = i * 0.63 + rng.uniform(-0.2, 0.2)
        r = rng.uniform(1.2, 3.8)
        c = (math.cos(a) * r, math.sin(a) * r, rng.uniform(8.8, 10.4))
        clumps.append((c, rng.uniform(1.4, 2)))
        tube(f"branch{i}", [(0.1, 0.2, 7.6), (c[0] * 0.5, c[1] * 0.5, 8.7), c], [0.2, 0.12, 0.03], "bark_dark", 5)
        blob(f"core{i}", c, 1.1, "foliage_core", i, 0.6, 1)
    leaf_cards("leaves", clumps, "foliage_jungle", "broad", 20, 0.8, 62)
    for i in range(9):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(1.5, 3.8)
        top = (math.cos(a) * r, math.sin(a) * r, 8.5)
        tube(f"vine{i}", [top, (top[0] + 0.1, top[1], 6), (top[0], top[1] + 0.1, rng.uniform(2, 4))], [0.04, 0.03, 0.015], "grass_dark", 4)


def fern():
    rng = random.Random(70)
    verts, faces, uvs, normals = [], [], [], []
    u0, v0 = FOLIAGE_CELLS["small"]
    for index in range(9):
        a = index / 9 * math.tau + rng.uniform(-0.2, 0.2)
        out = mathutils.Vector((math.cos(a), math.sin(a), 0))
        side = mathutils.Vector((-math.sin(a), math.cos(a), 0)) * 0.2
        length = rng.uniform(0.8, 1.15)
        base = len(verts)
        mid = out * length * 0.55 + mathutils.Vector((0, 0, 0.5))
        end = out * length + mathutils.Vector((0, 0, 0.2))
        for point, u, v in ((mathutils.Vector((0, 0, 0.02)) - side * 0.3, 0.1, 0), (mathutils.Vector((0, 0, 0.02)) + side * 0.3, 0.4, 0),
                            (mid + side, 0.5, 0.25), (mid - side, 0, 0.25), (end + side * 0.2, 0.3, 0.5), (end - side * 0.2, 0.2, 0.5)):
            verts.append(tuple(point))
            uvs.append((u0 + u * 0.995, 1 - (v0 + v * 0.995)))
            normals.append(tuple(mathutils.Vector((point.x * 0.6, point.y * 0.6, 1)).normalized()))
        faces += [(base, base + 1, base + 2, base + 3), (base + 3, base + 2, base + 4, base + 5)]
    mesh = mesh_from("fern", verts, faces)
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    mesh.normals_split_custom_set_from_vertices(normals)
    add_object("fern", mesh, "foliage_jungle")


def training_dummy():
    timber("pole", (0, 0, 0), (0, 0, 1.8), 0.14)
    timber("arms", (-0.7, 0, 1.3), (0.7, 0, 1.3), 0.11)
    blob("head", (0, 0, 1.75), 0.26, "sack", 2, 1, 2)
    box("torso", (0, 0, 1.15), (0.5, 0.34, 0.8), "sack")


def tent():
    """A patched hide shelter on crossed poles, with a dark mouth."""
    w, d, h = 1.7, 2.0, 2.4
    verts = [(-w, -d, 0), (w, -d, 0), (w, d, 0), (-w, d, 0), (0, -d - 0.15, h), (0, d + 0.15, h)]
    add_object("canvas", mesh_from("canvas", verts, [(0, 3, 5, 4), (1, 2, 5, 4), (2, 3, 5)]), "hide")
    add_object("mouth", mesh_from("mouth", [(-w * 0.8, -d + 0.02, 0), (w * 0.8, -d + 0.02, 0), (0, -d - 0.08, h * 0.86)], [(0, 1, 2)]), "hide_dark")
    for y in (-d - 0.1, d + 0.1):
        timber("pole", (-0.5, y, 0), (0.2, y, h + 0.5), 0.09, "wood_dark")
        timber("pole", (0.5, y, 0), (-0.2, y, h + 0.5), 0.09, "wood_dark")
    timber("ridge", (0, -d - 0.3, h + 0.02), (0, d + 0.3, h + 0.02), 0.08, "wood_dark")
    for i, (x, y, z) in enumerate(((0.8, -0.8, 1.05), (-0.9, 0.5, 0.9), (0.6, 1.0, 1.3))):
        box(f"patch{i}", (x, y, z), (0.5, 0.5, 0.05), "hide_dark")


def camp_fire():
    for index in range(9):
        angle = index / 9 * math.tau
        blob(f"stone{index}", (math.cos(angle) * 0.55, math.sin(angle) * 0.55, 0.08), 0.15, "stone_dark", index, 0.7, 1)
    for index in range(5):
        angle = index / 5 * math.tau + 0.4
        timber(f"log{index}", (math.cos(angle) * 0.42, math.sin(angle) * 0.42, 0.06), (math.cos(angle) * 0.06, math.sin(angle) * 0.06, 0.42), 0.1, "wood_dark")
    blob("embers", (0, 0, 0.06), 0.3, "window", 4, 0.3, 1)


def palisade():
    for index in range(9):
        x = -3.9 + index * 0.98
        cylinder(f"stake{index}", (x, 0, 1.4), 0.33, 2.9 + (index % 3) * 0.25, "wood")


def ruin():
    box("base", (0, 0, 0.25), (2.3, 2.3, 0.5), "stone")
    cylinder("column", (0, 0, 2.2), 0.72, 3.5, "stone", 8)
    blob("rubble", (1.4, 0.8, 0.2), 0.55, "stone_dark", 2, 0.7, 1)


MODELS = {
    "cottage": cottage,
    "tree_a": oak,
    "tree_b": oak_b,
    "birch": birch,
    "blossom": blossom,
    "apple": apple,
    "pine": pine,
    "pine_b": pine_b,
    "willow": willow,
    "dead_tree": dead_tree,
    "stump": stump,
    "fallen_log": fallen_log,
    "twigs": twigs,
    "berry_bush": berry_bush,
    "tall_bush": tall_bush,
    "reeds": reeds,
    "grass_tuft": grass_tuft,
    "rock": rock,
    "bush": bush,
    "anime_head": anime_head,
    "anime_hair_hero": anime_hair_hero,
    "anime_hair_mira": anime_hair_mira,
    "anime_hair_rhea": anime_hair_rhea,
    "anime_hair_bram": anime_hair_bram,
    "anime_hair_tess": anime_hair_tess,
    "anime_beard": anime_beard,
    "anime_hair_lina": anime_hair_lina,
    "anime_hair_spiky": anime_hair_spiky,
    "anime_hair_short": anime_hair_short,
    "anime_hair_sora": anime_hair_sora,
    "anime_hair_wavy": anime_hair_wavy,
    "anime_hair_elda": anime_hair_elda,
    "anime_bun": anime_bun,
    "anime_hair_pip": anime_hair_pip,
    "anime_headband": anime_headband,
    "anime_braids": anime_braids,
    "anime_ponytail": anime_ponytail,
    "anime_torso_f": anime_torso_f,
    "anime_vest": anime_vest,
    "anime_bow": anime_bow,
    "anime_armor": anime_armor,
    "anime_apron": anime_apron,
    "anime_robe": anime_robe,
    "anime_torso": anime_torso,
    "anime_pelvis": anime_pelvis,
    "anime_cloak": anime_cloak,
    "anime_upper_arm": anime_upper_arm,
    "anime_hair_long": anime_hair_long,
    "anime_skirt": anime_skirt,
    "guild_hall": guild_hall,
    "guild_interior": guild_interior,
    "trial_arena": trial_arena,
    "bridge": bridge,
    "cliff": cliff,
    "ruin_arch": ruin_arch,
    "ruin_wall": ruin_wall,
    "jungle_tree": jungle_tree,
    "fern": fern,
    "training_dummy": training_dummy,
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
    "falls_cliff": falls_cliff,
    "windmill": windmill,
    "watchtower": watchtower,
    "field": field,
    "pier": pier,
    "standing_stone": standing_stone,
    "camp_fire": camp_fire,
    "palisade": palisade,
    "ruin": ruin,
}

for beast in beasts.NAMES:
    MODELS["beast_" + beast] = (lambda kind: lambda: beasts.build(kind, add_object, smooth))(beast)

# Arguments after "--" pick models by name, so one change does not rebuild everything.
WANTED = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for model_name, build in MODELS.items():
    if WANTED and model_name not in WANTED:
        continue
    start_scene()
    build()
    export(model_name)
