"""Draws the planned world of Velmora from design/world/layout.json.

Usage: python tools/world_map.py [--scale 4]

Writes design/maps/world-map.png (the world as a player's map: land, water, roads, towns, dungeons,
waystones, regions with their levels) and design/maps/elevation.png (heights in colour with hill
shading and contour lines). The heights are built from the layout: mountain ranges and their passes,
plateaus, basins, river channels, lakes and the sea. North (-z) is up, as on the game's maps.
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ROOT = Path(__file__).resolve().parent.parent
LAYOUT = ROOT / "design" / "world" / "layout.json"
OUT = ROOT / "design" / "maps"
FONT = ROOT / "client" / "assets" / "ui" / "Nunito.ttf"


def font(size, weight=800):
    face = ImageFont.truetype(str(FONT), size)
    try:
        face.set_variation_by_axes([weight])
    except (OSError, AttributeError):
        pass
    return face


class Grid:
    """World metres to grid cells and back."""

    def __init__(self, bounds, metres_per_cell):
        self.x0, self.x1, self.z0, self.z1 = bounds["x0"], bounds["x1"], bounds["z0"], bounds["z1"]
        self.m = metres_per_cell
        self.w = int((self.x1 - self.x0) / self.m)
        self.h = int((self.z1 - self.z0) / self.m)
        xs = self.x0 + (np.arange(self.w) + 0.5) * self.m
        zs = self.z0 + (np.arange(self.h) + 0.5) * self.m
        self.X, self.Z = np.meshgrid(xs, zs)

    def cell(self, x, z):
        return ((x - self.x0) / self.m, (z - self.z0) / self.m)


def polyline_distance(grid, points):
    """Distance in metres from every cell to a polyline, and the 0..1 position along it of the nearest point."""
    best = np.full(grid.X.shape, np.inf)
    along = np.zeros(grid.X.shape)
    lengths = [math.dist(points[i], points[i + 1]) for i in range(len(points) - 1)]
    total = max(sum(lengths), 1e-6)
    walked = 0.0
    for (ax, az), (bx, bz), length in zip(points, points[1:], lengths):
        dx, dz = bx - ax, bz - az
        t = np.clip(((grid.X - ax) * dx + (grid.Z - az) * dz) / max(dx * dx + dz * dz, 1e-6), 0, 1)
        d = np.hypot(grid.X - (ax + t * dx), grid.Z - (az + t * dz))
        closer = d < best
        best = np.where(closer, d, best)
        along = np.where(closer, (walked + t * length) / total, along)
        walked += length
    return best, along


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)


def noise(grid, seed, sigma_metres, octaves=4):
    """Smooth value noise from blurred random fields, about -1..1."""
    rng = np.random.default_rng(seed)
    total = np.zeros(grid.X.shape)
    amplitude, sigma, norm = 1.0, sigma_metres, 0.0
    for _ in range(octaves):
        field = ndimage.gaussian_filter(rng.standard_normal(grid.X.shape), sigma / grid.m)
        field /= max(np.abs(field).max(), 1e-6)
        total += field * amplitude
        norm += amplitude
        amplitude *= 0.5
        sigma *= 0.5
    return total / norm


def land_mask(grid, coast):
    image = Image.new("L", (grid.w, grid.h), 0)
    ImageDraw.Draw(image).polygon([grid.cell(x, z) for x, z in coast], fill=255)
    return np.array(image) > 127


def heights(layout, grid):
    """The height of the land in metres above the sea."""
    h = 55 + 22 * noise(grid, 7, 260)
    for plateau in layout["plateaus"]:
        (cx, cz), (rx, rz) = plateau["centre"], plateau["radius"]
        r = np.hypot((grid.X - cx) / rx, (grid.Z - cz) / rz)
        h += plateau["height"] * smoothstep(1.15, 0.75, r) * (0.92 + 0.08 * noise(grid, 11, 90, 3))
    for basin in layout["basins"]:
        (cx, cz), (rx, rz) = basin["centre"], basin["radius"]
        r = np.hypot((grid.X - cx) / rx, (grid.Z - cz) / rz)
        h -= basin["depth"] * smoothstep(1.1, 0.4, r)
    rough = noise(grid, 23, 70, 4)
    for index, ridge in enumerate(layout["ranges"]):
        if "ring" in ridge:
            cx, cz = ridge["ring"]
            d = np.abs(np.hypot(grid.X - cx, grid.Z - cz) - ridge["radius"])
        else:
            d, _ = polyline_distance(grid, ridge["points"])
        half = ridge["width"] / 2
        crest = ridge["height"] * np.exp(-(d / half) ** 2) * (0.78 + 0.35 * np.abs(rough))
        for px, pz in ridge.get("passes", []):
            crest *= 1 - 0.82 * np.exp(-((np.hypot(grid.X - px, grid.Z - pz)) / (half * 0.9)) ** 2)
        h += crest
    land = land_mask(grid, layout["coast"])
    to_sea = ndimage.distance_transform_edt(land) * grid.m
    to_land = ndimage.distance_transform_edt(~land) * grid.m
    h = np.where(land, h * smoothstep(0, 220, to_sea) + 2, -4 - to_land * 0.12)
    for river in layout["rivers"]:
        d, along = polyline_distance(grid, river["points"])
        w = river["width"]
        bed = np.maximum(1.5, h * 0.25)
        h = np.where(land, h - (h - bed) * np.exp(-(d / (w * 2.2)) ** 2) * 0.85, h)
        h = np.where((d < w * 0.5) & land, np.minimum(h, 1.0), h)
    for lake in layout["lakes"]:
        cx, cz = lake["centre"]
        d = np.hypot(grid.X - cx, grid.Z - cz)
        # A lake lies in a hollow: the ground sinks towards its shore.
        h = h - 10 * smoothstep(lake["radius"] * 1.6, lake["radius"] * 0.6, d)
    return h, land


def water(layout, grid, h, land):
    wet = ~land
    for river in layout["rivers"]:
        d, _ = polyline_distance(grid, river["points"])
        wet |= d < river["width"] * 0.5
    for lake in layout["lakes"]:
        cx, cz = lake["centre"]
        wet |= np.hypot(grid.X - cx, grid.Z - cz) < lake["radius"]
    return wet


def hillshade(h, metres, azimuth=315, altitude=45):
    dz, dx = np.gradient(h, metres)
    az, alt = math.radians(azimuth), math.radians(altitude)
    slope = np.arctan(np.hypot(dx, dz) * 1.6)
    aspect = np.arctan2(-dx, dz)
    shade = np.sin(alt) * np.cos(slope) + np.cos(alt) * np.sin(slope) * np.cos(az - aspect)
    return np.clip(shade, 0, 1)


def ramp(values, stops):
    """Colours along a list of (value, hex) stops."""
    keys = np.array([s[0] for s in stops], float)
    cols = np.array([[int(s[1][i:i + 2], 16) for i in (1, 3, 5)] for s in stops], float)
    out = np.zeros(values.shape + (3,))
    for c in range(3):
        out[..., c] = np.interp(values, keys, cols[:, c])
    return out


LAND_STOPS = [(0, "#b8c98a"), (20, "#9cc27a"), (60, "#7ab85a"), (110, "#8fb05a"), (180, "#b4a86a"), (280, "#a68a64"),
              (450, "#8c7a6c"), (650, "#8e8c90"), (900, "#c9c9cf"), (1200, "#f2f2f5")]
SEA_STOPS = [(-120, "#16324e"), (-40, "#234d73"), (-6, "#3a78a3"), (0, "#5b9cc2")]


def terrain_colours(h, wet, blight, shade):
    land = ramp(h, LAND_STOPS)
    sea = ramp(np.minimum(h, 0), SEA_STOPS)
    colour = np.where(wet[..., None], sea, land)
    # The Blight: grey and dead past the Ridges.
    grey = colour.mean(axis=2, keepdims=True) * np.array([0.92, 0.88, 0.86])
    colour = colour * (1 - blight[..., None]) + grey * blight[..., None]
    light = 0.55 + 0.6 * shade
    colour = np.where(wet[..., None], colour * (0.92 + 0.08 * shade[..., None]), colour * light[..., None])
    return np.clip(colour, 0, 255).astype(np.uint8)


def blight_mask(grid):
    return smoothstep(-1380, -1560, grid.Z) * 0.85


def draw_label(draw, xy, text, size, fill, outline=(20, 18, 28), weight=800, anchor="mm", stroke=4):
    draw.text(xy, text, font=font(size, weight), fill=fill, anchor=anchor, stroke_width=stroke, stroke_fill=outline)


def render(layout, scale_metres):
    grid = Grid(layout["bounds"], scale_metres)
    h, land = heights(layout, grid)
    wet = water(layout, grid, h, land)
    shade = hillshade(h, grid.m)
    blight = blight_mask(grid) * land
    OUT.mkdir(parents=True, exist_ok=True)
    zoom = 2
    size = (grid.w * zoom, grid.h * zoom)

    def px(x, z):
        cx, cz = grid.cell(x, z)
        return (cx * zoom, cz * zoom)

    # ---------- Elevation map ----------
    colours = terrain_colours(h, wet, np.zeros_like(blight), shade)
    elevation = Image.fromarray(colours).resize(size, Image.NEAREST)
    draw = ImageDraw.Draw(elevation)
    contours = np.zeros(h.shape, bool)
    for level in range(50, 1300, 50):
        above = h >= level
        edge = above ^ ndimage.binary_erosion(above)
        contours |= edge & land
    major = np.zeros(h.shape, bool)
    for level in range(200, 1300, 200):
        above = h >= level
        major |= (above ^ ndimage.binary_erosion(above)) & land
    lines = Image.fromarray(np.where(major, 150, np.where(contours, 70, 0)).astype(np.uint8)).resize(size, Image.NEAREST)
    elevation = Image.composite(Image.new("RGB", size, (40, 32, 28)), elevation, lines)
    draw = ImageDraw.Draw(elevation)
    for ridge in layout["ranges"]:
        if "points" in ridge:
            mid = ridge["points"][len(ridge["points"]) // 2]
            draw_label(draw, px(*mid), f"{ridge['name']}  ~{ridge['height']} m", 22, (250, 246, 236))
        for p in ridge.get("passes", []):
            if "points" in ridge:
                draw_label(draw, px(*p), "pass", 15, (255, 230, 150), weight=700)
    for plateau in layout["plateaus"]:
        draw_label(draw, px(*plateau["centre"]), f"{plateau['name']}  ~{plateau['height'] + 55} m", 22, (250, 246, 236))
    for lake in layout["lakes"]:
        draw_label(draw, px(*lake["centre"]), lake["name"], 15, (225, 240, 255), weight=700)
    for river in layout["rivers"]:
        p = river["points"][len(river["points"]) // 2]
        draw_label(draw, px(p[0] + 60, p[1]), river["name"], 15, (200, 230, 255), weight=700, anchor="lm")
    draw_label(draw, (size[0] // 2, 40), "Velmora: elevation (plan, 2026-11-10)", 34, (255, 255, 255))
    legend_stops = [0, 60, 180, 450, 900, 1200]
    for i, value in enumerate(legend_stops):
        colour = tuple(int(c) for c in ramp(np.array([value], float), LAND_STOPS)[0])
        y = size[1] - 60 - (len(legend_stops) - 1 - i) * 36
        draw.rectangle([30, y, 70, y + 28], fill=colour, outline=(20, 20, 20))
        draw_label(draw, (82, y + 14), f"{value} m", 18, (255, 255, 255), anchor="lm", weight=700)
    scale_bar(draw, size, grid, zoom)
    elevation.save(OUT / "elevation.png")

    # ---------- World map ----------
    colours = terrain_colours(h, wet, blight, shade)
    base = Image.fromarray(colours).resize(size, Image.BILINEAR)
    overlay = Image.new("RGBA", size, (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    for region in layout["regions"]:
        (cx, cz), (rx, rz) = region["centre"], region["radius"]
        a, b = px(cx - rx, cz - rz), px(cx + rx, cz + rz)
        colour = tuple(int(region["colour"][i:i + 2], 16) for i in (1, 3, 5))
        od.ellipse([a, b], fill=colour + (34,), outline=colour + (170,), width=3)
    for forest in layout.get("forests", []):
        (cx, cz), (rx, rz) = forest["centre"], forest["radius"]
        od.ellipse([px(cx - rx, cz - rz), px(cx + rx, cz + rz)], fill=(30, 80, 40, 60))
    base = Image.alpha_composite(base.convert("RGBA"), overlay)
    draw = ImageDraw.Draw(base)

    for underground in layout["underground"]:
        dashed(draw, [px(*p) for p in underground["points"]], (170, 110, 220), 4, 14, 10)
    for crossing in layout["crossings"]:
        dashed(draw, [px(*p) for p in crossing["points"]], (230, 245, 255), 3, 6, 9)
    styles = {"highway": ((92, 58, 34), (226, 196, 130), 9), "road": ((92, 58, 34), (214, 180, 120), 6), "trail": (None, (200, 165, 110), 3)}
    for road in layout["roads"]:
        edge, fill, width = styles[road["kind"]]
        points = [px(*p) for p in road["points"]]
        if road["kind"] == "trail":
            dashed(draw, points, fill, width, 10, 7)
            continue
        draw.line(points, fill=edge, width=width + 4, joint="curve")
        draw.line(points, fill=fill, width=width, joint="curve")

    # The zone that exists today; its insides are drawn larger in the inset.
    a, b = px(-300, -300), px(300, 300)
    valley = base.crop((int(a[0]) - 30, int(a[1]) - 30, int(b[0]) + 30, int(b[1]) + 30))
    draw.rectangle([a, b], outline=(255, 255, 255), width=3)
    draw_label(draw, (a[0] + 6, a[1] - 12), "built: Meadow Valley zone (600 m), see the panel", 15, (255, 255, 255), anchor="lm", weight=700)

    def outside(at):
        return abs(at[0]) > 300 or abs(at[1]) > 300

    for camp in [c for c in layout["camps"] if outside(c["at"])]:
        x, y = px(*camp["at"])
        draw.polygon([(x, y - 12), (x + 11, y + 8), (x - 11, y + 8)], fill=(200, 60, 50), outline=(30, 10, 10))
        draw_label(draw, (x, y + 22), camp["name"], 14, (255, 210, 200), weight=700)
    for dungeon in [d for d in layout["dungeons"] if outside(d["at"])]:
        x, y = px(*dungeon["at"])
        draw.ellipse([x - 11, y - 11, x + 11, y + 11], fill=(40, 30, 50), outline=(220, 120, 255), width=3)
        draw.rectangle([x - 4, y - 5, x + 4, y + 6], fill=(220, 120, 255))
        draw_label(draw, (x + 16, y), f"{dungeon['name']} ({dungeon['level']})", 14, (240, 210, 255), anchor="lm", weight=700)
    for landmark in [m for m in layout["landmarks"] if outside(m["at"])]:
        x, y = px(*landmark["at"])
        star(draw, x, y, 10, (255, 220, 90))
        draw_label(draw, (x, y - 18), landmark["name"], 13, (255, 245, 210), weight=700)
    for x, z, name in [w for w in layout["waystones"] if outside(w[:2])]:
        x, y = px(x, z)
        draw.polygon([(x + 16, y - 16), (x + 25, y - 7), (x + 16, y + 2), (x + 7, y - 7)], fill=(44, 232, 245), outline=(10, 40, 60))
    sizes = {"capital": 22, "town": 16, "port": 16, "village": 11, "fort": 12, "hamlet": 8}
    texts = {"capital": 30, "town": 24, "port": 24, "village": 19, "fort": 18, "hamlet": 16}
    for town in [s for s in layout["settlements"] if outside(s["at"])]:
        x, y = px(*town["at"])
        r = sizes[town["kind"]]
        if town["kind"] == "fort":
            draw.rectangle([x - r, y - r, x + r, y + r], fill=(120, 120, 135), outline=(20, 20, 25), width=3)
        else:
            draw.ellipse([x - r, y - r, x + r, y + r], fill=(250, 236, 200), outline=(40, 28, 20), width=4)
            if town["kind"] == "capital":
                draw.ellipse([x - 8, y - 8, x + 8, y + 8], fill=(200, 60, 60))
            if town["kind"] == "port":
                draw.line([(x, y - 8), (x, y + 8)], fill=(40, 28, 20), width=3)
                draw.arc([x - 8, y - 2, x + 8, y + 12], 0, 180, fill=(40, 28, 20), width=3)
        draw_label(draw, (x, y - r - 16), town["name"], texts[town["kind"]], (255, 250, 235))

    for region in layout["regions"]:
        cx, cz = region["centre"]
        rz = region["radius"][1]
        x, y = px(*region["label"]) if "label" in region else px(cx, cz - rz * 0.62)
        draw_label(draw, (x, y), region["name"].upper(), 26, (255, 244, 200), weight=900, stroke=5)
        draw_label(draw, (x, y + 26), f"level {region['level']}", 17, (255, 230, 160), weight=700)

    for sea in layout.get("seas", []):
        draw_label(draw, px(*sea["at"]), sea["name"], 30, (210, 230, 250), weight=700, outline=(20, 40, 70))

    draw_label(draw, (size[0] // 2, 40), "Velmora: the Eastern March and Aldermark (world plan, 2026-11-10)", 34, (255, 255, 255))
    scale_bar(draw, size, grid, zoom)
    compass(draw, size)
    # A panel at the side: Meadow Valley drawn larger, and the legend.
    panel = 640
    sheet = Image.new("RGBA", (size[0] + panel, size[1]), (24, 22, 32, 255))
    sheet.paste(base, (0, 0))
    sheet_draw = ImageDraw.Draw(sheet)
    inset(sheet, sheet_draw, layout, valley, (size[0] + panel, size[1]))
    legend(sheet_draw, (size[0] + panel, size[1]), x=size[0] + 40)
    sheet.convert("RGB").save(OUT / "world-map.png")
    print("wrote", OUT / "world-map.png", "and", OUT / "elevation.png", f"({size[0]}x{size[1]})",
          f"heights {h[land].min():.0f}..{h.max():.0f} m")


def inset(base, draw, layout, valley, size):
    """Meadow Valley, the zone that exists, drawn larger in the sea at the bottom right, with what lies in it."""
    zoom = 1.55
    picture = valley.resize((int(valley.width * zoom), int(valley.height * zoom)), Image.BILINEAR)
    x0, y0 = size[0] - picture.width - 40, 130
    base.paste(picture, (x0, y0))
    draw.rectangle([x0, y0, x0 + picture.width, y0 + picture.height], outline=(255, 255, 255), width=4)
    draw_label(draw, (x0 + picture.width / 2, y0 - 18), "MEADOW VALLEY (built)", 22, (255, 244, 200), weight=900)
    # The crop starts 30 px before the zone's corner (-300, -300); the main map has 2 px for every 4 m.
    def at(x, z):
        return (x0 + (30 + (x + 300) / 2) * zoom, y0 + (30 + (z + 300) / 2) * zoom)

    for town in [s for s in layout["settlements"] if abs(s["at"][0]) <= 300 and abs(s["at"][1]) <= 300]:
        x, y = at(*town["at"])
        draw.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(250, 236, 200), outline=(40, 28, 20), width=3)
        draw_label(draw, (x, y - 22), town["name"], 18, (255, 250, 235))
    for dungeon in [d for d in layout["dungeons"] if abs(d["at"][0]) <= 300 and abs(d["at"][1]) <= 300]:
        x, y = at(*dungeon["at"])
        draw.ellipse([x - 9, y - 9, x + 9, y + 9], fill=(40, 30, 50), outline=(220, 120, 255), width=3)
        draw_label(draw, (x + 13, y + 1), f"{dungeon['name']} ({dungeon['level']})", 14, (240, 210, 255), anchor="lm", weight=700)
    for landmark in [m for m in layout["landmarks"] if abs(m["at"][0]) <= 300 and abs(m["at"][1]) <= 300]:
        x, y = at(*landmark["at"])
        star(draw, x, y, 8, (255, 220, 90))
        draw_label(draw, (x - 12, y), landmark["name"], 13, (255, 245, 210), anchor="rm", weight=700)
    for wx, wz, _ in [w for w in layout["waystones"] if abs(w[0]) <= 300 and abs(w[1]) <= 300]:
        x, y = at(wx, wz)
        draw.polygon([(x + 12, y - 16), (x + 20, y - 8), (x + 12, y), (x + 4, y - 8)], fill=(44, 232, 245), outline=(10, 40, 60))
    for name, (rx, rz) in {"Greyfang": (205, 195), "Moonfell": (-198, 206), "Ironjaw Outpost": (212, -132), "Westwood": (-230, 0),
                           "Whispering Woods": (-96, 46), "Slime Fields": (-8, -46), "Goblin Hollow": (84, -110), "Emerald Jungle": (-96, -112)}.items():
        draw_label(draw, at(rx, rz), name, 13, (220, 235, 210), weight=700, stroke=3)


def dashed(draw, points, fill, width, dash, gap):
    for (ax, ay), (bx, by) in zip(points, points[1:]):
        length = math.dist((ax, ay), (bx, by))
        steps = max(1, int(length // (dash + gap)))
        for i in range(steps + 1):
            t0 = i * (dash + gap) / max(length, 1e-6)
            t1 = min(1, (i * (dash + gap) + dash) / max(length, 1e-6))
            if t0 >= 1:
                break
            draw.line([(ax + (bx - ax) * t0, ay + (by - ay) * t0), (ax + (bx - ax) * t1, ay + (by - ay) * t1)], fill=fill, width=width)


def star(draw, x, y, r, fill):
    points = []
    for i in range(10):
        angle = math.pi / 2 + i * math.pi / 5
        radius = r if i % 2 == 0 else r * 0.45
        points.append((x + math.cos(angle) * radius, y - math.sin(angle) * radius))
    draw.polygon(points, fill=fill, outline=(60, 40, 10))


def scale_bar(draw, size, grid, zoom):
    metres = 500
    pixels = metres / grid.m * zoom
    x, y = size[0] - 60 - pixels, size[1] - 50
    draw.rectangle([x, y, x + pixels, y + 10], fill=(255, 255, 255), outline=(20, 20, 20), width=2)
    draw.rectangle([x, y, x + pixels / 2, y + 10], fill=(30, 30, 30))
    draw_label(draw, (x + pixels / 2, y - 16), f"{metres} m", 18, (255, 255, 255), weight=700)


def compass(draw, size):
    x, y = size[0] - 80, 120
    draw.polygon([(x, y - 40), (x + 12, y), (x, y - 8), (x - 12, y)], fill=(230, 60, 60), outline=(20, 20, 20))
    draw.polygon([(x, y + 40), (x + 12, y), (x, y + 8), (x - 12, y)], fill=(240, 240, 240), outline=(20, 20, 20))
    draw_label(draw, (x, y - 56), "N", 24, (255, 255, 255))


def legend(draw, size, x=30):
    y = size[1] - 360
    draw.rectangle([x - 10, y - 16, x + 330, y + 296], fill=(20, 18, 28), outline=(200, 190, 160), width=2)
    rows = [
        ("capital, town, port", lambda cx, cy: draw.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=(250, 236, 200), outline=(40, 28, 20), width=3)),
        ("village, hamlet", lambda cx, cy: draw.ellipse([cx - 7, cy - 7, cx + 7, cy + 7], fill=(250, 236, 200), outline=(40, 28, 20), width=3)),
        ("fort", lambda cx, cy: draw.rectangle([cx - 9, cy - 9, cx + 9, cy + 9], fill=(120, 120, 135), outline=(20, 20, 25), width=3)),
        ("dungeon (level)", lambda cx, cy: draw.ellipse([cx - 10, cy - 10, cx + 10, cy + 10], fill=(40, 30, 50), outline=(220, 120, 255), width=3)),
        ("waystone (fast travel)", lambda cx, cy: draw.polygon([(cx, cy - 10), (cx + 10, cy), (cx, cy + 10), (cx - 10, cy)], fill=(44, 232, 245))),
        ("landmark", lambda cx, cy: star(draw, cx, cy, 10, (255, 220, 90))),
        ("enemy camp", lambda cx, cy: draw.polygon([(cx, cy - 11), (cx + 10, cy + 8), (cx - 10, cy + 8)], fill=(200, 60, 50))),
        ("highway, road, trail", lambda cx, cy: draw.line([(cx - 14, cy), (cx + 14, cy)], fill=(226, 196, 130), width=7)),
        ("boat route", lambda cx, cy: dashed(draw, [(cx - 14, cy), (cx + 14, cy)], (230, 245, 255), 3, 5, 5)),
        ("underground way", lambda cx, cy: dashed(draw, [(cx - 14, cy), (cx + 14, cy)], (170, 110, 220), 4, 7, 5)),
    ]
    for i, (label, icon) in enumerate(rows):
        cy = y + 12 + i * 28
        icon(x + 16, cy)
        draw_label(draw, (x + 42, cy), label, 16, (240, 236, 220), anchor="lm", weight=700, stroke=0)


def main():
    scale = float(sys.argv[sys.argv.index("--scale") + 1]) if "--scale" in sys.argv else 4.0
    render(json.loads(LAYOUT.read_text(encoding="utf-8")), scale)


if __name__ == "__main__":
    main()
