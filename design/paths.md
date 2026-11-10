# Cesty hráče

> **Opuštěný směr (2026-11-10).** Hra už není příběhová. Tento dokument popisuje plán, který neplatí; může sloužit jako zásoba nápadů pro svět. Platí `design/direction.md`.

Jak se hráč může zařadit do světa. Cesty nejsou oddělené příběhy: jsou to **sady nástrojů** na stejné
situace (peníze, zákon, násilí, pašování, víra, přátelé) a **postoje** ve čtyřech proudech
(`design/story.md`). Situací je v aktu I asi dvanáct. Každá cesta je řeší jinak.

---

## 1. Pravidla členství

- Hráč může být členem **víc frakcí**, dokud se nedostanou do sporu. Pak musí volit, nebo lhát.
- **Dvojí hra** je možná: být členem jedné frakce a donášet jiné. Každá frakce má **podezření**
  (skryté číslo). Když přeteče, frakce hráče prověří (quest), vyhodí, nebo se pokusí zabít.
- **Hodnosti** v každé frakci: začátečník, člen, důvěrník, vnitřní kruh. Hodnost otevírá úkoly,
  zboží, lidi a informace. Vnitřní kruh zná tajemství frakce.
- **Zrada** je vždy možná a vždy něco stojí: frakce se mstí a ostatní frakce si zapamatují, že hráč zrazuje.
- **Odchod** bez zrady je možný, ale vnitřní kruh hráče nepustí s tajemstvím jen tak.

### Kdo s kým nejde dohromady
| | Guilda | Společnost | Karavany | Ashen Banner | Markraběnka | Církev | Orkové |
|---|---|---|---|---|---|---|---|
| Guilda | — | ano | tajně | ano | ano | ano | po míru |
| Společnost | ano | — | **ne** | ano (kontrakty) | ano | ano | ne |
| Karavany | tajně | **ne** | — | ano | ne | **ne** | ano |
| Ashen Banner | ano | ano | ano | — | ano | ano | ano |
| Markraběnka | ano | ano | ne | ano | — | ano | po míru |
| Církev | ano | ano | **ne** | ano | ano | — | ne |
| Orkové | po míru | ne | ano | ano | po míru | ne | — |

---

## 2. Cesty

Každá cesta: co nabízí, co stojí, podlinie (jak se může vyvinout) a jak řeší typické situace.

### 2.1 Dobrodruh Guildy (výchozí)
- **Nabízí:** ochranu zákona, joby, ranky, Anchor Stone, kamarádství v hale.
- **Stojí:** Guilda je neutrální, takže v sporech mlčí. Bere podíl. Má seznam Skyfallen, který
  prodává společnosti.
- **Podlinie:**
  1. *Věrný dobrodruh:* ranky až po C, zkoušky, pověst. Bezpečné, nejméně vlivu.
  2. *Reformátor:* odhalit prodej seznamů, vyměnit vedení pobočky (Corwin, Mira), udělat z pobočky
     útočiště pro Skyfallen a dlužníky. V aktu II spor s Guildmistrem Roem.
  3. *Svaz Skyfallen:* z Guildy udělat cech Lightborn, který si sám vybírá, komu slouží. Silný v aktu
     II, ale všechny mocné frakce v tom vidí hrozbu.

