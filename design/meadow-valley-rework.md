# Přestavba Meadow Valley (od 2026-11-10)

Rozhodnutí autora: svět ze zón (`design/world-plan.md`), a než přijde první nový region, Meadow Valley
se dotáhne do kvality. Údolí rostlo po kouscích: vnitřní hřebeny kolem poloměru 165 m byly hranicí
staršího, menšího údolí (360 m) a dnes ho nesmyslně dělí. Přestavba udělá z údolí jeden promyšlený
celek, který sedí do mapy světa.

Každá část končí commitem, snímky skutečné cesty hráče a seznamem, co si má autor zahrát.

## Stav na začátku

- Zóna 600 m, 21 oblastí, 8 hřebenů (tři z nich tvoří prstenec staršího údolí kolem r 165 m),
  2 řeky, 2 jezera, 19 cest, 54 táborů a stád, rozptýlená vegetace podle seedu.
- Jména světových stran neodpovídají mapě (sever = −z): „Southern Wilds" leží na severu,
  Greyfang na jihovýchodě, Moonfell na jihozápadě.
- Mapa světa počítá s údolím jako kruhovou kotlinou (dno kolem 75 m, okraj do 300 m) se sedly
  k sousedním regionům: západ (King's Road, Thornwood), východ (East Watch, Ironjaw), jih (Silver Falls,
  Mirrorrun k pobřeží), jihozápad (Moonfell Downs), severovýchod (Warren tunnels).

## Části

### A. Terén a rozvržení
- Odstranit vnitřní prstenec hřebenů. Okraj údolí přesunout na hranu zóny (r 260 až 300 m) jako
  horský val se sedly přesně tam, kde na mapě světa vedou cesty do sousedních regionů.
- Uvnitř místo zdí mírná krajina: dno kolem jezera a vesnice, pahorky (Mill Hill a další), terasy
  k okraji, skalní výchozy u ruin, údolí řeky.
- Voda: Mirror Lake napájí Mirrorrun, která teče na jih soutěskou se Silver Falls do sedla k pobřeží.
  Ironwash přitéká ze severovýchodu ze soutěsky.
- Místa (Greyfang, Moonfell, Ironjaw Outpost, Westwood, Northern Wilds) přesunout k sedlům, za nimiž
  na mapě světa leží jejich kraj.
- Nejdřív koncepční mapa (výšky a rozvržení) ke schválení autorem, pak stavba.
- Hlídá: `ZoneAuditTests` (dosažitelnost), `RoadTests`, `ScheduleTests`, výkon (`frameMs`).

### B. Vegetace a povrchy
- Mapa biomů místo rovnoměrného rozptylu: louky, pole, okraje lesů, lužní les u řeky, skalnatý okraj,
  vřesoviště u Moonfell, borovice u Greyfangu.
- Hustota a druhy rostlin podle biomu, podrost, květy, keře na okrajích polí, rákos u vody.
- Povrchy (`grounds`): vyšlapané stezky, bláto u brodů, štěrk u řeky, skála na svazích.

### C. Stavby a vesnice
- Kaple a hřbitov v Hearthmere.
- Detaily vesnice (`design/world-plan.md` 5.1): truhlíky, prádlo, sudy, vývěsní štíty, kouř, studna,
  slepice a kočky, cestičky k domům.
- Mosty a brody podle nových řek, milníky na King's Road, rozcestníky u sedel.

### D. Jména světových stran
- Southern Wilds → Northern Wilds; opravit popisy Greyfang a Moonfell v dokumentech.

### E. Monstra a jejich AI
- Projít každé monstrum: čitelnost útoků (varování, animace), zvuky, chování ve skupině, ústup,
  reakce na hráče, který utíká nebo se brání.
- Seznam vylepšení po druzích, pak úpravy s testy (`TacticsTests`, `WolfTests`, `WerewolfTests`).

### F. Questy s logikou
- Každý job má důvod, místo a zadavatele (kdo vyvěsil oznámení a proč), cíl v navigaci a odměnu,
  která dává smysl (peníze, předmět od zadavatele).
