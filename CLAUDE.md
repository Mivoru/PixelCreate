# PixelCreate

Workspace for pixel art and experiments around it.

## Layout

- `src/`: `.aseprite` source files. These are the source of truth.
- `export/`: generated PNG, GIF and spritesheets. Rebuild them from `src/`; do not edit them.
- `export/previews/`: review sheets, ignored by git.
- `palettes/`: `.gpl` palettes and material ramps (`ramps-*.json`).
- `refs/`: reference images. Use them for study; do not copy them into finished art.
- `tools/`: Python tooling (`compose.py` walks a sprite across a scene into a GIF). `tests/`: tests for it (`python tests/test_tools.py`).

## Which tool to draw with

- Default: Pixelwright, for sprites, characters, tilesets, scenes and animation. It writes pixels
  as text rows and audits the result. Work happens in the cloud, so export every finished piece
  in Aseprite format into `src/`.
- Aseprite through the `aseprite` MCP server (`mcp__aseprite__*`): local edits, inspection and
  export of files in `src/`. Save work from the temp sprite into `src/` with `save_as`.

The `aseprite` server is defined in `.mcp.json` and starts `pixel-mcp.exe` directly. The
pixel-plugin launcher can fail at session start because bash strips the backslashes from its
Windows path. Keep the plugin installed for its skills.

Known `pixel-mcp` quirks:

- `draw_pixels` takes coordinates relative to the cel, not the canvas. A cel is trimmed to its
  content, so pixels land shifted by the cel position, and pixels outside the cel are lost.
  Subtract the cel position first, or use `draw_rectangle` with size 1x1. Shape tools
  (`draw_rectangle`, `draw_contour`, `fill_area`) use canvas coordinates.
- Pass paths with forward slashes. Backslashes make `save_as` report a JSON parse error, even
  though the file is saved.

Known Pixelwright quirks:

- Keys that differ only by case (`a` and `A`) fail in `map_write` and `part_write` with "Request
  body contains duplicate JSON keys". Use different letters.
- Templates add their own colours to the palette. Move them onto the project palette with
  `palette_update` before export.

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
