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
RPG with a pixel art look, built code-first. Since 2026-11-10 it is **not a story game**: the focus is the
environment, world building, monsters, fighting, mechanics and movement, with levelling by killing monsters and
simple jobs. Keep: the intro film, the look creator, the capture by the finders and the goblin ambush, then the road
to Hearthmere and Mira, who signs the player up. No companions, no story quest chains, no dialogue trees: only the
people who serve (Mira, Hollis, Wren, Dorran at the tavern bar, Tess at her forge, the job board, the Anchor Stone) can be spoken to; everyone else only talks in
passing or among themselves. The direction, what was learned and what comes next: `design/direction.md`. The world plan (regions, towns, dungeons, travel, styles, build order): `design/world-plan.md`, data in `design/world/layout.json`, maps by `python tools/world_map.py` into `design/maps/`. The MMORPG goal is dropped; co-op for 2 to 4 players may come later. Do not tune difficulty
or balance yet (the measured numbers: `design/balance.md`, written by `PIXEL_BALANCE=1 dotnet test --filter BalanceReport`). Start a session with `design/handoff.md`. Godot 4.7 with C#; the server does not use Godot.
The story documents in `design/` (story, factions, stages 1 to 4, repair plan R4) describe the dropped direction.

The game still runs as a local server plus a client, which keeps co-op possible: the server stays
authoritative and owns the save (player and world state), and abilities, enemies, jobs and the few dialogues are
data rather than special code.

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
  Intelligence. Skill damage scales with level and Intelligence, not weapon power. Test mode knows every skill. Wisp Bolt (wisps, homing after the nearest enemy) and Vine Pull (vinelashers: cuts and
  drags enemies ahead to the player, `Enemy.DragTo`) come from the new monsters.
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
  A melee blow also tests where the enemy stood `Enemy.SeenLateTicks` ago (`Enemy.SeenAt`, `GameServer.InReach`): the player
  sees enemies a little late, and circling goblins kept stepping out of blows that looked like hits. The swing's trail
  must not reach past the red area in `attack_reach_all`.
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
  `ShopCommands.BuyBase`); Wren the alchemist has her stall by the Guild road. Dorran sells bread, Hearty Stew and Meat Pie at the Bent Nail's bar (role
  `barkeep`; the stools stand apart so a player can step up to the bar, `ScheduleTests.Dorran_CanBeReachedAcrossHisBar`).
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
- Waystones (`Waystones.cs`): NPCs of role `waystone` with a `waystone` prop and a dialogue in
  `world/dialogue/waystone_<id>.json` (written by `python tools/waystones.py`; run it after adding a stone: bind with "Lay your hand on the stone", flag
  `waystone.<id>`; travel to every other bound stone for 15 gold). Travel is only from stone to stone: the action
  `travel:<id>` works only beside a waystone and only to a bound one, and lands `Waystones.ArrivalDistance` in front of
  it. Eight in Meadow Valley (Hearthmere, East Watch, Silver Falls, Greyfang, Moonfell, Goblin Hollow, West Road,
  Mirror Way): one at each settlement, dungeon mouth and pass road. `net-tests/WaystoneTests.cs`.
- Every job needs a hand-in at Mira: `python tools/turnins.py` adds a `turn_in_<id>` node and entry for each job of
  `world/quests.json` (`StoryTests.EveryJob_CanBeHandedInToMira`). Until 2026-11-12 only the first five jobs had one.
- Hidden caches: NPCs of role `cache` (an "Old Chest", no figure, a `storage_chest` prop on the same spot) with a
  `note`, `found` and `reward` in the zone file; `python tools/caches.py` writes their dialogue (open once: reward and
  flag `cache.<id>`, then empty). Nine sit at the abandoned places, the watchtower, the quarry and Moonfell.
- Cooking fires: NPCs of role `hearth` (no figure, a floating "<name>: cook" sign) on a `camp_fire` prop or the Bent
  Nail's hearth; `python tools/cooking.py` writes their dialogue from its `RECIPES` (one option per raw item, shown
  only while the bag holds it: take one raw, give one cooked, stay at the fire). Four fires: the Bent Nail, East Watch,
  the quarry, the Woodcutters' Camp. Cooked heal values are guesses. The test kit carries two raw beef and a venison;
  `PIXEL_BOT=cook` (with `PIXEL_TEST=1` and `PIXEL_SPAWN` near a fire) cooks them. `net-tests/CookingTests.cs`.
