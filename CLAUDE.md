# PixelCreate

Workspace for pixel art and experiments around it.

## Layout

- `src/`: `.aseprite` source files. These are the source of truth.
- `export/`: generated PNG, GIF and spritesheets. Rebuild them from `src/`; do not edit them.
- `export/previews/`: review sheets, ignored by git.
- `palettes/`: `.gpl` palettes and material ramps (`ramps-*.json`).
- `refs/`: reference images. Use them for study; do not copy them into finished art.
- `tools/`: Python tooling (`compose.py` walks a sprite across a scene into a GIF). `tests/`: tests for it (`python tests/test_tools.py`).

## The game

A fast 3D action RPG with a pixel art look, built code-first. The long-term goal is an MMORPG, but
the MMO parts wait. Work now on the world, mechanics, movement, abilities and enemies. Do not tune
difficulty or balance yet. PvE comes first, PvP later. Accounts, databases and matchmaking wait until
the game is fun. Godot 4.7 with C#; the server does not use Godot.

Keep the MMORPG path open: the server stays authoritative, abilities and enemies are data rather
than special code, and the world is built from zones that one server process can run.

- `shared-core/`: movement, collision, protocol, server and client prediction. No engine and no
  packages. Keep it that way: anything here must run in `dotnet test`.
- `transport/`: real UDP through LiteNetLib. `server/`: the dedicated server console app.
- `client/`: Godot project. `asset-pipeline/`: Blender scripts that write `.glb` files.
- `net-tests/`: `bash scripts/test_net_sim.sh` plays two bots through a simulated network (80 ms RTT,
  2 % loss) on a virtual clock. Run it after every change to movement, abilities or networking.

Rules:

- Never use Godot physics for players. Reconciliation replays ticks, so movement stays in
  `Movement.Step` and collision against `World`.
- Every value that affects movement lives in `PlayerState`, and client and server both call
  `Movement.Step`. An ability the client cannot predict must correct through a snapshot.
- The Godot client can run on a newer .NET than the server. Avoid behaviour that differs between
  runtimes in shared code, such as casting a negative float to an unsigned integer. A live run with
  many corrections and no hits means client and server simulate differently.
- All game text is English. Enemy types live in `world/enemies.json`. Items live in `world/items.json`
  (crystals first, then materials, food and equipment); each enemy's `loot` table names them. The
  server owns the bag and sends it in slices; the client only decides which slot shows what. Gear pieces are server-owned instances (`Gear.cs`): base item,
  rarity, level and affixes from `world/affixes.json`; worn gear feeds `GearStats` into every blow and hit. Each enemy's
  `gear` table in `enemies.json` sets its drop chance, rarity weights and base items; `GearTests` prints pieces per hour.
  Gear commands start at `GearCommands.ToggleBase`. The worn weapon sets `PlayerState.Weapon`, which picks the attacks in `Movement.Step` (daggers: a two-stab flurry and a predicted leap; bow: hold to draw, release to loose a server arrow exactly along the fine aim in `InputCmd.AimYaw`/`AimPitch`, with no aim assist, a minimum draw and a range set in `Tuning`; the client aims over the shoulder and takes the aim from the point under the crosshair). Clubs swing in a wide arc (light) and spin once round (heavy); staves cast a fireball through the crosshair (light) and a straight strip of fire ahead (heavy, a player-owned `FireWallHazard`). `PIXEL_BOT=bow`, `club` or `staff` with `PIXEL_TEST=1` fights with that weapon. `PIXEL_BOT=gear` with `PIXEL_TEST=1` checks the bag and tooltips. Food is eaten with the command range from `ItemCommands.EatBase`.
- Item icons: `python asset-pipeline/generate_icons.py --sheet` paints all 16x16 icons from text shapes
  and writes a preview to `export/previews/icons.png`. A test fails if an item has no icon. Weapons get
  32x32 icons painted from their profile in `asset-pipeline/weapon_icons.py`. The HUD scales icons by whole
  steps only (a 64 px icon area: 4x for 16 px, 2x for 32 px). Run Godot's `--import` after painting.
