# Herní systémy pro příběh

Tutoriál, Guilda, questy, vyšetřování, stav světa, růst vesnice, postup hrou, lokace, monstra,
bossové, dungeony a technický plán. Děj je v `design/plot.md`.

---

## 1. Tutoriál (prolog „Fallen")

Cíl: za 25 až 35 minut naučit všechno podstatné. Každá mechanika má příběhový důvod. Rozhraní se
„probouzí" postupně: na začátku jen zdraví, po prvním monstru Mark (úroveň, zkušenosti), u Guildy
batoh, u Rhey stamina a blok.

| Krok | Místo | Učí | Jak |
|---|---|---|---|
| 0 | video | úprava vzhledu | 2D pixel art obrázky: přechod, náklaďák; pak jméno a úprava vzhledu |
| 1 | Mirror Lake | kamera, plavání ven | pád do jezera; pak síť a tma (scéna, žádný boj) |
| 2 | vůz lovců | rozhlížení, interakce | klec, Nell a Haruto mluví; hráč ohmatá zámek |
| 3 | les u cesty | lehký útok, pohyb v boji | goblini přepadnou vůz u ruin, hráč vezme dřevěný meč po mrtvé lovkyni, dva goblini |
| 4 | cesta | sbírání kořisti | mrtví lovci: chléb, lektvar, klíč od Nell |
| 5 | Slime Fields | těžký útok, červené značky, uhýbání, dash | slimáci na cestě; Nell radí („they jump where the ground glows") |
| 6 | Slime Fields | Mark, úroveň, krystal | první zabití rozsvítí rozhraní; Nell couvne |
| 7 | Harrow Farm | chůze kolem zvířat, sprint | Pip s vidlemi, stádo se plaší z běhu |
| 8 | pekárna | jídlo léčí | Elda nakrmí hráče, dá steak na cestu |
| 9 | Bram | dialog, volby odpovědí | první volba tónu (soucitný, pragmatický, tvrdý) |
| 10 | Guilda | registrace | Mira, Anchor Stone, rank F |
| 11 | aréna | blok, parry | Rhea, kolo 1: „hold the wheel, take my blows" |
| 12 | aréna | dash přes těžký úder, stamina | Rhea, kolo 2 |
| 13 | aréna | skutečný souboj, smrt a návrat | Rhea, kolo 3. Prohra = probuzení u Anchor Stone (učí smrt) |
| 14 | Guilda | tabule, odkupník, truhla, obchod | úvodní joby, Hollis, Wren |
| 15 | jih | skupinová AI, střelci, blok šípů | Goblin Bounty, pak rank E a odznak |

Bot `PIXEL_BOT=prologue` projde celý prolog přes skutečné rozhraní (rozšíření bota `guild`).

**Varianty, které jsem zvažoval:** začít rovnou ve vesnici (rychlejší, ale slabý háček a chybí
téma otroctví a lovců), začít v kleci bez pádu (chybí obraz „spadl z nebe", který vesnice
komentuje). **Doporučení:** pád, klec, útěk. Silný začátek a hned ukáže, o čem svět je.

---

## 2. Guilda

- **Ranky F, E, D, C, B, A, S.** Body ze jobů a zkouška. Akt I končí na C, akt II na A, S až po
  příběhu.
- **Zkoušky ranků:** E: Goblin Bounty. D: eskorta karavany Liny do East Watch (přepadení).
  C: noc v Greyfangu do rozbřesku. B: duel s Rheou bez omezení. A: dungeon na čas.
- **Tabule v2:** joby z dat, mění se podle stavu světa. Druhy: lov, sběr, doručení, eskorta, průzkum
  (najdi a vrať se), obrana (vlna na farmu), **výkup** (zaplať dlužnickou smlouvu a přiveď
  člověka zpět), **odměna na hlavu** (bandita, uprchlý lovec, později i Skyfallen zločinec).
  Joby s morální vahou: „Return escaped bondsman to Factor Holt, 40 gold." Hráč ho může přijmout,
  najít uprchlíka a nechat ho jít (job selže, karavany si to pamatují).
- **Memorial Wall:** deska se jmény padlých. NPC dobrodruzi umírají během příběhu (Brightwater Four,
  pojmenovaní dobrodruzi z jobů). Hráč u desky čte jména. Pro domorodce jedna chyba = konec.
- **Tituly:** Slime Slayer, Wolfbane, Oathbreaker, Friend of the Vel… Jen pro repliky NPC.
- **Pověst Guildy** je zvlášť od rank. Klesá při selhaných nebo odmítnutých jobech, roste při
  záchranách.

---

## 3. Questy a dialogy z dat

### Questy (`world/quests.json`)
- Quest má kroky. Krok má cíl, podmínky a výsledky.
- **Cíle:** zabij (id nebo prefix), přines předmět, mluv s NPC, dojdi na místo, vyber volbu
  v dialogu, přežij do času (obrana), doprovoď NPC.
- **Podmínky:** flag, rank, den a denní doba, pověst, hodnost ve frakci, důvěra postavy, předmět, atribut, NPC naživu.
- **Výsledky:** nastav flag, změň pověst, důvěru nebo podezření, posuň čas, dej nebo vezmi předmět a zlato, změň osud
  NPC (živý, mrtvý, odešel, přesunul se), spusť událost.
- **Termíny:** krok může mít `deadlineDay` a `onExpire` (výsledky, když hráč nestihne).
- Současné joby z `Guild.Quests` se převedou do nového formátu. Příkazy `AcceptSlimeQuest` a
  `AcceptGoblinQuest` zmizí ve prospěch `QuestCommands`.

### Dialogy (`world/dialogue/*.json`)
- Uzel: mluvčí, text, možnosti. Možnost: text, tón (soucitný, pragmatický, tvrdý, neutrální),
  podmínky (viz výše, plus atribut, například `Intelligence >= 6`), výsledky.
- Kontrola atributu se ukáže v textu možnosti: „[Intelligence] The ink on this page is fresh."
- Jeden NPC má víc „vstupů" seřazených podle priority podmínek. Bere se první platný.
  Tak se replika změní podle stavu světa bez kódu.
- `Dialogues.cs` zůstane jen jako vykreslování a obchody. Texty a větvení se přesunou do dat.

---

## 4. Vyšetřování

Hollisova vražda (oblouk A (Teeth and Iron)) a smrt krále (akt II) používají stejný systém.

- **Stopy** jsou předměty nebo fakta v deníku: „Poison bought from Wren", „Soldier's boot prints",
  „Garrick took coin".
- Stopy se dají najít (prozkoumat místo, kliknout na objekt), vyptat (dialog, někdy s kontrolou
  atributu), koupit (informátor), nebo vynutit (Strength, s následky).
- **Obvinění:** dialog s rozhodcem (Corwin, později královský soud). Hráč vybere viníka a předloží
  stopy. Málo stop = rozhodce nevěří a rozhodne dav. Špatný viník = trest pro nevinného.
- Vyšetřování má termín (do večera, do pohřbu). Svět nečeká.

---

## 5. Stav světa (WorldState)

- V `shared-core`, testovatelný v `dotnet test`:
  - **flagy** (`ch1.ledger.margravine`, `npc.hollis.dead`),
  - **pověsti** (Crown, Seraphine, Caius, Isolde, Company, Caravans, Church, Guild, Ironjaw,
    Moonfell, Hearthmere),
  - **kalendář** (den, denní doba, roční doba) a **plány frakcí** (který krok proběhl, který byl překažen),
  - **členství** (frakce, hodnost, podezření) a **důvěra** důležitých postav,
  - **osudy NPC** (živý, mrtvý, odešel, jinde),
  - **růst vesnice** (kdo se přistěhoval).
- Zóna JSON dostane u táborů, NPC, rekvizit a budov `requires` / `forbids` (podmínky jako v questech).
  Server zónu skládá podle stavu světa. Klient dostane jen výsledek.
- Server posílá stav světa klientovi zvlášť spolehlivou zprávou při změně, ne v každém snapshotu
  (snapshot se musí vejít do jednoho UDP paketu).
- **Uložení:** server ukládá hráče a stav světa do souboru (`user saves/<slot>.json`). Automaticky
  každé ráno a při odchodu. Ruční ukládání ne: volby mají být závazné. Jeden slot na postavu,
  plus automatická záloha z předchozího rána (pro pády a chyby, ne pro „zkusit to znovu").

**Varianty:** volné ukládání kdykoli (pohodlné, ale hráči by zkoušeli všechny volby a ztratilo by
se napětí), žádné ukládání mimo rána (čisté, ale bolí při pádu hry). **Doporučení:** auto-save
každé ráno a při odchodu, jedna záloha.

---

## 5a. Kalendář a plány frakcí

- Server drží kalendář (den, denní doba). Čas posouvá spánek, cestování, joby a čekání.
  Boj a rozhovory ne. Denní doba mění světlo, NPC rozvrhy (kdo kde stojí) a nepřátele (noc: vlci).
- Plány frakcí jsou data (`world/agendas.json`): kroky s dnem, podmínkou a výsledky, stejný formát
  jako questy. Server je vyhodnotí při každé změně času. Výchozí osa je v `design/timeline.md`.
- Zprávy a drby (`world/rumors.json`): text s podmínkou, šíří se tabulí, hospodou a NPC.
- Testy: simulovat 70 dní bez hráče a ověřit výchozí konec aktu I (podle `timeline.md` oblouk F (Moonfell)).

## 5b. Členství, podezření a důvěra

- Členství: frakce, hodnost (0 až 3), podezření (0 až 100). Při přetečení podezření se spustí
  quest prověrky nebo vyhazov.
- Důvěra postav (−100 až 100) a jejich hodnoty (co se jim líbí a nelíbí) jsou data u postavy.
- Kontrakty (Ashen Banner) jsou questy s druhem `contract` a zadavatelem; přijetí kontraktu proti
  frakci zvedne podezření u ní.

## 6. Růst Hearthmere

- Lidé, které hráč zachrání nebo vykoupí, se mohou usadit v Hearthmere: Nell, vykoupení dlužníci,
  uprchlí beastkin od karavan, Moonfell (pokud se neobnoví zvlášť), orčí kovář.
- Každý příchozí přidá něco viditelného: dům, stánek, službu (kovář s orčími zbraněmi, léčitel,
  lovec, krejčí). Stejně tak odchody a smrti něco uberou (zavřený obchod Liny, prázdná kovárna).
- V aktu III síla obrany Hearthmere = kdo tam žije. Odměna za soucit, která je vidět a hratelná.
- Inspirace: Suikoden (sbírání lidí do hradu), Fallout (osady), ale bez stavění: vesnice roste podle
  příběhu, ne podle hráčova stavitelství.

---

## 7. Společníci (později, po aktu I)

- Sora, Kai, Lisbet (léčba Moonfellu), Vashra (mír s orky), Nell (jen jako pomocnice ve vesnici,
  ne do boje), Corran (karavany).
- Jeden společník s hráčem. AI na serveru jako nepřítel naruby (stejné AI role: ranged, support,
  brute), schopnosti z dat.
- **Doporučení:** až po dokončení aktu I. Je to velká funkce (AI, animace, dialogy po cestě) a
  příběh aktu I bez ní funguje.

---

## 8. Postup hrou

| Část | Úroveň | Oblasti | Rank |
|---|---|---|---|
| Prolog | 1–3 | Mirror Lake, Slime Fields, Hearthmere, aréna | F |
| oblouk A (Teeth and Iron) | 3–8 | Goblin Hollow, Goblin Warren, Westwood, East Watch | E |
| oblouk E (Wolf Winter) | 7–11 | Harrow Farm, Greyfang, Stone Circle | D |
| oblouk F (Moonfell) | 10–14 | Moonfell, krypta | D |
| oblouk G (Iron and Honor) | 13–17 | Ironjaw, Southern Wilds, East Watch | C |
| Finále aktu I | 16–18 | Silver Falls, Sunken Archive | C |
| Akt II | 18–30 | Varn's Crossing, Marshes of Ell, Saltmere, Saltdeeps, Highcrest | B–A |
| Akt III | 30–40 | Marche v Blightu, Kareth, Blightmarch, Heart Lantern | A |

Úrovně jsou zatím odhad. Obtížnost a balanc se neladí (pravidlo projektu), měří se až později.

---

## 9. Lokace

### Akt I (Meadow Valley, existuje, potřebuje úpravy)
- **Hearthmere**: přejmenovat Meadow Village. Přidat hřbitov, Memorial Wall v Guildě, sklad Guildy
  (místo vraždy), hospodu (rvačka Kaie), místa pro budoucí domy.
- **Mirror Lake**: tábor lovců s klecemi a vozem.
- **Goblin Warren**: malý dungeon pod Goblin Hollow (interiér na platformě mimo mapu, jako Guilda).
- **East Watch**: pevnost, sklad společnosti, šibenice, cela.
- **Moonfell**: kaple, krypta (dungeon), domy.
- **Greyfang Den**: místo pro bosse.
- **Sunken Archive**: dungeon pod Silver Falls.

### Akt II a III
Varn's Crossing, Marshes of Ell, Saltmere, Saltdeeps, Highcrest, Undercanals, Blightmarch.
Vyžadují víc zón na jednom serveru a cestování mezi nimi (technicky velký krok).

---

## 10. Monstra

| Monstrum | Kde | Nápad |
|---|---|---|
| Blighted slime | Greyfang, sever | šedý, po smrti se rozdělí na dva menší |
| Blight wolf | Greyfang | stínový, mizí a objevuje se za hráčem |
| Husk | Blightmarch, Archive | chodící zbytek duše, pomalý, v hejnech |
| Auren construct | Archive, Old Ruins | golem z kamene a světla, štít z jedné strany |
| Lantern wisp | Archive | světélko, krade staminu, léčí konstrukty |
| Mimic | dungeony | truhla, která kousne; předtím se nepatrně hýbe |
| Bandit, sellsword | silnice, akt I a II | lidé, utíkají, když prohrávají, vzdávají se |
| Finder (lovec) | Mirror Lake, akt I | síť (znehybní), obušek; chce chytit, ne zabít |
| Inquisitor | Moonfell, akt II | stříbro, světelné kopí, štít víry |
| Giant spider | Westwood | sítě, jed, pavouci ze stropu v jeskyních |
| Harpy | Ridges | útok shora, krade předměty |
| Bridge troll | cesta k Varn's Crossing | chce mýtné, dá se zaplatit, nebo bojovat |
| Lizardfolk | Marshes of Ell | oštěpy z vody, ve skupinách |
| Crypt dead | Moonfell krypta | mrtví vesničané, které Blight zvedl |
| Hollowed | akt III | Skyfallen bez paměti, používají skilly hráče |

---

## 11. Regionální bossové

| Oblast | Boss | Fáze |
|---|---|---|
| Goblin Warren | **Warlord Grub** | 1: na kanci, nájezdy přes arénu. 2: kanec padne, Grub s ukovanou helmou a štítem, volá goblinské střelce. |
| Greyfang Den | **Old Greymaw** | 1: obří vlk, skoky a kousnutí. 2: rozpadne se na tři stíny, skutečný je ten, který vrhá stín. 3: zuřivost, Blight na zemi. |
| Moonfell krypta | **Moon-Eaten Lord** | 1: baron jako člověk mluví (dialog, prosba). 2: proměna, drápy, skok. 3: volá vlkodlaky, kteří váhají, pokud hráč nese Mirin šátek. |
| Ironjaw | **Warchief Gor Ashka** (jen válka) | 1: dvouruční sekera, otřesy. 2: volá klany, ostatní šampioni (pokud žijí). |
| Sunken Archive | **Archive Warden** | 1: golem s paprsky, kryje se za štítem. 2: hádanka se zrcadly, světlo na jeho jádro. 3: zuřivost. |
| Blightmarch | **The Rootmother** | akt III, několik fází, hlasy známých mrtvých |

Bossové jsou data jako ostatní nepřátelé (`enemies.json`), schopnosti v `GameServer.Fire`, plus
**fáze** (nový údaj: při jakém zdraví se mění sada schopností). Po zabití se nastaví flag.

---

## 12. Dungeony

Pravidla:
- Interiér na platformě mimo mapu (jako Guilda a aréna), vstup portálem.
- 15 až 30 minut, jedna smyčka se zkratkou zpět ke vchodu (otevřou se dveře zevnitř).
- Jeden nový nápad na dungeon: Warren = pasti a kotce, krypta = tma a světlo pochodně,
  Archive = světelné hádanky se zrcadly.
- Mini-boss v půlce, boss na konci, mimic truhly, jedna tajná místnost s lore (deník Skyfallen
  nebo auréjský nápis).

---

## 13. Technický plán

### Co je jisté (rozhodnuto)
Hra pro jednoho hráče, lokální server a klient zůstávají, co-op možná později.

### Etapy
Pořadí výroby je v `design/stages.md`.

### Rizika
- **Množství textu.** Akt I má odhadem 40 až 60 tisíc slov dialogů. Psát po etapách, hned do dat,
  a hned testovat botem.
- **Kombinatorika voleb.** Každá velká volba zdvojnásobí obsah. Proto cesty jako sady nástrojů na
  stejné situace (ne oddělené příběhy), málo velkých situací na oblouk, a následky přes flagy, které
  mění malé věci (repliky, NPC, tábory), ne celé zóny.
- **Nové modely.** Klec, vůz, šibenice, hřbitov, nové postavy, beastkin (uši a ocas na anime rig).
  Seznam na začátku každé etapy.
- **Velikost snapshotu** se nezmění, stav světa jde zvlášť.
