"""Shared helpers for loading sprites and palettes."""
from __future__ import annotations

import json
import os
import re
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageSequence

ROOT = Path(__file__).resolve().parent.parent
PALETTE_DIR = ROOT / "palettes"
ASEPRITE = os.environ.get("ASEPRITE_EXE", r"C:\Users\promi\AppData\Local\Aseprite\Aseprite.exe")
GPL_LINE = re.compile(r"^\s*(\d+)\s+(\d+)\s+(\d+)")


@dataclass
class Frame:
    pixels: np.ndarray  # (height, width, 4) uint8 RGBA
    duration_ms: int = 100


def load_frames(path: str | Path) -> list[Frame]:
    """Load every frame of a PNG, GIF or Aseprite file as RGBA."""
    path = Path(path)
    if path.suffix.lower() in (".aseprite", ".ase"):
        return _load_aseprite(path)
    with Image.open(path) as image:
        return [
            Frame(np.array(f.convert("RGBA")), int(f.info.get("duration", 100) or 100))
            for f in ImageSequence.Iterator(image)
        ]


def _load_aseprite(path: Path) -> list[Frame]:
    with tempfile.TemporaryDirectory() as tmp:
        sheet, data = Path(tmp, "sheet.png"), Path(tmp, "sheet.json")
        subprocess.run(
            [ASEPRITE, "--batch", str(path), "--sheet", str(sheet), "--data", str(data),
             "--format", "json-array"],
            check=True, capture_output=True, timeout=120,
        )
        meta = json.loads(data.read_text(encoding="utf-8"))
        with Image.open(sheet) as image:
            atlas = np.array(image.convert("RGBA"))
    frames = []
    for item in meta["frames"]:
        r = item["frame"]
        crop = atlas[r["y"]:r["y"] + r["h"], r["x"]:r["x"] + r["w"]].copy()
        frames.append(Frame(crop, int(item["duration"])))
    return frames


def resolve_palette(name: str) -> Path:
    """Accept a file path or a palette name from palettes/."""
    path = Path(name)
    if path.is_file():
        return path
    candidate = PALETTE_DIR / f"{name}.gpl"
    if candidate.is_file():
        return candidate
    known = ", ".join(sorted(p.stem for p in PALETTE_DIR.glob("*.gpl")))
    raise SystemExit(f"Unknown palette '{name}'. Known palettes: {known}")


def load_palette(name: str) -> list[tuple[int, int, int]]:
    """Read the colours of a GIMP .gpl palette."""
    colours = []
    for line in resolve_palette(name).read_text(encoding="utf-8").splitlines():
        match = GPL_LINE.match(line)
        if match:
            colours.append(tuple(int(v) for v in match.groups()))
    return colours


def to_hex(rgb) -> str:
    return "#{:02X}{:02X}{:02X}".format(*(int(v) for v in rgb[:3]))


def from_hex(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def luminance(rgb) -> float:
    """Perceived brightness from 0 to 255 (Rec. 601)."""
    return 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]
