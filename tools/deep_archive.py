"""Lays out the deep Sunken Archive (design/dungeon-archive.md) in world/zones/meadow-valley.json.

The first hall stays as it was, except the middle of its back wall, which becomes an arch. Behind it: the Drowned
Stair (a corridor), the Flooded Stacks (a hall of shelves with a secret wall in its east side and a hidden vault
behind it), a short passage, and the Keeper's Hall with the boss on its plinth and a gate to the way out.
Everything this script adds carries "deep": true, so running it again replaces its own pieces and nothing else:

    python tools/deep_archive.py

Walls are archive_wall pieces six metres long and 0.8 thick; yaw 0 faces +z, pi faces -z, +pi/2 faces +x.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZONE = ROOT / "world" / "zones" / "meadow-valley.json"
PI, HALF = 3.1416, 1.5708
NL = chr(10)


def wall(x, z, yaw, model="archive_wall", **extra):
    piece = {"model": model, "x": x, "z": z}
    if yaw:
        piece["yaw"] = yaw
    piece.update(extra)
    return piece


def cross_wall(z, yaw, middle="archive_arch"):
    """A wall across the dungeon from x 542 to 578 with a doorway (or another middle piece) at x 560."""
    return [wall(560, z, yaw, middle)] + [wall(x, z, yaw) for x in (545, 551, 569, 575)]


def side_walls(z0, count, west=541.6, east=578.4, skip=()):
    """Side walls of a hall: count pieces going south from z0 (each six metres), west facing +x, east facing -x."""
    pieces = []
    for i in range(count):
        z = round(z0 - 3 - i * 6, 1)
        pieces.append(wall(west, z, HALF))
        if z not in skip:
            pieces.append(wall(east, z, -HALF))
    return pieces


def corridor(z_list):
    return [p for z in z_list for p in (wall(557.6, z, HALF), wall(562.4, z, -HALF))]


def props():
    out = []
    # Floors and vaults: the deep floor and ceiling cover everything south of the first hall; the vault has its own.
    out += [wall(560, -66.5, 0, "archive_deep_floor"), wall(560, -66.5, 0, "archive_deep_ceiling"),
            wall(588.4, -52.4, 0, "archive_vault_floor"), wall(588, -52.4, 0, "archive_vault_ceiling")]
    # The first hall's back wall opens in the middle.
    out.append(wall(560, -17.6, 0, "archive_arch"))
    # 2. The Drowned Stair.
    out += corridor((-21, -27, -33, -34.6))
    # 3. The Flooded Stacks: north wall (arch), side walls (one east piece is the secret wall), south wall (arch).
    out += cross_wall(-37.2, PI)
    out += side_walls(-37.4, 5, skip=(-52.4,))
    out.append(wall(578.4, -52.4, -HALF, "archive_secret_wall", unless="archive.secret"))
    out += cross_wall(-67.8, 0)
    for x, z in ((548, -42), (572, -42), (548, -62.5), (572, -64)):
        out.append(wall(x, z, 0, "archive_pillar"))
    for x, z, yaw in ((546.5, -44.5, 0), (549.5, -44.5, 0), (570.5, -44.5, 0), (573.5, -44.5, 0),
                      (552, -52, 0), (555, -52, 0), (565, -52, 0), (568, -52, 0),
                      (546.5, -57.5, 0), (549.5, -57.5, 0), (567, -57.5, PI), (570, -57.5, PI)):
        out.append(wall(x, z, yaw, "archive_shelf"))
    out += [wall(553.2, -57.9, 0.3, "archive_shelf_fallen"), wall(575.5, -61.5, 1.2, "archive_shelf_fallen")]
    # 3b. The hidden vault behind the secret wall.
    out += [wall(582.2, -49.0, PI), wall(588.2, -49.0, PI), wall(582.2, -55.8, 0), wall(588.2, -55.8, 0), wall(591.6, -52.4, -HALF)]
    out += [wall(589.6, -52.4, -HALF, "storage_chest"), wall(583.0, -50.4, PI, "storage_chest")]
    out.append(wall(586.0, -54.6, 0, "archive_lectern"))
    # 4. The passage to the Keeper.
    out += corridor((-71.2, -73.2))
    # 5. The Keeper's Hall: arch north, eight pillars in a ring, the plinth, and the gate south.
    out += cross_wall(-76.6, PI)
    out += side_walls(-76.6, 5)
    out += cross_wall(-107.0, 0)
    out.append(wall(560, -107.0, 0, "archive_gate", unless="archive.keeper"))
    for k in range(8):
        a = math.pi / 8 + k * math.pi / 4
        out.append(wall(round(560 + math.cos(a) * 11, 2), round(-91.5 + math.sin(a) * 11, 2), 0, "archive_pillar"))
    out.append(wall(560, -99, PI, "keeper_plinth"))
    # The alcove behind the gate with the daylight and the way up.
    out += [wall(557.6, -110, HALF), wall(562.4, -110, -HALF), wall(560, -112.6, 0), wall(560, -110, 0, "archive_light_shaft")]
    for piece in out:
        piece["deep"] = True
    return out


def camps():
    out = [
        # Two sentinels inside the Stacks' north arch, facing it.
        {"x": 555.5, "z": -40.5, "radius": 0.3, "count": 1, "respawnTicks": 5400, "types": ["auren_sentinel"], "yaw": PI},
        {"x": 564.5, "z": -40.5, "radius": 0.3, "count": 1, "respawnTicks": 5400, "types": ["auren_sentinel"], "yaw": PI},
        # Wisps drifting in the open aisle, and slimes in the wet south end.
        {"x": 560, "z": -48, "radius": 3.5, "count": 3, "minLevel": 7, "maxLevel": 8, "respawnTicks": 3600, "types": ["barrow_wisp"]},
        {"x": 559, "z": -62.5, "radius": 3, "count": 3, "minLevel": 6, "maxLevel": 7, "respawnTicks": 3600, "types": ["slime"]},
        # The Keeper on its plinth, facing the way in.
        {"x": 560, "z": -99, "radius": 0.3, "count": 1, "respawnTicks": 108000, "types": ["archive_keeper"], "yaw": PI},
    ]
    for camp in out:
        camp["deep"] = True
    return out


def npcs():
    out = [
        {"id": "archive_glyph", "name": "Warm Glyph", "role": "secret", "x": 577.0, "z": -52.4, "yaw": -HALF},
        {"id": "cache_archive_vault", "name": "Rune-locked Chest", "role": "cache", "x": 589.6, "z": -52.4, "yaw": -HALF,
         "requires": "drowned_key",
         "note": "An old iron-bound chest with a lock of white stone set in its lid, cut like a rune. No pick will turn it; it wants its own key.",
         "locked": "The lock is shaped like an Auren rune. Old Tobb says the Auren threw their keys into Mirror Lake; maybe the lake still has this one.",
         "found": "the Archive's last treasury: Auren silver, a rune core and two flasks of a healer's draught, sealed in wax.",
         "reward": {"gold": 220, "items": {"rune_core": 1, "healing_potion": 2}}},
        {"id": "cache_archive_satchel", "name": "Old Chest", "role": "cache", "x": 583.0, "z": -50.4, "yaw": PI,
         "note": "A scribe's chest under the reading desk, its leather gone hard as wood.",
         "found": "a purse of old coin and two cold blue embers in a jar.",
         "reward": {"gold": 60, "items": {"wisp_ember": 2}}},
    ]
    for npc in out:
        npc["deep"] = True
    return out


def replace_list(lines, key, items, drop=lambda line: False):
    """Rewrites one list of the zone file in place, line by line (the file keeps one entry per line): drops the
    lines this script wrote before (and any others drop() names) and appends the new entries before the list ends."""
    start = next(i for i, line in enumerate(lines) if line.strip().startswith(f'"{key}": ['))
    end = start + 1
    while not lines[end].strip().startswith("]"):
        end += 1
    body = [line for line in lines[start + 1:end] if '"deep": true' not in line and not drop(line)]
    body = [line.rstrip().rstrip(",") for line in body if line.strip()]
    body += ["    " + json.dumps(item, ensure_ascii=False) for item in items]
    body = [line + "," for line in body[:-1]] + body[-1:]
    lines[start + 1:end] = body
    return lines


def main():
    lines = ZONE.read_text(encoding="utf-8").split(NL)

    def old_back_wall(line):
        # The first hall's two middle back wall pieces give way to the arch.
        try:
            item = json.loads(line.strip().rstrip(","))
        except ValueError:
            return False
        return item.get("model") == "archive_wall" and item.get("z") == -17.6 and item.get("x") in (557, 563)

    lines = replace_list(lines, "props", props(), old_back_wall)
    lines = replace_list(lines, "enemies", camps())
    lines = replace_list(lines, "npcs", npcs())
    lines = replace_list(lines, "platforms", [{"x0": 538, "z0": -115, "x1": 596, "z1": -20, "height": 0, "deep": True}])
    lines = replace_list(lines, "walls", [{"x0": 538, "z0": -114, "x1": 593, "z1": -18, "height": 7.4, "bottom": 6.3, "deep": True}])
    lines = replace_list(lines, "portals", [{"x": 560, "z": -110.6, "toX": -42, "toZ": 129.6, "deep": True}])
    text = NL.join(lines)
    json.loads(text)
    ZONE.write_text(text, encoding="utf-8", newline=NL)
    print(f"deep archive: {len(props())} props, {len(camps())} camps, {len(npcs())} people and things")


if __name__ == "__main__":
    main()
