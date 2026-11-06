#!/usr/bin/env python3
"""Walk a sprite across a scene and write an animated GIF.

Usage: python tools/compose.py src/scene.aseprite src/hero.aseprite \
           --start -8,48 --end 106,48 --out export/scene.gif [--step 3] [--hold 12] [--scale 4] \
           [--loop 6 --slow 2 --stand 6 --inside 3 --after 8]

The sprite plays its frames in a loop while it moves --step pixels per frame, then stands
for --hold frames. An animated scene cycles --loop frames; see compose() for its alternate
state. Scaling uses whole numbers and nearest neighbour.
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


def compose(scenes: list[Image.Image], sprites: list[Image.Image], points, hold: int = 0,
            loop: int | None = None, slow: int = 1, stand: int = 0, inside: int = 0,
            after: int = 0) -> list[Image.Image]:
    """Build the picture for each moment of the walk.

    The scene plays frames 0..loop-1 in a cycle, each shown for `slow` picture frames. When the
    scene has a second run of `loop` frames, those are its alternate state, such as an open door.
    Order: walk, `hold` standing, `stand` standing in the alternate state, `inside` with the
    sprite gone in the alternate state, then `after` with the sprite gone in the normal state.
    """
    loop = loop or len(scenes)
    alternate = loop if len(scenes) >= 2 * loop else 0
    rest = sprites[len(sprites) // 4]
    frames: list[Image.Image] = []

    def add(offset: int, sprite: Image.Image | None, point=None) -> None:
        frame = scenes[offset + (len(frames) // slow) % loop].copy()
        if sprite is not None:
            frame.paste(sprite, point, sprite)  # paste allows a start off the left edge
        frames.append(frame)

    for i, point in enumerate(points):
        add(0, sprites[i % len(sprites)], point)
    for _ in range(hold):
        add(0, rest, points[-1])
    for _ in range(stand):
        add(alternate, rest, points[-1])
    for _ in range(inside):
        add(alternate, None)
    for _ in range(after):
        add(0, None)
    return frames


def to_image(frame) -> Image.Image:
    return Image.fromarray(frame.pixels, "RGBA")


def save_gif(frames: list[Image.Image], out: Path, scale: int, ms: int) -> None:
    size = (frames[0].width * scale, frames[0].height * scale)
    big = [f.convert("RGB").resize(size, Image.NEAREST) for f in frames]
    # The palette must come from every frame: a sprite can be off screen in the first one.
    sheet = Image.new("RGB", (frames[0].width, frames[0].height * len(frames)))
    for i, frame in enumerate(frames):
        sheet.paste(frame.convert("RGB"), (0, i * frame.height))
    palette = sheet.quantize(colors=256, dither=Image.Dither.NONE)
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
    parser.add_argument("--ms", type=int, default=100)
    parser.add_argument("--loop", type=int, help="scene frames in one cycle; default all of them")
    parser.add_argument("--slow", type=int, default=1, help="picture frames per scene frame")
    parser.add_argument("--stand", type=int, default=0)
    parser.add_argument("--inside", type=int, default=0)
    parser.add_argument("--after", type=int, default=0)
    args = parser.parse_args()

    scenes = [to_image(f) for f in load_frames(args.scene)]
    sprites = [to_image(f) for f in load_frames(args.sprite)]
    frames = compose(scenes, sprites, path_points(args.start, args.end, args.step), args.hold,
                     args.loop, args.slow, args.stand, args.inside, args.after)
    save_gif(frames, args.out, args.scale, args.ms)
    print(f"{args.out} ({len(frames)} frames, {frames[0].width * args.scale}x{frames[0].height * args.scale})")


if __name__ == "__main__":
    main()
