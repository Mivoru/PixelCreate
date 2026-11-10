# Etapa 4: Živá vesnice (dny 3 až 10)

Cíl: pět dní v Hearthmere působí živě i bez příběhu. Lidé mají den, mluví o tom, co se děje, a svět
se hne, i když hráč jen loví. Hlavní test celé vize.

Návaznost: `design/stages.md` (etapa 4), `design/timeline.md` (dny 3 až 10), `design/characters.md`,
`design/relationships.md`. Kód: herci (`Actors.cs`), scény, `Story.cs`, `world/agendas.json`,
`world/rumors.json` (formát z etapy 1, zatím prázdné).

Texty ve hře anglicky, poznámky česky.

---

## 1. Co hráč zažije

- Ráno ve vesnici: Elda peče, Tess rozdělává výheň, Garrick stojí u jižní brány, Bram sedí u studny.
- Přes den jsou Brightwater Four pryč na jobu, Kai taky. Lina má otevřený stánek.
- Večer se lidé sejdou před hospodou (stoly na dvorku): Bram, Tess, Garrick po službě, Corwin (ten tam
  sedí pořád), Brightwater Four, Kai. Tam se mluví: drby, hádky, chlubení.
- V noci jsou lidé doma (zmizí ve dveřích). Na návsi zůstane jen Garrick a Corwin, který usnul u stolu.
- Každý den se něco stane bez hráče: den 5 nájezd na Harrow Farm, den 6 první job Brightwater Four,
  den 10 zpráva ze Saltmere. Lidé to druhý den komentují.

---

## 2. Rozvrhy (`world/schedules.json`)

```json
{ "elder": [
    { "from": 6,  "at": [-2, 11],  "yaw": 150 },
    { "from": 18, "at": [9.5, 2.5], "yaw": 90, "place": "tavern" },
    { "from": 22, "home": [-13, 9] }
] }
```

- Blok platí od hodiny `from` do dalšího bloku. `at` je místo, kde postava stojí; `home` jsou dveře,
  ve kterých zmizí (není ve světě, nejde s ní mluvit). `pose` je volitelná póza (`sit`, `fallen`).
- Server jednou za herní hodinu srovná každého herce s rozvrhem a pošle ho pěšky (`Actor.Path`).
  Herec, kterého drží scéna nebo který jde za hráčem, se rozvrhem neřídí.
