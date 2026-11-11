# Audit monster a AI (2026-11-10)

Prošel jsem všechny druhy v `world/enemies.json`, jejich schopnosti (nápřah, zotavení, prodleva,
varování na zemi), zvuky v `client/assets/sounds`, roli ve skupině (`EnemyTactics.cs`) a ústup.
Časy jsou v tikách serveru (60 za sekundu). Balanc se nemění (pravidlo projektu); u čísel, která
ovlivňují čitelnost, je jen návrh.

## Přehled

| Druh | Role | Lehký útok (nápřah) | Těžký útok, varování | Zvuky | Skupina | Ústup |
|---|---|---|---|---|---|---|
| slime, rare_slime | (skáče) | skok 0,3 s | plivnutí | vlastní sada (7) | ne | ne |
| goblin_skirmisher | skirmisher | melee **10 t (0,17 s)** | dashStrike 22 t, čára | goblin_* + shriek/cackle | ano, odskok po ráně | **nově ano** |
| goblin_brute | brute | slam 46 t, kruh | spin 54 t, kruh | goblin_*, club_slam/spin | ano | ne (stojí) |
| goblin_rapid_archer | ranged | šíp 38 t | salva 50 t, **bez varování** | bow_* | drží kruh | **nově ano** |
| goblin_sharpshooter | ranged | šíp 50 t | powershot 64 t, čára | bow_* | drží kruh | **nově ano** |
| goblin_shaman (vzácný) | support | fireball 34 t | ohnivá zeď 62 t, klín | fireball, fire_wall | za přední linií | **nově ano** |
| orc_warrior | brute | melee 22 t | airSlash 40 t, čára | orc_*, air_slash | ano | ne |
| orc_berserker | brute | melee 20 t | earthCrack 44 t, klín | orc_*, earth_crack | ano | ne |
| orc_spearwoman | skirmisher | melee 18 t | spearThrow 34 t, čára | orc_f_*, spear_throw | ano | ne |
| orc_huntress | flanker | melee 16 t | airSlash 34 t, čára | orc_f_*, air_slash | jde za záda | ne |
| orc_swordmaster (vz.) | brute | melee 16 t | combo 18 t, airSlashWide 34 t | orc_* | ano | ne |
| orc_spearcaller (vz.) | skirmisher | melee 16 t | summonSpears 40 t, **bez varování** | spear_summon | ano | ne |
| orc_stormaxe (vz.) | brute | melee 18 t | tornado 40 t, kruh | tornado | ano | ne |
| werewolf | flanker | dráp 18 t | pounce 28 t, kruh | werewolf_* | ano | ne |
| werewolf_alpha | brute | dráp 18 t | slam 40 t, pounce 28 t | werewolf_* | ano | ne |
| werewolf_redclaw (vz.) | flanker | dráp 17 t | fangSlash 30 t, čára | fang_slash | ano | ne |
| werewolf_moonleaper (vz.) | brute | dráp 17 t | superJump 54 t, kruh | super_jump | ano | ne |
| wolf, alpha_wolf | pack | kousnutí **14 t (0,23 s)** | pounce 26 t, kruh | wolf_growl/hurt/howl, **chybí smrt a kousnutí** | smečka, vůdce svolává | ne |
| mirror_wolf (vz.) | pack | kousnutí 15 t | klony | clone_summon | smečka | ne |
| shadow_wolf (vz.) | flanker | kousnutí **14 t** | shadowRun, bez varování (záměr) | shadow_sink/rise | ano | ne |
| zvířata | stádo | (prase 20 t) | — | vlastní sady | stádo, panika | utíkají |

## Zjištění

1. **Nikdo neustupoval.** Všechny bojové druhy bojovaly do smrti, i goblin s posledním životem.
   U goblinů to neodpovídá povaze (zbabělí, smečkoví). **Opraveno:** goblin (kromě brutů) pod
   čtvrtinou zdraví jednou za život ztratí odvahu, vzdá se svého tahu u hráče a 4 s utíká pryč
   a ke svému táboru; pak se otočí a bojuje dál, zahnaný do kouta. Goblini ze scén (prolog) a ze
   zkoušky neutíkají. Orkové, vlkodlaci a vlci stojí. `net-tests/MoraleTests.cs`.
2. **Příliš rychlé lehké útoky bez varování.** Goblin skirmisher (10 t), vlk a shadow_wolf (14 t),
   mirror_wolf (15 t) mají nápřah kratší než čtvrt sekundy. Hráč na ně nestihne reagovat blokem
   ani úhybem, jen je odnese. Návrh (čeká na autora, je to blízko balancu): nápřah lehkých útoků
   aspoň 16 t, nebo krátké bliknutí zbraně či tlamy na začátku nápřahu.
