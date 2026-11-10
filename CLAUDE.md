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

**Forty Crowns** (title decided 2026-11-09; the repository keeps the name PixelCreate): a fast 3D action
RPG with a pixel art look, built code-first: a single-player story game (an isekai:
the player is hit by a truck saving a child and falls into the world). The MMORPG goal is dropped
(decided 2026-11-09); co-op for 2 to 4 players may come later. Story, world, factions, the tutorial
and the roadmap live in `design/` (start with `design/story.md`): read it before story, quest, dialogue or zone work.
Work now on the world, story, mechanics, movement, abilities and enemies. Do not tune difficulty or
balance yet. Godot 4.7 with C#; the server does not use Godot.

The game still runs as a local server plus a client, which keeps co-op possible: the server stays
authoritative and owns the save (player and world state), abilities, enemies, quests and dialogue are
data rather than special code, and choices change the world through world state flags.

- `shared-core/`: movement, collision, protocol, server and client prediction. No engine and no
  packages. Keep it that way: anything here must run in `dotnet test`.
- `transport/`: real UDP through LiteNetLib. `server/`: the dedicated server console app.
- `client/`: Godot project. `asset-pipeline/`: Blender scripts that write `.glb` files.
- `net-tests/`: `bash scripts/test_net_sim.sh` plays two bots through a simulated network (80 ms RTT,
  2 % loss) on a virtual clock. Run it after every change to movement, abilities or networking.

Rules:

- Block: hold the mouse wheel button (`Buttons.Block`, `PlayerState.BlockTicks`/`BlockAim`, predicted in `Movement.Step`). The guard covers
  the front, slows the player and costs stamina per blow: light blows (`melee`, `combo`, arrows) stop completely,
  heavy ones let 40 % through, and an empty stamina bar breaks the guard. A blow in the first `Tuning.ParryTicks` is
  parried: an attacker of the player's level or lower reels 1.5 s, one or two levels higher 0.6 s
  (`EnemyAction.Stagger`), and takes 1.5x damage meanwhile. `GameServer.Guard`, `net-tests/BlockTests.cs`.
- Attributes (`Attributes.cs`, `PlayerAttribute`): Strength, Dexterity, Intelligence, Vitality, Endurance; 3 points a
  level, spent with `Command.RaiseAttribute` (arg = attribute) in the character window (K), reset with Mira for 20 gold a
  level. Per-point values are starting guesses, not measured balance. Endurance rides in `PlayerState.Endurance` because
  it changes stamina, which movement predicts. `PIXEL_BOT=character` opens the window.
- Skills fly through the crosshair: the client sends the crosshair aim (`CrosshairAim`) as `InputCmd.AimYaw`/`AimPitch`
  with every weapon, and the server keeps the aim of the input that carried `Command.CastSkill`. The crosshair shows
  faintly once a skill is known. Projectiles falling straight down need another up vector for `LookAt`: Godot warns
  every frame otherwise, and that flood of warnings froze the client once.
- Skills (`world/skills.json`, `Skills.cs`): each reuses an enemy ability's effect turned on enemies (fireball, air
  slash, earth crack, summoned spears, fang slash). Enemies listed as `teachers` drop `book_<skill>` items (rares at
  `rareBookChance`); reading one (`Command.UseItem`) teaches the skill. Only `Skills.Slots` (3) skills can be cast: the
  server keeps `Player.SkillSlots` (in `Snapshot.SkillSlots`), a learned skill fills the first empty one, and
  `Command.SlotSkill` (low byte skill or `Skills.NoSkill`, high byte slot) changes them; a newly slotted skill starts on
  its cooldown, so swapping cannot stand in for more slots. The skill bar left of the hotbar casts them with Q, R and F;
  skills are dragged onto it from the character window (K) and never sit on the hotbar. A slotted skill casts with
  `Command.CastSkill` along the latest aim, for stamina, with a cooldown cut by
  Intelligence. Skill damage scales with level and Intelligence, not weapon power. Test mode knows every skill.
  `Command.UseItem` (arg = item index) replaced the eat range for the interface, so there is no 64-item limit there.
