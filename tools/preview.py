#!/usr/bin/env python3
"""Build a review sheet for a sprite: every frame enlarged, plus value and silhouette views.

Usage: python tools/preview.py src/hero.aseprite [--out x.png] [--scale 8] [--bg checker|#RRGGBB]
The default output is export/previews/<name>.png.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from pixlib import ROOT, Frame, from_hex, load_frames

GAP = 8
LABEL_HEIGHT = 12
SHEET_BG = (38, 38, 46, 255)
LABEL = (225, 225, 230, 255)
TARGET_SIZE = 256
MAX_COLUMNS = 8


def auto_scale(width: int, height: int) -> int:
    return max(1, min(16, TARGET_SIZE // max(width, height, 1)))


def backdrop(width: int, height: int, scale: int, bg: str) -> Image.Image:
    if bg != "checker":
        return Image.new("RGBA", (width * scale, height * scale), (*from_hex(bg), 255))
    ys, xs = np.indices((height * scale, width * scale))
    cell = max(scale, 4)
    odd = ((xs // cell + ys // cell) % 2).astype(bool)
    pixels = np.empty((*odd.shape, 4), np.uint8)
    pixels[...] = (104, 104, 112, 255)
    pixels[odd] = (136, 136, 144, 255)
    return Image.fromarray(pixels, "RGBA")


def enlarge(pixels: np.ndarray, scale: int, bg: str) -> Image.Image:
    height, width = pixels.shape[:2]
    sprite = Image.fromarray(pixels, "RGBA").resize((width * scale, height * scale), Image.NEAREST)
    base = backdrop(width, height, scale, bg)
    base.alpha_composite(sprite)
    return base


def value_view(pixels: np.ndarray) -> np.ndarray:
    grey = (pixels[..., :3] @ np.array([0.299, 0.587, 0.114])).astype(np.uint8)
    return np.dstack([grey, grey, grey, pixels[..., 3]])


def silhouette_view(pixels: np.ndarray) -> np.ndarray:
    out = np.zeros_like(pixels)
    out[..., 3] = np.where(pixels[..., 3] > 0, 255, 0)
    return out


def build_sheet(frames: list[Frame], scale: int, bg: str) -> Image.Image:
    panels = [(f"f{i + 1} {f.duration_ms}ms", enlarge(f.pixels, scale, bg)) for i, f in enumerate(frames)]
    first = frames[0].pixels
    panels.append(("value", enlarge(value_view(first), scale, bg)))
    panels.append(("silhouette", enlarge(silhouette_view(first), scale, "#E8E8E8")))
    panels.append(("1x", enlarge(first, 1, bg)))

    cell_w = max(p.width for _, p in panels)
    cell_h = max(p.height for _, p in panels) + LABEL_HEIGHT
    columns = min(MAX_COLUMNS, len(panels))
    rows = -(-len(panels) // columns)
    sheet = Image.new("RGBA", (columns * (cell_w + GAP) + GAP, rows * (cell_h + GAP) + GAP), SHEET_BG)
    draw = ImageDraw.Draw(sheet)
    for index, (label, panel) in enumerate(panels):
        x = GAP + (index % columns) * (cell_w + GAP)
        y = GAP + (index // columns) * (cell_h + GAP)
        draw.text((x, y), label, fill=LABEL)
        sheet.paste(panel, (x, y + LABEL_HEIGHT))
    return sheet


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("sprite")
    parser.add_argument("--out")
    parser.add_argument("--scale", type=int)
    parser.add_argument("--bg", default="checker")
    args = parser.parse_args()

    frames = load_frames(args.sprite)
    height, width = frames[0].pixels.shape[:2]
    out = Path(args.out) if args.out else ROOT / "export" / "previews" / f"{Path(args.sprite).stem}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    build_sheet(frames, args.scale or auto_scale(width, height), args.bg).save(out)
    print(f"{out} ({len(frames)} frames, {width}x{height})")


if __name__ == "__main__":
    main()