- Forging: Tess (role `smith`, at her anvil from six) forges gear from monster materials and gold. `python
  tools/forge.py` writes `world/dialogue/smith.json` from its `RECIPES`: each recipe is a "Forge the ..." option shown
  while bag and purse hold enough (take, pay, server action `forge:<item>`, `GameServer.ForgePrefix`: a rare piece of
  the player's level into the gear list, or on the ground when it is full) and a "(Not enough yet.)" twin otherwise.
  Recipes and prices are guesses. An open dialogue rebuilds its options when the bag or purse changes
  (`Dialogues.Refresh`), since a choice that returns to the same node is shown before the new bag arrives.
  `net-tests/ForgeTests.cs`.
- Herbs and mushrooms: items with `"gather": true` (Meadow Sage, Glowcap, Moonpetal) grow where the zone's `gathers`
  circles put them (`Zone.PlaceGathers`: seeded spots on dry, open, gentle ground off roads, `World.Gathers`). The
  server makes each spot a loot of its own (`Loot.Spot`) for every player within `GatherReach` whose spot is not
  regrowing, takes it away past `GatherDrop`, and a pick (walking over it, like any drop) regrows after
  `GatherRegrowTicks`; real drops go into the snapshot first. The client draws them low with a green ring. Wren
  brews potions from them (`python tools/brew.py` writes her dialogue). `net-tests/GatherTests.cs`.
- Fishing: NPCs of role `fishing` (no figure, "Fishing Spot: fish" sign) at the water end of the two Mirror Lake piers;
  `python tools/fishing.py` writes their dialogue. "Cast a line." runs the action `fish`: a bite comes after
  `FishBiteMinTicks` to `FishBiteMaxTicks` if the player stays within `FishStillReach` (`GameServer.StepFishing`):
  a Lake Perch, a Silver Trout or now and then a mirefin fillet (`GameServer.Catch`), all cookable. The client shows
  "Line out..." while it waits and "The line went slack" on a step away. The lake's mirefins (and the Old Gulper)
  attack players on the piers, so tests freeze enemies. `PIXEL_BOT=fish` (PIXEL_SPAWN near a pier) fishes three.
  `net-tests/FishingTests.cs`.
- Why gather, fish, cook and forge (design/dungeon-archive.md, part 5): cooked food (`"buff": "wellFed"` in items.json)
  makes the player Well Fed (`GameServer.WellFedHealthShare` more maximum health and a slow regeneration for
  `WellFedTicks`); the Draught of Warding (Wren: glowcap, moonpetal, fin spine) makes them Warded (blows land at
  `WardedDamageTaken` for `WardedTicks`). Both end on death and ride in `Snapshot.WellFedSeconds`/`WardedSeconds`
  for the HUD pills under the status card. A bite has `DrownedKeyChance` of bringing up the Drowned Auren Key (only
  while the player has none and the archive vault is unopened). Tess forges the Seal of the Keeper from the Keeper's
  heart, moonpetals and rune cores. Repeatable gather jobs: sage for Wren, perch for the Bent Nail, glowcaps for the
  Guild; `Navigation` leads a bring step for an item nobody drops to where it grows or to a fishing spot.
  `net-tests/BuffTests.cs`.
- `Command.UseItem` eats and drinks through `GameServer.Consume`: the old eat command range (`ItemCommands.Eat`)
  reaches only the first 64 items.
- Test a far area: set `PIXEL_SPAWN=x,z` before starting the server.
- Enemy abilities and crystal drops are data in `world/enemies.json`; new effects go in `GameServer.Fire`.
- Group AI (`EnemyTactics.cs`): fighters keep apart (`AllySpacing`), and a close blow needs a turn from the server
  (`TryTakeTurn`, at most `GameServer.MaxCloseAttackers` = 2 per player, two blows or 5 s each, ended only after
  the recovery). The rest hold a ring and feint. A fighter chases up to `Enemy.ChaseLeash` (45 m) past its camp, so
  players can pull one or two away from a group. `behavior` in `enemies.json` picks the role: brute, flanker (works to
  the player's back), skirmisher (falls back after a blow), ranged, support (keeps behind its front line) or pack;
  a `leader` wolf's call rallies its kind and lets two more of the pack strike for 5 s. `net-tests/TacticsTests.cs`.
  Morale: a goblin (not a brute) below `Enemy.RoutHealth` of its health loses heart once in its life, gives up its
  turn and runs for its camp for `Enemy.RoutDuration`, then fights again; scene goblins (`Enemy.Staged`) and trial
  enemies never run, nor do orcs and werewolves. A pack wolf below half health runs once the leader of its camp
  (`Def.Leader`) has fallen (`Enemy.PackLed`). `net-tests/MoraleTests.cs`; the audit is `design/monster-audit.md`.
- Goblins and orcs also roam: a camp with a `route` in the zone file is a patrol (`Patrol.cs`) that walks its points
  and back, resting at each, and small wild camps sit in Westwood, the Northern Wilds and north of East Watch. The
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
  `PIXEL_TIME=21` starts a new game at that hour and `PIXEL_OFF=daytime` keeps it day. Rain (`GameTime.Rain`, from the day
  and hour alone, so every player sees the same) greys the sky and fog (`PixelStage.Rain`) and falls round the camera
  (`Ambient.Rain`); `PIXEL_RAIN=1` forces it. A schedule block with `"rain": true` holds only in a rainy
  hour (`Schedules.RainyHour`, judged at the middle of the hour so server and client agree) and then wins: the people
  who work outside go indoors. Fireflies drift after dark, lantern posts light (`Ambient.Lamps`) and window
  panes and lamp boxes glow (`prop.gdshader` `glow`, `Ambient.Glowing`, about two houses of three). The journal (L,
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
  from the step in the snapshot (`GameClient.UpdateScene`). Any step may carry `"if"` (skipped unless it holds when
  reached) and a waiting step `"timeout"` in seconds. The client replays the guards of past steps from its story copy,
  so only an `effects` step may test the player's place; turn such a test into a flag there (`prologue.netted`) and
  guard the shown steps by the flag. `StoryRulesTests` checks this, a timeout or `until` on every wait while the player
  has the controls, and that no line names a person without a proving condition (`Proofs`). `follow` sets `with.<id>` (cleared by `unfollow` and `despawn`); a loaded game brings followers back
  from those flags (`RestoreFollowers`). `follow` takes `{ "actor", "speed" }` for someone who closes in at a walk.
  More steps: `throw` (a prop actor such as `net`, role `prop:net_cover`, flies homing from the thrower to the player
  and holds them where it lands, even while a speaker walks up; `unroot` lets go), `drag` (the held player is pulled
  towards a point, the prop with them), `spawn`/`place` with `nearPlayer` offsets, `spawnEnemies` with a `home` they
  walk to and the scene's `level` (not the type's range), `homeEnemies` (send a tag elsewhere) and `despawnEnemies`.
  The condition `nearActor` (`{ "id", "r" }`) is answered by the server only. Rules found by playing:
  - People speak face to face: a line or a talk waits until speaker and player are within `SceneData.SpeakRange`.
    Until then the player walks freely and the objective reads "Go to <name>"; a follower or a speaker with
    `approach` comes over. A `bark` carries any distance and never holds the controls.
  - A player a scene holds still is no target for enemies. A scene that follows another must lock input itself:
    the client works the lock out from the steps of the scene playing.
  - A `checkpoint` is where a player not yet bound to the Anchor Stone wakes after dying. Without one, a lost
    fight in a scene wakes them in the village while the scene waits for kills for ever.
  - A talk's follow-up node keeps `Hud.SceneDialog`: without it the dialogue closed and reopened at its root.
  - A door nearer than the nearest person wins the E key: a follower once stood between the player and the Guild.
- The prologue is scenes `prologue_1_intro`, `prologue_2_cage` and `prologue_3_ambush`; a new game starts with it
  (`ServerOptions.Prologue`, `PIXEL_SKIP_PROLOGUE=1` skips it, test mode never plays it). Film, look creator, then the
  finders walk up to the fallen player; one who runs is caught by Gil's thrown net; they drag the player to the solid
  cage cart at the camp (69.9, 101, turned along the road, on flat ground). On the road the finders take a stand by
  the cart and fight the goblins from the ruins for real (`fight`); Hanne, Corb and Gil fall, Vosk flees, a goblin
  breaks the bars, the player jumps out, takes Hanne's Shortsword where she fell (`hannes_shortsword`, the wooden
  sword's numbers) and finishes the wounded goblins. `prologue.done` is set then; the quest `prologue_fallen` points to
  Mira, whose "Sign me up." registers the player (action `register`) and completes it. Rhea's trial is the step to rank
  E from level `GameServer.RankTrialLevel` (5; flag `rank.e`); a lost trial leaves the player registered. Rank D
  comes from level `GameServer.RankDTrialLevel` (9) after rank E: Mira offers it (action `beginTrialD`) and Rhea
  fights at full strength (`guild_examiner_d`, level 10, an air slash added; numbers are guesses; flag `rank.d`).
  `Guild.IsExaminer` covers both forms. Enemies may have up to eight abilities (`Enemy._cooldowns`).
  `scripts/prologue.ps1` plays it with `PIXEL_BOT=prologue` against a throwaway save and passes on `BOT done`;
  `-Contrary` plays the flight from the finders (`PIXEL_CONTRARY=flee`). `-Steps` prints every scene step.
  Keep the ruins camp and patrol away from the ambush on the road at (71.5, 72): level 4 goblins joined the first fight.
  Each finder has a look and weapon of their own (`finder_gil` dagger, `finder_hanne` sword, `finder_corb` club, Vosk
  a bow). Cage and ambush are shown from fixed side cameras until the player is free. Before moving a prop, measure the
  ground under its corners and check for water: the cart once sank half into the river bank.
- Roads are the highways of everyone who walks by themselves (`RoadMap` in `Schedules.cs`, built from the zone's
  `paths` in `World.Roads`): `Route` goes straight when the line is clear and short, else onto a road by a clear line,
  along the roads and off again, so nobody walks into a wall. NPCs on their day (`world/schedules.json`,
  `Schedules.RestOf`, `GameServer.ApplySchedules`) and the bots (`GameClient.BotGo`, which also jumps and plans again
  when it has not moved for 90 ticks) both use it. Where no road lies within 30 m (indoors, the wilds) the walk is
  straight: a fallback to the nearest road anywhere once sent the bot out of the Guild hall into a wall.
  `net-tests/RoadTests.cs` walks real routes in the zone. New places people go to need a road near them.
- Days (`world/schedules.json`): per person, blocks `from` an hour: `at` a place (with `yaw` in degrees or `face` a
  point, and a `pose` such as `sit`) or `home` a door, where they are out of the world. `via` is where someone comes
  out who was indoors far away (the Guild hall's people). Each game hour the server walks everyone to their new place
  (`Actor.WalkTo`); a jump in time puts them there. The client works the same resting place out by itself, so only
  walkers are sent. A scene that needs someone outside their hours places them first and despawns them after
  (Elda waits up in the prologue). A figure's front is -Z at yaw 0 (`Actor.Facing`): the other sign walked backwards.
  Door points must lie about a metre clear of the house's boxes; `net-tests/ScheduleTests.cs` checks every place.
  `by` lists points walked last on the way in and first on the way out (`Schedules.Way`): round a table to the far
  bench, round the end of a fence, into Wren's stall by its open end. `ScheduleTests` walks every leg of every day
  and fails when one crosses something solid, so give a new seat or a fenced place its `by`.
  A block's `pose` may be `work`: the person swings the item in hand again and again (`RigLook.WorkEvery`,
  `WorkHeavy`, quiet: no slash trail, `PlayerRig.Quiet`), with a thud close by. The Woodcutters' Camp east of East Watch
  (a track from the East Watch road) has Oswin and his nephew Jory chopping at their blocks and the sawbuck, eating
  at the fire at noon and in the evening and sleeping in their cabin; the yard in the middle is kept open so their
  walks go round the props (`by` through it).
  Fisher's Landing on Mirror Lake's east shore (a second pier, a rowboat, a fish rack, the hut over the road) has old
  Tobb casting from the pier end with his rod (`work` every 7 s), resting on the bench at noon and gutting at the rack.
  Shepherd's Croft north of the King's Road (a stone pen with a gate, sheep inside, a cottage and well) has Brin
  tending her flock with a crook. The Old Watchtower (`tower_ruin`) in the Northern Wilds is a goblin lookout.
  The Last Lantern, an inn on Mirror Way before the south pass: Marta keeps it, Aldous the pilgrim walks to the
  Pilgrim Shrine each morning and back, Fennick the peddler hangs about his cart. All the new people have voices.
  The Bent Nail (Hearthmere's tavern) has a common room off the map at (420, 60), behind the tavern door (portal at
  17.4, -2): `asset-pipeline/interiors.py` builds `inn_shell` (floor, ceiling, beams, non-solid), `inn_walls_ns`,
  `inn_walls_ew` and `inn_fittings` (hearth, bar); a closed ring of walls measured as one model fills solid, room and
  all (`collision.py` fills enclosed cells), hence the split. Dorran keeps the bar. The regulars sit in the yard from
  six and go in at eight, or at once in the rain (rain blocks), Corwin from eight. Walks into and out of an interior
  go through its door (`Schedules.Door`, `Schedules.Way`; `Actor.Step` steps across the gap); someone at home who is
  due indoors is simply there. `PIXEL_BOT=tavern` (with `PIXEL_TEST=1 PIXEL_SPAWN=22,-5`) walks to the door, goes in
  and steps up to the bar, printing `BOT` stages and `BOT done`.
  The tavern yard (three tables east of the tavern door, footpath from the south road) fills from six: Corwin keeps
  his bench all day, the Brightwater Four (Edwin, Moss, Fen, Rook) have the south table. Mornings the Four meet by
  the Guild door, Osric and Anwen are at the mill, Hamm, Maddy and Tobias on Harrow Farm, Tam by Hollis's counter.
- Chats (`world/chats.json`, `Chats.cs`, `GameClient.Overhear`): little exchanges between two people at rest near
  each other (within 12 m), played line by line as barks with voices when the player stands within 13 m and the
  `when` holds; not repeated for four minutes. `generate_voices.py` records chat lines too. `net-tests/ChatTests.cs`.
- People (`world/people.json`, `People.cs`): `barks` are lines said in passing, drawn over the speaker's head
  (`Hud.Barks`, `GameClient.Temper`), and `moods` the face they wear. The rest of the file (tones, judgements, bonds,
  `about`) belonged to the dropped story and is unused. Only people with a file in `world/dialogue` get a "Talk to"
  prompt (`StoryTests.ThePeopleWhoServe_HaveSomethingToSay_AndOnlyThey` lists them).
- Rumours, faction plans and the days 3 to 10 events are gone (`world/rumors.json` and `world/agendas.json` are empty);
  the systems remain in code. A conditional prop's collision comes and goes with it (`Aabb.Flag`/`Unless`,
  `World.HasFlag` fed from the server's world state and the client's copy), so the finders' cage holds the player.
- Until `prologue.done` only what a scene sends comes for the player; the wild leaves them be unless struck first.
- A weapon picked up with empty hands is worn at once (the prologue's player has none until the wooden sword).
- Interiors live outside the map on platforms (the guild at x 420, the trial ground at x 480, the Sunken Archive at
  x 560). Doors are portals; the zone audit's flood fill walks through them. Dungeons are built from pieces in
  `asset-pipeline/dungeons.py` (floor and ceiling non-solid, walls, shelves and pillars measured) and a ceiling wall
  box; past `ArchiveX` the client dims the light (`PixelStage.Underground`) and rune lamps glow (`ZoneView`). The
  archive's way in is a cave mouth beside Silver Falls (-42, 134). Greystone Mine (x 720, the warren's shell with lanterns, goblins that raid the quarry, a stoneback, a cache) is
  reached by a hole at the foot of the quarry's cut face (148, 49). The Goblin Warren (x 640, warm torches) is reached by
  a burrow just west of the goblin fort (70, -114). A dungeon platform must reach past every wall piece's corners, or
  `Zone.Settle` sinks the piece towards the terrain outside.
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
  The new monsters have their own (crow caw, dive and hurt; wisp hum, bolt and death; stoneback shell; vine lash and
  hurt; fish splash and flop; thornback charge, quill burst, war horn, snare, rune wave). `LOOPS` in the same script
  are 20 s beds faded at both ends: `Ambience.cs` plays each on two players that overlap by the fade, eased by
  `Ambient.Night` and `Ambient.Rain`: birds by day, crickets at night, rain (a little indoors), running water within
  25 m of a river or 40 m of the falls, dripping stone underground. Run Godot's `--import` after generating.
- Voices: Mira, Rhea and the finders (Vosk, Gil, Hanne, Corb) speak. A person with a `voice` in `world/people.json` (ElevenLabs voice id and settings)
  speaks. `python asset-pipeline/generate_voices.py` records their dialogue nodes, barks, `fight` shouts, `death` words and scene lines (`say` by actor or name) to
  `client/assets/voices/<npc>/<first 16 hex of the text's SHA-1>.mp3` (not in git; `--dry` counts characters, `--voices`
  lists voices; only new or changed lines are paid for). `Voices.cs` finds a line by the same hash: dialogue lines play
  flat, barks from the speaker's place, a line without a file stays silent. Lines with `{gold}`-style fills and stage
  directions in brackets are never recorded. The client prints `VOICE <npc> <hash>` for each line said.
- A snapshot must fit one UDP packet, so the server sends each player only nearby enemies. `GameServer.Fit` trims
  the farthest shots, drops and enemies until it is under `MaxSnapshotBytes` (a rain of arrows over a camp in test
  mode reached 1065 bytes, and LiteNetLib's refusal killed the server); `UdpTransport.Send` drops a packet that is
  still too big instead of throwing. The server's errors go to `export/previews/server-errors.log`, the client's
  output to `export/previews/client.log`, and a crashed .NET server also leaves its stack in the Windows Application log.
- Pose check: run the Godot console executable with `--path client res://scenes/pose_test.tscn -- <folder>`.
- Regenerate textures, palette and UI font with `python asset-pipeline/generate_textures.py`.
- Zones live in `world/zones/*.json`: the layout is authored, the scattered detail comes from the seed.
  Meadow Valley is 600 m across: the old 360 m valley is ringed by `ridges` (mountain chains along a line, ends sinking
  into walkable passes), and beyond them lie Greyfang Forest (wolves, south-east), Moonfell (south-west) and the
  Ironjaw Warcamp (north-east). `grounds` paint forest floor, blight or trampled earth on terrain and grass. Props for
  these lands come from `asset-pipeline/lands.py`. The terrain is drawn in 60 m chunks so off-screen ground is culled.
  The mountains round the zone are `Zone.Rim`: rounded corners, a foot that wanders, low foothills before it, and
  `passes` (a point on the border, `width`, `floor` metres climbed to the border, `to` the region beyond) where the
  wall opens into a saddle. Four passes: west (Thornwood), east (Ironjaw Highlands), south (Mirror Coast) and
  south-west (Moonfell Downs). Keep a hill's height at or under 0.26 of its radius (about 20 degrees at its steepest);
  the three crags at Silver Falls are steep on purpose. The audit and what comes next: `design/meadow-valley-rework.md`.
- Blender: `D:\Program\Blender Foundation\Blender 5.2\blender.exe -b --factory-startup -P <script>`.
- Play: `powershell -ExecutionPolicy Bypass -File scripts/play.ps1` opens the main menu (`MainMenu.cs`, scene
  `menu.tscn`, the project's main scene): Continue (newest save), New Game (a named slot, `SaveFile.SlotFor`), Load
  Game (`SaveFile.List`: day, level, place; Delete asks first), Settings, Quit. A choice starts the game's own server
  for that slot (`LocalServer`, a Release build: the zone takes about 19 s to build there, 34 s in Debug; the slow part
  is `Zone.Scatter`) with `PIXEL_PARENT`, so the server stops after the quit save or when the game is gone (saving
  first). Esc in the game then offers Save and Quit to Menu. A host on the command line skips the menu, so `-Test`,
  `-Capture`, bots and the studio start the server from the script as before (`PIXEL_SLOT` etc.). `-MenuShots
  <folder>` photographs the menu pages; `PIXEL_MENU_CHECK=1` plays the loop once (new game, back to the menu, the
  save is there) and prints `MENU check passed`. The background is the studio shot `village_drone`
  (`client/assets/ui/menu_bg.jpg`). Add `-Test` for test mode (gear kit). `PIXEL_FRAIL=1` gives the Guild examiner one hit point (the full check sets it).
- The intro is a film (`client/assets/video/intro.ogv`, Ogg Theora at 720p from `refs/isekai_inkprint_v1.mp4`:
  `ffmpeg -vf scale=1280:720 -c:v libtheora -q:v 6 -c:a libvorbis -q:a 4`), the scene step `video` (`IntroVideo.cs`).
  The film keeps its own clock and the client moves the scene on when it and the title after it have played (the
  server waits at most twice the length); Space, Enter or Esc skips it. The bot skips after two seconds unless
  `PIXEL_FILM=full`. The client's HUD does not lay out its children: size a control from the viewport.
- Live check: add `-Capture <folder>` to that command. A bot plays, saves frames and prints a `STATS` line.
- Minimap (`Minimap.cs`, top right, north = -Z up): the studio's `map` job renders `client/assets/ui/minimap.png`;
  run it again after changing terrain, roads or buildings. Buildings are marked by model (`Minimap.Landmarks`), and
  places appear once found (`Settings.Discovered`, saved in `user://settings.cfg`).
- Rhea's training (`Tutorial.cs`, `HudTutorial.cs`, scene `tutorial`): offered by Mira after signing up ("Later."
  defers it; her default node keeps "Train with Rhea"). Five lessons (light, heavy, block, dash, slide), each counted on
  the server and flagged `tutorial.<lesson>`; Rhea cannot be hurt, stands still in the striking and sliding lessons,
  her blows stop at `Tutorial.MaxBlow` and never kill. Backspace (`Command.SkipTutorial`) skips. Bots skip it except
  `PIXEL_BOT=tutorial`. `TutorialTests`. Fighting actors (`fight` step) shout `fight` lines and say `death` words from
  `people.json`, with the voice when recorded, else a generic shout or cry; goblins shriek or cackle as they strike.
- The dialogue portrait renders only the speaker: their meshes join `PortraitLayer` while they talk and the portrait
  camera culls everything else, framed from the body's front on an eased head position.
- Navigation: `Navigation` (shared) gives the target of the tracked quest step (the prologue first, then jobs): a
  person's place for `talk`, the nearest camp for `kill`, where the item drops for `bring` (then Mira once the bag
  holds enough), or the step's `at`; `"route": false` shows nothing (in the cage, in the fight). A place behind a door
  is reached through the door (`Navigation.Reachable`). `Navigator` (client) adds the player's own mark (kept per
  save in `user://waypoints.cfg`, cleared on arrival), works out the way with `RoadMap.Route` (again when the target
  moves or the player strays 6 m) and lays gold chevrons on the ground for 40 m ahead (opaque: the game does not draw
  alpha materials; `PIXEL_OFF=trail` turns them off, measured at no cost). The minimap draws the way and the target,
  or an arrow on its rim with the metres. M opens the big map (`WorldMap.cs`, `Hud.MapOpen`): wheel zooms, drag
  moves, a click sets the mark, a click on it or a right click clears it. `PIXEL_BOT=map` marks the Old Ruins, follows
  the trail and opens the map. `NavigationTests` check that every quest step with a route has a place.
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
  inside a camp it dies at once and the rest of the run measures the village. `PIXEL_OFF=fog,ssao,glow,shadow,batches,grass,shake` switches features off (shake: the camera kick on
  landed blows and hits taken, `GameClient.Kick`).