### 2.2 Muž (žena) společnosti: obchodní kariéra
- **Nabízí:** peníze, slevy, licence (i licenci lovce!), informace o dluzích, vlastní obchod.
- **Stojí:** špinavé ruce (vymáhání, dluhy, „dlužníci"), nenávist vesnice, karavany jdou po krku.
- **Podlinie:**
  1. *Kariérista:* vymáhat dluhy, převzít sklad po Holtovi, stát se faktorem v East Watch. Na konci
     aktu II Ottilie nabídne partnerství.
  2. *Lepší správce:* vést sklad poctivěji než Holt (bez goblinů, bez únosů). Vydělává méně, Ottilie
     to nějakou dobu snese.
  3. *Sabotér zevnitř:* donášet karavanám nebo markraběnce. Dvojí hra.
- Obchodní nástroje: vykoupit dluh, odkoupit farmu, zaplatit wergild, uplatit soudce, najmout žoldnéře.

### 2.3 Vlastní obchodní dům (nezávislý obchodník)
- Odbočka obchodní cesty bez společnosti. S Linou (pokud jí hráč pomůže) nebo s Corranem založit
  **vlastní obchodní dům**: koupit vůz, najmout stráž, vozit zboží z údolí do East Watch a zpět.
- **Nabízí:** nezávislost, zisk, vlastní lidi, v aktu II vstup do obchodu ve Varn's Crossing.
- **Stojí:** společnost konkurenci nesnáší (přepadení vozů, daně, žaloby). Peníze investované do
  zboží nejsou na lektvary a gear.
- Mechanika (jednoduchá): podíly v karavanách na trase, výnos podle stavu světa (válka zvedá ceny
  železa, vlci ceny masa). Žádná simulace burzy.

### 2.4 Free Caravan League: pašerák a osvoboditel
- **Nabízí:** levné zboží, černý trh s krystaly, informace, podzemní cestu pro lidi (Nell!),
  přátelství beastkin.
- **Stojí:** zločin (šibenice), koruna i církev jdou po hráči, karavany vraždí a hráč u toho bývá.
- **Podlinie:**
  1. *Osvoboditel:* vyvádět dlužníky a otroky na jih. Nejvíc zachráněných, nejvíc nepřátel.
  2. *Pašerák:* krystaly a zboží. Hodně peněz, méně ideálů.
  3. *Junin dědic:* vnitřní kruh, rozhodovat o životě a smrti faktorů. V aktu II vést karavany.
  4. *Bandita:* pod praporem svobody přepadat kohokoli. Cesta, kam karavany samy sklouzávají.

### 2.5 Žoldnéř: Ashen Banner
- **Nová frakce.** Žoldnéřská rota asi čtyřiceti mužů a žen, mezi nimi tři Skyfallen. Táboří
  u East Watch. Vede ji **kapitánka Bryn Halloway**, domorodka, veteránka z karethské války.
- **Nabízí:** kontrakty za dobré peníze pro kohokoli (markraběnka, společnost, Caius, orkové,
  karavany), bratrstvo v rotě, výcvik, svoboda stran.
- **Stojí:** žoldnéři nemají čest v očích nikoho. Kontrakty bývají ošklivé (vypálit farmu, vybrat
  daň, vyvraždit tábor). Odmítnout kontrakt = ztratit důvěru roty.
- **Podlinie:**
  1. *Voják:* plnit kontrakty, stoupat v rotě, v kapitole Iron and Honor bojovat za toho, kdo zaplatí víc.
  2. *Kapitán:* převzít rotu (Bryn se stáhne, nebo zemře), rozhodovat o kontraktech.
  3. *Nesmrtelná rota:* najímat Skyfallen, udělat z roty to, čeho se všichni bojí: armádu, která
     se po smrti vrací. Silné v aktu II, každá frakce rotu chce, nebo zničit.
  4. *Rota se svědomím:* odmítat špinavé kontrakty, bránit vesnice. Chudá rota, lidé odcházejí.

### 2.6 Služba markraběnce: strážce zákona
- **Nabízí:** postavení **wardena** (strážce práva v údolí): honit bandity, vyšetřovat, **soudit malé
  spory** ve vesnicích (dluh za krávu, krádež, rvačka). Zákon jako nástroj (zpochybnit dlužní smlouvu,
  zatknout faktora).
- **Stojí:** sloužit šlechtě: vybírat daně, věšet, chránit Aldrica. Markraběnka je zadlužená
  a společnost ji drží.
- **Podlinie:**
  1. *Věrný warden:* zákon je zákon. Pořádek, ale i šibenice pro lidi, které hráč zná.
  2. *Spravedlivý warden:* ohýbat zákon pro slabé. Isolde to snese do chvíle, kdy to stojí peníze.
  3. *Caiova armáda:* přes markraběnku do vojska. Akt II: válka.
  4. *Seraphinin zvěd:* donášet princezně. Akt II: dvorské intriky.

### 2.7 Církev
- **Nabízí:** léčitele, posvátné útočiště (kostel chrání před lovci), přístup k Lanterně, ochranu
  před davem, vzdělání (auréjské písmo).
- **Stojí:** pokora před inkvizicí, Moonfell, Wren. Vnitřní kruh je Morrowův.
- **Podlinie:**
  1. *Aveliny poutník:* pomáhat chudým a nemocným. Laskavá cesta, ale naivní a bezmocná.
  2. *Inkvizice:* honit čarodějnice a dezertéry. Moc, strach, pravda o Harvestu (z vnitřního kruhu).
  3. *Heroldův stín:* stát se strážcem falešného Herolda Aurela a rozhodnout, co s tou lží.
  4. *Reformátor zevnitř:* sbírat důkazy proti Morrowovi a dát je Aveline.

### 2.8 Pokrevní pouto s orky (až po míru v aktu I)
- **Nabízí:** orčí kovářství, Vashru, souboje cti, šamanské vědění o mrtvých, spojence v aktu III.
- **Stojí:** pro lidi z Marche zrádce. Caius, společnost a církev jsou proti.
- **Podlinie:** *bratr klanu* (žít s Ironjaw, klanová politika: Bruk proti Gor Ashkovi),
  *vyslanec* (most mezi orky a lidmi, každý incident je hráčova vina).

### 2.9 Sólo lovec (nezávislý)
- Registrovaný v Guildě kvůli ochraně, jinak nikomu nepatří. Lov, prodej, vlastní cíle.
- **Nabízí:** svobodu, žádné dluhy u nikoho, žádnou zradu, nikdo hráči nerozkazuje.
  Pověst „Grey Stranger": lidé si ho najímají tajně, protože nepatří nikomu.
- **Stojí:** nikdo hráče nekryje. Méně informací, žádné vnitřní kruhy, frakce ho berou jako nástroj.
- Detaily sólo hry v `design/relationships.md`.

### 2.10 Nic nedělat (tichý život)
- Hráč se zaregistruje, loví, prodává, jí, spí. Neplete se do ničeho.
- Kalendář běží, výchozí osa (`design/timeline.md`) se naplní. Akt I skončí Royal Levy.
- Hráč pořád může **na konci odmítnout Levy** a utéct (stane se psancem), nebo se hlásit (akt II
  začne v Highcrestu jako „jeden z mnoha" v táboře Skyfallen).
- Vlastní konec v aktu III: **The Quiet Life**. Přežije, kde přežít jde, s těmi, kdo přežili.
- Smysl: hráč, který „nic nedělal", vidí temný svět a ví, že to šlo jinak. Nikdo ho netrestá.

---

## 3. Jak cesty řeší stejné situace (příklady z aktu I)

### Nell (Vosk si pro ni přijde, den 11)
| Cesta | Nástroj | Výsledek |
|---|---|---|
| Guilda | koupit smlouvu (15 korun) | Nell učnicí, hráč bez peněz |
| Společnost | zaměstnanecká sleva, nebo přepsat smlouvu na sebe | Nell u hráče jako „dlužnice", hráč ji může propustit |
| Karavany | odvést ji v noci na jih | Nell u karavan, Vosk pálí Linin sklad |
| Ashen Banner | zastrašit Vosku (rota stojí za hráčem) | Vosk ustoupí, společnost si stěžuje u markraběnky |
| Markraběnka | zpochybnit smlouvu (otec ji podepsal pod nátlakem: najít svědka) | smlouva neplatná, Nell volná, Holt naštvaný |
| Církev | posvátné útočiště v kapli | Nell v bezpečí, dokud zůstane v kapli; církev si ji vezme jako novicku |
| Sólo | zabít Vosku v lese | Nell volná, hráč hledaný za vraždu (pokud svědci) |
| Nic | — | Mira ji vydá |

### Hollisova vražda (den 9)
| Cesta | Nástroj |
|---|---|
| Guilda | vyšetřování s Corwinem, obvinění před vesnicí |
| Společnost | hráč ví, kdo to byl (vnitřní kruh). Může mlčet, vydírat, nebo udat |
| Karavany | Juna Vosku unese a vyslechne po svém |
| Markraběnka | warden vede oficiální vyšetřování, smí zatknout |
| Církev | Sister Vey nabídne „zkoušku ohněm" pro podezřelé (ordál) |
| Ashen Banner | rota zná Vosku, hráč si může najmout svědka |

### Bitva o Ironjaw (den 50)
| Cesta | Nástroj |
|---|---|
| Ashen Banner | kontrakt od Caia, nebo od orků (dvojnásobná cena, poloviční šance) |
| Markraběnka | vést domobranu, nebo vyjednávat jménem Isolde |
| Společnost | zásobovat Caia (zisk), nebo zásoby „ztratit" |
| Karavany | vyvést orčí ženy a děti před bitvou |
| Orkové | souboj s Urzogem, mír |
| Církev | požehnat vojsku (Caius víc věří), nebo vyhlásit orky za „zvířata" |
| Sólo | zabít Bruka (válečnou stranu) potají |

---

## 4. Plusy a mínusy cest v kostce

| Cesta | Největší plus | Největší mínus | Kdo trpí |
|---|---|---|---|
| Guilda | bezpečí, stabilita | malý vliv | ti, za které Guilda mlčí |
| Společnost | peníze a moc | špinavé ruce | dlužníci, karavany, vesnice |
| Vlastní dům | nezávislost a zisk | společnost tě ničí | peněženka, zaměstnanci |
| Karavany | zachránění lidé | psanec, krev | stráže, faktoři, i nevinní |
| Ashen Banner | síla, svoboda stran | ošklivé kontrakty | kdo je na druhé straně |
| Markraběnka | zákon v ruce | sloužit šlechtě | ti, které zákon nechrání |
| Církev | útočiště, vědění | inkvizice | čarodějnice, Moonfell, Skyfallen |
| Orkové | spojenci, vědění | zrádce lidí | vesnice, které orkové vypálili |
| Sólo | svoboda | nikdo tě nekryje | nikdo, a nikdo nevděčí |
| Nic | klid | svět jde do tmy | všichni |
