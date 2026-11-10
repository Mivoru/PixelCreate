# Plán světa Velmora (2026-11-10)

Plán celého hratelného světa: regiony, města, vesnice, dungeony, cesty, voda, výšky, rychlé cestování,
podzemí, zkratky a vizuální styl každého místa. Jména ve hře anglicky, popis česky. Hra už není
příběhová (`design/direction.md`): svět má hlavu a patu sám o sobě, zeměpisem, kulturami a tím, co
v něm žije. Starší lore (`design/world.md`) je zásoba jmen a dějin, ze které plán čerpá.

- Data plánu: `design/world/layout.json` (souřadnice v metrech, sever = −z jako na herních mapách).
- Mapy: `python tools/world_map.py` vykreslí `design/maps/world-map.png` (mapa světa s regiony,
  úrovněmi, městy, dungeony, waystones, cestami a výřezem Meadow Valley) a `design/maps/elevation.png`
  (výšky, stínování, vrstevnice po 50 m, hřebeny, průsmyky, řeky a jezera).
- Monstra u regionů jsou jen návrh. Rozměry a úrovně jsou plán, ne balanc.

---

## 1. Velikost a stavba světa (rozhodnutí pro autora)

**Doporučení: svět ze zón, každá zóna jeden region.** Celá mapa má 4 × 3,6 km. Hratelná část je
deset regionů po 600 až 1 200 m, každý jedna zóna (`world/zones/*.json`), jako dnešní Meadow Valley.
Server drží vždy jen zónu, ve které hráč je. Mezi zónami se přechází přirozenými hrdly: průsmyk,
most, brod, přístav, brána. Krátký přechod (zatmění, zhruba 2 s) skryje načítání.

| Varianta | Pro | Proti |
|---|---|---|
| **Zóny po regionech (doporučeno)** | sedí na dnešní technice (zóna, kolize, minimapa, audit cest), každý region se staví a ladí zvlášť, výkon zůstane jako dnes (8 až 10 ms) | přechod mezi zónami, hrdla musí být navržená |
| Jeden souvislý svět 4 km | žádné přechody, volný pohled do dálky | streamování terénu, rekvizit a nepřátel, velký zásah do serveru i klienta, riziko výkonu na RTX 3050, měsíce práce před prvním novým regionem |
| Menší svět (3 až 4 regiony) | rychle hotové | málo místa pro přístav, hlavní město a pozdní obsah |

Proč 4 km a ne víc: Meadow Valley (600 m) zabrala týdny. Deset regionů je zhruba rok práce v dnešním
tempu. Větší svět by zůstal prázdný.

**Měřítko výšek:** mapa udává výšku nad mořem. Zóny mají vlastní terén. Při stavbě se výška regionu
převede posunem: Meadow Valley leží kolem 75 m, Highcrest kolem 245 m. Ve hře se to pozná podle
výhledů (z Ridges je vidět dolů na údolí) a podle stoupání silnic mezi zónami.

**Světové strany:** herní mapa má sever nahoře (−z). Tři jména v dnešní zóně tomu odporují. „Southern
Wilds" leží na severu (z −215). Greyfang leží na jihovýchodě, ne na severovýchodě. Moonfell leží na
jihozápadě, ne na severozápadě. Návrh: Southern Wilds přejmenovat na **Northern Wilds**. Popisy
Greyfang a Moonfell opravit v dokumentech. Jejich polohu neměnit, navazují na regiony za okrajem.

---

## 2. Velké tvary světa

- **Sever: The Ridges** (hřebeny 500 až 900 m). Zeď mezi královstvím a mrtvým severem. Dva průsmyky:
  u Lantern Watch a u The Watchers (obří auréjské sochy).
- **Za hřebeny: Blight Frontier.** Šedá plošina kolem 320 m. Popel, mrtvé stromy, černé kořeny,
  vybledlé barvy. Pozdní obsah, Heart Lantern na samém okraji mapy.
- **Západ: Greyspine Mountains** (do 1 600 m, jediné zasněžené štíty). Hranice s Karethem. Jeden
  průsmyk, Greyspine Gate.
- **Střed: Riverlands.** Nížina kolem řeky Alder (20 až 50 m), pole, sady, mlýny, nejhustší osídlení.
- **Severozápad od středu: Highcrest Plateau.** Náhorní plošina kolem 245 m s hlavním městem
  a jezerem Lake Calder pod útesem.
