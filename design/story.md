# Forty Crowns: vize, principy a mapa designu

> **Opuštěný směr (2026-11-10).** Hra už není příběhová. Tento dokument popisuje plán, který neplatí; může sloužit jako zásoba nápadů pro svět. Platí `design/direction.md`.

Název hry je **Forty Crowns**: tolik stojí Skyfallen na trhu (Vosk: "Forty crowns, that one. Don't damage
the face."). Hned říká, že svět je krutý a hráč je pro něj zboží, ne vyvolený.

Hlavní dokument designu. Jména ve hře anglicky (pravidlo projektu), popis česky.

| Soubor | Obsah |
|---|---|
| `design/story.md` | vize, role hráče, pilíře, hlavní proudy, revize a originalita, doporučení |
| `design/world.md` | metafyzika (Lattice, duše, krystaly, Blight), dějiny, zeměpis, společnost, právo, víra, kalendář |
| `design/factions.md` | frakce, jejich lidé, cíle, zločiny a vztahy |
| `design/timeline.md` | **výchozí časová osa aktu I: co se stane, když hráč nic neudělá**, plány frakcí po krocích |
| `design/paths.md` | cesty hráče: členství ve frakcích, kariéry, žoldnéři, obchod, sólo, nic nedělat |
| `design/relationships.md` | přátelé, společníci, sólo hra, romance a jak se jim vyhnout |
| `design/characters.md` | postavy, tajemství, možné osudy |
| `design/plot.md` | hlavní proudy a podlinie, situace aktu I, osnova aktů II a III, skládané konce |
| `design/systems.md` | tutoriál, Guilda, questy a dialogy z dat, vyšetřování, stav světa, monstra, bossové, dungeony |
| `design/stages.md` | **etapy výroby**: co se staví v jakém pořadí a jak se každá etapa ověří |
| `design/stage-1.md` až `stage-3.md` | detailní návrhy etap: základy (data, kalendář, ukládání), postava a mimika, prolog s replikami |
| `design/status.md` | co ve hře běží a co je jen návrh, otevřená rozhodnutí |

Rozhodnuto: hra pro jednoho hráče s příběhem, lokální server a klient zůstávají (co-op možná
později). Hráč: vlastní jméno, volby odpovědí, minulost jen úvodní video (zachrání dítě, srazí ho
náklaďák). Hráč není vyvolený: je součástí světa, který jde vlastní cestou. Hráč je vždy muž, vzhled si upraví. Vesnice Hearthmere, svět Velmora, akt I trvá 70 dní, ukládání automaticky každé ráno (2026-11-09).

---

## 1. O čem hra je

Hráč zemře na Zemi a spadne z nebe do Velmory, středověkého světa, který vypadá jako RPG. Hned po
dopadu ho chytí lovci lidí. Uteče, zaregistruje se v Guildě a začne žít: lovit, obchodovat,
poznávat lidi. Svět kolem něj má vlastní problémy a vlastní plány, které běží, ať hráč chce, nebo ne.
Obchodní společnost skupuje dluhy a lidi, církev zahlazuje svůj zločin, princ táhne s vojskem na
orky, Blight se plíží ze severu.

Hráč není vyvolený. Letos je **třetí**, kdo spadl z nebe, a v království jich žijí desítky.
Nikdo na něj nečeká a žádné proroctví o něm není. Co udělá, je jen jeho věc: může se přidat
k Guildě, ke společnosti, k pašerákům, k žoldákům, k markraběnce, k církvi, může žít sám jako lovec,
nebo nedělat vůbec nic. Svět si s tím poradí po svém, obvykle hůř.

**Hlavní otázka:** hráč nemá kam se vrátit. Komu tady bude patřit, a co v tomhle světě stojí za to
zachránit, když nejde zachránit všechno?

**Pocit z dobré hry:** „Udělal jsem, co jsem mohl. Tady, kde jsem byl, je to lepší, než by bylo
beze mě. Jinde ne, protože jsem tam nebyl."

**Tón:** začíná teple, každou kapitolou tmavne. Středověk se vším: otroctví, šibenice, mor, dluhy,
právo silnějšího. Smrt je trvalá pro všechny kromě Skyfallen. Humor zůstává v lidech.

---

## 2. Role hráče: jeden z mnoha

1. **Svět má vlastní plány.** Každá frakce má plán po krocích, který postupuje s kalendářem
   (`design/timeline.md`). Bez zásahu uspějí. Výchozí svět je ten temný.
2. **Hráč je jeden z mnoha.** Jiné party, jiní Skyfallen, jiní agenti řeší věci i bez něj.
   Goblinský tábor vyčistí Brightwater Four, jen při tom dva z nich zemřou.
3. **Hráč nemůže být všude.** Události se překrývají. Stihne jen část. Svět to unese.
4. **Vliv roste pomalu a zůstává omezený.** Akt I: vesnice a údolí. Akt II: agent někoho
   mocnějšího. Akt III: jeden z lidí, kteří pomohou koalici, kterou si sám vybral. Nikdy král,
   nikdy spasitel.
5. **Nicnedělání je platná cesta.** Hráč může jen lovit a prodávat. Kalendář běží dál, akty se
   mění světovými událostmi, ne hráčovými činy. Hra má i pro takového hráče konec.
6. **Žádná ideální linie.** Každá cesta má zisky a ztráty. Nedá se mít všechno. To, co zachrání
   jedni, zaplatí jiní.

---

## 3. Pilíře hry

1. **Rychlý, čitelný akční souboj.** Červené značky, uhýbání, blok a parry, mastery pohyby.
2. **Živý svět, který jde bez hráče.** Kalendář, plány frakcí, NPC s vlastními životy, zprávy
   a drby o tom, co se děje jinde.
3. **Mnoho cest, žádná ideální.** Členství, kariéry, přátelství, sólo, romance, nicnedělání.
   Každá cesta vypadá jinak a nic není zadarmo.
4. **Následky, které jsou vidět.** Lidé, místa, obchody, tábory, jména na Memorial Wall, se
   zpožděním.
5. **Vidět růst.** Úrovně, mastery a gear u hráče, lidé a domy ve vesnici.

### Herní smyčka
- **Souboj (sekundy):** přečíst útok, uhnout, blokovat nebo odrazit, potrestat.
- **Den (minuty):** ráno drby a tabule, přes den job, kontrakt nebo vlastní cíl, večer prodej,
  hospoda, rozhovory. Spánek posune kalendář.
- **Týdny (hodiny):** frakční plány se pohnou, hráč se rozhodne, kam sáhne a kam ne, svět se změní.

---

## 4. Čtyři hlavní proudy

Celou hrou tečou čtyři velké příběhové proudy. Běží samy. Hráč do nich může vstoupit kdekoli,
z kterékoli strany, nebo je nechat být. Každý se větví do podlinií (`design/plot.md`).

1. **The Lightborn Question** (otázka Skyfallen): jsou Skyfallen lidé, zboží, zbraň, nebo palivo?
   Lovci, registrace, Royal Levy, Harvest, práva, Hollowed.
2. **The Crystal Trade** (obchod a dluh): krystaly, peníze kryté krystaly, dluhy, otroctví,
   monopol společnosti proti karavanám a koruně.
3. **The Crown** (moc a válka): markrabství, princ a orkové, nástupnictví, Kareth.
4. **The Blight** (pravda a mrtví): co jsou krystaly, kam jdou duše, co roste na severu.

Každá frakce stojí v proudech jinde, proto je hráčovo členství v jedné frakci zároveň postoj
ve všech čtyřech proudech.

---

## 5. Revize: co jsem na původním návrhu změnil a proč

| Problém v původním návrhu | Změna |
|---|---|
| Hráč rozhoduje o trůnu i o osudu sítě: vyvolený v přestrojení | Hráč pomáhá koalicím. Konce se skládají ze stavu proudů, ne z jednoho tlačítka |
| Kapitoly s pevnými třemi volbami | Situace s výchozím vývojem. Možnosti hráče závisí na jeho cestě a nástrojích |
| Beaty se posouvají jen hráčovým postupem: když hráč nic nedělá, svět stojí | Kalendář ve dnech. Čas běží spánkem, cestováním a prací |
| V kapitole 1 tři vraždy a poprava za pár dní | Události rozložené v kalendáři |
| Síť chytá jen „hrdinské" smrti, ale Kai zemřel u počítače | Pravidlo: síť chytá **náhlé smrti s nedokončenou vůlí** |
| Frakce jsou jen strany voleb | Frakce mají plány, členství, kariéry, hodnosti a kontrakty |
| Chybí žoldnéři a obchodní kariéra | Nové: žoldnéřská rota Ashen Banner, obchodní cesta s vlastním domem |
| Chybí místní politika | Nové: obecní rada Hearthmere, volba rychtáře |
| Romance jen jako „možná později" | Volitelné romance napříč cestami, jde jim úplně vypnout |
| Sólo hra není promyšlená | Sólo jako vlastní styl s vlastními výhodami a pověstí |

---

## 6. Co je na tomhle světě originální

1. **Zkušenosti jsou duše.** Mark roste tím, že pohlcuje úlomky duší ze zabitých monster. Úroveň
   je doslova snědená cizí duše. Domorodci to tuší a proto se Skyfallen štítí. Hráč to zjistí
   v polovině aktu I, a hra se nezmění, změní se jen to, jak se na ni dívá.
2. **Tvoje smrti tě čekají.** Každé složení u Anchor Stone nechá v Hlubině zbytek předchozího těla.
   V Blightu hráč potká **vlastní ozvěny**: zbytky svých smrtí, se zbraní a stylem, jaký měl, když
   zemřel. Hra si místa a výbavu smrtí pamatuje. Kdo umíral často, má v aktu III víc nepřátel.
3. **Rootmother je z velké části Undying Emperor.** Čtyři sta let jeho smrtí, slepených v jedno.
   Největší nepřítel světa vznikl z toho, kdo se nejvíc bojí zemřít.
4. **Peníze stojí na duších.** Společnost vydává směnky kryté krystaly. Kdo síť zničí, zničí
   i peníze. Obchodní cesta má v konci skutečnou sázku.
5. **Skyfallen jako středověká zbraň hromadného ničení.** Nesmrtelní žoldnéři mění válku. Otázka
   jejich práv je otázka, kdo smí vlastnit zbraň, která mluví.
6. **Falešný vyvolený.** Církev vyhlásí „Herolda Sky Mother": jiného Skyfallen, loutku. Hráč se
   na to dívá ze strany a může tu lež podpořit, odhalit, nebo ji nechat být.
7. **Malá politika má váhu.** Obecní rada Hearthmere, volba rychtáře, soud o dluh za farmu.
   Hráč mění vesnici, ne říši.
8. **Kalendář a roční doby.** Akt I trvá od jara do zimy. Svět se mění s nimi (zima přivede vlky).
9. **Pro domorodce je každá chyba poslední.** Memorial Wall, Brightwater Four, trvalé smrti.

---

## 7. Inspirace

| Dílo | Co si bereme |
|---|---|
| Log Horizon | domorodci jsou lidé; ekonomika a politika jako konflikt; Skyfallen jako nová třída |
| Grimgar | začátek dole, smrt domorodých dobrodruhů, drsnost lovu |
| Shield Hero | přivolaní jako politický nástroj, falešné obvinění, otroctví |
| Mushoku Tensei | otroctví jako běžná součást světa |
| Re:Zero | nástupnictví, každý kandidát má pravdu i temné tajemství |
| Spice and Wolf | obchod, dluhy, monopol; obchodní cesta |
| Overlord | nesmrtelný vládce jako varování |
| Berserk, Vinland Saga | válka, žoldnéři a otroctví bez romantiky (Ashen Banner) |
| Mount & Blade | žoldnéřské kontrakty, frakce, které jednají samy |
| Kingdom Come: Deliverance | obyčejný člověk ve velkých dějinách, středověký všední den |
| Fallout: New Vegas | frakce jako cesty, každá s vlastním koncem a cenou; skládaný epilog |
| Pathologic | čas běží, nestihneš všechno, výchozí svět umírá |
| Disco Elysium | informace jako hratelnost, tóny odpovědí, postavy lžou |
| Witcher 3 | questy bez čisté volby (Moonfell) |
| Suikoden | zachránění lidé ve vesnici |

---

## 8. Doporučení (závěr)

1. **Detailně akt I, osnova aktů II a III.** Akt I je celá hra (8 až 12 hodin, s cestami a
   kalendářem). Akty II a III se upřesní, až bude akt I hratelný.
2. **Psát výchozí osu dřív než hráčovy možnosti.** Nejdřív co se stane bez hráče, pak co může
   změnit a čím (podle cesty). To drží svět živý a obsah zvladatelný.
3. **Cesty jako sady nástrojů, ne jako oddělené příběhy.** Každá cesta dává hráči jiné nástroje
   na stejné situace (peníze, zákon, násilí, pašování, víra). Situací je málo, způsobů mnoho. Tak
   každá linie vypadá jinak, aniž by se obsah násobil.
4. **Následky přes malé viditelné změny**, ne přes celé nové zóny.
5. **Kalendář s velkorysými termíny a jasnými náznaky.** Tlak ano, panika ne.
6. **Ukládání automaticky** (každé ráno a při odchodu) plus jedna záloha pro pád hry.
7. **Temnotu ukazovat, ne vysvětlovat.** Krutá cesta je vždy volba. Sexuální násilí ve hře není,
   smrt dětí jen mimo obraz.
8. **Etapy podle `design/stages.md`**: každá etapa projde návrhem, stavbou, hraním a úpravou
   dokumentace, než začne další.
