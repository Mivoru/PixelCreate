"""Build low-poly game assets with headless Blender.

Usage: blender -b --factory-startup -P asset-pipeline/build_meshes.py
Writes one .glb per model into client/assets. Colours come from the Endesga 32 palette.
"""
import math
import random
from pathlib import Path

import bmesh
import bpy

OUT = Path(__file__).resolve().parent.parent / "client" / "assets"

COLORS = {
    "grass": "3E8948", "grass_dark": "265C42", "leaf": "63C74D", "path": "D77643",
    "wall": "E4A672", "roof": "A22633", "wood": "733E39", "wood_dark": "3E2731",
    "stone": "8B9BB4", "stone_dark": "5A6988", "window": "FEE761", "tunic": "0099DB",
    "skin": "E0AC8C", "hat": "E4A672", "hair": "5A3A28",
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


def cottage():
    box("walls", (0, 0, 1.5), (6, 4.4, 3), "wall")
    roof("roof", (0, 0, 3), 6.9, 5.4, 2.3, "roof")
    box("chimney", (1.7, 0.8, 4.6), (0.8, 0.8, 2.2), "stone")
    box("door", (0, -2.22, 1.05), (1.1, 0.08, 2.1), "wood")
    for x in (-1.9, 1.9):
        box(f"frame{x}", (x, -2.21, 1.7), (1.2, 0.05, 1.2), "wood_dark")
        box(f"window{x}", (x, -2.24, 1.7), (0.95, 0.05, 0.95), "window")


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


def sculpt(bm, shape):
    for vert in bm.verts:
        vert.co = shape(vert.co.x, vert.co.y, vert.co.z)


def face_shape(x, y, z):
    """Turns a unit sphere into a head that looks along +Y."""
    gauss = lambda value, width: math.exp(-(value / width) ** 2)
    jaw = max(0.0, -z)
    width = 0.105 * (1 - 0.34 * jaw ** 1.6)
    depth = 0.122 * (1 - 0.16 * jaw)
    front = max(0.0, y)
    px, py, pz = x * width, y * depth, z * 0.142
    nose = 0.034 * gauss(x, 0.17) * gauss(z + 0.14, 0.26) * front
    bridge = 0.016 * gauss(x, 0.13) * gauss(z - 0.1, 0.2) * front
    brow = 0.011 * gauss(z - 0.3, 0.12) * front
    socket = 0.012 * gauss(abs(x) - 0.42, 0.2) * gauss(z - 0.16, 0.13) * front
    lips = 0.009 * gauss(x, 0.28) * gauss(z + 0.5, 0.1) * front
    chin = 0.012 * gauss(x, 0.3) * gauss(z + 0.82, 0.14) * front
    cheek = 0.006 * gauss(abs(x) - 0.55, 0.25) * gauss(z + 0.2, 0.25)
    return (px + math.copysign(cheek, x), py + nose + bridge + brow - socket + lips + chin, pz)


def human_head():
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1)
    sculpt(bm, face_shape)
    mesh = bpy.data.meshes.new("head")
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    add_object("head", mesh, "skin")
    for side in (-1, 1):
        ear = bmesh.new()
        bmesh.ops.create_icosphere(ear, subdivisions=2, radius=1)
        for vert in ear.verts:
            vert.co = (vert.co.x * 0.012, vert.co.y * 0.023, vert.co.z * 0.036)
        ear_mesh = bpy.data.meshes.new("ear")
        ear.to_mesh(ear_mesh)
        ear.free()
        for polygon in ear_mesh.polygons:
            polygon.use_smooth = True
        add_object("ear", ear_mesh, "skin", (side * 0.106, -0.005, 0.0))


def hair_shape(x, y, z):
    """A cap of hair over the crown and back of the head, with an uneven lower edge."""
    edge = 0.15 + 0.05 * math.sin(x * 9) * math.cos(y * 7)
    limit = -0.55 if y < 0 else edge
    drop = max(0.0, limit - z) if y > 0 else 0.0
    return (x * 0.116, y * 0.13 - 0.004, max(z, limit - 0.6) * 0.158 + 0.012 + drop * 0.0)


def human_hair():
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1)
    remove = [v for v in bm.verts if v.co.z < (0.15 if v.co.y > 0.25 else -0.62)]
    bmesh.ops.delete(bm, geom=remove, context="VERTS")
    sculpt(bm, hair_shape)
    mesh = bpy.data.meshes.new("hair")
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    add_object("hair", mesh, "hair")


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
    "human_head": human_head,
    "human_hair": human_hair,
    "tent": tent,
    "camp_fire": camp_fire,
    "palisade": palisade,
    "ruin": ruin,
}

for model_name, build in MODELS.items():
    start_scene()
    build()
    export(model_name)
