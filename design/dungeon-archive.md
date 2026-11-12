# Bod 6: The Sunken Archive jako skutečný dungeon (návrh 2026-11-12)

Dnešní Sunken Archive je jedna síň 50 x 40 m za jeskyní u Silver Falls. Návrh z ní dělá dungeon o pěti
částech s bossem, skrytou cestou a zkratkou ven, a zároveň dává smysl systémům z bodu 5 (vaření, byliny,
rybaření, kovárna): každý z nich je potřeba na přípravu, na odměnu nebo na tajemství dungeonu.

## 1. Proč zrovna Archive

- Má nejsilnější námět (Auren, runy, utopená knihovna) a už má strážce: dva Auren Sentinely u vstupu a vzácného
  Archive Wardena u zadní stěny. Warden se stane strážcem dveří do hlubších částí.
- Stojí na samostatné plošině v x 534..586; směrem k záporné ose z je volno, dungeon se tedy dá protáhnout bez
  zásahu do ostatních interiérů (Guilda x 420, aréna 480, Warren 640, důl 720).
- Vstup je u Silver Falls (-42, 134), kousek od Mirror Lake: to propojí rybaření s tajemstvím (kap. 5).
- Úroveň: nepřátelé 5 až 8 v první síni; nové části 7 až 9, boss 10. Cíl pro hráče po ranku E, před rankem D.

## 2. Mapa (pohled shora, sever = -z)

```
              z  21  +---------------------------+   vstup (portál z jeskyně)
                     |   1. VSTUPNÍ SÍŇ (stávající) |  sentinely, slizy, goblini, šaman
              z -19  +-----------[ oblouk ]-------+   Archive Warden hlídá oblouk
                                 |   |
              z -22..-36         | 2 |  UTOPENÉ SCHODIŠTĚ: chodba 8 m, kanál vody, 2 sentinely v nikách
                                 |   |
              z -36  +-----------+   +-----------+
                     | 3. ZATOPENÉ REGÁLY        |==[ tajná stěna ]==+---------------+
                     |  uličky mezi regály,      |   (glyf, E)        | 3b. SKRYTÝ    |
                     |  tůně, padlé regály,      |                    |  TREZOR       |
                     |  bludičky a slizy         |                    |  truhla na    |
              z -66  +-----------+   +-----------+                    |  Auren klíč   |
                                 | 4 |  RUNOVÁ BRÁNA (předsíň bossa)  +---------------+
              z -70..-76         |   |
              z -76  +-----------+   +-----------+
                     | 5. STRÁŽCOVA SÍŇ          |  kruh pilířů, dva rozbité sentinely
                     |      (boss: The Keeper)   |
                     |        [ brána ven ]      |  za bossem: zavřená brána, za ní šachta světla
              z-106  +---------------------------+  (portál ven k Silver Falls, otevře se po prvním zabití)
```

Rozměry (herní souřadnice, střed x 560):
1. Vstupní síň: x 535..585, z -19..21 (beze změny, jen dva prostřední kusy zadní zdi nahradí oblouk).
2. Utopené schodiště: x 556..564, z -22..-36, strop 5 m, vodní kanál uprostřed pokračuje ze síně.
3. Zatopené regály: x 540..580, z -36..-66, strop 6,5 m; tři řady regálů tvoří uličky (žádná slepá past bez
   cesty ven), dvě tůně, padlé regály jako kryt proti lukostřelcům.
   3b. Skrytý trezor: x 580..594, z -56..-46 za tajnou stěnou ve východní zdi.
4. Předsíň s runovou bránou: x 555..565, z -66..-76 (brána je ozdobná, průchozí; boss se probouzí až uvnitř).
5. Strážcova síň: x 543..577, z -76..-106, strop 8 m, osm pilířů v kruhu (kryt proti runovým vlnám), uprostřed
   kamenný kruh na podlaze, na jižní stěně zavřená brána ven.

## 3. Boss: The Keeper (Strážce stránek)

Obří Auren konstrukt, který dál hlídá knihovnu, i když knihy shnily. Stavba jako Auren Sentinel, jen větší
(size 1,7), s velkým mečem; stojí jako socha na podstavci, dokud hráč nevejde do síně (`statue`, aggroRange 14).

- Úroveň 10, kolem 2400 životů (odhad, neladěno). Zepředu kryje (`frontGuard` 0,5), do zad bere víc
  (`backBonus` 1,5): hráč se učí obcházet, jako u sentinelů.
- Schopnosti (vše z existujících efektů, aby se dalo hned stavět a testovat):
  - `melee` široký sek zblízka, `slam` kruh kolem sebe (nápřah 44 tiků, varování na zemi),
  - `runeWave` tři pruhy run přes síň s mezerami (jako Warden): pilíře a mezery jsou úkryt,
  - `dashStrike` výpad na hráče, který utíká,
  - nové `summonWisps`: vyvolá dvě bludičky stránek (`page_wisp`, slabé, žijí 20 s, střílí bolty),
    s dlouhým cooldownem; obecný efekt `summon` s typem a počtem v datech, aby šel použít i jinde.
