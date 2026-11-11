# Předávka pro další session (stav 2026-11-10 večer)

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

## Co dělat jako první

Zeptat se autora, co si zahrál, a podle `design/direction.md` oddíl 5 vybrat další kus: prostředí,
monstra, boj, mechaniky nebo pohyb. Nové joby rovnou s cílem pro navigaci.

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
