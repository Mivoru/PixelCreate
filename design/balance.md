# Balance report (measured)

Written by `net-tests/BalanceReport.cs` (`PIXEL_BALANCE=1 dotnet test --filter BalanceReport`) on 2026-11-12.
Each enemy at its lowest level against a player of the same level with Hanne's Shortsword (the starting
sword), no attributes and no other gear. Light blow: one real blow on the server. Kill time counts light
blows back to back, so a real fight (dodging, blocking, a running enemy) takes longer.

| Enemy | Lv | Health | Light blow | Blows to kill | Seconds | Its worst blow | Player health | Its blows to kill you | XP | Kills per level |
|---|---|---|---|---|---|---|---|---|---|---|
| Slime | 1 | 40 | 15 | 3 | 1,1 | 10 | 100 | 10 | 30 | 5 |
| Rare Slime (rare) | 1 | 70 | 15 | 5 | 1,8 | 12 | 100 | 9 | 90 | 2 |
| Goblin Raider | 1 | 34 | 15 | 3 | 1,1 | 7 | 100 | 15 | 30 | 5 |
| Goblin Archer | 1 | 28 | 15 | 2 | 0,7 | 6 | 100 | 17 | 30 | 5 |
| Goblin Skirmisher | 3 | 90 | 18 | 5 | 1,8 | 12 | 130 | 11 | 50 | 10 |
| Goblin Archer | 3 | 74 | 18 | 5 | 1,8 | 8 | 130 | 17 | 50 | 10 |
| Carrion Crow | 3 | 32 | 18 | 2 | 0,7 | 6 | 130 | 22 | 50 | 10 |
| Goblin Brute | 4 | 240 | 17 | 15 | 5,5 | 30 | 145 | 5 | 60 | 13 |
| Goblin Sharpshooter | 4 | 91 | 20 | 5 | 1,8 | 34 | 145 | 5 | 60 | 13 |
| Goblin Trapper | 4 | 96 | 20 | 5 | 1,8 | 7 | 145 | 21 | 60 | 13 |
| Thornback | 4 | 202 | 18 | 12 | 4,4 | 20 | 145 | 8 | 60 | 13 |
| Mirefin | 4 | 122 | 20 | 7 | 2,6 | 9 | 145 | 17 | 60 | 13 |
| Bandit Cutthroat | 5 | 192 | 19 | 11 | 4,0 | 18 | 160 | 9 | 70 | 16 |
| Bandit Archer | 5 | 146 | 21 | 7 | 2,6 | 26 | 160 | 7 | 70 | 16 |
| Wolf | 5 | 126 | 21 | 6 | 2,2 | 14 | 160 | 12 | 70 | 16 |
| Vinelasher | 5 | 174 | 21 | 9 | 3,3 | 15 | 160 | 11 | 70 | 16 |
| Stoneback | 6 | 325 | 20 | 17 | 6,2 | 18 | 175 | 10 | 80 | 20 |
| Barrow Wisp | 6 | 180 | 22 | 9 | 3,3 | 12 | 175 | 15 | 80 | 20 |
| Gallows Rook (rare) | 6 | 260 | 22 | 12 | 4,4 | 11 | 175 | 16 | 240 | 7 |
| Goblin Shaman (rare) | 7 | 420 | 24 | 18 | 6,6 | 26 | 190 | 8 | 270 | 8 |
| Auren Sentinel | 7 | 516 | 28 | 19 | 7,0 | 36 | 190 | 6 | 90 | 23 |
| Greyfang Alpha | 8 | 430 | 23 | 19 | 7,0 | 24 | 205 | 9 | 100 | 27 |
| Briarking (rare) | 8 | 590 | 22 | 27 | 9,9 | 28 | 205 | 8 | 300 | 9 |
| Stranglethorn (rare) | 8 | 590 | 26 | 23 | 8,4 | 22 | 205 | 10 | 300 | 9 |
| Old Gulper (rare) | 8 | 570 | 26 | 22 | 8,1 | 16 | 205 | 13 | 300 | 9 |
| Goblin Warchief (rare) | 9 | 660 | 22 | 30 | 11,0 | 18 | 220 | 13 | 330 | 11 |
| Red Hand Garrow (rare) | 9 | 720 | 22 | 33 | 12,1 | 26 | 220 | 9 | 330 | 11 |
| Orc Warrior | 9 | 512 | 22 | 24 | 8,8 | 28 | 220 | 8 | 110 | 31 |
| Orc Spearwoman | 9 | 440 | 24 | 19 | 7,0 | 24 | 220 | 10 | 110 | 31 |
| Mirrorfang (rare) | 9 | 516 | 24 | 22 | 8,1 | 22 | 220 | 10 | 330 | 11 |
| Shadowstalker (rare) | 9 | 470 | 25 | 19 | 7,0 | 24 | 220 | 10 | 330 | 11 |
| Mossback Elder (rare) | 9 | 760 | 22 | 35 | 12,8 | 24 | 220 | 10 | 330 | 11 |
| Lantern of the Barrow (rare) | 9 | 720 | 27 | 27 | 9,9 | 16 | 220 | 14 | 330 | 11 |
| Orc Berserker | 10 | 584 | 25 | 24 | 8,8 | 30 | 235 | 8 | 120 | 35 |
| Orc Huntress | 10 | 480 | 25 | 20 | 7,3 | 24 | 235 | 10 | 120 | 35 |
| Archive Warden (rare) | 10 | 980 | 33 | 30 | 11,0 | 42 | 235 | 6 | 360 | 12 |
| Ironjaw Swordmaster (rare) | 12 | 1070 | 25 | 43 | 15,8 | 30 | 265 | 9 | 420 | 14 |
| Ironjaw Spearcaller (rare) | 12 | 924 | 27 | 35 | 12,8 | 26 | 265 | 11 | 420 | 14 |
| Ironjaw Stormaxe (rare) | 12 | 1132 | 27 | 42 | 15,4 | 30 | 265 | 9 | 420 | 14 |
| Werewolf | 12 | 760 | 27 | 29 | 10,6 | 34 | 265 | 8 | 140 | 42 |
| Redclaw (rare) | 14 | 1290 | 29 | 45 | 16,5 | 36 | 295 | 9 | 480 | 17 |
| Moonleaper (rare) | 14 | 1224 | 29 | 43 | 15,8 | 48 | 295 | 7 | 480 | 17 |
| Blackfang | 15 | 1740 | 29 | 60 | 22,0 | 46 | 310 | 7 | 170 | 54 |

## What stands out (for the author to decide; nothing is tuned yet)

- Kills per level climb from 5 at level 1 to 30 to 55 from level 9: jobs soften it, but later levels grind.
- With the starting sword the Goblin Brute (15 blows) and the Stoneback (17) are tanks beside the 5 to 12 of their level.
- The Goblin Sharpshooter's power shot and the Archive Warden's rune wave hit hardest for their level (5 and 6 to kill).
- Gear, attributes and mastery raise the player's blow; this table is the floor, the starting kit.

Levels: Lv 1 needs 140 XP, Lv 2 needs 260 XP, Lv 3 needs 460 XP, Lv 4 needs 740 XP, Lv 5 needs 1100 XP, Lv 6 needs 1540 XP, Lv 7 needs 2060 XP, Lv 8 needs 2660 XP, Lv 9 needs 3340 XP, Lv 10 needs 4100 XP, Lv 11 needs 4940 XP, Lv 12 needs 5860 XP
