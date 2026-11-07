"""Collision boxes measured from the models themselves, so every hitbox follows the shape it belongs to.

After a model is built, its surfaces are sampled densely and the samples are dropped into a 25 cm
grid seen from above. A cell is solid when some surface passes through it at a height a player
could bump into; its box reaches up to the highest surface over that cell. Neighbouring cells of a
similar height are merged into rectangles. The result goes to world/collision.json in game axes
(x, and z = -y in Blender), around the model's origin, which the game rotates, scales and places.

Parts hung under an empty (the mill's sails) move in the game, so they are left out.
"""
import json
import math
from pathlib import Path

import bmesh
import bpy

CELL = 0.25
# Surfaces between these heights above the model's foot block a walker (who is 1.8 m tall); lower ones are floors and
# rugs, higher ones are roofs and crowns overhead.
LOW, HIGH = 0.12, 2.0
TOP_STEP = 0.25
SPACING = 0.08

FILE = Path(__file__).resolve().parent.parent / "world" / "collision.json"

# Leaves, grass, flowers and crops bend aside; a walker pushes through them.
SOFT = ("foliage", "leaf", "grass", "reed", "crop", "flower", "fern")
# Boughs, twigs and vines are thin and give way too, whatever they are made of.
SOFT_PARTS = ("bough", "branch", "twig", "vine", "leaves", "strand")


def sample(obj):
    """Points spread over every face of an object, in world space."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = obj.evaluated_get(depsgraph).to_mesh()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    matrix = obj.matrix_world
    materials = [m.name if m else "" for m in obj.data.materials]
    for face in bm.faces:
        material = materials[face.material_index] if face.material_index < len(materials) else ""
        if material.startswith(SOFT):
            continue
        a, b, c = (matrix @ v.co for v in face.verts)
        longest = max((b - a).length, (c - b).length, (a - c).length)
        steps = max(1, math.ceil(longest / SPACING))
        for i in range(steps + 1):
            for j in range(steps + 1 - i):
                u, v = i / steps, j / steps
                yield a + (b - a) * u + (c - a) * v
    bm.free()
    obj.evaluated_get(depsgraph).to_mesh_clear()


def measure(high=HIGH):
    """The collision boxes of the scene as built: [x0, z0, x1, z1, top] in game axes. A tree passes
    a lower high so only its trunk counts and a walker brushes under its boughs."""
    solid = {}
    tops = {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or (obj.parent is not None and obj.parent.type == "EMPTY") or obj.name.startswith(SOFT_PARTS):
            continue
        for point in sample(obj):
            key = (math.floor(point.x / CELL), math.floor(-point.y / CELL))
            tops[key] = max(tops.get(key, -1e9), point.z)
            if LOW < point.z < high:
                solid[key] = True
    # A solid with no surface at walker height inside (a crag is only walls, a foot and a top) is
    # filled: every cell the outside cannot reach without crossing a solid cell is solid too.
    if solid:
        xs = [k[0] for k in solid]
        zs = [k[1] for k in solid]
        x0, x1, z0, z1 = min(xs) - 1, max(xs) + 1, min(zs) - 1, max(zs) + 1
        outside = {(x0, z0)}
        frontier = [(x0, z0)]
        while frontier:
            cx, cz = frontier.pop()
            for nx, nz in ((cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1)):
                if x0 <= nx <= x1 and z0 <= nz <= z1 and (nx, nz) not in outside and (nx, nz) not in solid:
                    outside.add((nx, nz))
                    frontier.append((nx, nz))
        for x in range(x0, x1 + 1):
            for z in range(z0, z1 + 1):
                if (x, z) not in outside and (x, z) not in solid:
                    solid[(x, z)] = True
                    tops.setdefault((x, z), HIGH)
    # Low things keep their height, so a step or a fence top is exact; anything taller than a
    # walker can reach is one class, which lets big buildings merge into few boxes.
    heights = {key: math.ceil(tops[key] / TOP_STEP) * TOP_STEP for key in solid}
    cells = {key: (h if h < HIGH else HIGH) for key, h in heights.items()}
    boxes = []
    done = set()
    for key in sorted(cells):
        if key in done:
            continue
        x, z = key
        top = cells[key]
        width = 1
        while (x + width, z) in cells and (x + width, z) not in done and cells[(x + width, z)] == top:
            width += 1
        depth = 1
        while all((x + k, z + depth) in cells and (x + k, z + depth) not in done and cells[(x + k, z + depth)] == top
                  for k in range(width)):
            depth += 1
        for dx in range(width):
            for dz in range(depth):
                done.add((x + dx, z + dz))
        top = max(heights[(x + dx, z + dz)] for dx in range(width) for dz in range(depth))
        boxes.append([round(x * CELL, 2), round(z * CELL, 2), round((x + width) * CELL, 2), round((z + depth) * CELL, 2),
                      round(top, 2)])
    return boxes


def record(name, boxes):
    """Writes one model's boxes into the shared file, keeping the other models' entries."""
    data = json.loads(FILE.read_text()) if FILE.exists() else {}
    data[name] = boxes
    lines = [f'  "{key}": {json.dumps(data[key], separators=(",", ":"))}' for key in sorted(data)]
    FILE.write_text("{\n" + ",\n".join(lines) + "\n}\n")
