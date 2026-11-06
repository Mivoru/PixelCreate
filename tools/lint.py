#!/usr/bin/env python3
"""Check sprites against pixel-art rules.

Usage: python tools/lint.py src/hero.aseprite [more files] [--palette endesga-32] [--max-colors 16]

Errors (exit code 1): semi-transparent pixels, colours outside the palette, too many
colours. Warnings do not fail: stray pixels, near-duplicate colours, possible upscaling.
"""
from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass, field

import numpy as np

from pixlib import Frame, load_frames, load_palette, to_hex

NEAR_DUPLICATE_DISTANCE = 12
MAX_LISTED = 8


@dataclass
class Report:
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)


def used_colours(frames: list[Frame]) -> np.ndarray:
    opaque = [f.pixels[f.pixels[..., 3] > 0][:, :3] for f in frames]
    joined = np.concatenate(opaque) if opaque else np.empty((0, 3), np.uint8)
    return np.unique(joined, axis=0)


def stray_pixels(alpha: np.ndarray) -> list[tuple[int, int]]:
    """Opaque pixels with no opaque pixel among their 8 neighbours."""
    solid = np.pad(alpha > 0, 1)
    neighbours = sum(
        np.roll(np.roll(solid, dy, 0), dx, 1).astype(np.uint8)
        for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx
    )
    ys, xs = np.nonzero(solid & (neighbours == 0))
    return [(int(x) - 1, int(y) - 1) for x, y in zip(xs, ys)]


def upscale_factor(pixels: np.ndarray) -> int:
    """Largest block size that every pixel of the image repeats in, or 1."""
    height, width = pixels.shape[:2]
    for factor in range(min(height, width), 1, -1):
        if height % factor or width % factor:
            continue
        small = pixels[::factor, ::factor]
        if np.array_equal(np.repeat(np.repeat(small, factor, 0), factor, 1), pixels):
            return factor
    return 1


def near_duplicates(colours: np.ndarray) -> list[tuple[str, str]]:
    values = colours.astype(int)
    pairs = []
    for i in range(len(values)):
        for j in range(i + 1, len(values)):
            if np.abs(values[i] - values[j]).sum() <= NEAR_DUPLICATE_DISTANCE:
                pairs.append((to_hex(values[i]), to_hex(values[j])))
    return pairs


def listed(items: list) -> str:
    extra = len(items) - MAX_LISTED
    return ", ".join(str(i) for i in items[:MAX_LISTED]) + (f" and {extra} more" if extra > 0 else "")


def lint(frames: list[Frame], palette: list | None, max_colors: int | None) -> Report:
    report = Report()
    colours = used_colours(frames)

    if len(colours) == 0:
        report.errors.append("sprite is empty")
        return report

    for number, frame in enumerate(frames, 1):
        alpha = frame.pixels[..., 3]
        partial = int(((alpha > 0) & (alpha < 255)).sum())
        if partial:
            report.errors.append(f"frame {number}: {partial} semi-transparent pixels")
        stray = stray_pixels(alpha)
        if stray:
            report.warnings.append(f"frame {number}: stray pixels at {listed(stray)}")

    factor = upscale_factor(frames[0].pixels)
    if factor > 1:
        report.warnings.append(f"art repeats in {factor}x{factor} blocks; check it is not upscaled")

    if max_colors is not None and len(colours) > max_colors:
        report.errors.append(f"{len(colours)} colours, limit is {max_colors}")

    if palette is not None:
        allowed = {tuple(c) for c in palette}
        outside = [to_hex(c) for c in colours if tuple(int(v) for v in c) not in allowed]
        if outside:
            report.errors.append(f"{len(outside)} colours outside palette: {listed(outside)}")
    else:
        pairs = near_duplicates(colours)
        if pairs:
            report.warnings.append(f"near-duplicate colours: {listed(pairs)}")
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("sprites", nargs="+")
    parser.add_argument("--palette")
    parser.add_argument("--max-colors", type=int)
    args = parser.parse_args()

    palette = load_palette(args.palette) if args.palette else None
    failed = False
    for path in args.sprites:
        frames = load_frames(path)
        report = lint(frames, palette, args.max_colors)
        height, width = frames[0].pixels.shape[:2]
        status = "FAIL" if report.errors else "ok"
        print(f"{status} {path}: {width}x{height}, {len(frames)} frames, {len(used_colours(frames))} colours")
        for message in report.errors:
            print(f"  error: {message}")
        for message in report.warnings:
            print(f"  warning: {message}")
        failed = failed or bool(report.errors)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
