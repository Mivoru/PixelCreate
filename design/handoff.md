# Předávka pro další session (stav 2026-11-11 pozdě večer)

## Směr hry se změnil (rozhodnutí autora 2026-11-10)

Forty Crowns už není příběhová hra. Žádná Nell, žádné příběhové questy, žádné stromy rozhovorů.
Soustředit se na prostředí, world building, monstra, boj, mechaniky a pohyb; postup levelováním
(zabíjení monster) a jednoduchými joby. Mluví jen ti, kdo slouží: Mira, Hollis, Wren, nástěnka,
Anchor Stone. Ostatní mají jen hlášky nad hlavou nebo mluví mezi sebou.

Zůstává: úvodní video, výběr vzhledu, zajetí lovci (síť, klec), přepadení goblinů se skutečným
bojem, Hannin meč, cesta do Hearthmere k Miře, která hráče jedním rozhovorem zaregistruje (rank F).
Zkouška s Rheou je postup na rank E od levelu 5.

Smazán obsah, kód zůstal: rozhovory 22 lidí, scény 4 až 10, drby, plány frakcí na dny 3 až 10,
poznámky deníku, Nell a Haruto. Dokumenty v `design/` o příběhu (story, factions, stage 1 až 4,
repair-plan R1 a R4) popisují opuštěný směr.

## Hotovo 2026-11-10 večer: mapa a navigace

Velká mapa na M (posun, přiblížení, klik = vlastní značka, klik na ni nebo pravé tlačítko = zrušit),
cesta po silnicích na minimapě, na velké mapě a jako zlaté šipky na zemi (40 m dopředu), cíl z kroku
questu nebo jobu. Značka se drží v `user://waypoints.cfg` po savech a zmizí po příchodu. Šipky na
zemi nic neměřitelného nestojí (10,0 proti 10,3 ms). Čeká na zahrání autorem.

## Hotovo 2026-11-11: tábory goblinů a orků už nejsou ohrady

Autor: tábory byly moc uzavřené a nevypadaly dobře, chce neúplné oplocení. Goblin camp, goblin fort a Ironjaw
Warcamp mají palisádu v obloucích s volnými úseky (`palisade_half`, `palisade_broken` s průchozím průlomem,
v mezerách `spike_wall`, kůly s lebkami, hranice dřeva). Trámy palisády jsou na vnitřní straně tábora.
Ověřeno: 335 testů, studio záběry všech tří táborů z otevřených stran, jeden živý záběr u goblin campu.
Neověřeno hraním: jak goblini a orkové průlomy používají v boji (vybíhají ven, hráč je láká po jednom).

## Hotovo 2026-11-11 odpoledne: hory, opuštěná místa, Auren Sentinel