3. **Dvě silné schopnosti bez varování na zemi:** salva rapid archera a summonSpears spearcallerky.
   Návrh: salva dostane kruh v místě dopadu, kopí nad hlavou už viditelná jsou (tam stačí zvuk).
4. **Chybějící zvuky:** vlk nemá zvuk kousnutí ani smrti (používá growl/hurt), šaman nemá vlastní
   hlas, prase nemá útok. Návrh: `wolf_bite`, `wolf_die` (dva krátké zvuky z ElevenLabs).
5. **Smečka bez vůdce:** když padne alpha_wolf, zbytek smečky bojoval stejně. **Opraveno:** vlk pod
   polovinou zdraví po smrti vůdce svého tábora jednou uteče (`MoraleTests.Pack_LosesHeart_WhenItsLeaderFalls`).
6. **Šaman jako support** se drží za linií, ale neléčí ani nepovzbuzuje. Návrh: tanec, který na 5 s
   zrychlí blízké gobliny (čitelné: zelené plameny kolem nich).
7. **Orkové** jsou čitelní (každý těžký útok má varování 34 až 44 t a vlastní zvuk). Bez změn.
8. **Vlkodlaci** mají dobré varování u skoků; lehký dráp 17 až 18 t je na hraně, ale odpovídá síle.

## Pořadí dalších vylepšení (návrh)

2. Varování u salvy (bod 3).
3. Zvuky vlka (bod 4), až bude dovoleno platit generování.
4. Šamanův tanec (bod 6).
5. Nápřahy lehkých útoků (bod 2): rozhodnutí autora.


---

## Nová monstra, vzácné varianty a doladění goblinů a orků (2026-11-11)

Autor schválil všech sedm navržených monster (`design/meadow-valley-rework.md`) a chce ke každému
vzácnou variantu se zvláštní schopností. Úrovně a čísla jsou výchozí odhad, ne balanc.

### Postaveno

- **Thornback** (Emerald Jungle, 4 až 6, tři a dva kusy): rýpnutí zblízka a rozběh po přímce
  (`dashStrike`, nápřah 36 t s čárou na zemi). Po rozběhu stojí 64 t udýchaný: to je okno pro hráče.
- **Briarking** (vzácný, úroveň 8, jeden kus hlouběji v džungli): delší a silnější rozběh a k tomu
  zmítání (`spin`, kruh 3,8 m, nápřah 46 t), když ho hráč obstoupí zblízka.
- Nepostaveno z návrhu: omráčení po nárazu do stromu (server nezná náraz do překážky při rozběhu).
  `net-tests/ThornbackTests.cs`.

### Vzácné varianty ostatních (návrh)

| Monstrum | Vzácná varianta | Zvláštní schopnost | Kniha dovednosti pro hráče |
|---|---|---|---|
| Vinelasher | **Stranglethorn** | tři šlahouny naráz ve vějíři; kdo je chycen, je 1,5 s držen a ostatní šlahouny ho bijí | Vine Pull: přitáhne nepřítele k hráči |
| Mirefin | **Old Gulper** | spolkne vodu a vychrlí proud po přímce, který odhodí hráče od břehu | žádná |
| Barrow Wisp | **Lantern of the Barrow** | rozdělí se na tři menší bludičky; pravou prozradí, že jediná vrhá stín | Wisp Bolt: pomalá střela, která zatáčí za cílem |
| Auren Sentinel | **Archive Warden** | nabije runy a pošle po zemi vlnu ve třech pruzích s mezerami, ve kterých se dá stát | Rune Ward: krátký štít zepředu |
| Stoneback | **Mossback Elder** | v krunýři se roztočí a klouže po zemi jako kámen, odráží se od skal | žádná |
| Carrion Crow | **Gallows Rook** | zavolá hejno: pět vran nalétá postupně v řadě, každou jde srazit jednou ranou | žádná |

Pořadí stavby: Auren Sentinel a Archive Warden (ruiny a Sunken Archive nemají vlastního nepřítele),
Barrow Wisp, Carrion Crow, Stoneback, nakonec Vinelasher a Mirefin (nový druh pohybu).

### Goblini: co doladit

Dobré: role ve skupině, morálka, hlídky, lov. Slabé: všichni vypadají a zní skoro stejně a šaman
je jediný vzácný.

1. **Uděláno:** lehký útok skirmishera má nápřah 16 t místo 10 (čtvrt sekundy, dá se na něj reagovat).
   Stejně vlk, Mirrorfang a Shadowstalker (14 a 15 t na 16).