- Postava se smí zmínit o rozvrhu („I'm at the forge till dusk"), aby hráč věděl, kde ji hledat.
- Podmíněné bloky: `when` (podmínka nad stavem světa). První platný blok dne vyhrává. Tak se rozvrh
  změní po události (po nájezdu stojí Pip na hlídce u ohrady).
- Mrtví a odešlí (osud NPC) rozvrh nemají.

Rozvrhy v etapě 4: Bram, Tess, Lina, Wren, Garrick, Elda, Pip, Corwin, Tam, Edwin, Moss, Fen, Rook,
mlynář Osric, Anwen, Hamm, Maddy, Tobias, Kai, Sora, Nell (po prologu ve skladu Guildy, ven jen večer).

---

## 3. Drby a zprávy (`world/rumors.json`)

```json
{ "id": "harrow_raid", "text": "Goblins hit Harrow Farm in the night. Took two of Hamm's people.",
  "when": { "agenda": { "company.raid_harrow": "done" } }, "from": ["tavern", "pip", "garrick"],
  "days": 6, "note": "rumor.harrow_raid" }
```

- Drb má text, podmínku, kdo ho říká (`from`: id postav, nebo `tavern` = kdokoli večer u hospody,
  `board` = tabule v Guildě), jak dlouho se o něm mluví (`days` od chvíle, kdy začal platit) a
  poznámku do deníku.
- Každá postava, která nějaký platný drb má, dostane v rozhovoru možnost **„Heard anything?"**.
  Řekne nejnovější drb, který hráč ještě neslyšel. Slyšený drb se zapíše do deníku (záložka Notes).
- Drby lžou podle toho, kdo mluví: tentýž fakt může mít dvě znění (`by`: text podle mluvčího). Lina
  o nájezdu řekne, že „the Company will see the roads safe", Pip řekne, kdo chybí.
- Zprávy z dalekého světa (Saltmere, král) jdou přes `board` a přes Linu (karavany).

---

## 4. Guilda: ranky F a E, tabule v2

- Rank je písmeno: F po registraci, E po zkoušce. Další ranky až s oblouky. Stav: `guild.rank` (číslo
  0 až 6 ve stavu světa), body `guild.points`.
- Rank E: 3 splněné joby a pak zkouška **Goblin Bounty** (už existující job jako zkouška: Mira ji
  nabídne, až má hráč body).
- Tabule: joby z dat už jsou. Nové: job má `rank` (od kterého ranku), `days` (kolik dní visí) a
  `reward.points`. Dva nové joby ranku F: *Bread for the Watch* (doručení, učí rozvrhy) a *Stray
  Sheep* (najít ovce u Mill Hill a dojít je ohlásit Pipovi).
- Memorial Wall: deska v Guildě se jmény (zatím jen starší jména; přibývat začnou v etapě 5).

---

## 5. Noví lidé

| Postava | Kde | Co ukazuje | Háček |
|---|---|---|---|
| **Corwin Hale**, vedoucí pobočky | u stolu před hospodou, pořád | pije, vtipkuje, všechno ví | kdysi rank A (viz `characters.md`); ví, proč nesmí Guilda do Moonfellu |
| **Tam** (15), Hollisův učeň | u Hollise v Guildě, večer doma | horlivý, bojí se chyby | Hollis ho učí značky na železe (oblouk A) |
| **Edwin** (meč), vůdce Brightwater Four | ráno Guilda, den pryč, večer hospoda | sebejistý, hodný | zve hráče na pivo, nabídne společný job |
| **Moss** (luk) | s Edwinem | tichý, suchý humor | všímá si stop (první řekne o železe) |
| **Fen** (léčitelka) | s Edwinem, ráno u Wren | starostlivá | kupuje u Wren byliny na dluh |
| **Rook** (štít) | s Edwinem | hlučný, hladový | bratr mlynářova zetě |
| **Osric**, mlynář | mlýn na Mill Hill | lakomý, pyšný na dceru | svatba v den 24, člen rady |
| **Anwen**, jeho dcera | mlýn, večer u studny | zamilovaná, nervózní | svatba |
| **Hamm**, Pipův otec | Harrow Farm | unavený, dluží | člen rady, dluh u společnosti |
| **Maddy** (17) a **Tobias** | Harrow Farm, do dne 5 | Maddy drzá, Tobias mlčí | den 5 uneseni |

Každý má rozhovor se 3 až 5 uzly, jednu volbu tónu, která hýbe důvěrou, a rozvrh.

**Postaveno (2026-11-10):** všech 11 lidí je ve hře. Vzhled je zatím ruční `RigLook` v `NpcLooks` ze slovníku úpravy
vzhledu (účesy, vousy, `FaceStyle`), protože volby v `appearance.json` jsou jen mužské a ženy potřebují vlastní účesy.
Nové jméno: **Dunn**, Rookův bratr a Anwenin ženich. Corwinova sázka nastavuje flag `corwin.bet` (quest přijde později).
Maddy a Tobias jsou na farmě stále; zmizí až s událostí dne 5.

---

## 6. Důvěra a tóny

- Důvěra už ve stavu světa je. Nové: rozhovory ji **čtou** (`when: trust >= 10` otevře osobnější
  uzel) a deník ji ukáže slovem (stranger, known, trusted, friend), ne číslem.
- Tři tóny odpovědí (soucitný, pragmatický, tvrdý) mají v seznamu možností malou značku. Každá
  postava má hodnoty: komu se co líbí. Bram cení upřímnost, Lina zisk, Garrick klid, Edwin odvahu.
- První osobní questy (krátké, bez boje nebo s málem boje):
  - **Elda: A Letter for Jory.** Elda neumí psát. Hráč napíše dopis synovi ve vojsku (tři volby
    obsahu). Odpověď přijde v etapě 9.
  - **Tess: The Cold Forge.** Tess shání zprávy o muži Orrinovi. Hráč se zeptá Liny (ta lže), drb od
    karavan dorazí den 10. Háček na oblouk B a Saltdeeps.
  - **Corwin: One Honest Drink.** Corwin vsadí pivo, že hráč nepřežije týden. Sedmý den zaplatí a
    poprvé mluví vážně.

---

## 7. Výchozí události dnů 3 až 10 (`world/agendas.json`)

| Den | Plán | Co se stane | Co je vidět |
|---|---|---|---|
| 3 | company.iron | železo jde k goblinům | drb od Mosse: goblini u Hollow mají nové čepele |
| 5 | company.raid_harrow | nájezd na Harrow Farm v noci | Maddy a Tobias pryč (osud Gone), spálený plot, Pip na hlídce, Hamm u Garricka |
| 6 | brightwater.first_job | první job čtyřky | večer se chlubí, Rook má obvázanou ruku |
| 8 | village.council | rada mluví o volbě rychtáře | Lina obchází domy, drb |
| 10 | news.saltmere | zpráva o vzpouře otroků | tabule a Lina (dvě znění) |

Hráč do nich v etapě 4 zasáhnout nemůže (to je etapa 5); etapa 4 jen ukazuje, že běží.

---

## 8. Hotovo když

- Bot `village` přežije pět dní (spí v Guildě) a v logu je vidět, kdo kde kdy stál.
- Test nasimuluje dny 3 až 10 a ověří rozvrhy (kdo je večer u hospody, kdo v noci doma), události a
  to, že drb o nájezdu říká Pip jinak než Lina.
- Studio vyfotí nové lidi.
- Hráč získá rank E.

## 9. Co etapa 4 nedělá

Vyšetřování, zásah do nájezdu, Goblin Warren, holič, romance (jen přepínač v nastavení a žádné
romantické repliky), společníci.
