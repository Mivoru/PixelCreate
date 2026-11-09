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
| Stav světa (flagy, pověsti, kalendář, plány frakcí, osudy NPC) | `systems.md` 5 | základ všech následků |
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

## 3. Otevřená rozhodnutí

| # | Rozhodnutí | Blokuje | Můj návrh |
|---|---|---|---|
| 1 | Název hry (pracovně PixelCreate) | úvod, menu | rozhodnout před etapou 3 |
| 2 | Vzhled gearu na postavě (helma, zbroj podle nošených předmětů) | nic teď | až po aktu I |

## 3a. Rozhodnuto

- 2026-11-09: hra pro jednoho hráče (lokální server zůstává), hráčova postava s volbami odpovědí,
  hráč není vyvolený, žádná ideální linie, cesty přes frakce, sólo i nicnedělání jsou platné, romance
  volitelné a vypínatelné.
- 2026-11-09: jména **Hearthmere** (vesnice) a **Velmora** (svět). Hráč je **vždy muž**, vzhled si
  upraví; postava, mimika a vlasy se zlepší (etapa 2) a poslouží i NPC. Úvodní video jako **2D pixel
  art obrázky**. Akt I trvá **70 dní**. Ukládání **automaticky každé ráno** a při odchodu, jedna záloha.

- 2026-11-09 (podle návrhu): scény jednoduchým skriptovaným systémem (kamera, repliky, pohyb NPC,
  bez střihu); beastkin až v aktu II; smrt hráče stojí 10 % neprodaných krystalů a zapíše ozvěnu.
- 2026-11-09: čas běží plynule při hraní (1 herní hodina = 26 s), spánek přeskočí noc
  (úprava oproti „čas jen akcí", viz `design/stage-1.md`). Dialog má živý portrét mluvčího.

## 4. Další krok

Etapa 0 je hotová: detailní návrhy `design/stage-1.md`, `stage-2.md` a `stage-3.md`. Další je
**etapa 1** (základy živého světa).
