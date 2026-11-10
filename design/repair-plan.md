# Plán oprav po prvním hraní (2026-11-10)

Hráč (autor hry) poprvé prošel prolog a vesnici a výsledek označil za otřesný. Testy a boti přitom
procházeli. Tento plán má přednost před zbytkem etapy 4. Nový obsah (ranky, questy, další lidé) se
nepřidává, dokud fáze R1 až R4 nejsou zahrané a schválené autorem.

Plán je psaný pro agenta, který začíná bez kontextu. Pravidla práce jsou v `CLAUDE.md`, oddíl
„Done means". Stav poslední session je v `design/handoff.md`.

Texty ve hře anglicky, poznámky česky.

---

## 0. Jak s plánem pracovat

- **Jedna fáze = jedna session.** Fáze končí commitem, pushem, řádkem v `design/status.md` a krátkým
  seznamem „co si má autor vyzkoušet" v závěrečné zprávě. Další fáze až po jeho odpovědi.
- **Nejdřív ověř, pak oprav.** U každé fáze je uvedeno, co je potvrzené v datech a co je jen hlášení
  od autora. Hlášení nejdřív zopakuj ve hře (bot, snímky), teprve potom měň kód.
- **Vzdorovitý hráč.** Poslušný bot `prologue` chodí přesně tam, kde scéna čeká, a proto chyby
  neviděl. Každá fáze, která sahá na příběh, přidá nebo rozšíří běh, kde hráč dělá něco jiného
  (uteče, přeskočí postavu, přijde v jiném pořadí).
- **Souběžná práce.** R1 a R4 sahají na stejné soubory (`world/dialogue`, `world/scenes`) a musí jít
  po sobě. R2 (mapa, `Minimap.cs`, `Hud.cs`), R3 (kamera, `GameClient.UpdateSpeaker`, `FaceRig`) a
  R5 (vzhledy, `Dialogues.cs` část `NpcLooks`) se navzájem nepřekrývají a snesou souběh ve
  vlastních worktree. `GameClient.cs` a `Hud.cs` jsou velké sdílené soubory: dva agenti do nich
  současně nepíší.
- **Rozhodnutí, která patří autorovi** (větvení příběhu, vzhled, co je hotové), se neodhadují:
  doporučení, kompromisy, otázka.

---

## R1. Prolog drží pohromadě

**Cíl:** prolog nejde rozbít tím, že hráč udělá něco jiného, a nikdo nemluví o tom, co se nestalo.

### Potvrzeno v datech

| Problém | Příčina |
|---|---|
| Únoscům jde utéct | `prologue_1_intro` odemkne ovládání a čeká na `waitFor at (70,108) r 3.5` bez časového limitu |
| Mira vítá Nell, která tam není | `mira.json` má vstup `prologue_arrive` na `prologue.started` + `!prologue.met_mira` + rank 0; `prologue.started` se nastaví v prvním kroku úvodu, ne při události |
| Nell jde za hráčem donekonečna | `follow nell` skončí až ve scéně `prologue_8_guild`; ta vyžaduje `prologue.slept`, který nastaví jen scéna u Eldy (`prologue_6_elda`, okruh 4,5 m u pekárny, jen v noci). Kdo jde rovnou do Guildy, scénu 8 nespustí |
| Hráč neví, kam Nell dovést | krok questu říká „Get Nell to Hearthmere, west along the road"; cílem je Elda u pekárny, to text neříká a značka na mapě není |
| „da" místo „dad" | `nell.json`: záměrný nářeční tvar, čte se jako překlep |

### Rozhodnuto autorem

- **Zajetí je nevyhnutelné.** Když se hráč od jezera vzdálí nebo otálí, lovci hodí síť. Větev
  „utekl jsem" se nestaví.

### Úkoly

1. **Zajetí.** Krok `waitFor` ve scénách dostane `timeout` (sekundy) a podmínka smí být složená
   (`any`). V úvodu: čekat na „hráč u lovců" NEBO „hráč dál než 14 m od břehu" NEBO 25 s. Pak lovci
   k hráči dojdou (`approach`), ovládání se zamkne, proběhne Voskův rozhovor a síť. Hráč, který
   utíká, uslyší jinou první větu než ten, kdo přišel sám (jedna věta navíc, ne větev).
