"""Join studio pictures into labelled contact sheets, so a review reads few images.

Usage: python tools/sheet.py <folder>
Pictures are grouped by the first part of their name, or the first three parts for motion strips.
Sheets are written to <folder>/sheets as <group>-<page>.png.
"""
import re
import sys
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageDraw

COLUMNS = {"person": 5, "motion": 8, "model": 6, "world": 3, "box": 3, "map": 2, "window": 4}
TILE_WIDTH = {"person": 300, "motion": 200, "model": 260, "world": 520, "box": 520, "map": 800, "window": 400}
ROWS_PER_PAGE = {"person": 4, "motion": 8, "model": 7, "world": 4, "box": 4, "map": 1, "window": 6}
CROP = {"person": 0.5, "motion": 0.5}


def group_of(name):
    parts = name.replace("-", "_").split("_")
    return "_".join(parts[:3]) if parts[0] == "motion" else parts[0]


def tile(path, kind):
    image = Image.open(path).convert("RGB")
    if kind in CROP:
        width = int(image.width * CROP[kind])
        left = (image.width - width) // 2
        image = image.crop((left, 0, left + width, image.height))
    target = TILE_WIDTH[kind]
    return image.resize((target, image.height * target // image.width), Image.NEAREST)


def main():
    folder = Path(sys.argv[1])
    sheets = folder / "sheets"
    sheets.mkdir(exist_ok=True)
    for old in sheets.glob("*.png"):
        old.unlink()
    groups = defaultdict(list)
    for path in sorted(folder.glob("*.png"), key=lambda p: [int(t) if t.isdigit() else t for t in re.split(r"(\d+)", p.stem)]):
        groups[group_of(path.stem)].append(path)
    for group, paths in groups.items():
        kind = group.split("_")[0]
        columns = COLUMNS[kind]
        per_page = columns * ROWS_PER_PAGE[kind]
        for page, start in enumerate(range(0, len(paths), per_page)):
            tiles = [(path.stem, tile(path, kind)) for path in paths[start:start + per_page]]
            width, height = tiles[0][1].size
            label = 13
            rows = (len(tiles) + columns - 1) // columns
            sheet = Image.new("RGB", (columns * width, rows * (height + label)), "#111118")
            draw = ImageDraw.Draw(sheet)
            for index, (name, image) in enumerate(tiles):
                x, y = index % columns * width, index // columns * (height + label)
                sheet.paste(image, (x, y + label))
                draw.text((x + 2, y + 1), name.replace(group + "_", "", 1), fill="#ffe680")
            sheet.save(sheets / f"{group}-{page}.png")
    print(f"{len(list(sheets.glob('*.png')))} sheets in {sheets}")


if __name__ == "__main__":
    main()
