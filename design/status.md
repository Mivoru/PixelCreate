# Stav: co je hotové, co je jen návrh, co je otevřené

Audit designu z 2026-11-09. Odděluje to, co **ve hře běží**, od toho, co je **jen v návrhu**.
Udržovat při každé větší změně: posunout řádek z „navrženo" do „hotovo", když to projde testy.

---

## 1. Hotovo a hratelné (ve hře, s testy)

Hra už je hratelný prototyp akční části. Zdroj pravdy je kód a `CLAUDE.md`.

| Oblast | Stav |
|---|---|
| Pohyb | běh, skok, dash, slide, predikce na klientovi, rekonciliace |
| Boj | 5 druhů zbraní (meč, dýky, luk, palice, hůl), lehký a těžký útok, blok, parry, riposte |
| Progrese | úrovně, 5 atributů, mastery stromy pro zbraně (15 nových pohybů), 5 skillů z knih, 3 sloty |
| Předměty | batoh, gear s raritou a affixy, jídlo, lektvary, obchod (Wren), truhla v Guildě, odkupník |
| Nepřátelé | slimáci, 6 druhů goblinů, 7 orků (3 vzácní), 5 vlků (2 vzácní), 4 vlkodlaci (2 vzácní), Rhea |
| AI | skupinová taktika (role, tahy na blízko, kruh), hlídky, lov zvířat, stáda zvířat |
| Svět | zóna Meadow Valley 600 m, 21 oblastí, hřebeny, lesy, tábory, interiéry (Guilda, aréna), minimapa |
| Guilda | registrace, zkouška u Rhey, tabule s 5 joby (jeden aktivní), prodej krystalů a kořisti |
| NPC | 13 postav, každá jedna statická replika (Mira, Hollis a tabule mají větvení podle ranku) |
| Ostatní | menu a nastavení, zvuky, foto studio, boti pro živé testy, síťový simulátor |

## 2. Navrženo, ale neexistuje

| Věc | Kde je návrh | Poznámka |
|---|---|---|
| Ukládání hry | `systems.md` 5 | dnes stav žije jen v paměti serveru; po restartu je pryč |
| Stav světa (flagy, pověsti, beat, osudy NPC) | `systems.md` 5 | základ všech následků |
| Questy z dat, víc kroků, termíny | `systems.md` 3 | dnes pevné pole v `Guild.cs` |
| Dialogy z dat, volby odpovědí, kontrola atributů | `systems.md` 3 | dnes `switch` v `Dialogues.cs` |
| Deník (journal) | `systems.md` 3 | |
| Podmíněný obsah zón (`requires` / `forbids`) | `systems.md` 5 | |
| Vyšetřování | `systems.md` 4 | |
| Fáze bossů | `systems.md` 11 | dnes jen vzácní nepřátelé se schopnostmi |
| Dungeony | `systems.md` 12 | interiéry na platformách už fungují, dungeon ne |
| Scény (úvodní video, pád, klec) | `systems.md` 1 | ve hře není žádný systém scén |
| Ranky F až S, zkoušky ranků | `systems.md` 2 | dnes jen Newcomer, InTrial, Adventurer |
| Růst vesnice | `systems.md` 6 | |
| Společníci | `systems.md` 7 | až po aktu I |
| Nové postavy | `characters.md` | Nell, Haruto, Vosk, Holt, Corwin, Tam, Dex, Brightwater Four, Aldric, Isolde |
| Nové lokace | `systems.md` 9 | tábor lovců, Goblin Warren, sklad v East Watch, šibenice, hřbitov, krypta, archiv |
| Nová monstra | `systems.md` 10 | lovci, bandité, Blight varianty, konstrukty, mimic |
| Beastkin | `world.md` 4.2 | nová rasa pro anime rig |
| Akt II a III | `plot.md` 4, 5 | jen skica, záměrně |

## 3. Otevřená rozhodnutí (seřazeno podle toho, co blokuje dřív)

| # | Rozhodnutí | Blokuje | Můj návrh |
|---|---|---|---|
| 1 | Co je první hratelný příběhový úsek | všechno další | prolog + první polovina kapitoly 1 (viz níže) |
| 2 | Jména vesnice a světa | texty, minimapa | Hearthmere, Velmora |
| 3 | Úvodní video: 2D obrázky nebo 3D scéna | prolog | 2D pixel art obrázky, levné a ve stylu |
| 4 | Jak dělat scény (pád, klec, přepadení) | prolog | jednoduchý skriptovaný systém: kamera, repliky, pohyb NPC, bez střihu |
| 5 | Ukládání: kdy a kolik slotů | základy | auto na beatech a při odchodu, jedna záloha |
| 6 | Beastkin v aktu I, nebo až v aktu II | modely | až v aktu II, v aktu I jen zmínky a jeden kurýr na šibenici |
| 7 | Jak dlouhý má být akt I | rozsah | 6 až 10 hodin |
| 8 | Romantické linky | dialogy | zatím ne |

## 4. První hratelný příběhový úsek (návrh)

Ověří příběh, atmosféru, souboj, architekturu i tempo najednou. Odhad 45 až 60 minut hraní.

1. **Příchod:** úvodní obrázky, pád do Mirror Lake, síť lovců, klec na voze (Nell, Haruto, Vosk).
2. **Prostředí:** přepadení goblinů, útěk přes Slime Fields, Hearthmere (Pip, Elda, Bram).
3. **Důležité NPC:** Nell (osud visí na hráči) a Mira (registrace, Anchor Stone).
4. **Hlavní quest:** zkouška u Rhey, pak „Teeth and Iron": stopovat únosce z Harrow Farm do Goblin
   Hollow, najít železo se značkou, Hollis značku pozná.
5. **Vedlejší úkol:** „The Second One", osvobodit Haruta od lovců u Mirror Lake.
6. **Boss:** Warlord Grub v Goblin Warren (dvě fáze), zachránit zajatce, najít Holtovu knihu.
7. **Odměna a důvod pokračovat:** rank E, Vosk přichází pro Nell (první velká volba), Hollis večer
   říká, že ví, čí je to značka. Ráno je mrtvý. Konec úseku.

Co úsek potřebuje technicky (jen tolik, kolik úsek žádá, ne celé systémy z návrhu):
- ukládání hráče a malého stavu světa (flagy, osudy NPC),
- questy s kroky a dialogy s volbami z dat, v rozsahu, který úsek použije,
- jednoduché scény (kamera, repliky, pohyb NPC),
- deník jen se seznamem úkolů,
- fáze bosse,
- nové modely: vůz s klecí, tábor lovců, Goblin Warren, Nell, Vosk, lovci.

Co se nedělá: vyšetřování (další úsek), ranky nad E, příběhové hodiny s termíny (stačí flagy),
růst vesnice, společníci, akt II.