- Weapon models: `asset-pipeline/weapons.py` builds `weapon_<item id>` (origin in the middle of the grip,
  blade along the game's -Z); `PlayerRig.WeaponModel` loads them and the worn item picks the model. Bows are
  `BowView` in code, so they can bend; `BowLook.For` gives each bow item its shape. `scripts/studio.ps1 -Jobs weapons`
  photographs every weapon up close. Dagger attacks and the bow place the hands with keys and reach (`PlayerRig.Reach`).
- Test a far area: set `PIXEL_SPAWN=x,z` before starting the server.
- Enemy abilities and crystal drops are data in `world/enemies.json`; new effects go in `GameServer.Fire`.
- Interiors live outside the map on platforms (the guild at x 420, the trial ground at x 480). Doors are portals.
- Character models come from Blender (`anime_*`) and use `toon.gdshader`, which also bends hair and cloth.
- Sound effects come from ElevenLabs: `python asset-pipeline/generate_sounds.py` (cached, keys stay outside the repo).
- A snapshot must fit one UDP packet, so the server sends each player only nearby enemies.
- Pose check: run the Godot console executable with `--path client res://scenes/pose_test.tscn -- <folder>`.
- Regenerate textures, palette and UI font with `python asset-pipeline/generate_textures.py`.
- Zones live in `world/zones/*.json`: the layout is authored, the scattered detail comes from the seed.
- Blender: `D:\Program\Blender Foundation\Blender 5.2\blender.exe -b --factory-startup -P <script>`.
- Play: `powershell -ExecutionPolicy Bypass -File scripts/play.ps1` starts a local server and the client. Add `-Test` for test mode (gear kit, frail examiner).
- Live check: add `-Capture <folder>` to that command. A bot plays, saves frames and prints a `STATS` line.
- Esc opens the menu (`HudMenu.cs`): Resume, Settings (sound effects, music, mouse and bow aim sensitivity, saved by `Settings.cs` to `user://settings.cfg`) and Quit Game. Sounds play on the `SFX` bus; music belongs on `Music`. `PIXEL_BOT=menu` shows both pages.
- `PIXEL_BOT` picks the bot: `hunt` kills animals near the spawn, loots, eats and opens the bag; `guild` plays the Guild story through the real interface and prints `BOT` stages,
  with `PIXEL_TEST=1` making the examiner frail. Also `idle`, `talk`, `trial`, `keys`. `PIXEL_YAW` turns the camera.
- The 3D view renders at the window resolution (`PIXEL_LINES=540` for the old chunky pixels); outlines are thin and fade
  with distance. F3 shows the player position, for reporting a problem spot.
- `STATS` reports draws, objects and `frameMs` away from capture stalls. Earlier runs measured the open
  world at about 18 ms a frame on the RTX 3050 laptop; later runs (hunt bot by the sheep, idle in the
  village) measured 7 to 9 ms, and the reason for the change was never pinned down. Measure before
  trusting either number. `PIXEL_OFF=fog,ssao,glow,shadow,batches,grass` switches features off.
- Full check after a change: `powershell -ExecutionPolicy Bypass -File scripts/check.ps1`. It lists script
  exceptions from the live run: one exception a frame freezes the HUD and the player without crashing.
- Photo studio: `scripts/studio.ps1 -Jobs people,animals,monsters,motion,attacks,models,world,hitboxes,map` needs no server and writes
  labelled contact sheets to `export/previews/studio/sheets`. Read them after every visual change. World shots
  live in `client/studio/shots.json`; add one for each new place. `PIXEL_SHOTS=guild,arena` films only shots
  whose names start with those words.
- `net-tests/ZoneAuditTests.cs` walks the real zone: keep every door, camp and bridge reachable.
- Collision is measured from the models: building a model writes its boxes to `world/collision.json`
  (`asset-pipeline/collision.py`). Never hand-size a hitbox; rebuild the model. Plants and models with
  platform floors are listed as non-solid in `Zone.NonSolid`. Grass never grows inside a solid box.
- Studio model shots: `PIXEL_MODELS=cottage,bridge` photographs only those models, from five sides, with
  their collision boxes drawn.
- Models: edit `asset-pipeline/build_meshes.py`, then `scripts/build_assets.ps1` builds and imports them.
  Name models to rebuild only those: `scripts/build_assets.ps1 guild_interior beast_cow`. Animals are
  `beast_*` (asset-pipeline/beasts.py) and goblin parts and camp props come from asset-pipeline/goblins.py.
  Beasts keep their parts (body, neck, head, ears, tail, legs) as separate nodes for AnimalView.
- Animals live in herds (`EnemyAnimal.cs`): graze, watch (`alertRange`), bolt (`aggroRange`), and spread a
  scare through the herd. `net-tests/AnimalTests.cs` checks each behaviour on an empty field.
- Hair and cloth are `ClothChain` grids; the toon shader bends meshes along them. Rig looks live in `Dialogues.cs`.
- The server waits for a late input instead of skipping it. Catching up spends only ticks missed while stalled,
  so no client can run faster than real time: extra inputs are thrown away.

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