2. **Uděláno:** salva rapid archera má čáru na zemi jako každý jiný těžký útok.
3. **Návrh, šamanův tanec:** šaman místo třetí ohnivé zdi zatančí a blízcí goblini 5 s běhají
   a útočí rychleji (zelené plameny kolem nich). Zabít šamana první pak dává smysl.
4. **Návrh, dva noví vzácní goblini:** **Warchief** v Goblin Warren (roh, který svolá hlídku
   z nejbližší chodby, a rána štítem, která odhodí) a **Trapper** u Old Ruins (klade nášlapné sítě,
   které hráče na 1 s zpomalí; síť je na zemi vidět).
5. **Návrh, brute:** dnes nikdy neutíká a nikdy se nerozzuří. Pod třetinou zdraví zařve a jeho
   nápřahy se zkrátí o pětinu: hráč vidí, že se boj mění.

### Orkové: co doladit

Dobré: čtyři zbraně se čtyřmi styly, tři vzácní s vlastními schopnostmi, čitelné varování.
Slabé: tábor je jen místo, kde stojí; nic nedělají společně.

1. **Návrh, válečný buben:** v Ironjaw Warcampu buben stojí jako rekvizita. Když hráče uvidí hlídka
   u brány, jeden ork k bubnu doběhne a po 3 s bubnování se seběhne celý tábor. Kdo bubeníka zastaví,
   bojuje jen s hlídkou. Dává důvod plížit se průlomem v palisádě.
2. **Návrh, summonSpears** spearcallerky: přidat zvuk nabíjení (kopí nad hlavou už vidět jsou).
3. **Návrh, berserker:** jméno slibuje zuřivost, ale bojuje jako warrior se sekerou. Pod polovinou
   zdraví přestane couvat po ráně a útočí bez přestávky (kratší prodleva), ale ztratí obranu.
4. **Návrh, huntress:** flanker bez střelby. Dát jí vrhací sekeru na 8 m, aby hráč, který utíká
   od tábora, nebyl v bezpečí hned za palisádou.
5. Orkové neustupují: nechat, sedí k nim.

### Postaveno 2026-11-11: Auren Sentinel a Archive Warden