- **Východ: Eastern March.** Pohraničí. Meadow Valley je kruhové údolí (dno 75 m, okraj do 300 m)
  se sedmi sedly. Severovýchodně Ironjaw Highlands (plošina 325 m, orkové), jihozápadně Moonfell Downs.
- **Jih: pobřeží.** Moře Sea of Sails. Saltcliffs (útesy 70 m) mezi přístavem Saltmere a rybářskou
  osadou Gull's Rest. Na jihozápadě se moře stáčí do zálivu The Saltreach.
- **Jihozápad od středu: Marshes of Ell.** Bažina v pánvi pod Riverlands, jezero Blackmere.

**Voda:** Alder pramení pod Ridges, teče jezerem Greywater, městem Varn's Crossing a bažinou do moře
u Saltmere. Je to páteř světa: lodní doprava, mlýny, mosty, brody. Přítoky: Calder Stream z Lake Calder
a Westbrook. Voda v údolí odpovídá postavené zóně: **Ironwash** teče z Ironjaw Highlands soutěskou
(Ironwash Gorge) východním sedlem do údolí a ústí do Mirror Lake. Z jezera odtéká potok k úpatí
Silver Falls, kde začíná **Silverbrook**; ten teče údolím na sever a západním sedlem ven, odtud jako
**Westbrook** Thornwoodem do Alder. **Mirrorrun** pramení za jižním okrajem údolí (nad Silver Falls)
a teče na jih k moři u Gull's Rest. Jezera: Mirror Lake (místo dopadu Skyfallen), Lake Calder,
Blackmere, Moonfell Tarn, Greywater.

**Cesty:**
- **King's Road** (hlavní silnice, dlážděná, s milníky): Highcrest, Varn's Crossing, Brindle,
  Hearthmere, East Watch, Fort Ironwatch.
- **Salt Road** (štěrk, v bažině povalová cesta na kůlech): Varn's Crossing, Reedmoor, Saltmere.
- **Coast Road**: Saltmere, Gull's Rest, po hraně útesů.
- **Ridge Road**: Highcrest, Lantern Watch.
- **Greyspine Road**: Highcrest, Greyspine Gate.
- **Stezky** (vyšlapané, bez dlažby): Mirror Way podél Mirrorrun, Thornwood Track, Moonfell Path,
  Ironjaw Trail, Oakhurst Lane.

Každá silnice dostane svůj vzhled (dlažba, štěrk, hlína, prkna), aby hráč poznal, kde je.

---

## 3. Cestování

- **Waystones**: auréjské kameny (bílý kámen, azurové runy, stejné jako Anchor Stone). Dotykem se
  odemknou. Cestuje se **jen od kamene ke kameni**: hráč stojí u odemčeného kamene a z jeho mapy
  vybere jiný odemčený kámen, za drobný poplatek v krystalech. Z libovolného místa mapy se cestovat
  nedá, aby cesta krajinou zůstala hrou a ne čekáním na načtení.
  Jeden kámen v každém městě a vesnici a jeden u každého velkého dungeonu, celkem 14
  (`layout.json` → `waystones`).
- **Lodě:** říční člun z Varn's Crossing do Saltmere (vyhlídková plavba po Alder, přeskočitelná)
  a loď ze Saltmere do Gull's Rest podél útesů. Lístek u přístavníka.
- **Zkratky, které se odemykají** (jako v Dark Souls: dveře, které se otevřou až z druhé strany):
  - **The Old Way**: auréjská podzemní silnice z Old Ruins (Meadow Valley) přes Fernhollow
    a Tanner's Drop (Varn's Crossing) do Undercanals (Highcrest). Každý úsek se otevírá z druhé
    strany, takže pozdější průchod je rychlá zkratka.
  - **Warren tunnels**: z Goblin Hollow (Meadow Valley) pod okrajem údolí do Ironjaw Highlands.
  - **Smugglers' Path**: z Reedmooru bažinou do jeskyně pod Saltmere (obchází Salt Road).
  - **Hanging Bridge**: provazový most přes soutěsku Alder pod Highcrestem. Spustí se páka na druhé
    straně a ušetří půl cesty kolem jezera.
  - **Giant's Stair**: schody vytesané do Saltcliffs, z pláže na Coast Road.
- **Výhledy:** z vrcholu každé věže a z průsmyků je vidět sousední region. Hráč vidí, kam půjde.

---

## 4. Regiony

Každý region má: úroveň, náladu, krajinu, sídla, zajímavá místa, dungeony, návrh monster a hrdla
do sousedních regionů.