- Joby reagují na svět: goblini u farmy vyvolají odměnu za gobliny, slimové na cestách úklid cest.
- Bez příběhu a bez rozhovorů, jen oznámení na nástěnce a hlášení u Miry.

### G. Dungeony
- **Sunken Archive** pod Silver Falls (úroveň 5 až 8) a **Goblin Warren** z Goblin Hollow
  (7 až 10). Interiéry na plošinách mimo mapu jako Guilda, vchody jako portály.

Pak: technika přechodů mezi zónami, waystones, a první nový region Thornwood.

---

## Kontrola celé zóny 2026-11-11 (zadání autora: zkontrolovat a vylepšit všechno)

Podklad: kompletní studio (svět, lidé, monstra, modely), mapa sklonů z terénní funkce, výpis všech
spawnů. Co se nekontrolovalo, je uvedené na konci.

### Nálezy

1. **Terén.** 16 % plochy mělo sklon nad 18°, 7 % nad 26°: čtrnáct osamělých strmých kuželů na
   rovné desce (profil kopce dává největší sklon 1,43 × výška / poloměr, u většiny přes 30°).
   Silverbrook tekl přímo skrz kopec u západního okraje. Okraj zóny byla čtvercová zeď bez sedel.
2. **Přechody.** Žádná cesta nevedla k okraji, žádné sedlo neexistovalo.
3. **Hlavní postava.** Vlasy jsou čepice s rovnou vodorovnou linií nad čelem. „Tidy" má z boku
   vyčnívající hřeben, „Swept back" věnec kolem hlavy, „Shaved" vypadá jako helma, „Wild" je zezadu
   shluk střepů. Jizvy jsou tmavé šmouhy, čtou se jako špína. Tunika je plochá šedá plocha.
4. **Monstra.** Modely goblinů, orků, vlků a vlkodlaků obstojí. Slabina je šířka: pět rodin na celou
   zónu, Emerald Jungle, Stone Circle, Silver Falls a jezero nemají vlastní monstrum. Úrovně: slimové
   1 až 3, goblini 3 až 7, vlci 5 až 10, orkové 9 až 13, vlkodlaci 12 až 15.
5. **Stavby.** Tábory byly uzavřené ohrady (opraveno). Břehy pod mosty jsou šedé strmé plochy.
   Hory na okraji jsou hladké bílé boule bez skal. Domy zblízka proti standardu 8.4 zatím neprojité.
6. **Rychlé cestování.** Pět waystonů (Hearthmere, East Watch, Silver Falls, Greyfang, Moonfell).
   Severovýchod (Goblin Hollow, Ironjaw) a západ nemají žádný.

### Hotovo 2026-11-11

- Tábory goblinů a orků: palisáda v obloucích s průlomy místo uzavřeného kruhu.
- Kopce: strmé kopce rozšířené a snížené (výška nejvýš 0,26 poloměru, tedy sklon kolem 20°), dva
  vnitřní hřebeny (Greyfang, Old Ruins) snížené, kopec posunutý z koryta Silverbrooku. Tři skály
  u Silver Falls zůstaly strmé záměrně.
- Okraj (`Zone.Rim`): zaoblené rohy, pata hor se vlní, před ní předhůří do 3,5 m.
- Sedla (`passes` v zóně): západ (Thornwood, koryto Silverbrooku), východ (Ironjaw Highlands, koryto
  Ironwash), jih (Mirror Coast), jihozápad (Moonfell Downs). Záběry `pass_*`, `rim_*`, `hills_*`.

### Pořadí dalších částí (doporučení)

