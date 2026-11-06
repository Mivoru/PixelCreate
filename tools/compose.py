#!/usr/bin/env python3
"""Walk a sprite across a scene and write an animated GIF.

Usage: python tools/compose.py src/scene.aseprite src/hero.aseprite \
           --start -8,48 --end 106,48 --out export/scene.gif [--step 3] [--hold 12] [--scale 4]

The sprite plays its frames in a loop while it moves --step pixels per frame, then
holds its last frame for --hold frames. Scaling uses whole numbers and nearest neighbour.
"""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

from pixlib import load_frames


def path_points(start: tuple[int, int], end: tuple[int, int], step: int) -> list[tuple[int, int]]:
    """Whole-pixel positions from start to end, always ending exactly on end."""
    dx, dy = end[0] - start[0], end[1] - start[1]
    count = max(1, -(-max(abs(dx), abs(dy)) // step))
    return [(start[0] + dx * i // count, start[1] + dy * i // count) for i in range(count + 1)]


def compose(scene: Image.Image, sprites: list[Image.Image], points, hold: int) -> list[Image.Image]:
    """One picture per position; the sprite cycles, then holds on its last shown frame."""
    frames = []
    for i, point in enumerate(points):
        frame = scene.copy()
        sprite = sprites[i % len(sprites)]
        frame.paste(sprite, point, sprite)  # paste allows a start off the left edge
        frames.append(frame)
    return frames + [frames[-1].copy() for _ in range(hold)]


def to_image(frame) -> Image.Image:
    return Image.fromarray(frame.pixels, "RGBA")


def save_gif(frames: list[Image.Image], out: Path, scale: int, ms: int) -> None:
    size = (frames[0].width * scale, frames[0].height * scale)
    big = [f.convert("RGB").resize(size, Image.NEAREST) for f in frames]
    palette = big[0].quantize(colors=256, dither=Image.Dither.NONE)
    paletted = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in big]
    out.parent.mkdir(parents=True, exist_ok=True)
    paletted[0].save(out, save_all=True, append_images=paletted[1:], duration=ms, loop=0, optimize=False)


def parse_point(text: str) -> tuple[int, int]:
    x, y = text.split(",")
    return int(x), int(y)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("scene")
    parser.add_argument("sprite")
    parser.add_argument("--start", type=parse_point, required=True)
    parser.add_argument("--end", type=parse_point, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--step", type=int, default=3)
    parser.add_argument("--hold", type=int, default=12)
    parser.add_argument("--scale", type=int, default=4)
    parser.add_argument("--ms", type=int, default=120)
    args = parser.parse_args()

    scene = to_image(load_frames(args.scene)[0])
    sprites = [to_image(f) for f in load_frames(args.sprite)]
    frames = compose(scene, sprites, path_points(args.start, args.end, args.step), args.hold)
    save_gif(frames, args.out, args.scale, args.ms)
    print(f"{args.out} ({len(frames)} frames, {frames[0].width * args.scale}x{frames[0].height * args.scale})")


if __name__ == "__main__":
    main()
