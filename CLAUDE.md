# PixelCreate

Workspace for pixel art and experiments around it.

## Layout

- `src/`: `.aseprite` source files. These are the source of truth.
- `export/`: generated PNG, GIF and spritesheets. Rebuild them from `src/`; do not edit them.
- `export/previews/`: review sheets, ignored by git.
- `palettes/`: `.gpl` palettes and material ramps (`ramps-*.json`).
- `refs/`: reference images. Use them for study; do not copy them into finished art.
- `tools/`: Python tooling. `tests/`: tests for it (`python tests/test_tools.py`).

## Which tool to draw with

- Default: Aseprite through the `aseprite` MCP server (`mcp__aseprite__*`). Files stay local.
  Save work from the temp sprite into `src/` with `save_as`.
- Pixelwright: rigged characters, tilesets, or when the user wants to watch the drawing live.
  Export finished pieces in Aseprite format into `src/`.

The `aseprite` server is defined in `.mcp.json` and starts `pixel-mcp.exe` directly. The
pixel-plugin launcher can fail at session start because bash strips the backslashes from its
Windows path. Keep the plugin installed for its skills.

## Workflow for every sprite

1. Pick a palette from `palettes/` and use its ramps. Do not invent colours.
2. Draw in stages: silhouette, flat colours, shading, then details.
3. After each stage run `python tools/preview.py <file>` and read the PNG it writes. Judge the
   enlarged view, value view, silhouette and 1x view.
4. Finish with `python tools/lint.py <file> --palette <name>` and fix every error.

## Pixel art rules

- Whole pixels only. No semi-transparency and no blur.
- One light source for a piece. Default: top left.
- Use 3 to 5 shades per material, with hue shifted across the ramp.
- Avoid pillow shading, jaggies, and banding.
- Outline with the palette's darkest colour. Inner lines use the darkest step of the neighbour.
- Scale only by whole numbers with nearest neighbour.