- Fáze 2 pod 50 % (`enrageAt` 0,5): rychlejší nápřahy a zotavení, bere o 15 % víc (otevřenější).
- Pruh životů bosse nahoře na obrazovce, dokud boss bojuje (`boss: true` v datech, HUD `DrawBossBar`).
- Smrt: flag `archive.keeper` (nové pole `killFlag`), který otevře bránu ven (rekvizita s `unless`) a zapíše
  poznámku do deníku. Boss se po 30 minutách obnoví, brána zůstane otevřená.
- Kořist: vždy `keeper_heart` (materiál na nejlepší kus u Tess), runové jádro, zlato; šance na knihu Rune Ward.

## 4. Skrytá cesta

- Ve východní zdi Zatopených regálů je jeden kus zdi jiný: prasklý, s jediným slabě blikajícím glyfem v nice.
  Nemá ceduli. Kdo stojí blízko, uvidí výzvu „[E] Touch the glyph“.
- Dotyk nastaví flag `archive.secret`: kus zdi (`archive_secret_wall`, `unless: archive.secret`) zmizí i se
  svou kolizí a za ním je chodba do trezoru. Deník zapíše poznámku.
- Nápovědy: Wardenova síň má u řečnického pultu poznámku (text pultu) o „glyfu, který není jako ostatní“;
  Tobb na molu občas utrousí, že Auren házeli klíče do jezera.
- V trezoru je truhla zamčená Auren zámkem (kap. 5) a volně ležící poklad (zlato, runové jádro).

## 5. Aby všechno k něčemu bylo (smysl rybaření, bylin, vaření a kovárny)

Každá činnost má dostat jasný důvod, ne jen prodej za pár mincí:

| Činnost | Proč ji dělat | Kde se to projeví |
|---|---|---|
| Vaření | Uvařené jídlo dá **Well Fed**: +15 % max. životů a pomalá regenerace na 10 minut (syrové ne). | Příprava na bosse a dlouhé výpravy. |
| Byliny | Lektvary u Wren (už hotové) a nový **Draught of Warding** (Glowcap + Moonpetal + rybí ploutev): o 25 % menší poškození na 60 s. | Boss, Ironjaw, rare monstra. |
| Rybaření | Ryby se vaří (Well Fed); vzácný úlovek **Drowned Auren Key** (asi 4 %) otevře zamčenou truhlu ve skrytém trezoru. | Tajemství dungeonu, nejlepší truhla. |
| Kovárna | Z `keeper_heart`, runových jader a Moonpetalu ukove Tess **Seal of the Keeper** (prsten, nejlepší ve hře). | Cíl po bossovi, důvod sbírat Moonpetal. |
| Zakázky | Nové opakované joby na nástěnce: šalvěj pro Wren, ryby pro Bent Nail, houby pro Guildu. Zlato a XP: sběr pomáhá k levelu. | Postup úrovní bez boje. |

Čísla (bonus, trvání, šance na klíč, odměny) jsou odhady k pozdějšímu měření, ne ladění balancu.

## 6. Zakázka a navigace

- Job „The Keeper Below“ na nástěnce od úrovně 8: zabít Strážce, odevzdat Mire (zlato, hodně XP). Navigace vede
  dovnitř přes jeskyni a dveře (`Navigation.Reachable`).
- Objektivy pojmenují místo a cíl: „Find the Keeper in the deepest hall of the Sunken Archive (Silver Falls)“.

## 7. Technika

- Kusy v `asset-pipeline/dungeons.py`: `archive_arch` (6 m zdi s průchodem), `archive_secret_wall` (zeď
  s prasklinou a glyfem), `archive_gate` (zavřená brána, solidní), `archive_gate_open` (otevřený rám),
  podlahy a stropy pro chodbu, regály a strážcovu síň (nesolidní, `Zone.NonSolid`), `keeper_plinth`.
  Uzavřený kruh zdí nikdy jako jeden model (flood fill v `collision.py`).
- Zóna: plošina se protáhne do z -108 a doprava na x 596 (trezor); stropní boxy pro kameru nad každou částí;
  světla run v `ZoneView` pro nové části.
- Server: obecný efekt `summon` (typ a počet z dat), `EnemyDef.Boss` a `KillFlag`, buffy hráče (`WellFedUntil`,
  `WardedUntil`) v `MaxHealth`, regeneraci a v příjmu poškození, buff v snapshotu pro HUD.
- Data: nové předměty (klíč, srdce, prsten, lektvar), zakázky (`tools/turnins.py`), deník, dialogy glyfu
  a truhly (role `secret`, truhla s podmínkou na klíč).
- Testy: audit zóny (vše dosažitelné, trezor po otevření), boss (probuzení, vyvolání, flag po smrti, brána),
  tajná stěna (kolize zmizí s flagem), buffy, rybí klíč, recept prstenu. Studio záběry každé části.
- Živě: bot projde dungeon (PIXEL_BOT=archive) a vyfotí cestu; boss s testovací výbavou.

## 8. Pořadí práce

1. Buffy (Well Fed, Warding) a sběrné zakázky: smysl systémů hned, i bez dungeonu.
2. Kusy dungeonu v Blenderu a rozložení zóny, studio záběry, audit.
3. Boss (data, summon, boss bar, killFlag, brána ven), test.
4. Skrytá cesta, klíč z jezera, prsten u Tess, zakázka „The Keeper Below“.
5. Živý průchod botem, deník, handoff.
