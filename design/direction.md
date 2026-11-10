# Směr hry od 2026-11-10

Tento dokument platí před všemi staršími návrhy v `design/`. Shrnuje, co se 9. a 10. listopadu
2026 udělalo, co z toho autor vyvodil a jak hra pokračuje.

## 1. Rozhodnutí

**Forty Crowns není příběhová hra.** Je to rychlé akční RPG v 3D s pixelartovým vzhledem, které stojí
na prostředí, budování světa, monstrech, boji, mechanikách a pohybu. Postup je levelování
zabíjením monster a jednoduché joby. Příběh je jen rámec na začátku.

Proč (autor po druhém zahrání prologu, 2026-11-10): příběhová práce zabrala dva dny, testy a boti
procházeli, ale hra byla hratelně „otřesná": rozhovory jako výplň, scény, které šly rozbít, hráč
nevěděl, kam jít. Na rozvětvený příběh s mnoha postavami teď není kapacita a AI to zatím
nezvládá v kvalitě, kterou autor chce. Úsilí se přesouvá tam, kde hra je silná.

## 2. Co ve hře zůstává

- **Hlavní menu**: Continue, New Game (pojmenovaný save), Load Game (den, level, místo, mazání
  s potvrzením), Settings, Quit. Pozadí je letecký záběr na Hearthmere. Hra si sama spustí svůj
  server pro zvolený save; Esc ve hře nabízí uložit a vrátit se do menu.
- **Úvodní video** (autorův sestříhaný klip z Gemini) s titulkem Forty Crowns, přeskočitelné.
- **Výběr vzhledu** postavy.
- **Zajetí**: lovci (Vosk, Gil, Hanne, Corb, každý jinak vypadá a má svou zbraň) jdou k hráči;
  kdo utíká, na toho Gil hodí síť (skutečná schopnost, letí a znehybní). Odtáhnou ho do pevné klece.
- **Přepadení**: goblini od ruin, lovci s nimi opravdu bojují a padnou, Vosk uteče, goblin rozbije
  mříže. Hráč vezme Hannin krátký meč a dobije zraněné gobliny.
- **Do vesnice**: cíl a navigace vedou k Miře v Guildě. Mira jedním rozhovorem zaregistruje (rank F).
- **Zkouška s Rheou** je postup na rank E od levelu 5 (normální souboj; prohra nic nebere).
- **Mluví jen ti, kdo slouží**: Mira (registrace, joby, krystaly, spánek), Hollis (výkup), Wren
  (obchod), nástěnka s joby, Anchor Stone. Ostatní lidé mají jen hlášky nad hlavou.
- **Mapa a navigace** (hotovo 2026-11-10): velká mapa na M, klik položí vlastní značku, cesta po
  silnicích na minimapě, na velké mapě i jako šipky na zemi; questy a joby si cíl nastaví samy.

## 3. Co bylo odstraněno

Obsah je pryč, systémy v kódu zůstaly (scény, podmínky, flagy, stav světa, plány frakcí, drby,
povahy lidí): Nell a Haruto, scény prologu 4 až 10 (cesta s Nell, Pip, Elda, Bram, uvítání v Guildě,
odznak), noční zastavení času a zavřená Guilda, rozhovory 22 lidí, drby, události dnů 3 až 10
(nájezd na Harrow Farm a další), poznámky deníku, druhý quest, zkouška jako podmínka registrace.
Dokumenty `story.md`, `plot.md`, `characters.md`, `factions.md`, `paths.md`, `relationships.md`,
`timeline.md`, `stages.md`, `stage-1.md` až `stage-4.md` a `repair-plan.md` popisují opuštěný směr.
Mohou sloužit jako zásoba nápadů pro svět (místa, frakce, monstra), ne jako plán.

## 4. Závěry z práce (co se naučilo)

1. **Testy a boti nejsou hraní.** Poslušný bot jde přesně tam, kam scéna čeká. Rozbitá místa našel
   až autor. Každá scéna se zkouší i jako „vzdorovitý hráč" (`scripts/prologue.ps1 -Contrary`).
2. **Dívat se na snímky skutečné cesty hráče.** Kamera mířící na střechu vozu, zabořený vůz,
   goblini na úrovni 4 místo 1 nebo neviditelné značky na zemi se ukázaly až na snímcích.
3. **Nic „naskriptovaného na sílu".** Lovci, kteří padají podle stopek, vypadají hůř než žádný boj.
   Co se má ve hře stát, má se stát mechanikou hry (skutečný boj, skutečná síť, skutečná kolize).
4. **Jeden kus do kvality, pak další.** Autor má hotový kus zahrát a schválit, než se přidá další.
5. **Šířka před hloubkou škodí.** Jedenáct nových lidí, drby a hlasy na prologu, který nikdo nehrál,
   byly zbytečná práce.
6. **Hráč musí vždy vědět, kam jít.** Proto navigace a cíl u každého kroku questu (test hlídá, že
   každý krok s cestou má místo, které mapa umí označit).
7. **Než se posune rekvizita, změřit terén pod rohy a vodu.**

## 5. Jak hra pokračuje

Celý svět je naplánovaný v `design/world-plan.md` (mapy v `design/maps/`, data v `design/world/layout.json`).


Pořadí určuje autor; toto je nabídka podle pilířů.

**Prostředí a svět**
- Doladit Meadow Valley: místa, která stojí za objevení (ruiny, jeskyně, výhledy), jejich jména na mapě.
- Okolní země (Greyfang, Moonfell, Ironjaw) dotáhnout na kvalitu vesnice; každá s vlastní náladou.
- Počasí a denní doba jako součást světa (mlha v lesích, noc nebezpečnější).

**Monstra a boj**
- Další druhy monster a vzácní nepřátelé s vlastními schopnostmi (jako vzácní vlci a vlkodlaci).
- Bossové na konci oblastí.
- Čitelnost boje: zásahy, varování útoků, zpětná vazba.

**Mechaniky a postup**
- Joby na nástěnce: víc druhů (lov, sběr, doprovod, vyčištění tábora), navigace je umí ukázat.
- Rank E a další ranky přes zkoušky a joby; lepší vybavení podle ranku.
- Řemesla a výroba z materiálů (kůže, gel, rudy).

**Pohyb**
- Doladit pocit z běhu, skoku, dashe a slidu; lezení nebo šplhání jen pokud sedí ke světu.

**NPC**
- Chytří lidé bez příběhu: denní rozvrhy už mají; mohou spolu mluvit (hlášky mezi sebou),
  reagovat na čas a počasí. Žádné stromy rozhovorů.

## 6. Pracovní pravidla, která platí dál

- Hotový kus končí seznamem, co si autor má zahrát, a hlášením, co ověřeno nebylo.
- Snímky skutečné cesty se čtou po každé viditelné změně.
- Balanc se zatím neladí.