- **Auren Sentinel** (úroveň 7 až 9, `kind: sentinel`): mramorový strážce s velkým mečem. Stojí jako socha
  (`statue`) čelem, kam ukazuje tábor, a probudí se, když hráč přijde na 6 m nebo ho trefí. Zepředu bere 30 %
  poškození (jiskry a „Guarded"), zezadu 150 % (modré úlomky): boj je o obcházení. Lehký úder 32 t, těžký
  dopad 64 t s kruhem. Neucukne a neodletí po zásahu.
- **Archive Warden** (vzácný, úroveň 10): k tomu runová vlna, tři rovné pruhy po 1,6 m se dvěma mezerami po
  1,8 m, nápřah 70 t s varováním `lanes`. Kdo varování přečte, stoupne si do mezery.
- Kniha dovednosti Rune Ward (z návrhu) zatím není; Warden dává Rune Core.

### Postaveno 2026-11-11: goblini a orkové

- **Šamanův tanec** (`warDance`): šaman se na místě točí s holí nad hlavou (nápřah 54 t, s chechtotem) a goblini
  jeho tábora do 9 m mají 5 s zrychlení: rychlejší běh, kratší nápřahy, schopnosti se vracejí dvakrát rychleji.
  Zelené plameny u nohou. Sám tančí jen tehdy, když má koho povzbudit.
- **Goblin brute** pod třetinou zdraví zařve („Enraged!", rudé jiskry) a jeho nápřahy se zkrátí na 80 %.
- **Orc berserker** pod polovinou zdraví zuří: zotavení po ráně poloviční, ale bere o čtvrtinu víc poškození.
- **Orc huntress** hází ruční sekeru (4 až 10 m, nápřah 30 t s čárou), která letí obloukem a točí se.
- **Válečný buben** v Ironjaw Warcampu: kdo z orků hráče zahlédne, doběhne k bubnu a 3 s bubnuje („War drum!",
  rány do bubnu); pak se seběhne celý tábor. Rána do bubeníka bubnování přeruší, buben pak minutu odpočívá po
  každém zaznění. Na skutečné zóně ověřeno: buben zazněl a tábor hráče přemohl.

Neověřeno očima: zelené plameny a rudé jiskry ve hře (efekty jsou jen v klientu, ne ve studiu), letící sekera a
bubeník u bubnu (živý záběr ukázal útok orků, ale buben byl mimo záběr). Zvuky jsou ze stávající sady.

### Postaveno 2026-11-11 večer: Barrow Wisp a Lantern of the Barrow

- **Barrow Wisp** (Stone Circle, úroveň 6 až 8, čtyři): jen v noci (20 až 6 h, `GameTime.IsDark`, tábor
  `"night": true`); za svítání zhasne tam, kde je (bez kořisti a zkušeností) a vrátí se se soumrakem. Drží odstup
  8 m a krouží (role `ranged`), střílí `wispBolt`: pomalé světlo (6,5 m/s), které se za hráčem stáčí 1,3 rad/s,
  dá se mu uhnout úskokem a blok ho zastaví celé (lehká rána). Server je drží u země; klient je kreslí ve výšce
  hrudi (`WispView`) a hitbox podle `size` 1 sahá dost vysoko.
- **Menhiry** (`"wards"` u tábora, pět kamenů kruhu): když hráč stojí do 2,6 m od kamene, bludičky do 16 m od něj
  zhasnou (`EnemyAction.Dim`): nestřílí, sklesnou, přiletí ke kameni na dosah čepele a berou 1,5násobek. Střela,
  která doletí ke kameni, zhasne. Po odchodu od kamene se rozsvítí za půl sekundy.
- **Lantern of the Barrow** (vzácná, úroveň 9, ve stejném kruhu, 5 min respawn): `wispSplit` ji rozdělí na tři
  světla na kruhu 4,5 m; ona je v jednom z nich náhodně. Dvě falešná (`lantern_decoy`, `Decoy`) mají stejné jméno,
  úroveň i zdraví, zhasnou po jediné ráně, nic nedají a za 12 s zmizí samy. Pravou prozradí jen stín na zemi.
  Rozdělí se znovu, až falešná zhasnou. Kořist: `wisp_ember`, `barrow_glass`.
- Testy `net-tests/WispTests.cs`. Studio `PIXEL_MONSTERS=barrow,lantern`. Živě ověřeno ve 22 h u kruhu: střely
  zasahují, Lantern se rozdělila na tři, u menhiru bludičky zhasly a hráč zůstal na plném zdraví.

Neověřeno: bludičky zhasínající za svítání ve hře, souboj s Lantern až do konce (rozbití falešných světel),
jak se kruh hraje se čtyřmi bludičkami a Lantern naráz (síla nenastavena). Zvuky ze stávající sady.

### Postaveno 2026-11-11 v noci: Carrion Crow a Gallows Rook

- **Crowfield** (nové místo, −138, 166, 15 m od cesty do Moonfell): staré bojiště s meči a kopími zabodnutými
  v zemi, rozbitým štítem, přilbou a kostmi (`battle_debris`), dvěma vybledlými modrými prapory (`fallen_banner`),
  suchým stromem, převráceným vozem a zpustlou zemí. Moonfell sám má vlkodlaky 12 až 15, proto vrány (3 až 5)
  sedí na jeho okraji, ne mezi nimi. Studio záběr `place_crowfield`.
- **Carrion Crow** (čtyři, úroveň 3 až 5, role `flock`): hejno krouží kolem hráče ve výšce (klient 3,6 m) a tam
  nejde zasáhnout (`Enemy.Aloft`, `Enemy.Untouchable`; čepel, šíp, dovednost i mistrovské tahy jím projdou).
  Nálet (`dive`, varování čárou, 34 t) přijde nejvýš od dvou vran naráz (tahy na blízký útok); po něm vrána
  1,2 s kluje na zemi a tehdy jde zasáhnout. Zasažená vrána rozpráší celé hejno na 2 s (`ScatterTicks`), pak se
  vrátí.
- **Gallows Rook** (vzácný, úroveň 6): vlastní nálet a `rookCall`: pět vran (`gallows_crow`, jedna rána, nic
  nedají, za 10 s zmizí) se seřadí za ním a nalétnou na hráče jedna po druhé, 18 t od sebe.
- Vedlejší oprava: stínový vlk pod zemí a uhýbající duelistka už nepočítají zásah (dřív rána, která neublížila,
  přesto spustila krádež života a počítadla).
- Testy `net-tests/CrowTests.cs`, studio `PIXEL_MONSTERS=carrion,gallows`. Živě ověřeno na Crowfieldu: kroužení,
  varovná čára, klování u nohou hráče, Rook svolal svých pět vran.

Neověřeno: boj hráčem (bot stál), jestli vrána prolétající blízko kamery neruší; jmenovky visí pod letícími
vranami. Zvuky ze stávající sady (nálet = `dash`).
