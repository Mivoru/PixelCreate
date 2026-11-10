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
