"""Draws Meadow Valley as it really is in the game: heights from export/previews/zone-height.bin
(written by the test ZoneHeightDump with PIXEL_DUMP=1), with hill shading, contours every 2 m, water,
roads, buildings, camps and place names from world/zones/meadow-valley.json.

Usage: python tools/zone_view.py <out.png> [title]
"""
import json
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
from world_map import draw_label, hillshade, ramp, star  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
STOPS = [(-4, "#3a78a3"), (0, "#9cc27a"), (4, "#7ab85a"), (10, "#8fb05a"), (18, "#b4a86a"), (28, "#a68a64"), (40, "#8e8c90"), (60, "#d9d9de")]
BUILDINGS = {"cottage", "tavern", "guild_hall", "windmill", "farmhouse", "barn", "watchtower", "fort_wall", "smithy", "bakery", "stall",
             "alchemy_stall", "chapel", "house", "longhouse"}


def main():
    out = Path(sys.argv[1])
    title = sys.argv[2] if len(sys.argv) > 2 else "Meadow Valley"
    raw = (ROOT / "export" / "previews" / "zone-height.bin").read_bytes()
    size = struct.unpack_from("<i", raw)[0]
    h = np.frombuffer(raw, "<f4", size * size, 4).reshape(size, size).astype(float)
    zone = json.loads((ROOT / "world" / "zones" / "meadow-valley.json").read_text(encoding="utf-8"))
    zoom = 2
    shade = hillshade(h, 1.0)
    colour = ramp(h, STOPS) * (0.55 + 0.6 * shade[..., None])
    water = np.zeros(h.shape, bool)

    def cell(x, z):
        return ((x + size / 2) * zoom, (z + size / 2) * zoom)

    image = Image.fromarray(np.clip(colour, 0, 255).astype(np.uint8)).resize((size * zoom, size * zoom), Image.BILINEAR)
    lines = np.zeros(h.shape, bool)
    for level in range(-2, 80, 2):
        above = h >= level
        lines |= above ^ ndimage.binary_erosion(above)
    mask = Image.fromarray((lines * 90).astype(np.uint8)).resize(image.size, Image.NEAREST)
    image = Image.composite(Image.new("RGB", image.size, (40, 34, 30)), image, mask)
    draw = ImageDraw.Draw(image)
    for river in zone.get("rivers", []):
        draw.line([cell(*p) for p in river["points"]], fill=(70, 140, 200), width=int(river["width"] * zoom))
    for lake in zone.get("lakes", []):
        x, y = cell(lake["x"], lake["z"])
        r = lake["radius"] * zoom
        draw.ellipse([x - r, y - r, x + r, y + r], fill=(70, 140, 200))
    for path in zone.get("paths", []):
        points = path.get("points", path) if isinstance(path, dict) else path
        try:
            draw.line([cell(*p[:2]) for p in points], fill=(214, 180, 120), width=6)
        except (TypeError, ValueError):
            pass
    for prop in zone.get("props", []):
        if prop["model"] in BUILDINGS or "hall" in prop["model"] or "house" in prop["model"]:
            x, y = cell(prop["x"], prop["z"])
            draw.rectangle([x - 6, y - 6, x + 6, y + 6], fill=(150, 70, 50), outline=(30, 15, 10))
    for camp in zone.get("enemies", []):
        x, y = cell(camp["x"], camp["z"])
        draw.polygon([(x, y - 7), (x + 6, y + 5), (x - 6, y + 5)], fill=(200, 60, 50))
    for region in zone.get("regions", []):
        if abs(region["x"]) <= size / 2 and abs(region["z"]) <= size / 2:
            draw_label(draw, cell(region["x"], region["z"]), region["name"], 16, (255, 250, 230), weight=800, stroke=4)
    draw_label(draw, (image.width / 2, 26), title, 26, (255, 255, 255))
    draw_label(draw, (image.width - 40, 60), "N", 22, (255, 120, 120))
    out.parent.mkdir(parents=True, exist_ok=True)
    image.save(out)
    print("wrote", out, f"heights {h.min():.1f}..{h.max():.1f} m")


if __name__ == "__main__":
    main()