### 4.1 Meadow Valley (úroveň 1 až 8): postavená zóna
Kruhové údolí, zelené a bezpečné uprostřed, divočejší u okraje. Hearthmere, Guilda, mlýn, Harrow
Farm, Mirror Lake s tábořištěm lovců, Silver Falls, Stone Circle, Old Ruins, Goblin Hollow,
Whispering Woods, Emerald Jungle, Slime Fields. Na okraji Greyfang (vlci), Moonfell (vlkodlaci),
Ironjaw Outpost (orkové).
- **Dungeony k doplnění:** Sunken Archive pod Silver Falls (5 až 8, voda a auréjské knihovny,
  slimové a utopenci), Old Ruins Undercroft (6 až 9, základ Lanterny, auréjští strážci), Goblin
  Warren (7 až 10, chodby goblinů, vězni, náčelník), Greyfang Den (6 až 9, jeskyně alfy vlků).
- **Hrdla:** západní sedlo (King's Road do Thornwoodu), východní sedlo (East Watch, Ironjaw),
  jižní sedlo (Silver Falls, Mirror Way k pobřeží), jihozápadní sedlo (Moonfell Path).

### 4.2 Thornwood (6 až 12)
Starý listnatý les západně od údolí: duby, buky, mech, kapradí, světlo v paprscích, kořeny přes
cestu. Kdysi domov beastkin (the Vel), dnes jejich zarostlé kamenné kruhové domy a vyřezávané
totemy zvířat.
- **Sídla:** **Fernhollow** (vesnice na mýtině kolem pramene Westbrooku), **Brindle** (osada
  u King's Road: hostinec, kovář, stáje).
- **Zajímavá místa:** Elder Oak (obří dub s dutinou), Vel Shrine (kruh totemů), Thornback Hills
  (skalnatý hřbet na severu s výhledem na celé Riverlands), opuštěná pila.
- **Dungeon:** **Thornwood Barrows** (9 až 12): mohyly Vel, kamenné komory, kořeny skrz strop,
  oživlí strážci hrobů, pavouci.
- **Monstra (návrh):** obří pavouci, divočáci, medvěd, bandité v lese, dryádí strážci (oživlé
  kořeny), vlci.
- **Hrdla:** King's Road na západ do Riverlands, Thornwood Track do Thornback Hills.

### 4.3 Moonfell Downs (10 až 16)
Vřesoviště a řídké borovice jihozápadně od údolí, mlha každou noc, Moonfell Tarn s ostrůvkem.
Vesnice Moonfell, kterou církev „léčila" světlem Lanterny, je dnes zpustlá a obydlená vlkodlaky.
- **Zajímavá místa:** zpustlá vesnice Moonfell (vyhořelý kostel s prasklou lucernou), Tarn s menhirem
  uprostřed, Redfen (goblini u bažinatého potoka).
- **Dungeon:** **Moonfell Hollow** (11 až 15): jeskyně pod jezírkem, doupě vlkodlaků, boss Redclaw
  nebo Moonleaper ve velkém.
- **Monstra:** vlkodlaci (všechny dnešní varianty), přízraky v mlze, goblini z Redfenu, havrani.
- **Hrdla:** Moonfell Path do údolí, brod přes Alder do Marshes of Ell.

### 4.4 Mirror Coast (6 až 11)
Údolí Mirrorrun od Silver Falls k moři: louky, olše u řeky, pastviny ovcí, pak duny a oblázková pláž.
Klidný region pro nováčky, alternativa k lesu.
- **Sídla:** **Gull's Rest** (rybářská osada: šindelové domy na kůlech, sítě, sušárny ryb, maják na
  skále, přístaviště lodi do Saltmere).
- **Zajímavá místa:** Shipwreck Shore (vrak karaky na pláži, kořist a krabi), Smugglers' Cove
  (jeskyně banditů pod útesem), kaskády Mirrorrun.
- **Monstra:** krabi, mořští slimové, harpyje na útesech, bandité, divoké kozy.
- **Hrdla:** Mirror Way do údolí, Coast Road do Saltmere, loď.

### 4.5 Ironjaw Highlands (11 až 17)
Náhorní plošina severovýchodně: suchá tráva, rezavá skála, osamělé borovice (Pinehold), vítr.
Orčí země, kam se orkové tlačí z Ashsteppes. Ironwash Gorge rozděluje plošinu na dvě poloviny.
- **Sídla:** **Fort Ironwatch** (hraniční pevnost markraběnky: kamenná hradba, dřevěné věže,
  kasárna, kovárna, waystone).
- **Orčí tábory:** **Ironjaw Warcamp** (velký tábor: kožené stany, kůlová palisáda, totemy z kostí
  a klů, velká kovárna s výhní) a **Tuskridge Camp**.
- **Zajímavá místa:** Ancestor Mound (orčí mohyla, šamani, kteří už neslyší předky), Ironwash Gorge
  (soutěska s visutými lávkami), Pinehold (jediný les plošiny, lovci orků).
- **Dungeon:** **Ironjaw Deep** (13 až 16): orčí důl, koleje, výheň, propast, boss orčí vůdce.
- **Monstra:** orkové (všichni dnešní a vzácní), vargové jako jezdecká zvířata orků, kamenní
  golemové v soutěsce, horské kozy.
- **Hrdla:** East Watch a King's Road, Warren tunnels z údolí.

### 4.6 Riverlands (9 až 15)
Srdce království: široká nížina kolem Alder, pole s žitem a lnem, vinice na svazích, vrby u vody,
větrné a vodní mlýny, kamenné mosty. Nejvíc lidí, nejvíc cest. Méně monster, víc banditů a vlků.
- **Sídla:** **Varn's Crossing** (město, viz 5.3), **Oakhurst** (osada sadařů s lisovnou moštu).
- **Zajímavá místa:** Alder Weir (jez s mlýnem), Old Bridge (románský most s kaplí uprostřed),
  Calderwood (les mezi městem a plošinou), Field of Banners na západě (staré bojiště z války
  s Karethem, rezavé zbroje, duchové).
- **Dungeon:** **Tanner's Drop** (10 až 14): stoky a sklepy pod chudou čtvrtí, krysáci, pašeráci,
  vstup do The Old Way.
- **Monstra:** bandité, vlci, obří krysy, říční hadi, utopenci u jezu, duchové na Field of Banners.
- **Hrdla:** King's Road (na východ do Thornwoodu, na sever do Crownlands), Salt Road na jih, lodě.

### 4.7 Marshes of Ell (13 až 19)
Bažina v pánvi: rákos, ostřice, kmeny topolů v černé vodě, mlha, bludičky, kuňkání. Lid z Ell
žije na kůlech a ví, kudy se chodí.
- **Sídla:** **Reedmoor** (vesnice na kůlech, viz 5.6).
- **Zajímavá místa:** Blackmere (černé jezero s potopeným kostelem; za klidu je slyšet Sunken Bell),
  Lizardfolk Mound (hnízdiště ještěřích lidí), rašeliniště s těly.
- **Dungeon:** **Drowned Abbey** (14 až 18): klášter napůl pod vodou, zatopené chodby, mniši
  zakletí jako utopenci, boss Bog Abbess.
- **Monstra:** ještěří lidé (bojovníci, šamani, lovci s oštěpy), bažinní slimové, pijavice,
  bludičky, utopenci, obří žába.
- **Hrdla:** Salt Road (povalová cesta), Smugglers' Path, říční člun, brod do Moonfell Downs.

### 4.8 Salt Coast (16 až 23)
Pobřeží kolem Saltmere: bílé útesy, solné pánve, větrné mlýny čerpající mořskou vodu, maják,
racci, ostrý vítr. Pod pobřežím Saltdeeps, doly na staré krystaly z hlubokého Blightu.
- **Sídla:** **Saltmere** (přístavní město, viz 5.5).
- **Zajímavá místa:** Salt Flats (bílá pláň, fata morgána za horka), Saltmere Lighthouse, Giant's
  Stair, mořské jeskyně.
- **Dungeon:** **Saltdeeps** (18 až 23): solný důl, krystalové žíly, šachty do Blightu, zbytky
  (husks) a krystalové golemy. Nejhlubší patro je předzvěst Blightu.
- **Monstra:** krystalové golemy, zbytky (husks), pobřežní piráti, mořští hadi u přístavu, harpyje.
- **Hrdla:** Salt Road, Coast Road, lodě.

### 4.9 Crownlands (16 až 24)
Náhorní plošina s hlavním městem Highcrest. Upravená krajina: pole v pravidelných pásech, aleje,
statky šlechty, kamenné zdi mezi poli. Na severu Ridge Road k Blight Frontier, na západě
Greyspine Road ke Karethu.
- **Sídla:** **Highcrest** (hlavní město, viz 5.4).
- **Zajímavá místa:** Lake Calder pod útesem plošiny (vodopád z města do jezera), Lantern of
  Brightwater (zbořená auréjská věž, vyhlídka), Hanging Bridge, Greywater (horské jezero pod Ridges).
- **Dungeon:** **Undercanals** (18 až 24): kanály a hrobky pod městem, krysí lid, kultisté,
  auréjské sály, vstup do The Old Way.
- **Monstra:** chrliče na střechách katedrály (noc), krysí lid, kultisté Lanterny, rytíři-renegáti
  na silnicích.
- **Hrdla:** King's Road, Ridge Road, Greyspine Road, Hanging Bridge.

### 4.10 Greyspine (22 až 28)
Hory na západě: suť, smrky, sníh nad 1 000 m, ledové potoky, orli. Hranice s Karethem a kraj
draků z dávných válek (Wyrm Courts).
- **Sídla:** **Greyspine Gate** (pevnost v průsmyku, celnice, karethští uprchlíci).
- **Zajímavá místa:** zamrzlý vodopád, kostra draka v suti, opuštěný klášter na skále.
- **Dungeon:** **Wyrm Hollow** (22 až 26): ledové jeskyně, drakulové (drakes), boss starý wyrm.
- **Monstra:** drakulové, sněžní vlci, yetti nebo trollové, karethští zvědové.

### 4.11 Blight Frontier (25+)
Za Ridges. Popel, šedé světlo, kořeny trčící ze země, mrtvá auréjská města.
- **Sídla:** **Lantern Watch** (poslední pevnost před Blightem, církevní řád, kasárna, karanténa).
- **Zajímavá místa:** The Watchers (dvě obří sochy v průsmyku), Blight Scar (místo, kde se Blight
  poprvé vrátil), auréjské ruiny.
- **Dungeon:** **Heart Lantern** (28+): srdce sítě, závěr hry.
- **Monstra:** zbytky (husks), kořenová zrůda (rootspawn), ozvěny Skyfallen, zkažená zvířata.

---

## 5. Města a vesnice v detailu

Každé sídlo dostává: rozvržení, čtvrti, klíčové budovy, typy domů s detaily, materiály a paletu,
služby (bez rozhovorů, jen lidé, kteří slouží) a život (chodci, zvuky).

### 5.1 Hearthmere (vesnice, postavená)
**Styl Eastern March:** hrázděné domy, dubové trámy tmavě hnědé (#4a3424), výplň hlína
a vápenná omítka (krémová #e8dcc0, okrová #c8a868), doškové střechy (sláma #d8b85a, stáří
tmavne do #8a7040), podezdívka z lomového kamene (#8e909c), dřevěné ploty, kupky sena, svatyňky
Ohně (kamenné výklenky se svíčkou) u studny a u mlýna.
- **Typy domů:** chalupa (přízemí a podkroví, jeden komín, okenice zelené #4e6e3c), statek
  (dvorec s chlévem a stodolou), dílna (kovárna s otevřenou stranou, výheň, kovadlina).
- **Detaily, které chybí:** truhlíky s květinami pod okny, sušící se prádlo, sudy a bedny u dveří,
  vývěsní štíty (hostinec, kovář, pekař), dřevěné stoky pod střechou, kouř z komínů ráno a večer,
  slepice a kočky, vyšlapané cestičky k domům, studna s rumpálem.
- **Plán:** zachovat a zpřesnit. Doplnit kostel nebo kapli (tichý, bez kněze) a hřbitov.

### 5.2 East Watch, Fort Ironwatch, Lantern Watch, Greyspine Gate (pevnosti)
Šedá žula (#666874, #8e909c), břidlicové střechy (#3c4250), dřevěné ochozy, prapory markraběnky
(modrá #2f4070 se stříbrným jelenem), u East Watch šibenice a sklad Halveth Company (pečeť: zlatá
mince s váhami). Lantern Watch: bílé pláště řádu, karanténní stany, ohně s modrým plamenem.

### 5.3 Varn's Crossing (město na řece)
**Rozvržení:** na obou březích Alder, spojené **Old Bridge** s domy na mostě. Východní břeh: trh,
Guilda, hostince, řemesla. Západní břeh: věž Halveth Company (bílý kámen, zlatá kopule), sklady,
přístav s jeřáby. Na jihu chudá čtvrť **Tanner's Drop** (koželuhové, kanály, zápach, schody dolů
do stok). Aréna z kamene a dřeva na severu.
- **Styl Riverlands:** cihla (#9e5a3a, #b8704a) a hrázdění, dvě až tři patra, horní patra
  přečnívají nad ulici, červené pálené tašky (#b84040, mechem #6e5a3e), štíty do ulice, dlážděné
  ulice s odtokem uprostřed.
- **Typy domů:** měšťanský dům (krám v přízemí, dílna na dvoře), skladiště (velké brány, kladka
  ve štítu), hostinec s dvorem a stájí, chudé domy z prken v Tanner's Drop.
- **Detaily:** jeřáby a lodě u nábřeží, vývěsní štíty kovové, lucerny na zdech, barvířské kádě
  s barevnou vodou, prádlo přes ulici, kostelní věž s hodinami.
- **Služby:** Guilda (větší než v Hearthmere, víc jobů), kovář a zbrojíř, alchymista, obchod se
  zásobami, stáje, převozník lodí, aréna (souboje za odměnu).

### 5.4 Highcrest (hlavní město)
**Rozvržení:** tři prstence hradeb na plošině. Uvnitř palác Aldane a **katedrála postavená kolem
Lanterny** (azurová záře krystalů #2ce8f5 je vidět v noci z celé Crownlands). Střední prstenec:
šlechtické domy, cechy, velký trh, akademie. Vnější prstenec: řemeslníci. Za hradbami slumy z prken
a hlíny. Ze severní strany padá z plošiny vodopád do Lake Calder.
- **Styl Crownlands:** světlý vápenec (#d8d0c0, #b8b0a0), modrošedá břidlice (#4a5468), gotické
  oblouky, věžičky, okna s olověným sklem, sochy světců, barevné fasády šlechtických domů
  (modrá, vínová, zelená).
- **Typy domů:** kamenný dům cechu (arkády v přízemí), šlechtický palác s dvorem, řadové domy
  řemeslníků, chatrče ve slumech.
- **Detaily:** prapory na hradbách, stráže v modrém, lešení na katedrále (věčně se staví), kašny,
  vozy s vínem, holubi, nápisy cechů.
- **Služby:** všechno nejlepší a nejdražší, hlavní Guilda (vyšší ranky), knihovna (mapy dungeonů),
  mistr kovář.

### 5.5 Saltmere (přístav)
**Rozvržení:** město stéká po svahu k přístavu. Nahoře solivary a větrné mlýny, uprostřed bílé
domy a trh s rybami, dole přístav, mola, loděnice, kotviště lodí. Na skále maják. Pod městem
vchod do Saltdeeps.
- **Styl Salt Coast:** vybílený kámen (#eee4c8), modré okenice a dveře (#2c5a8a), nízké střechy
  z tašek vybledlých sluncem (#c88a6a), ploché terasy, venkovní schodiště, provazy a sítě všude.
- **Typy domů:** rybářský dům (dílna na sítě v přízemí), kupecký dům se skladem, krčma u přístavu,
  chatrče z naplaveného dřeva u loděnice.
- **Detaily:** lodě různé velikosti, sudy se solí, sušené ryby, racci na sloupech, koše s krabi,
  stožáry nad střechami, zvonek u přístavníka.
- **Služby:** loďaři (cesty), trh s exotickým zbožím, kovář, Guilda (pobočka), doly (vstup).

### 5.6 Reedmoor (vesnice na kůlech)
Domy na kůlech nad vodou spojené lávkami, střechy z rákosu (#a8945a), stěny z proutí a hlíny, malé
loďky, sítě, udírny ryb, lucerny na tyčích (ve tmě jediná orientace). Rašelinový kouř.
- **Služby:** převozník, stará bylinkářka (lektvary proti jedu), obchod s rašelinou a rybami.

### 5.7 Fernhollow (vesnice v lese)
Mýtina kolem pramene, nové srubové domy osadníků vedle **zarostlých kruhových domů Vel** (kámen
porostlý mechem, kuželové střechy z drnu). Totemy zvířat, vyřezávané brány. Pila a uhlíři.
- **Služby:** lovec (kůže, maso), bylinkář, waystone v kruhu kamenů.

### 5.8 Gull's Rest, Brindle, Oakhurst (osady)
- **Gull's Rest:** šedomodrá prkna (#5a6a7a), drnové střechy, vytažené loďky, sušárny ryb.
- **Brindle:** hostinec u silnice, kovář, stáje, pár chalup v hrázděném stylu Eastern March.
- **Oakhurst:** sady, lis na mošt, kamenná stodola, včelíny.

### 5.9 Orčí tábory (Ironjaw)
Kožené stany na kostrách z kmenů, palisády z naostřených kůlů, totemy z lebek a klů, rudá okrová
barva (#b03a1e) na všem, velká společná výheň, bubny, lovecké trofeje. Žádné kamenné stavby:
orkové jsou tu cizí a kočují.

### 5.10 Auréjské ruiny (všude)
Bílý mramor zešedlý časem (#c8c8d0 → #8e909c), azurové runové vložky (#2ce8f5, ve tmě svítí),
vysoké štíhlé věže (Lanterny), zborcené oblouky, kruhové náměstí s mozaikou. Poznávací znak světa:
kde je bílý kámen a azur, tam bylo Auren.

---

## 6. Návrh monster podle regionů (jen návrh)

| Region | Dnes ve hře | Nové (návrh) |
|---|---|---|
| Meadow Valley | slimové, goblini (6 druhů), vlci, zvířata | utopenci v Sunken Archive, auréjští strážci |
| Thornwood | (vlci na okraji) | obří pavouci, divočáci, medvěd, bandité, oživlé kořeny |
| Moonfell Downs | vlkodlaci (4 druhy) | přízraky v mlze, havrani |
| Mirror Coast | | krabi, mořští slimové, harpyje, bandité |
| Ironjaw Highlands | orkové (7 druhů) | vargové, kamenní golemové |
| Riverlands | | bandité, krysy, říční hadi, duchové bojiště |
| Marshes of Ell | | ještěří lidé (3 role), pijavice, bludičky, utopenci, obří žába |
| Salt Coast | | krystalové golemy, zbytky (husks), piráti, mořští hadi |
| Crownlands | | chrliči, krysí lid, kultisté, rytíři-renegáti |
| Greyspine | | drakulové, sněžní vlci, trollové, wyrm (boss) |
| Blight Frontier | | zbytky, rootspawn, ozvěny Skyfallen |

---

## 7. Pořadí stavby (doporučení)

1. **Meadow Valley dotáhnout:** opravit jména světových stran, doplnit dungeony Sunken Archive
   a Goblin Warren, hřbitov a kapli, detaily vesnice (5.1).
2. **Technika zón:** přechod mezi zónami v hrdlech, waystones a rychlé cestování z mapy, velká
   mapa celého světa (z `layout.json`).
3. **Thornwood + Brindle a Fernhollow:** první nový region, les a pavouci. Ověří stavbu regionu
   od začátku do konce.
4. **Riverlands + Varn's Crossing:** první město. Ověří, jak dělat velké sídlo s výkonem.
5. **Mirror Coast + Gull's Rest:** moře a pobřeží (voda, pláž, lodě).
6. **Ironjaw Highlands, Moonfell Downs:** regiony pro dnešní orky a vlkodlaky.
7. **Marshes of Ell, Salt Coast + Saltmere:** bažina a přístav.
8. **Crownlands + Highcrest**, potom **Greyspine** a **Blight Frontier**.

Každý region je hotový, až ho autor projde: jména na mapě, waystone, aspoň jeden dungeon, joby na
nástěnce místní Guildy nebo hostince, a každá budova zblízka v kvalitě vesnice.

---

## 8. Biomy, atmosféra, logika sídel a opuštěná místa

Doplněno z druhého návrhu (ChatGPT, 2026-11-10), převzaté jen to, co sedí na zóny a dnešní techniku.

### 8.1 Biomy podle výšky, sklonu a vody

Povrch a vegetaci určí pravidla, ne ruční malování. `Zone.Scatter` dostane biom z výšky nad dnem
zóny, sklonu a vzdálenosti k vodě; zóna smí pravidla přepsat oblastmi (`grounds`).

| Biom | Kde | Povrch | Vegetace | Zvířata |
|---|---|---|---|---|
| Louka | rovina, sklon pod 8°, dál než 12 m od vody | tráva, polní kvítí | trsy trávy, keře osamoceně, solitérní dub | ovce, krávy, jeleni |
| Lužní les | do 20 m od řeky nebo jezera, nízko | vlhká tráva, bahno u břehu | vrby, olše, rákos, orobinec | kachny, žáby (zvuk) |
| Okraj lesa | pás 10 až 15 m kolem lesa | tráva přechází v hrabanku | keře, mladé stromky, kapradí | srnci, divočáci |
| Listnatý les | střední výšky, sklon pod 20° | hrabanka, mech | duby, buky, břízy, kapradí | divočáci, vlci |
| Jehličnatý les | vyšší polohy, Greyfang | jehličí, kameny | borovice, smrky, brusinky | vlci |
| Vřesoviště | holé návrší, Moonfell | vřes, krátká tráva, kameny | vřes, jalovec, osamělá bříza | havrani, vlkodlaci v noci |
| Skalnatý svah | sklon nad 30° | skála, suť | trs trávy ve škvírách, kosodřevina | kozy (návrh) |
| Pole a pastviny | u statků, ručně | pole v pásech, cesty | žádné stromy, ploty, kupky | ovce, krávy |
| Zpustošená zem | tábory goblinů a orků | udupaná hlína, popel | pahýly, suché keře | nic |

Přechody jsou měkké: pravděpodobnost rostliny se mění plynule s výškou a sklonem, žádná ostrá hranice.

### 8.2 Atmosféra regionů

Každý region má vlastní mlhu, barvu oblohy, světlo a zvukovou kulisu. Na hranici zóny i uvnitř
zóny (Greyfang, Moonfell, Ironjaw) se nastavení prolíná na **40 m**, aby změna nebyla skok.

| Region | Mlha | Obloha a světlo | Zvuk |
|---|---|---|---|
| Meadow Valley | slabá, ranní u jezera | jasně modrá, teplé slunce | ptáci, vítr v trávě, potok |
| Greyfang | modrošedá v údolíčkách | studenější, nižší slunce | vítr v jehličí, vytí v dálce |
| Moonfell | hustá, nízká, v noci stříbrná | fialová soumrak, velký měsíc | havrani, tiché zvony kaple |
| Ironjaw | prach, kouř z ohňů | do oranžova | bubny, kovadliny, vítr |
| Thornwood | zelená, paprsky světla | stíněné, skvrnité světlo | datel, praskání větví, cvrčci |

### 8.3 Logika sídel

- **Voda:** studny a pitná voda proti proudu, koželuhové, jatka a stoka po proudu. Ve městech je
  stoka malý dungeon (Tanner's Drop, Undercanals).
- **Tři šířky ulic:** hlavní silnice (vůz a chodci, 6 až 8 m), ulice (3 až 4 m), ulička a průchod
  (do 2 m). Hlavní silnice vede k trhu a bráně.
- **Lucerny** svítí od 19 do 6 h; u bran a na mostech celou noc.
- **Ukazatele** na každé křižovatce silnic, se jmény míst (anglicky), aby hráč věděl bez mapy.
- Řemesla podle surovin: mlýn u vody, kovárna u silnice, pila u lesa, rybáři u vody.

### 8.4 Konstrukční standard domů

Každý dům, který hráč uvidí zblízka, má: podezdívku (dům nestojí na trávě), viditelné nosné trámy
nebo kámen v rozích, dveře se zárubní a prahem, okna s rámem a okenicemi, střechu s přesahem
a okapem, komín u domů s ohništěm, a aspoň dva detaily u vchodu (sud, bedna, lavice, truhlík,
nářadí). Kolize vždy z modelu. Dům na svahu má podezdívku dorovnanou ke svahu, ne zapuštěnou.

### 8.5 Opuštěná místa a nová zajímavá místa

Svět působí starší, když v něm jsou místa, kde něco skončilo. Bez textu, jen tím, co tam leží.

- **Meadow Valley:** vypálený statek u západního sedla (černé trámy, rozbitý vůz, goblinské stopy),
  **jeskyně za Silver Falls** (vstup do Sunken Archive), opuštěný tábor lovců u Greyfangu, starý
  brod se zborceným mostem na Silverbrooku.
- **Thornwood:** zarostlé kruhové domy Vel, pila zničená bouří, oběšencův dub.
- **Greyspine:** **teplé prameny** v sedle (pára, modrá voda, místo k odpočinku), opuštěný důl.
- **Riverlands:** zatopená vesnice u Alder (střechy nad vodou), bojiště s rezavými zbraněmi.
- **Pobřeží:** vrak lodi na pláži, maják bez strážce.

Každé místo dostane snímek ve studiu a značku na mapě až po objevení.

---

## 9. Co plán zatím neřeší

- Přesné rozvržení ulic měst (bude dokument pro každé město při jeho stavbě).
- Interiéry (kolik domů má vnitřek: návrh jen služby a hostince).
- Počasí a roční období po regionech.
- Hudba regionů (zvukové kulisy viz 8.2).