1. **Sedla dotáhnout:** cesty k nim (King's Road na západ a východ, Mirror Way, Moonfell Path),
   rozcestníky se jménem kraje, zával nebo závora, dokud sousední zóna není, a spoušť přechodu.
2. **Hlavní postava:** deset účesů znovu v Blenderu (pramínky přes linii čela, objem vzadu), jizvy
   jako tenká světlá čára s tmavým okrajem, tunika se švy a páskem. Záběry `creator`.
3. **Hory na okraji:** skalní stěny a římsy místo hladkých boulí, sníh jen na vrcholech.
4. **Waystony:** pravidlo jeden u každého sídla, vchodu do dungeonu a sedla. Doplnit Goblin Hollow
   (rozcestí 84, −92), West Gate a South Pass; celkem osm.
5. **Nová místa** podle `world-plan.md` 8.5: vypálený statek u západního sedla, zborcený most na
   Silverbrooku, opuštěný tábor lovců u Greyfangu, poutní kaplička u jižního sedla.
6. **Monstra podle biomů** (návrh ke schválení): džungle, kamenný kruh, jezero, ruiny.
7. **Vegetace a domy zblízka**, břehy pod mosty.

### Nekontrolováno

Animace a zvuky monster, každý dům zblízka, hustota vegetace po biomech, chování monster v boji
u nových průlomů, hra na novém terénu (jen testy a studio, žádný živý průchod).

### Hotovo 2026-11-11 (druhá část)

- **Cesty k sedlům:** King's Road na západ (podél Silverbrooku) a na východ (podél Ironwash), Mirror Way
  od jezerní cesty na jih, Moonfell Path jižně kolem hřbitova. Každou zavírá zával (`rockfall`)
  s rozcestníkem, dokud sousední zóna není postavená.
- **Neškodná fauna:** liška (`fox`, Whispering Woods, Westwood, Northern Wilds), horský beran
  (`mountain_ram`, předhůří u jižního sedla, u západního sedla a na severovýchodě), bílý jelen
  (`white_hart`, jeden ve Whispering Woods, obnoví se po dlouhé době, vždy dá parohy) a zdivočelé
  ovce (`moor_sheep`, Moonfell). Jsou to přebarvené stavby vlka, kozy, jelena a ovce.
- **Flóra:** obří muchomůrky (`giant_mushroom`) ve Whispering Woods a Emerald Jungle, vřes (`heather`)
  v Moonfellu a na jižním předhůří.

Slabiny, které zůstaly: liška má vlčí nohy (na zvíře poloviční velikosti moc dlouhé), beranovy rohy
trčí dozadu místo kolem hlavy. Zvířata jsem viděl jen ve studiu, ne ve hře.

### Návrh nových monster podle biomů (ke schválení, nic z toho není postavené)

Úrovně jsou plán, ne balanc. Každé monstrum má jedno chování, které ostatní nemají.

| Monstrum | Kde | Úroveň | Chování | Schopnost |
|---|---|---|---|---|
| **Thornback** (trnitý kanec) | Emerald Jungle | 4 až 6 | brute, hájí území kolem doupěte | rozběh po přímce, který jde uhnout do strany; po nárazu do stromu je chvíli omráčený |
| **Vinelasher** (liána, která stojí na místě) | Emerald Jungle, u ruin | 5 až 7 | nehýbe se, hlídá průchod | šleh na 6 m a přitažení hráče k sobě; zranitelná zblízka |
| **Mirefin** (dravá ryba, půl ve vodě) | Mirror Lake, brody | 4 až 6 | útočí jen na hráče ve vodě nebo na břehu | výskok z vody, po něm leží na břehu a jde dobít |
| **Barrow Wisp** (bludička) | Stone Circle, v noci | 6 až 8 | drží odstup, krouží | střela, která letí pomalu a zatáčí; zhasne, když hráč stojí u menhiru |
| **Auren Sentinel** (kamenný strážce) | Old Ruins, Sunken Archive | 7 až 9 | stojí jako socha, probudí se zblízka | pomalý úder s velkým dosahem a dlouhým varováním, štít zepředu, slabé místo na zádech |
| **Stoneback** (želva se skálou na zádech) | předhůří, sedla | 6 až 8 | pomalý, zatáhne se do krunýře | v krunýři nejde zranit; vyleze, když hráč odstoupí, a kousne |
| **Carrion Crow** (hejno vran) | Moonfell, bojiště | 3 až 5 | hejno tří až pěti, slétá se na padlé | nálet z výšky, po zásahu jedné se hejno rozprchne a vrátí |

Doporučené pořadí stavby: Thornback (stavba kance už existuje), Auren Sentinel (dá ruinám
a Sunken Archive vlastního nepřítele), Barrow Wisp, pak ostatní. Mirefin a Vinelasher potřebují
nový druh pohybu (ve vodě, bez pohybu), proto jsou až na konci.
