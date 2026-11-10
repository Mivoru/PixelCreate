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
| NPC | 24 postav s povahou (`people.json`), rozvrhem dne a rozhovorem; noví z etapy 4: Corwin, Tam, Brightwater Four (Edwin, Moss, Fen, Rook), Osric, Anwen, Hamm, Maddy, Tobias |
| Ostatní | menu a nastavení, zvuky, foto studio, boti pro živé testy, síťový simulátor |
| Postava (etapa 2) | mimika (12 výrazů z dat), živý portrét v dialogu, pohyb úst, výrazy v boji, úprava vzhledu (11 voleb, 10 účesů, 6 vousů), výchozí výrazy NPC |
| Prolog (etapa 3) | úvodní obrázky, pád, lovci, klec, přepadení u ruin, cesta s Nell, Pip, Elda, Bram, Guilda, Anchor Stone, zkouška ve třech kolech; herci, scény, podmíněné rekvizity; bot `prologue` projde celý |
| Základy příběhu (etapa 1) | kalendář s dnem a nocí, spánek, stav světa, questy a dialogy z dat, plány frakcí (engine), ukládání každé ráno, smrt stojí 10 % krystalů, deník (L) |

## 2. Navrženo, ale neexistuje

| Věc | Kde je návrh | Poznámka |
|---|---|---|
| Podmíněný obsah zón (`requires` / `forbids`) | `systems.md` 5 | přesunuto do etapy 4 (až bude co podmiňovat) |
| Termíny questů (`deadline`) | `stage-1.md` 5 | formát se načítá, vyhodnocení až s prvním questem, který termín má |
| Vyšetřování | `systems.md` 4 | |
| Fáze bossů | `systems.md` 11 | dnes jen vzácní nepřátelé se schopnostmi |
| Dungeony | `systems.md` 12 | interiéry na platformách už fungují, dungeon ne |
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
| — | nic neblokuje | | |

## 3a. Rozhodnuto

- 2026-11-09: hra pro jednoho hráče (lokální server zůstává), hráčova postava s volbami odpovědí,
  hráč není vyvolený, žádná ideální linie, cesty přes frakce, sólo i nicnedělání jsou platné, romance
  volitelné a vypínatelné.
- 2026-11-09: jména **Hearthmere** (vesnice) a **Velmora** (svět). Hráč je **vždy muž**, vzhled si
  upraví; postava, mimika a vlasy se zlepší (etapa 2) a poslouží i NPC. Úvodní video jako **2D pixel
  art obrázky**. Akt I trvá **70 dní**. Ukládání **automaticky každé ráno** a při odchodu, jedna záloha.

- 2026-11-09 (podle návrhu): scény jednoduchým skriptovaným systémem (kamera, repliky, pohyb NPC,
  bez střihu); beastkin až v aktu II; smrt hráče stojí 10 % neprodaných krystalů a zapíše ozvěnu.
- 2026-11-09: název hry **Forty Crowns** (cena Skyfallen na trhu). Repozitář zůstává PixelCreate.
- 2026-11-09: nošený gear (helma, zbroj, boty) se na postavě **zatím neukazuje**. Vrátit se k tomu
  nejdřív po aktu I.
- 2026-11-09: čas běží plynule při hraní (1 herní hodina = 26 s), spánek přeskočí noc
  (úprava oproti „čas jen akcí", viz `design/stage-1.md`). Dialog má živý portrét mluvčího.

## 4. Další krok

Etapy 0 až 3 jsou hotové. Běží **etapa 4** (živá vesnice, `design/stage-4.md`).

Z etapy 4 hotovo: mapa cest pro chodce, rozvrhy dne (20 lidí), dvorek hospody se třemi stoly, povahy (tóny, soudy,
pouta, nálady, hlášky), 11 nových lidí s rozhovory a vzhledem, drby („Heard anything?", znění podle mluvčího, poznámka
do deníku), události dnů 3 až 10 s reakcemi lidí (rozvrhy po nájezdu, hlášky, rozhovory Pipa a Hamma), test dnů 3 až 10,
zkouška hlasů (Bram, Mira, Corwin přes ElevenLabs; nahrávky mimo git).

Z etapy 4 zbývá: ranky F a E s body a dvěma novými joby, Memorial Wall, tři osobní questy (Elda, Tess, Corwin), důvěra
slovem v deníku, bot `village` (pět dní), spálený plot po nájezdu (skrytá rekvizita si drží kolizi, chce to model),
Rookova obvázaná ruka, Lina obcházející domy v den 8, hlasy pro ostatní postavy (po rozhodnutí o zkoušce).

Z etapy 3 zůstalo stranou: cesta vozem se neukazuje (stmívačka), lovci s hráčem nebojují, Haruto zmizí bez efektu světla, úvodní obrázky jsou malované skriptem (ne v Pixelwrightu).

Z etapy 2 zůstalo stranou: ruce (dnešní dlaň s palcem stačí), barber ve vesnici (etapa 4), NPC zatím drží své vlastní vzhledy v `NpcLooks` místo voleb z `appearance.json` (stačí, až budou nové postavy v etapě 3).