Hory na okraji mají hřebeny, skalní pásy a sníh jen nahoře. Přibyla čtyři místa (Burnt Farm, Fallen Bridge,
Hunters' Camp, Pilgrim Shrine) a kamenní strážci Auren Sentinel s vzácným Archive Warden. Podrobnosti a co
nebylo ověřeno hraním: `design/meadow-valley-rework.md` (pátá část) a `design/monster-audit.md`.
Večer přibyly schválené úpravy goblinů a orků: šamanův tanec, rozzuřený brute a berserker, vrhací sekera huntress
a válečný buben v Ironjaw (`design/monster-audit.md`). Další na řadě: Barrow Wisp a Carrion Crow, dva noví vzácní
goblini (Warchief, Trapper), pak vegetace a domy zblízka.

## Hotovo 2026-11-11 pozdě večer: Barrow Wisp (krok 1 plánu)

Bludičky u Stone Circle jen v noci, naváděná pomalá střela, zhasnutí u menhiru, vzácná Lantern of the Barrow se
rozdělí na tři (pravou prozradí stín). Podrobnosti a co nebylo ověřeno: `design/monster-audit.md`, poslední oddíl.

## Hotovo 2026-11-11 v noci: Carrion Crow (krok 2 plánu)

Vrány na novém bojišti Crowfield na okraji Moonfell, nálety, rozprášení hejna, vzácný Gallows Rook s řadou pěti
vran. Podrobnosti: `design/monster-audit.md`, poslední oddíl. Autor zatím nic nehrál (2026-11-11).

## Noc 2026-11-11 až 6:00: seznam z hraní autora (pracovat samostatně)

Autor si zahrál a chce samostatnou práci do 6:00. Zaškrtávat, co je hotové:

- [x] Podzemí: mezery ve stěnách a mezi stěnou a podlahou, je vidět prázdno.
- [ ] Zkontrolovat hitboxy a dosah všech útoků (lehký útok mečem často netrefí).
- [x] Kliknutí v dialogu zároveň zaútočí.
- [x] Interiéry (Guilda, podzemí) jsou z okraje mapy vidět, jak levitují.
- [x] Goblini na začátku (prolog) slabší.
- [x] Trial v Guildě: úskok platí jen načasovaný, hráči to nikdo neřekne a Shift jde mačkat pořád.
- [ ] Thornback a Briarking: útoky, animace a AI (zasekávání).
- [ ] Bludičky: zblízka přestanou svítit a jsou slabé.
- [ ] Mapa místy prázdná a nudná: víc prvků, terén, stavby, obydlí, monstra, zvířata.
- [ ] Pak dál ladit startovní lokaci: chytřejší NPC, kteří žijí normálně, namluvené hlášky, postupné joby,
      animace; ptát se, co jde logicky nebo graficky zlepšit, inspirace z jiných RPG a MMORPG.

## Co dělat jako první

Zeptat se autora, co si zahrál. Pokud nic jiného neurčí, pokračovat schváleným plánem níže (autor schválil
všechna navržená monstra, vzácné varianty i úpravy goblinů a orků: „souhlasím se všema“, 2026-11-11).

## Schválený plán dalších kroků (v tomto pořadí)

Návrhy jsou v `design/meadow-valley-rework.md` (tabulka monster: kde, úroveň, chování, schopnost) a
`design/monster-audit.md` (tabulka vzácných variant a oddíl Goblini, bod 4). Každý krok: data v
`world/enemies.json` a zóně, efekt v `GameServer.Fire`, test v `net-tests/<Monstrum>Tests.cs`, model
v `asset-pipeline/`, studio záběr (`PIXEL_MONSTERS=...`), dokumentace, commit a push.

1. ~~**Barrow Wisp**~~ hotovo 2026-11-11.
2. ~~**Carrion Crow**~~ hotovo 2026-11-11 (Crowfield).
3. **Goblin Warchief** (Goblin Warren): roh svolá hlídku z nejbližší chodby (lze vyjít z `SoundAlarm`
   válečného bubnu), rána štítem, která odhodí. **Goblin Trapper** (Old Ruins): klade viditelné nášlapné
   sítě, které hráče na 1 s zpomalí (zpomalení musí jít přes `PlayerState`, aby ho klient předpověděl).
4. **Stoneback** (předhůří a sedla) s **Mossback Elder** (roztočí se v krunýři a klouže).
5. **Vinelasher** (Emerald Jungle, nehýbe se) se **Stranglethorn** a **Mirefin** (Mirror Lake, ve vodě)
   s **Old Gulper**. Oba potřebují nový druh pohybu, proto až na konci.
6. Vegetace a domy zblízka (kontrola každé budovy zblízka), břehy pod mosty.
7. Volitelně knihy dovedností ze vzácných: Rune Ward, Wisp Bolt, Vine Pull.

Zvuky monster: zatím ze stávající sady. Nové zvuky z ElevenLabs stojí peníze, generovat jen po souhlasu autora.

## Neověřeno očima z posledních kroků

Probuzení strážce a runová vlna ve hře, zelené plameny zrychlení a rudé jiskry rozzuření, letící sekera
huntress (drží při hodu dál meč), bubeník u bubnu. Bot v testovacím režimu bez prologu nepřitahuje
nepřátele: pro živý záběr boje postavit hráče do dosahu (`aggroRange`) nebo nechat bota zaútočit.

## Na co si dát pozor

- Než posuneš rekvizitu, změř terén pod jejími rohy a zkontroluj vodu (vůz se jednou zabořil do břehu).
- Kolize podmíněných rekvizit se řídí jejich flagem (`World.HasFlag`).
- Server se v Debug buildu startuje 34 s (pomalé `Zone.Scatter`), v Release 19 s; menu používá Release.
- `Select-Object -First N` za během hry utrhne výpis a běh pak nikdy neskončí. Spolehlivé je
  `play.ps1 ... | Select-String -Pattern "BOT|STATS|ERROR"`; přesměrování `*>` do souboru u přímého
  `play.ps1` jednou nechalo hru běžet bez výstupu i snímků.
- Hra nekreslí poloprůhledné materiály (alpha); efekty používají stipple shader (`Fx.Stipple`).
- Když zmizí zvukové zařízení (sluchátka), Godot ve WASAPI zamrzne a běh bota visí bez snímků.
  Proto `play.ps1 -Capture` spouští hru s `--audio-driver Dummy`.
- Vlastní značka na mapě se ukládá jen ve hře z hlavního menu; boti a testy začínají bez ní.
- Skripty přes heredoc v Bash nástroji se rozbíjejí; jednorázový Python piš do scratchpadu.

## Užitečné příkazy

```powershell
powershell -ExecutionPolicy Bypass -File scripts/check.ps1        # testy + živý průchod Guildou
powershell -ExecutionPolicy Bypass -File scripts/prologue.ps1     # bot projde zajetí a přepadení
powershell -ExecutionPolicy Bypass -File scripts/prologue.ps1 -Contrary   # bot utíká před lovci
powershell -ExecutionPolicy Bypass -File scripts/play.ps1         # hlavní menu
```