2. **Flag říká, co se stalo.** Projít všechny flagy `prologue.*` a u každého zapsat do tabulky
   v `design/stage-3.md`: kdy se nastaví a co tvrdí. `prologue.started` znamená jen „prolog běží".
   Co tvrdí víc (Nell je s hráčem, hráč byl v kleci), dostane vlastní flag nastavený v tu chvíli.
3. **Nell je s hráčem.** Server nastaví `nell.with_player` při `follow` a smaže ho, když Nell
   zůstane jinde. Každá replika, která Nell zmiňuje (Mira, Bram, Pip, Elda), má tuto podmínku.
   Totéž pro jiné společníky později: zavést obecně (`with.<id>`), ne jen pro Nell.
4. **Pořadí míst.** Tabulka všech scén prologu: spouštěč, co když hráč přijde jinam dřív. Řešení
   pro noc: Guilda je v noci zamčená (věta u dveří „Shut till morning. Elda's oven is lit.") a cíl
   ukazuje na pekárnu. Scéna u Eldy se spustí v širším okruhu a Elda hráče osloví sama (`approach`).
5. **Cíle s místem.** Každý krok `prologue_fallen` má text, který jmenuje místo, a cíl s polohou
   (viz R2). Krok 2: „Take Nell to the bakery in Hearthmere. Elda bakes before dawn."
6. **Prostá angličtina.** `da` → `father`. Projít texty prologu vcelku jako scénář (nástroj z R4).
7. **Vzdorovitý bot.** `PIXEL_BOT=prologue` dostane varianty (`PIXEL_CONTRARY=flee,guild_first,
   skip_bram,skip_pip`): uteče od jezera, jde v noci rovnou do Guildy, vynechá Brama, vynechá Pipa.
   `scripts/prologue.ps1 -Contrary` je všechny projde; každá musí skončit `BOT done`.

### Hotovo když

- `scripts/prologue.ps1` i `-Contrary` projdou.
- Test nad daty: každý `waitFor` ve scéně s odemčeným ovládáním má `timeout` nebo `until`.
- Test: žádný uzel dialogu s textem „Nell" není dosažitelný bez `nell.with_player` nebo
  `nell.hidden` (seznam jmen a jejich dokazujících podmínek v testu, rozšiřitelný).
- Autor prolog prošel a nenašel místo, kde nevěděl, co dál.

---

## R2. Hráč ví, kam jít

**Cíl:** v každé chvíli je vidět cíl: text, značka na minimapě, velká mapa, cesta ve světě.

### Stav

- `Hud.Objective` (text pod minimapou) existuje, skládá ho `GameClient.Objective`.
- Kroky questů mají `goal` (`{"talk": "elda"}`), tedy cíl jde odvodit z polohy postavy
  (`Schedules.RestOf`, pro chodce z `ActorView`).
- Minimapa (`Minimap.cs`) značku cíle nemá. Obrázek mapy `client/assets/ui/minimap.png` existuje.
- `RoadMap.Route` (v `shared-core/Schedules.cs`) umí cestu po silnicích; klient ji může počítat sám.

### Úkoly

1. **Cíl jako data.** Krok questu smí mít `at: [x, z]` a `place: "the bakery"`; bez nich se cíl
   vezme z `goal` (postava, nepřítel → nejbližší tábor, předmět → kde se bere). Funkce
   `Story.Target(step, context, world)` ve sdíleném kódu, aby ji měl klient i testy.
2. **Značka na minimapě.** Zlatý kosočtverec v místě cíle; mimo výřez šipka na okraji s metry.
3. **Velká mapa na M.** Celá obrazovka, posun a přiblížení myší, objevená místa se jmény, hráč,
   cíl, postavy s úkolem. `PIXEL_BOT=map` ji otevře pro snímek.
4. **Vlastní bod.** Klik na velké mapě položí bod (další klik ho zruší). Uložit na serveru v savu
   (`Command.SetWaypoint`), ne v `settings.cfg`: patří ke hře, ne k počítači.
5. **Cesta.** Od hráče k cíli nebo k vlastnímu bodu přes `RoadMap.Route`: tečkovaná čára na obou
   mapách a ve světě řada světlých značek na zemi před hráčem (prvních 40 m, přepočet při odchylce
   nad 6 m nebo po 2 s). Uvnitř budov a v divočině bez cest vede přímá čára ke dveřím nebo k cíli.
6. **Test.** Každý krok každého questu má cíl, který `Story.Target` umí najít, nebo výslovné
   `explore: true` s textem, který říká kde hledat.

### Hotovo když

- Snímky: minimapa se značkou, velká mapa s bodem a cestou, cesta na zemi v noci i ve dne.
- Měření `frameMs` s cestou zapnutou a vypnutou (trail nesmí stát víc než 0,3 ms).
- Autor došel z jezera do pekárny jen podle značek.

---

## R3. Pohled na mluvčího

**Cíl:** v rozhovoru je vždy vidět obličej, nic nestojí v cestě a obraz se necuká.

### Hlášení autora (neověřeno v kódu)

„Někdy se to cuká, někdy není vidět obličej, někdy je něco v cestě."

### Pravděpodobná příčina (ověřit)

`GameClient.UpdateSpeaker` staví kameru portrétu do světa 2,4 m před hlavu mluvčího
(`head.ToGlobal(0.12, 0.02, -2.4)`) a snímá živou scénu. Sedí-li postava u stolu nebo stojí u zdi
či pultu, je mezi kamerou a obličejem stůl, soused nebo zeď. Cukání: hlava se hýbe s animací a
kamera ji sleduje bez vyhlazení; postava se během řeči může otáčet nebo jít podle rozvrhu.

### Úkoly

1. **Zopakovat.** Studio úloha `talks`: každá postava na každém místě svého dne (stojí, sedí,
   za pultem), snímek portrétu a snímek hlavní kamery. Arch přečíst a spočítat vadné.
2. **Portrét bez světa.** Mluvčí se kreslí do portrétu sám: vlastní vrstva (`VisualInstance3D.Layers`)
   a `CullMask` kamery jen na ni, vlastní světlo a pozadí. Nic ze světa pak nemůže překážet.
3. **Klid.** Kamera portrétu se váže na kotvu, která se vyhlazuje (jen poloha hlavy bez kývání).
   Postava, se kterou hráč mluví, na serveru stojí a dívá se na hráče, dokud rozhovor trvá
   (rozvrh počká; ověřit, že to tak je i mimo scény).
4. **Hlavní kamera.** V rozhovoru se posune tak, aby byli v záběru oba a mezi kamerou a mluvčím
   nebylo nic pevného (zkouška paprskem proti `World`; při zásahu se kamera přiblíží).
5. **Automatická kontrola.** Na snímku portrétu musí barva kůže mluvčího zabírat střed obrazu;
   studio úloha vypíše, kdo neprošel.

### Hotovo když

- Arch `talks` bez vadného portrétu, včetně sedících u všech tří stolů a Wren za pultem.
- Autor promluvil s pěti lidmi po sobě a pohled ho nerušil.

---

## R4. Rozhovory s hlavou a patou

**Cíl:** každý rozhovor má důvod, odpovídá tomu, co se stalo, a čte se jako řeč jednoho člověka.

### Potvrzeno

- Bram má mimo prolog jednu větu a pak jen „What do you make of...?" o sedmi lidech.
- Starší postavy (Kai, Sora, Lina, Garrick, Tess) mají jednu statickou větu.
- Nových 11 lidí z etapy 4 má 3 až 5 uzlů psaných podle šablony „dvě otázky + volba tónu".
- Nikdo nereaguje na to, kdo hráč je (nový Lightborn, registrovaný, s Nell, po nájezdu), kromě
  Pipa a Hamma po nájezdu.

### Postup pro každou postavu

1. **List postavy** (do `design/people/<id>.md`, česky, půl stránky): co chce, čeho se bojí, co ví
   a odkud, co tají, co si myslí o Lightborn, jak mluví (dvě tři věty vzorku), co od hráče může
   chtít. Vychází z `design/characters.md` a `world/people.json`.
2. **Stavy setkání.** Úvodní uzel podle stavu, od nejkonkrétnějšího: právě se stalo něco, co se jí
   týká → hráč pro ni něco udělal nebo jí ublížil → zná hráče (důvěra „known") → první setkání.
   První setkání se řekne jen jednou (`met.<id>`).
3. **Témata místo šablony.** Otázky hráče jsou to, na co by se člověk v té chvíli opravdu zeptal:
   kde jsem, co je tohle za místo, co se tu děje, co potřebuješ. Téma zmizí, když je vyčerpané,
   a nové se objeví s událostí.
4. **„What do you make of...?"** jen o lidech, ke kterým má postava silný vztah (pouto nad 20 nebo
   pod −10), nejvýš tři najednou, a až od důvěry „known". Cizímu člověku nikdo nepomlouvá sousedy.
5. **Každá věta nese informaci nebo povahu.** Věta, kterou lze škrtnout beze ztráty, se škrtne.
6. **Čtení vcelku.** `tools/script.py <id>` vypíše rozhovor jako scénář (stavy, větve, podmínky).
   Agent ho přečte nahlas od začátku do konce za každý stav a opraví, co nenavazuje.
7. **Korektura.** `tools/proof.py` projde `world/dialogue`, `people.json`, `rumors.json`,
   `world/scenes`, `quests.json`, `journal.json`: slova mimo slovník (anglický seznam + jména ze
   hry), zdvojené mezery, chybějící tečky, nářeční tvary. Běží v `tests/test_tools.py`.

### Pořadí

Bram → Mira → Nell → Vosk a lovci → Elda → Pip → Garrick → Tess → Lina → Wren → Hollis → Kai → Sora
→ Corwin → Brightwater Four → mlýn → Harrow Farm. Po každých třech postavách commit a seznam pro
autora.

### Hlasy

Nahrávky se dělají až po schválení textu postavy (`generate_voices.py` platí jen změněné věty).
Dnešní nahrávky Brama, Miry a Corwina přepsáním zastarají; to je v pořádku, byla to zkouška.

### Hotovo když

- Každá postava má list, aspoň dva stavy setkání a žádnou větu, která tvrdí neověřenou věc.
- `tools/proof.py` bez nálezu.
- Autor přečetl scénáře prvních tří postav a schválil styl, než se píší další.

---

## R5. Lovci nejsou tři stejní muži

**Potvrzeno:** Gil, Hanne a Corb mají roli `finder` a ta má jediný vzhled s palicí.

**Úkoly:** `NpcLooks.For` hledá nejdřív podle id, pak podle role. Gil: hubený, síť přes rameno,
dýka. Hanne: žena, luk, krátké vlasy. Corb: velký, palice, vousy. Zbraně, které rig umí: `sword`,
`dagger`, `bow`, `club`, `staff`, `none`. Síť jako nový díl v Blenderu (`asset-pipeline`), pokud
jde udělat do hodiny; jinak jen v textu.

**Hotovo když:** studio `people` ukáže tři rozlišitelné lovce a Voska; autor je od sebe pozná.

---

## R6. Úvodní video

Autor vyrobí dvě videa po 10 s v Gemini (prompty dostal 2026-11-10) a uloží je do `refs/`.

**Úkoly:** převod do Ogg Theora (`ffmpeg -c:v libtheora -q:v 8`, Godot jiné video nepřehraje),
krok scény `video` (soubor, přeskočení klávesou), nahradit krok `slides` v `prologue_1_intro`.
Text „Unfinished." a „Bound." a název hry zůstanou jako titulky přes konec videa.

**Hotovo když:** nová hra začne videem, jde přeskočit, bot `prologue` projde.

---

## Potom

Zbytek etapy 4 (`design/stage-4.md`): ranky F a E, dva nové joby, Memorial Wall, tři osobní questy,
důvěra slovem v deníku, bot `village`. Všechno už podle pravidel „Done means".
