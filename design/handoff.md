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

## Co dělat jako první

**Mapa a navigace** (autor o ni žádal několikrát, nesmí znovu sklouznout):
1. Velká mapa na klávese M: celá obrazovka, posun a přiblížení myší, objevená místa se jmény, hráč.
2. Klik na místo položí vlastní bod a zapne navigaci (další klik ho zruší). Bod patří k savu.
3. Cesta od hráče k cíli přes `RoadMap.Route`: čára na minimapě i velké mapě a ve světě řada značek
   na zemi před hráčem (prvních zhruba 40 m, přepočet při odbočení).
4. Questy (krok `prologue_fallen` k Miře, joby z nástěnky) cíl nastaví samy: cíl z `goal` (postava,
   nepřítel, předmět) nebo z nového `at`.
5. Měřit `frameMs` se zapnutou a vypnutou cestou.

Pak podle autora: prostředí, monstra, boj, pohyb.

## Na co si dát pozor

- Než posuneš rekvizitu, změř terén pod jejími rohy a zkontroluj vodu (vůz se jednou zabořil do břehu).
- Kolize podmíněných rekvizit se řídí jejich flagem (`World.HasFlag`).
- Server se v Debug buildu startuje 34 s (pomalé `Zone.Scatter`), v Release 19 s; menu používá Release.
- `Select-Object -First N` za během hry utrhne výpis a běh pak nikdy neskončí: výstup piš do souboru.
- Skripty přes heredoc v Bash nástroji se rozbíjejí; jednorázový Python piš do scratchpadu.

## Užitečné příkazy

```powershell
powershell -ExecutionPolicy Bypass -File scripts/check.ps1        # testy + živý průchod Guildou
powershell -ExecutionPolicy Bypass -File scripts/prologue.ps1     # bot projde zajetí a přepadení
powershell -ExecutionPolicy Bypass -File scripts/prologue.ps1 -Contrary   # bot utíká před lovci
powershell -ExecutionPolicy Bypass -File scripts/play.ps1         # hlavní menu
```
