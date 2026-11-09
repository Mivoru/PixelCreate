# Etapy výroby

Akt I se staví po etapách. Každá etapa je hratelná a projde stejným cyklem:

1. **Návrh do detailu:** situace, výchozí vývoj, možnosti cest, koncepty dialogů, seznam postav,
   modelů, lokací, nepřátel a dat. Zapsat do `design/` (nový soubor `design/stage-<n>.md`).
2. **Stavba:** kód, data, modely, testy, bot.
3. **Hraní:** zahrát si to (ty), přečíst fotky ze studia, bot projde.
4. **Úprava:** opravit dokumentaci podle toho, co se ukázalo. Posunout řádky v `design/status.md`.

Teprve pak další etapa. Každá etapa končí commitem a pushem.

Pořadí sleduje kalendář aktu I (`design/timeline.md`), aby se každá nová etapa dala hrát navazovat
na předchozí. Cesty hráče (`design/paths.md`) přibývají postupně, s obloukem, kde se poprvé objeví.

---

## Etapa 0: Uzavřít návrh etapy 1 a 2
- Rozhodnout otevřené otázky, které blokují (viz `design/status.md`): jména, úvodní video, pohlaví
  postavy, scény.
- Napsat `design/stage-1.md` (datové formáty) a `design/stage-2.md` (prolog do detailu, repliky).
- **Hotovo když:** víme přesně, co se staví, a nic zásadního nechybí.

## Etapa 1: Základy živého světa (bez nového obsahu)
- Ukládání hry (hráč, batoh, gear, mastery, stav světa), auto každé ráno, záloha.
- Stav světa: flagy, pověsti, osudy NPC, členství, důvěra.
- Kalendář: den a denní doba na serveru, spánek, posun času joby a cestováním, světlo podle doby.
- Data: questy, dialogy s volbami a podmínkami, plány frakcí, drby. Převést současné joby a repliky.
- Deník (klávesa L): úkoly a fakta.
- **Hotovo když:** současná hra funguje jako dřív, ale z dat; uložení a načtení vrátí stejný stav;
  test nasimuluje 70 dní plánů frakcí bez hráče a dojde k výchozímu konci podle `timeline.md`;
  bot `guild` projde.

## Etapa 2: Prolog „Fallen" (dny 1 a 2)
- Úvodní video, pád do Mirror Lake, síť, klec na voze, přepadení goblinů, útěk s Nell, Hearthmere,
  registrace, zkouška u Rhey ve třech kolech.
- Jednoduchý systém scén (kamera, repliky, pohyb NPC, bez střihu).
- Nové: Nell, Vosk, lovci (nepřítel se sítí), Haruto, vůz s klecí, tábor lovců.
- Přejmenovat Meadow Village na Hearthmere (pokud schválíš).
- **Hotovo když:** prolog jde dohrát za 25 až 35 minut, bot `prologue` projde, studio ukáže nové modely.

## Etapa 3: Živá vesnice (dny 3 až 10, bez velkých oblouků)
- NPC denní rozvrhy (ráno práce, večer hospoda, noc doma), hospoda, drby, zprávy z dalekého světa.
- Tabule v2 (joby z dat, délka jobu v čase), ranky F a E.
- Důvěra a hodnoty postav, tóny odpovědí, první osobní questy.
- Noví: Corwin, Tam, Brightwater Four, mlynář, Maddy a Tobias.
- Začátky romancí (první rozhovory, odmítnutí romance, nastavení).
- **Hotovo když:** pět dní ve vesnici působí živě i bez příběhu. To je hlavní test celé vize.

