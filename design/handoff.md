# Předávka pro další session (stav 2026-11-12 odpoledne)

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

## Noc 2026-11-11 až 15:00 (prodlouženo): seznam z hraní autora (pracovat samostatně)

Autor si zahrál a chce samostatnou práci do 6:00. Zaškrtávat, co je hotové:

- [x] Podzemí: mezery ve stěnách a mezi stěnou a podlahou, je vidět prázdno.
- [x] Zkontrolovat hitboxy a dosah všech útoků (lehký útok mečem často netrefí).
- [x] Kliknutí v dialogu zároveň zaútočí.
- [x] Interiéry (Guilda, podzemí) jsou z okraje mapy vidět, jak levitují.
- [x] Goblini na začátku (prolog) slabší.
- [x] Trial v Guildě: úskok platí jen načasovaný, hráči to nikdo neřekne a Shift jde mačkat pořád.
- [x] Thornback a Briarking: útoky, animace a AI (zasekávání).
- [x] Bludičky: zblízka přestanou svítit a jsou slabé.
- [x] Mapa místy prázdná: viz seznam míst níže. a nudná: víc prvků, terén, stavby, obydlí, monstra, zvířata.
- [~] Pak dál ladit startovní lokaci (rozhovory, déšť, truhly, hledaní hotové): chytřejší NPC, kteří žijí normálně, namluvené hlášky, postupné joby,
      animace; ptát se, co jde logicky nebo graficky zlepšit, inspirace z jiných RPG a MMORPG.

### Co přibylo v noci 2026-11-11/12 (průběžně, commity na main)

- Opravy z hraní: díry v podzemí, interiéry z okraje mapy (render vrstvy), klik v dialogu neútočí, slabší goblini
  v prologu, úskok s tolerancí 15 t a novým stiskem Shift + nápověda v trialu, zásahy na blízko podle toho, kde hráč
  nepřítele viděl (`Enemy.SeenAt`) a delší dosah meče.
- Monstra: Carrion Crow + Gallows Rook (Crowfield), Thornback přepracovaný (skutečná zteč, omráčení o strom, rozběh;
  Briarking věnec trnů), bludičky silnější s menším dosahem menhiru, Goblin Warchief (roh, štít) a Trapper (oka),
  Stoneback + Mossback Elder (krunýř, roztočení), Vinelasher + Stranglethorn (šlehy, přitažení), Mirefin + Old Gulper
  (Mirror Lake), bandité + Red Hand Garrow (Bandit Hollow), zajíci, kachny.
- Místa: Woodcutters' Camp, Fisher's Landing, Old Watchtower, Shepherd's Croft, The Last Lantern (hostinec),
  Greystone Quarry, Bandit Hollow, Crowfield; kopce na plochých loukách; hlinité břehy pod mosty; devět skrytých truhel.
- Život: práce (`work` póza: sekání, rybaření, krumpáč), děti, Garrickova obchůzka, rozhovory mezi NPC (`chats.json`),
  hlasy všech mluvících lidí, déšť (vybrané dny) a schovávání lidí před deštěm, světlušky v noci.
- Joby: 7 nových + 8 hledaných (wanted) jednorázových; nástěnka na třech stránkách. Dovednosti: Wisp Bolt, Vine Pull,
  Rune Ward.
- Dopoledne 2026-11-12: oprava odevzdávání jobů (u Miry šlo odevzdat jen prvních pět, `tools/turnins.py` + test),
  otřes kamery při zásahu (`PIXEL_OFF=shake`), jižní okraj Hearthmere (2 chalupy, zahrada, Greta), Greystone Mine
  pod lomem (nora u stěny, skřeti, stoneback, truhla, job od Borse), nářadí se při sezení odkládá.
- Odpoledne: noční světla luceren a svítící okna, lidé se rozhlížejí a přešlapují, další rozhovory (mlynář s dcerou,
  Marta a Fennick, Pip a Tobias, Fen a Wren). Kontrola, prolog i nejtěžší místo (10,6 ms) v pořádku.

Neověřeno hraním člověkem: vše výše jen boty, testy (385) a snímky. Síla monster a odměny nenastaveny (pravidlo
„neladit obtížnost“), jen hrubě odhadnuté; ryby u mola byly pro level 1 smrtelné, ztlumil jsem je.

## Plán (autor 2026-11-12): body 1 až 3 dělám teď, 4 až 6 jsou další na řadě

1. [x] Měření balancu: doba zabití každého monstra, poškození od něj, XP za level a čas na level. Výstup
   `design/balance.md` (`PIXEL_BALANCE=1 dotnet test --filter BalanceReport`), ladit až podle poznámek autora z hraní.
   Nápadné: Goblin Brute 15 ran a Stoneback 17 při startovním meči, od levelu 9 přes 30 zabití na level.
2. [x] Pocit z boje: krátký hit stop u těžkých ran, znamení „otevřený“ u nepřátel, kteří jsou chvíli zranitelní
   (vrána po náletu, omráčený Thornback, želva po výpadu, ryba na břehu), zámek na cíl (Tab).
   Hotovo: hit stop 0,08 s / 0,035 s jen v obraze, „Open!“ se zeleným kruhem, Tab zámek se zlatými šipkami (taška
   je teď jen na I). Viděno v záběrech bota (Open! u Thornbacka, zámek na slizech); hit stop očima ve hře ne, jen
   kódem: autor ať řekne, jestli je moc nebo málo.