- Check after a change: `powershell -ExecutionPolicy Bypass -File scripts/check.ps1` runs the tests and the Guild
  story live, and lists script exceptions from the live run: one exception a frame freezes the HUD and the player
  without crashing. `FreezeWatch` (client) writes `FREEZE ... last mark: <part of the frame>` to the client's output when no
  frame begins for 4 s, and `FREEZE over` when it moves on; check.ps1 prints those lines. `PIXEL_FREEZE_TEST=1` sleeps
  the main thread once to prove it. Add `-Jobs monsters,world` (studio jobs whose looks changed) for pictures; a server or AI
  change needs none. `-Jobs all` photographs everything and takes about ten minutes, so use it only when asked.
- Photo studio: `scripts/studio.ps1 -Jobs people,animals,monsters,motion,attacks,models,world,hitboxes,map` needs no server and writes
  labelled contact sheets to `export/previews/studio/sheets`. Read them after every visual change. World shots
  live in `client/studio/shots.json`; add one for each new place. `PIXEL_SHOTS=guild,arena` films only shots
  whose names start with those words.
- `net-tests/ZoneAuditTests.cs` walks the real zone: keep every door, camp and bridge reachable.
- Collision is measured from the models: building a model writes its boxes to `world/collision.json`
  (`asset-pipeline/collision.py`). Never hand-size a hitbox; rebuild the model. Plants and models with
  platform floors are listed as non-solid in `Zone.NonSolid`. Grass never grows inside a solid box.