## Etapa 4: Teeth and Iron a Lightborn Hunters (oblouky A a B, dny 3 až 30)
- Únos z Harrow Farm, Goblin Hollow, Goblin Warren (první dungeon), boss Warlord Grub (fáze bossů).
- Hollisova vražda a vyšetřování (systém stop a obvinění).
- Nell, Haruto, tábor lovců, Holtova kniha a její cesty.
- Cesty: společnost a karavany (hodnost 1), licence lovce jako temná možnost.
- Noví: Holt, Corran, Mother Juna (jen zmínky a posel), Dex (první souboj).
- **Hotovo když:** oblouky jdou projít aspoň čtyřmi cestami (Guilda, společnost, karavany, nic)
  a každá vede k jinému stavu vesnice.

## Etapa 5: The Village a Mud on a Horse (oblouky C a D, dny 15 až 50)
- Dluhy Harrow Farm, Lina informátorka, volba rychtáře (den 30), svatba mlynářovy dcery (24),
  Corwinův quest, Aldric a Wilt.
- East Watch: šibenice, sklad společnosti, markraběnka Isolde, Aldric.
- Cesta: markraběnka a warden (soud malých sporů).
- **Hotovo když:** výsledek volby rychtáře mění vesnici viditelně (desátek, kdo smí přijít, obchod).

## Etapa 6: The Moon of Moonfell (oblouk F, dny 25 až 40)
- Moonfell a krypta (dungeon), deník Anselma, Rheino přiznání, inkvizice, Sister Vey, Wren.
- Boss Moon-Eaten Lord, léčba a obnova Moonfellu jako podmíněný obsah zóny.
- Herold Aurel. Cesta: církev.
- Lisbet (romance, později společnice).
- **Hotovo když:** všechny čtyři výsledky (léčba, očista, důkaz, nic) mění Moonfell, Miru a Wren.

## Etapa 7: Wolf Winter (oblouk E, dny 35 až 60)
- Roční doby (podzim, sníh v zimě), obrana Harrow Farm jako událost s dilematem, Greyfang a Blight,
  Stone Circle a odhalení „zkušenosti jsou duše", boss Old Greymaw.
- Blight vlci a blighted slimáci.
- **Hotovo když:** zima je vidět a hrát (noci nebezpečnější, ceny masa, vlci u vesnice).

## Etapa 8: Iron and Honor (oblouk G, dny 12 až 62)
- Gharr, odvety, odvody a Jory, Caiovo vojsko, Urzogův souboj, bitva ve vlnách, mír a orčí osada,
  Brukova vražda vyslance, Aldricův masakr, Caiův Mark.
- Cesty: Ashen Banner (kontrakty), orkové (pokrevní pouto).
- Noví: Caius, Bryn, Sergeant Odd, Vashra jako postava (romance).
- **Hotovo když:** válka, mír, krvácení a kontrakt pro obě strany vedou k jiné podobě Southern Wilds
  a East Watch.

## Etapa 9: Finále aktu I (dny 52 až 70)
- Sunken Archive (dungeon), Archive Warden, Bramův osud, Royal Levy, epilog aktu I (souhrn stavu světa).
- Dokončení romancí aktu I.
- Celý průchod aktem I několika cestami (bot i ručně).
- **Hotovo když:** akt I jde dohrát od pádu po Levy jako celá hra s několika výrazně odlišnými konci.

## Etapa 10: Společníci
- AI společníka, Sora, Kai, Lisbet, Vashra, Corran, Bryn, Edwin podle cest.
- **Hotovo když:** společník umí boj a komentáře a může odejít.

## Potom
Návrh aktu II do detailu podle toho, co se v aktu I osvědčilo.

---

## Odhad rozsahu

| Etapa | Nové hraní | Hlavní riziko |
|---|---|---|
| 1 | žádné | správné datové formáty |
| 2 | 30 min | scény, nové modely |
| 3 | 1 h | jestli vesnice působí živě |
| 4 | 1,5 až 2 h | vyšetřování, větvení cest |
| 5 | 1 h | volby a politika ve vesnici |
| 6 | 1,5 h | dungeon, inkvizice |
| 7 | 1 h | roční doby (vizuál) |
| 8 | 1,5 až 2 h | bitva ve vlnách |
| 9 | 1 h | dungeon, celkové propojení |