- Weapon mastery (`world/mastery.json`, `Mastery.cs`, window on J): kills earn the weapon kind in hand mastery
  experience (as much as the kill's experience); each mastery level is a point for that weapon's tree: three branches
  taken top down, stat and passive nodes feeding `GearStats` (`Mastery.AddTo` in `RecomputeStats`) only while that
  weapon is in hand. The last node of each branch is a new move; it can be taken once its id is in
  `MasteryNode.BuiltUnlocks`, and shows as coming soon until then. At most 64 nodes (one mask in the snapshot).
  `PIXEL_BOT=mastery` opens the window. `Mastery.Has` asks for a move with its weapon in hand; `GrantMastery` takes a
  branch in tests. Built moves: Riposte (sword: a parry within `Tuning.RiposteReach` lands a heavy blow at once,
  `Snapshot.Ripostes`), Triple Shot (bow: two weaker arrows either side of every shot), Piercing Arrow (bow: arrows fly
  through every enemy, `Projectile.Struck`), Stunning Bash (club: the heavy spin staggers whatever it hits), Shockwave
  (club: the spin also strikes out to `Tuning.ShockwaveRadius`, client ring `Fx.Ring`) and Chain Lightning (staff: the
  fireball's nearest victim sends a `ProjectileKind.ChainBolt` leaping on, `Tuning.ChainJumps` times). Test mode takes
  every built move. `net-tests/MasteryMoveTests.cs` checks each with and without it.
  All 15 moves are built. Moves that change how attacks start are predicted: the server puts them in
  `PlayerState.Moves` (`MasteryMoves`), and `Movement.Variant` swaps the attack: heavy from a slide is Rising Slash
  (sword, `Enemy.Launch`), light from a slide is Blade Dance (daggers, five stabs darting on); heavy in the air is
  Whirlwind (sword, floats down), Ground Pound (club, the blow lands where the plunge meets the ground) or Shadow Step
  (daggers: the server moves the player behind the enemy ahead, which the client takes from the snapshot). Runs of
  blows use `AttackDef.HitEvery`/`LastHitAt`. Server-side moves: Fan of Knives (`ProjectileKind.Knife` ring when the
  leap lands), Meteor (`ProjectileKind.Meteor` on the far end of the fire line), Rain of Arrows (`MasteryMoves.RainOfArrows` lets the draw
  run `Tuning.RainHoldTicks` past full, with a blue ring round the crosshair; letting go rains arrows on the spot the
  aim meets, `GameServer.RainSpot`) and Arcane Barrier (`Player.Barrier`, soaks blows before
  health, refills when unhurt, `Snapshot.Barrier`). The studio's attacks job films the new player attacks.
  Air moves need the feet `Tuning.AirMoveHeight` above the ground (running over bumps leaves the ground for a tick,
  which once let a held heavy chain ground pounds), and every predicted mastery move sets `PlayerState.MoveCooldown`.
  Shadow Step needs an open way (`GameServer.Open`: no solid box and no ground across the line at waist height) to
  the enemy and round it, and reaches at most `Tuning.ShadowStepChain` enemies in a row. Staff fireballs and walls of
  fire have their own predicted cooldowns (`PlayerState.BoltCooldown`/`HeavyCooldown`), shown as two arcs under the
  crosshair. The club spin shares `HeavyCooldown` (`Tuning.ClubSpinCooldownTicks`) and, like Ground Pound, needs a fresh
  press of the heavy button (`PlayerState.HeavyHeld`): held down, it once spun and staggered until the stamina ran out.
- Attack reaches in `Tuning` are the weapon tip's reach at the blow plus about half a metre; the enemy's half size is
  added in `GameServer.Strike`. The studio's attacks job saves `attack_reach_all` (each blow from above with its area:
  red the reach and arc, yellow the reach to a goblin's middle) and `attack_carry_all` (each weapon idle, walking,
  running, in the air and dashing). Check both after changing a weapon model, pose or reach.
- Enemy views are placed from `GameClient._enemyShown`, never from their own root: a leaping werewolf's root is
  tipped round its middle, and feeding that back into the easing made Moonleaper stutter in the air.
- Slide: Ctrl while running on the ground (`Buttons.Slide`, `PlayerState.SlideTicks`/`SlideDirection`, in `Movement.Step`):
  a burst from 17 to 7 m/s over half a second, steerable a little, for stamina; a jump out of it keeps the speed in the
  air, and an attack or a dash ends it. `net-tests/SlideTests.cs`.
- Never use Godot physics for players. Reconciliation replays ticks, so movement stays in
  `Movement.Step` and collision against `World`.
- Every value that affects movement lives in `PlayerState`, and client and server both call
  `Movement.Step`. An ability the client cannot predict must correct through a snapshot.
- The Godot client can run on a newer .NET than the server. Avoid behaviour that differs between
  runtimes in shared code, such as casting a negative float to an unsigned integer. A live run with
  many corrections and no hits means client and server simulate differently.
- All game text is English. Enemy types live in `world/enemies.json`. Items live in `world/items.json`
  (crystals first, then materials, food, potions and equipment); each enemy's `loot` table names them. The
  server owns the bag and sends it in slices; the client only decides which slot shows what. Gear pieces are server-owned instances (`Gear.cs`): base item,
  rarity, level and affixes from `world/affixes.json`; worn gear feeds `GearStats` into every blow and hit. Each enemy's
  `gear` table in `enemies.json` sets its drop chance, rarity weights and base items; `GearTests` prints pieces per hour.
  Gear commands start at `GearCommands.ToggleBase`. The worn weapon sets `PlayerState.Weapon`, which picks the attacks in `Movement.Step` (daggers: a two-stab flurry and a predicted leap; bow: hold to draw, release to loose a server arrow exactly along the fine aim in `InputCmd.AimYaw`/`AimPitch`, with no aim assist, a minimum draw and a range set in `Tuning`; the client aims over the shoulder and takes the aim from the point under the crosshair). Clubs swing in a wide arc (light) and spin once round (heavy); staves cast a fireball through the crosshair (light) and a straight strip of fire ahead (heavy, a player-owned `FireWallHazard`). `PIXEL_BOT=bow`, `club` or `staff` with `PIXEL_TEST=1` fights with that weapon. `PIXEL_BOT=gear` with `PIXEL_TEST=1` checks the bag and tooltips. Food is eaten with the command range from `ItemCommands.EatBase`.
  Potions (`kind: potion`) drink the same way with a shared 3 s cooldown; three potion slots sit beside the hotbar
  (Z, X, C, client-side shortcuts). Shops sell items whose `shop` names the keeper's role (`ItemDef.Wares`,
  `ShopCommands.BuyBase`); Wren the alchemist has her stall by the Guild road.
  The Guild chest (an NPC of role `storage` by the quest board wall) keeps stacks and unworn gear per player:
  `Command.StoreItem/TakeItem/StoreGear/TakeGear` with `InputCmd.Arg`, sent in slices only while the player stands by it.