- Places where something ended (`design/world-plan.md` 8.5), each a region found on the map and a `place_*` studio
  shot: the Burnt Farm by the west pass (a charred `burned_barn`, a `cart_wreck` with goblin arrows, a farm track),
  the Fallen Bridge on Silverbrook (`bridge_broken_end` on each bank, origin on the bank, stub along +X; `Zone.Settle`
  keeps its origin height), the Hunters' Camp in Greyfang (`hunters_rack`) and the Pilgrim Shrine on Mirror Way
  (`wayside_shrine`, a short track from the road). Crowfield on Moonfell's edge (`battle_debris`, `fallen_banner`, a
  `dead_tree`, blighted ground) is the old battlefield where the carrion crows gather. Studio shot `eye` and `at` heights are above the ground there.
- The rim mountains (`Zone.Rim`) have ridged crests (`Crest`) and cliff bands (`CliffBand`, warped so they never ring
  like a stadium); the terrain shader lays snow only on ledges and tops by the world normal. A GLSL variable cannot be
  called `flat`.
- Camp stockades are never a closed ring (author, 2026-11-11: closed camps looked like pens). Build them from
  arcs of `palisade` (9 m), `palisade_half` (ends a run; its snapped end is local +X, so it joins the piece on its
  -X side) and `palisade_broken` (a 3 m breach a walker fits through, `BrokenStockades_LetAWalkerThrough`), with
  open ground between the arcs held by a `spike_wall`, skull pikes or a log pile. The rails are on the camp side
  (the game's +Z at yaw 0). Studio shots `goblin_camp_east`, `goblin_fort_west`, `warcamp_west` show the open sides.
- Studio model shots: `PIXEL_MODELS=cottage,bridge` photographs only those models, from five sides, with
  their collision boxes drawn.
- Models: edit `asset-pipeline/build_meshes.py`, then `scripts/build_assets.ps1` builds and imports them.
  Name models to rebuild only those: `scripts/build_assets.ps1 guild_interior beast_cow`. Animals are
  `beast_*` (asset-pipeline/beasts.py) and goblin parts and camp props come from asset-pipeline/goblins.py.
  Beasts keep their parts (body, neck, head, ears, tail, legs) as separate nodes for AnimalView.
- Animals live in herds (`EnemyAnimal.cs`): graze, watch (`alertRange`), bolt (`aggroRange`), and spread a
  scare through the herd. `net-tests/AnimalTests.cs` checks each behaviour on an empty field.
  Harmless animals in another coat (`fox`, `mountain_ram`, `white_hart`, `moor_sheep`) are `beasts.recolour` copies
  of the wolf, goat, deer and sheep specs (brown hares are a fox build with long ears, mallards a hen in other
  feathers: `beasts.chicken(duck=True)`, on the lake shores); `AnimalView` maps each to the kind it moves and sounds like. Give a new
  one loot from items that exist. Thematic plants: `giant_mushroom` (Whispering Woods, Emerald Jungle), `heather`
  (Moonfell, the southern foothills). A `rockfall` closes each pass road until the region beyond is built.
  Thornbacks (Emerald Jungle; kind `wolf`, behaviour `charger`, the boar's build with thorns, `EnemyCharge.cs`): a gore
  and a real `charge` that runs the whole way at `Enemy.ChargeSpeed`, gores once on the way, runs on past and leaves
  them winded; a charge that rams something solid stuns them (`Enemy.RamStunTicks`, client "Stunned!"). Without a
  turn they pace at `Enemy.ChargeRing` for a run-up (a charge needs no turn). The client paws dirt in the wind-up,
  runs head down and tosses the tusks on a gore (`AnimalView.AnimateCharger`). The rare Briarking's `quillBurst`
  throws `GameServer.QuillCount` thorns (`ProjectileKind.Thorn`) all round. `net-tests/ThornbackTests.cs`.
  Auren sentinels (kind `sentinel`, `PlayerRig.BuildSentinel`, parts from `asset-pipeline/sentinels.py`, the greatsword
  `weapon_sentinel_blade`): `statue` enemies stand still on their `Post` facing the camp's `yaw` until a player comes
  within `aggroRange`, never flinch or slide when struck, and freeze in the client (`GoblinView`) while asleep.
  `frontGuard` scales blows from the front (client: sparks and "Guarded"), `backBonus` blows from behind
  (`Enemy.GuardSide`, `Enemy.GuardCone`). Two flank the Sunken Archive's door, two stand in the Old Ruins. The rare
  Archive Warden (back wall of the archive) adds `runeWave`: three straight lanes (`RuneWave`, `HazardKind.RuneWave`,
  telegraph `lanes`, client `RuneWaveNode`) with gaps to stand in; enemy hazards with a `HalfWidth` hit as strips.
  `net-tests/SentinelTests.cs`. `PIXEL_MONSTERS=auren,archive` photographs only those monsters in the studio.
  The deep Sunken Archive (design/dungeon-archive.md, laid out by `python tools/deep_archive.py`, whose pieces carry
  `"deep": true` in the zone file so a rerun replaces only them): an arch in the first hall's back wall, the Drowned
  Stair, the Flooded Stacks (sentinels, wisps out at any hour underground, slimes), a secret wall
  (`archive_secret_wall`, `unless: archive.secret`) opened by the Warm Glyph (role `secret`), the hidden vault with
  a Rune-locked Chest (a cache with `requires: drowned_key`, `tools/caches.py`), and the Keeper's Hall: the boss
  `archive_keeper` (`boss: true` shows its bar at the top, `killFlag: archive.keeper` opens `archive_gate` on the way
  out, a portal in the daylit alcove to Silver Falls). The generic ability effect `summon` (`summon`, `count`,
  `life`) calls up helpers, one lot at a time (the Keeper's page wisps). Job "The Keeper Below" from level 8.
  `net-tests/DeepArchiveTests.cs`; studio shots `deeparchive_*`. `PIXEL_BOT=archive` walks it.
  Barrow wisps (kind `wisp`, `WispView`, `EnemyWisp.cs`) haunt the Stone Circle at night only: a camp with
  `"night": true` fades at dawn (`GameTime.IsDark`, no loot) and rises at dusk. They keep their distance, flare (`spin`) at a player
  who closes in, and loose `wispBolt` (`ProjectileKind.WispBolt`, `Projectile.Homing` turns it after its player). A camp's `wards` are its
  standing stones: a player within `Enemy.WardReach` of one puts the wisps out (`EnemyAction.Dim`: no casting, they
  drift to the stone, take `Enemy.DimTaken`), and bolts fizzle there. The server keeps wisps on the ground; the client
  draws them at chest height. The rare Lantern of the Barrow's `wispSplit` makes it three lights, two false
  (`lantern_decoy`, `EnemyDef.Decoy`: same name and health, gone at one touch); only the true one casts a shadow.
  `net-tests/WispTests.cs`.
  Carrion crows (kind `crow`, behaviour `flock`, `CrowView`, `EnemyCrow.cs`) circle the player at `Enemy.FlockRing`
  out of reach (`Enemy.Aloft`; `Enemy.Untouchable` also covers shadow and dodge, and every player blow skips it),
  `dive` with a line warning (a close blow, so they take turns) and peck on the ground afterwards, open to blows.
  A struck crow scatters its flock (`ScatterTicks`). They live on Crowfield, an old battlefield on Moonfell's edge
  (`battle_debris`, `fallen_banner`). The rare Gallows Rook's `rookCall` lines up `GameServer.RookFlock` one-blow
  `gallows_crow`s that dive one after another (`Enemy.CastAt`). `net-tests/CrowTests.cs`.
  Rage and dance (`net-tests/RageAndDanceTests.cs`): `enrageAt` (share of health) sets `Enemy.Enraged` once, then
  `enrageWindup`/`enrageRecover` scale its wind-ups and recoveries (never under `Enemy.MinWindupTicks`) and
  `enrageTaken` the damage it takes: the goblin brute winds up faster below a third, the orc berserker recovers twice
  as fast below half but takes a quarter more. The shaman's `warDance` (only with goblins of its camp within
  `Enemy.DanceRadius`) gives them `HasteTicks`: faster feet, quicker wind-ups, cooldowns twice as fast. Snapshots
  carry both in the enemy's state byte (`EnemyView.Hasted`/`Enraged`); the client shows green flames and red embers.
  The orc huntress throws a hand axe (`axeThrow`, `ProjectileKind.ThrownAxe`, tumbling in the client).
  The rare Goblin Warchief (Goblin Warren) blows `hornCall` (every goblin within `GameServer.HornReach` not yet
  fighting comes) and `shieldBash` (throws the player back). Goblin Trappers (by the Old Ruins) `layTrap`: a snare
  (`HazardKind.Snare`, client `SnareNode`) partway to the player that holds whoever steps in it (`Snare.HoldTicks`
  of slow) and is spent; at most `Snare.PerTrapper` each, lifted when the trapper dies. `net-tests/GoblinChiefTests.cs`.
  Stonebacks (northern foothills; kind `wolf`, `shells`, `EnemyShell.cs`): struck, one pulls into its shell
  (`EnemyAction.Shell`, `Untouchable`), comes out once the player stands back `Enemy.ShellCalmDistance` for a moment and
  lunges (`dashStrike`); the rare Mossback Elder's `shellSpin` slides at the player, glancing off what stops it
  (`Enemy.Glanced`), then stands dizzy. The client tucks head, legs and tail in (`AnimalView.TuckIntoShell`).
  `net-tests/StonebackTests.cs`.
  Vinelashers (Emerald Jungle; kind `vine`, behaviour `rooted`, `statue`, `VineView`) never move: they bite up close
  and `vineWhip` a player within reach, yanking them in (`GameServer.Lash`, `GameServer.VinePull`); the rare
  Stranglethorn's `vineGrab` throws three vines in a fan that also hold (`GameServer.VineHold` of slow).
  Vine lashes send a visible tip (`ProjectileKind.Vine`, no hits of its own). `net-tests/VineTests.cs`.
  Mirefins (Mirror Lake; kind `fish`, behaviour `lurker`, `EnemyFish.cs`, `FishView`): the camp's middle and radius are
  the lake. Under water (`Enemy.Submerged`) only the fin shows and blows pass through; they shadow a player along the
  shore and `pounce` only at one within `Enemy.ShoreReach` of the edge, surfacing in the wind-up, then lie stranded
  (`Stagger`, `Enemy.StrandedTicks`) and wriggle back. The rare Old Gulper's `waterJet` throws the player back
  (`Lash` with a negative pull). `net-tests/FishTests.cs`.
  Bandits (Bandit Hollow in the south woods west of the Last Lantern; kind `human`, looks from `NpcLooks.Enemy`,
  human voices in `GoblinView`): cutthroats (flankers with a sword), archers and the rare Red Hand Garrow. Marta's
  job "Bandits on Mirror Way" sends the player there from level 5.
  Greystone Quarry north-east of the Old Ruins (three `cliff` blocks as the cut face, `stone_blocks`, `quarry_crane`,
  a track from the woodcutters' track): Bors swings a pick at the face, Wilm dresses blocks and works the crane.
  War drum (`net-tests/WarDrumTests.cs`): camps with a `drum` point (every Ironjaw camp) answer it. A fighter that
  notices a player within 45 m of the drum runs to it instead (`Enemy.RunsToDrum`, one at a time via
  `ClaimDrum`), beats it for `Enemy.DrumTicks` (`EnemyAction.Drum`), and then every camp of that drum within 60 m
  turns on the player (`SoundAlarm`, `Stats.Alarms`); the drum then rests a minute. Any blow on the drummer stops it.
  A new monster can often be data: a kind picks the view (`animal` and `wolf` use
  `AnimalView`), abilities pick the attacks. Rare variants and goblin and orc proposals: `design/monster-audit.md`.
  No light blow has a wind-up under 16 ticks.
- Combat feel (client only, nothing in the simulation waits): a landed blow holds the swing and the enemy figures
  still for `GameClient.HeavyHitStop` (heavy, spin, ground pound) or `LightHitStop`, then the swing plays twice as
  fast until it has caught up (`ShownAttackTick`); `PIXEL_OFF=hitstop` turns it off. An enemy open to blows (a
  stagger, a wisp dimmed by its stone, a crow pecking after its dive, a thornback winded after its charge: `Strike`
  or `Splat` right after a `Leap` for `flock` and `charger`) says "Open!" once and pulses a green ring
  (`GameClient.ShowOpening`). Tab locks the camera on the hostile enemy nearest the middle of the view
  (`ToggleLock`, within 22 m; let go at 30 m, on death or a second Tab); the camera leans to it while the mouse still
  aims, and four gold arrowheads mark it (`Hud.DrawLockMark`). The bag is on I only. `PIXEL_LOCK=1` makes a bot lock on.
- Hair and cloth are `ClothChain` grids; the toon shader bends meshes along them. Rig looks live in `Dialogues.cs`.
- The server waits for a late input instead of skipping it. Catching up spends only ticks missed while stalled,
  so no client can run faster than real time: extra inputs are thrown away.

## Done means

- **Play it as a contrary player.** Before a scene or a job is done, run it as someone who walks away or arrives in
  another order. Every step of a scene that waits while the player has the controls needs a `timeout` or an `until`.
- **The player always knows what to do.** Every quest step has an objective that names a place and a target.
- **Check with eyes.** Read the frames of the player's actual path, not only studio sheets.
- **Report what was not checked.** A passing test is not a played game: say what was checked and what was not.

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
