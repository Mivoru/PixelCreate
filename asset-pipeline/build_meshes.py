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
    "skin": "E8B796", "hat": "E4A672",
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


def blob(name, center, radius, material, seed, squash=1.0, subdivisions=2):
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)
    for vert in bm.verts:
        vert.co *= 1 + rng.uniform(-0.14, 0.14)
        vert.co.z *= squash
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
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


MODELS = {
    "village": village,
    "cottage": cottage,
    "tree_a": lambda: tree("a", 0, 0, 1.0, 11),
    "tree_b": lambda: tree("b", 0, 0, 1.0, 21),
    "rock": rock,
    "bush": bush,
}

for model_name, build in MODELS.items():
    start_scene()
    build()
    export(model_name)