- Item icons: `python asset-pipeline/generate_icons.py --sheet` paints every icon at 32x32: text shapes at 16x16 doubled with Scale2x and edge-lit
  and writes a preview to `export/previews/icons.png`. A test fails if an item has no icon. Weapons get
  32x32 icons painted from their profile in `asset-pipeline/weapon_icons.py`. The HUD scales icons by whole
  steps only (a 64 px icon area: 4x for 16 px, 2x for 32 px). Run Godot's `--import` after painting.
- Weapon models: `asset-pipeline/weapons.py` builds `weapon_<item id>` (origin in the middle of the grip,
  blade along the game's -Z); `PlayerRig.WeaponModel` loads them and the worn item picks the model. Bows are
  `BowView` in code, so they can bend; `BowLook.For` gives each bow item its shape. `scripts/studio.ps1 -Jobs weapons`
  photographs every weapon up close. Dagger attacks and the bow place the hands with keys and reach (`PlayerRig.Reach`).
- Test a far area: set `PIXEL_SPAWN=x,z` before starting the server.
- Enemy abilities and crystal drops are data in `world/enemies.json`; new effects go in `GameServer.Fire`.
- Group AI (`EnemyTactics.cs`): fighters keep apart (`AllySpacing`), and a close blow needs a turn from the server
  (`TryTakeTurn`, at most `GameServer.MaxCloseAttackers` = 2 per player, two blows or 5 s each, ended only after
  the recovery). The rest hold a ring and feint. A fighter chases up to `Enemy.ChaseLeash` (45 m) past its camp, so
  players can pull one or two away from a group. `behavior` in `enemies.json` picks the role: brute, flanker (works to
  the player's back), skirmisher (falls back after a blow), ranged, support (keeps behind its front line) or pack;
  a `leader` wolf's call rallies its kind and lets two more of the pack strike for 5 s. `net-tests/TacticsTests.cs`.
- Goblins and orcs also roam: a camp with a `route` in the zone file is a patrol (`Patrol.cs`) that walks its points
  and back, resting at each, and small wild camps sit in Westwood, the Southern Wilds and south of East Watch. The
  zone audit keeps every patrol point reachable and every hostile camp or point over 50 m from the village. Put camps
  on flat ground: a slope check before placing saves a rebuild.
- Camps and patrols marked `hunts` go after deer and wild boar now and then (`Hunt.cs`): up to three hunters, a
  deer only if one can shoot or throw, the prey's position as their home while the chase lasts, then a meal where
  it fell. Creature targets are ids from `Enemy.CreatureBase` up; `GameServer.CreatureTargets` adds hunters and
  hunted to the target list (animals run from hunters, a boar fights back) and `Notice` ignores them, so a player
  near always wins. `net-tests/HuntTests.cs`.
  Orcs fight by weapon: longswords swing flat (light) and send an `airSlash` crescent about 5 m (heavy); spears stab
  and throw (`spearThrow`, the spear is back in her hand at once); axes swing flat and split the ground in a small V
  (`earthCrack`, a `FireWallHazard` of kind `EarthCrack` drawn by `EarthCrackNode`, hurting only while it runs).
  The studio's monsters job films each orc ability's wind-up and blow.
  Rare orcs in the Ironjaw camp: the Swordmaster (`combo`, three blows; `airSlashWide`, a level crescent), the
  Spearcaller (`summonSpears`: three spears hang over her head and shoot one by one) and the Stormaxe (`tornado`,
  `EnemyAction.Tornado`: five turns of the axe, hurting every pulse while it drifts at the player).
  Wolves bite (light) and `pounce`, a real leap (heavy). Two rare wolves live in Greyfang: Mirrorfang calls two
  `shadow_clone`s (weak, no loot, gone after 14 s or with their wolf; summons join `_enemies` after the step loop),
  and Shadowstalker's `shadowRun` sinks into the ground (`EnemyAction.Shadow`, cannot be hit), slides out to
  alternate sides, rises (`Surface`) and bursts out at the player from close by. `net-tests/WolfTests.cs`.
  Clones use `ghost.gdshader`. The forest camera can end up inside pine branches near these dens.
  Werewolves claw (light) and pounce (heavy). Two rare ones live in Moonfell: Redclaw's `fangSlash` sends three
  slanted red crescents side by side, and Moonleaper's `superJump` crouches with swelling thighs, cracks the ground
  at take-off, flies as an arrow and lands as a strike (`Enemy.Landed`) or, on a miss, rolls (`EnemyAction.Roll`).
  `net-tests/WerewolfTests.cs`.
- Story data (`Story.cs`, design in `design/stage-1.md`): quests in `world/quests.json`, what people say in
  `world/dialogue/<npc id>.json` (every NPC but the chest has one; entries pick the opening node by condition),
  faction plans in `world/agendas.json`, rumours in `world/rumors.json`, journal notes in `world/journal.json`.
  Conditions and effects share one small JSON language (`Conditions.cs`, `Effects.cs`); server actions an effect may
  name are listed in `Effect.Actions`. A dialogue choice sends `Command.Choose` with the option's place in
  `Story.Options`; the server checks it again (near that NPC, node reachable, condition holds) and applies the effects.
  Effects must read their JSON values while parsing: the documents are disposed after loading, and a lazy read once
  crashed the server on the first choice. `StoryTests` run every choice once to catch that.
- World state (`WorldState.cs`): flags, reputation, trust, NPC fates, memberships, quest states and agenda steps, one
  per server. Clients get it as `StoryEntry` slices in snapshots (changed first, then a rotation, `StoryMirror`) and
  keep a copy in `ClientPrediction.Story`; a key must be in `Story.Keys` (built from the data and `Story.CodeFlags`)
  to reach the client.
- Calendar (`Calendar.cs`): a game hour is 26 s, a day starts at six, `Command.Sleep` from eight in the evening
  wakes the player at six the next morning (Mira offers the Guild bunk room then). Faction plans run every game hour.
  Act I lasts 70 days. The clock stands still while every player reads a dialogue or the menu (`Buttons.Reading`).
  The client lights the world by the hour (`PixelStage.SetTime`: dusk, dawn, a dark blue night with the moon);
  `PIXEL_TIME=21` starts a new game at that hour and `PIXEL_OFF=daytime` keeps it day. The journal (L,
  `PIXEL_BOT=journal`) lists active quests and the notes of flags the player has.
- Saves (`SaveGame.cs`): the server writes `%LOCALAPPDATA%/PixelCreate/saves/<slot>.json` each morning and on
  `Command.Quit` (the client sends it when quitting or closing the window), keeping `<slot>.bak.json`. `PIXEL_SLOT`
  picks the slot (default `main`), `PIXEL_SAVE` the folder; test mode neither loads nor saves. A death after
  registering costs a tenth of the unsold crystals and is written down for the act III echoes.
- Actors (`Actors.cs`): every NPC of the zone plus the people of `world/actors.json` (away until a scene brings them).
  The server moves them and sends only those not at rest (`ActorView`); the client works out where the rest stand.
- Scenes (`Scenes.cs`, `world/scenes/*.json`, run in `GameServer.StepScene`): steps in order (lockInput, spawn, place,
  move, follow, pose, camera, fade, slides, say, talk, teleport, time, effects, wait, waitFor, spawnEnemies with
  `health`, waitForKills, drop, checkpoint, scene). A `trigger` starts one by condition and place, `until` ends it early
  (the trial's lines stop when the trial does), and `scene.<id>` is set when it has played. The client shows a scene
  from the step in the snapshot (`GameClient.UpdateScene`). Rules found by playing:
  - People speak face to face: a line or a talk waits until speaker and player are within `SceneData.SpeakRange`.
    Until then the player walks freely and the objective reads "Go to <name>"; a follower or a speaker with
    `approach` comes over. A `bark` carries any distance and never holds the controls.
  - A player a scene holds still is no target for enemies. A scene that follows another must lock input itself:
    the client works the lock out from the steps of the scene playing.
  - A `checkpoint` is where a player not yet bound to the Anchor Stone wakes after dying. Without one, a lost
    fight in a scene wakes them in the village while the scene waits for kills for ever.
  - A talk's follow-up node keeps `Hud.SceneDialog`: without it the dialogue closed and reopened at its root.
  - A door nearer than the nearest person wins the E key: a follower once stood between the player and the Guild.
- The prologue (`design/stage-3.md`) is scenes `prologue_1_intro` to `prologue_10_badge`; a new game starts with it
  (`ServerOptions.Prologue`, `PIXEL_SKIP_PROLOGUE=1` skips it, test mode never plays it). `scripts/prologue.ps1`
  plays it with `PIXEL_BOT=prologue` against a throwaway save and passes when it prints `BOT done`; `-Steps`
  prints every scene step, `-Flags` and `-Spawn` start partway (`PIXEL_FLAGS` sets story flags on a new game).
  The intro's pictures are painted by `python asset-pipeline/generate_intro.py` (then Godot's `--import`).
  Keep the ruins camp and patrol away from the ambush on the road at (71.5, 72): level 4 goblins joined the first fight.
- Roads are the highways of everyone who walks by themselves (`RoadMap` in `Schedules.cs`, built from the zone's
  `paths` in `World.Roads`): `Route` goes straight when the line is clear and short, else onto a road by a clear line,
  along the roads and off again, so nobody walks into a wall. NPCs on their day (`world/schedules.json`,
  `Schedules.RestOf`, `GameServer.ApplySchedules`) and the bots (`GameClient.BotGo`, which also jumps and plans again
  when it has not moved for 90 ticks) both use it. Where no road lies within 30 m (indoors, the wilds) the walk is
  straight: a fallback to the nearest road anywhere once sent the bot out of the Guild hall into a wall.
  `net-tests/RoadTests.cs` walks real routes in the zone. New places people go to need a road near them.
- A weapon picked up with empty hands is worn at once (the prologue's player has none until the wooden sword).
- Interiors live outside the map on platforms (the guild at x 420, the trial ground at x 480). Doors are portals.
- Character models come from Blender (`anime_*`) and use `toon.gdshader`, which also bends hair and cloth.
- Faces (`FaceRig.cs`): eyes, lids, lashes, brows, nose, mouth shapes, blush, tear and sweat are small flat discs
  on the head, eased towards an expression from `world/faces.json` (`Faces.cs`). Brows sit in front of the fringe
  and the lashes slant with them, so anger and sorrow read where the hair hides the brows. A dialogue node's
  `face` sets the speaker's expression; the mouth moves while the words appear, and a second camera renders the
  speaker into the dialogue box (`GameClient.UpdateSpeaker`; the portrait viewport needs `View.FindWorld3D()` and a
  current camera, or it renders grey). NPCs rest in `RigLook.Mood`; the player winces when hit, strains on heavy
  blows and tires on low stamina. Studio job `faces` photographs every expression.
- Appearance (`Appearance.cs`, `world/appearance.json`, `PlayerLook.cs`): the player is always a man and picks
  face, nose, skin, eyes, eye shape, brows, hair, hair colour, beard, scar and height. `Command.SetAppearance`
  (high byte field, low byte choice), kept in the save and sent in `Snapshot.Appearance`; the client rebuilds the
  rig when it changes. A new game opens the creator (`Hud.OpenCreator`, `PIXEL_BOT=creator`); test runs and
  `PIXEL_LOOK=default` start with the default look. Studio job `creator` photographs every choice.
- Sound effects come from ElevenLabs: `python asset-pipeline/generate_sounds.py` (cached, keys stay outside the repo).
- A snapshot must fit one UDP packet, so the server sends each player only nearby enemies. `GameServer.Fit` trims
  the farthest shots, drops and enemies until it is under `MaxSnapshotBytes` (a rain of arrows over a camp in test
  mode reached 1065 bytes, and LiteNetLib's refusal killed the server); `UdpTransport.Send` drops a packet that is
  still too big instead of throwing. The server's errors go to `export/previews/server-errors.log`, the client's
  output to `export/previews/client.log`, and a crashed .NET server also leaves its stack in the Windows Application log.
- Pose check: run the Godot console executable with `--path client res://scenes/pose_test.tscn -- <folder>`.
- Regenerate textures, palette and UI font with `python asset-pipeline/generate_textures.py`.
- Zones live in `world/zones/*.json`: the layout is authored, the scattered detail comes from the seed.
  Meadow Valley is 600 m across: the old 360 m valley is ringed by `ridges` (mountain chains along a line, ends sinking
  into walkable passes), and beyond them lie Greyfang Forest (wolves, north-east), Moonfell (north-west) and the
  Ironjaw Warcamp (south-east). `grounds` paint forest floor, blight or trampled earth on terrain and grass. Props for
  these lands come from `asset-pipeline/lands.py`. The terrain is drawn in 60 m chunks so off-screen ground is culled.
- Blender: `D:\Program\Blender Foundation\Blender 5.2\blender.exe -b --factory-startup -P <script>`.
- Play: `powershell -ExecutionPolicy Bypass -File scripts/play.ps1` starts a local server and the client. Add `-Test` for test mode (gear kit). `PIXEL_FRAIL=1` gives the Guild examiner one hit point (the full check sets it).
- Live check: add `-Capture <folder>` to that command. A bot plays, saves frames and prints a `STATS` line.
- Minimap (`Minimap.cs`, top right, north = -Z up): the studio's `map` job renders `client/assets/ui/minimap.png`;
  run it again after changing terrain, roads or buildings. Buildings are marked by model (`Minimap.Landmarks`), and
  places appear once found (`Settings.Discovered`, saved in `user://settings.cfg`).
- Esc opens the menu (`HudMenu.cs`): Resume, Settings (sound effects, music, mouse and bow aim sensitivity, saved by `Settings.cs` to `user://settings.cfg`) and Quit Game. Sounds play on the `SFX` bus; music belongs on `Music`. `PIXEL_BOT=menu` shows both pages.
- `PIXEL_BOT` picks the bot: `hunt` kills animals near the spawn, loots, eats and opens the bag; `guild` plays the Guild story through the real interface and prints `BOT` stages,
  with `PIXEL_FRAIL=1` making the examiner frail. The examiner is a level 5 `duelist`:
  she circles just out of reach, steps in when a blow is ready, dashes aside from swings (`EnemyAction.Dodge`,
  beyond harm like a player's dash) and sometimes springs back after her own blow (`EnemyDuelist.cs`). Also `idle`, `talk`, `trial`, `keys`. `PIXEL_YAW` turns the camera. `PIXEL_CAPTURE_EVERY=6` captures a frame every 6 ticks (default 45) to catch short effects.
- The 3D view renders at the window resolution (`PIXEL_LINES=540` for the old chunky pixels); outlines are thin and fade
  with distance. F3 shows the player position, for reporting a problem spot.
- `STATS` reports draws, objects and `frameMs` away from capture stalls. Earlier runs measured the open
  world at about 18 ms a frame on the RTX 3050 laptop; later runs (hunt bot by the sheep, idle in the
  village) measured 7 to 9 ms, and the reason for the change was never pinned down. Measure before
  trusting either number. On 2026-11-08 an idle bot measured 7 to 10 ms everywhere: village 9.6, Greyfang 8.8,
  the Ironjaw Warcamp 8.6 (2351 draws, the most), Moonfell 7.0. Spawn a measuring bot outside enemy aggro range:
  inside a camp it dies at once and the rest of the run measures the village. `PIXEL_OFF=fog,ssao,glow,shadow,batches,grass` switches features off.
- Check after a change: `powershell -ExecutionPolicy Bypass -File scripts/check.ps1` runs the tests and the Guild
  story live, and lists script exceptions from the live run: one exception a frame freezes the HUD and the player
  without crashing. Add `-Jobs monsters,world` (studio jobs whose looks changed) for pictures; a server or AI
  change needs none. `-Jobs all` photographs everything and takes about ten minutes, so use it only when asked.
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