3. [x] Zvuky z ElevenLabs (autor souhlasil): vlastní zvuky nových monster a okolí (ptáci ve dne, cvrčci v noci,
   déšť, řeka).
   Hotovo: 17 zvuků monster a 5 smyček prostředí (den, noc, déšť, řeka, jeskyně). Napojeno a běží bez chyb, ale bot
   hraje s tichým ovladačem: poslechnout musí autor (hlasitost smyček, jestli neruší, jestli je slyšet šev).
4. [x] Interiér hospody: vejít dovnitř, večer tam sedí hosté (Brightwater Four, Corwin, Bram) a běží rozhovory;
   rozvrhy přes `via`. Případně interiéry dalších domů.
   Hotovo (2026-11-12): hospoda The Bent Nail má sál za dveřmi (420, 60): výčep s hospodským Dorranem (nový, s hlasem),
   krb, tři stoly, kout s loutnou. Štamgasti sedí ve dvoře od 18 a v 20 jdou dovnitř skutečně dveřmi (při dešti hned),
   Corwin od 20. Čtyři nové rozhovory uvnitř (s hlasy). Interiéry dalších domů zatím ne.
   Dorran prodává chléb, Hearty Stew (8 zl., léčí 50) a Meat Pie (5 zl., léčí 32); ceny jsou odhad, neladěno.
5. [ ] Systémy: rybaření na molech, sběr bylin a hub (pro Wren), vaření u ohně, řemeslo u Tess z materiálů monster,
   rank D se zkouškou v Guildě jako cíl po úrovni 9.
6. [ ] Delší dungeon s bossem a skrytou cestou (Archive i Warren jsou dnes jedna síň).
7. [x] Kvalita: hlídač zaseknutí klienta (zápis, co hra dělala), audit děr ve zbylých interiérech.
   Hlídač hotový (2026-11-12, `FreezeWatch.cs`): když klient 4 s nezačne snímek, zapíše do `client.log` řádek
   `FREEZE ... last mark: <část snímku>` a po rozjetí `FREEZE over`. Příčinu zaseknutí v Guildě to samo neopraví,
   jen ukáže, kde visí. Audit děr: snímky `audit_*` (rohy Guildy u podlahy, strop Guildy, aréna, strop hospody)
   žádnou díru neukázaly; podzemí bylo opravené dřív.

### Co si zahrát jako první (2026-11-12)

1. Nový začátek: prolog (slabší goblini), úskok v trialu u Rhey (Shift těsně před ranou, každý úskok nový stisk).
2. Lehký útok mečem proti kroužícím goblinům: má teď trefovat.
3. Nástěnka v Guildě (tři stránky) a odevzdání libovolného jobu u Miry (dřív šlo jen u prvních pěti).
4. Noc ve vesnici a u The Last Lantern (svítící okna, lucerny, světlušky, vytí), Stone Circle v noci (bludičky).
5. Nová místa: Woodcutters' Camp, Greystone Quarry a Mine, Fisher's Landing, Shepherd's Croft, Bandit Hollow,
   Crowfield, Old Watchtower, East Watch se strážemi; truhly k nalezení.
6. Monstra: Thornback (zteč, omráčení o strom), vrány, želvy, liány, ryby, Warchief, Trapper, bandité.

7. Hospoda The Bent Nail v Hearthmere (dveře hospody u dvora): večer od 20 h (při dešti dřív) uvnitř štamgasti a
   rozhovory, u výčepu Dorran prodává jídlo. Hlídač zaseknutí: když hra zamrzne, pošli řádek `FREEZE` z
   `export/previews/client.log`.

Ladění síly a odměn je pořád hrubý odhad (pravidlo „neladit obtížnost“): co je moc silné nebo slabé, napiš.

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
3. ~~**Goblin Warchief** a **Goblin Trapper**~~ hotovo 2026-11-11 v noci. Původní zadání: (Goblin Warren): roh svolá hlídku z nejbližší chodby (lze vyjít z `SoundAlarm`
   válečného bubnu), rána štítem, která odhodí. **Goblin Trapper** (Old Ruins): klade viditelné nášlapné
   sítě, které hráče na 1 s zpomalí (zpomalení musí jít přes `PlayerState`, aby ho klient předpověděl).
4. ~~**Stoneback**~~ hotovo 2026-11-11 v noci (severní předhůří). Původně: (předhůří a sedla) s **Mossback Elder** (roztočí se v krunýři a klouže).
5. ~~**Vinelasher**~~ hotovo 2026-11-12 (i Mirefin hotov). Původně: **Vinelasher** (Emerald Jungle, nehýbe se) se **Stranglethorn** a **Mirefin** (Mirror Lake, ve vodě)
   s **Old Gulper**. Oba potřebují nový druh pohybu, proto až na konci.
6. Vegetace a domy zblízka (kontrola každé budovy zblízka). Břehy pod mosty hotové 2026-11-12 (hlína v terénním shaderu, kameny).
7. ~~Knihy dovedností~~: Wisp Bolt, Vine Pull a Rune Ward hotové 2026-11-12.

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
