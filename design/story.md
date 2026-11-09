# Příběh hry: přehled, doporučení a varianty

Hlavní dokument designu. Podrobnosti jsou v dalších souborech:

| Soubor | Obsah |
|---|---|
| `design/world.md` | metafyzika (Lattice, duše, krystaly, Blight), dějiny, zeměpis, společnost, otroctví, právo, víra |
| `design/factions.md` | koruna, společnost, karavany, církev, Guilda, markraběnka, orkové, Moonfell, goblini, Kareth |
| `design/characters.md` | hráč, vesnice, Skyfallen, protivníci, jejich tajemství a možné konce |
| `design/plot.md` | prolog, akty I až III, příběhové hodiny, konce, tabulka následků |
| `design/systems.md` | tutoriál, Guilda, questy a dialogy z dat, vyšetřování, stav světa, růst vesnice, monstra, bossové, dungeony, technický plán |
| `design/status.md` | co ve hře běží a co je jen návrh, otevřená rozhodnutí, první hratelný příběhový úsek |

Jména ve hře jsou anglicky (pravidlo projektu), popis česky.

Rozhodnuto 2026-11-09: hra pro jednoho hráče s příběhem, lokální server a klient zůstávají,
co-op možná později. Hráč: vlastní jméno, volby odpovědí, minulost jen úvodní video (zachrání
dítě, srazí ho náklaďák).

---

## 1. O čem hra je

Hráč zemře na Zemi a spadne z nebe do Velmory, středověkého světa, který vypadá jako RPG: úrovně,
atributy, krystaly z monster, Guilda s tabulí jobů. Hned po dopadu ho chytí lovci lidí a prodají
by ho, kdyby vůz nepřepadli goblini. Svět, kam spadl, není hra. Je tu otroctví, šibenice, mor,
dluhy, šlechtic, který zabije sedláka za zablácené boty a zaplatí pokutu.

Postupně hráč zjistí, že „hra" je **Lattice**: staré kouzlo, které chytá duše umírajících z jiných
světů a dělá z nich nesmrtelné vojáky. Pohání ho krystaly, a krystaly jsou zbytky duší. Síť pije
i duše domácích mrtvých, a co nespálí, hnije jako **Blight**, který pomalu požírá sever. Kdo síť
ovládá, ovládá válku, obchod i korunu. A hráč je jejím produktem.

**Hlavní otázka hry:** hráč nemá kam se vrátit. Komu tady bude patřit a co z tohohle světa udělá?

**Tón:** začíná teple (vesnice, slimáci, Eldin chleba), každou kapitolou tmavne. Smrt je trvalá
pro všechny kromě Skyfallen, a to je hlavní kontrast. Humor zůstává v postavách, ne ve světě.

### Pilíře hry
1. **Rychlý, čitelný akční souboj.** Červené značky, uhýbání, blok a parry, mastery pohyby.
2. **Svět si pamatuje.** Volby mění lidi, místa a frakce, často se zpožděním.
3. **Šedí lidé, skutečná cena.** Žádná frakce není čistě dobrá, nic není zadarmo.
4. **Vidět růst.** Úrovně, mastery a gear u hráče, zachránění lidé ve vesnici.

### Herní smyčka
- **Souboj (sekundy):** přečíst útok, uhnout, blokovat nebo odrazit, potrestat.
- **Job (minuty):** vzít úkol, vyrazit, lovit a sbírat, vrátit se do Guildy, prodat, zlepšit gear,
  mastery a skilly.
- **Kapitola (hodiny):** zjistit, co se děje, rozhodnout, vidět, jak se svět změnil, otevřít
  novou oblast.

---

## 2. Inspirace

| Dílo | Co si bereme |
|---|---|
| Log Horizon | domorodci jsou lidé, ne NPC; ekonomika a politika jako konflikt; Skyfallen jako nová třída |
| Grimgar | začátek dole, smrt domorodých dobrodruhů (Brightwater Four), drsnost lovu |
| Shield Hero | přivolaní jako politický nástroj, falešné obvinění (smrt krále), otroctví beastkin |
| Mushoku Tensei | otroctví jako běžná součást světa, trh s lidmi |
| Re:Zero | volba nástupce, každý kandidát má pravdu a temné tajemství |
| Spice and Wolf | obchod, dluhy, monopol na krystaly |
| Overlord | nesmrtelný Skyfallen vládce (Undying Emperor) jako varování |
| Berserk, Vinland Saga | válka a otroctví bez romantiky |
| SAO | otázka „je to hra?", Kai jako hráč, který svět nebere vážně |
| Witcher 3 (Krvavý baron) | quest, kde žádná volba není čistá (Moonfell) |
| Disco Elysium | informace jako hratelnost, tóny odpovědí, postavy lžou |
| Fallout 1 | příběhové hodiny: svět nečeká a rozhodne sám |
| Suikoden | zachránění lidé se stěhují do vesnice a je to vidět |

---

## 3. Moje doporučení (závěr)

1. **Postavit celý akt I jako hotovou hru, než se sáhne na akt II.** Akt I má začátek (pád a klec),
   čtyři kapitoly s volbami, pět bossů, tři dungeony a konec (archiv, smrt Brama). Je to 6 až 10
   hodin hraní. Akty II a III zůstávají skicou, která se přizpůsobí tomu, co se v aktu I osvědčí.
2. **Nejdřív technické základy, pak obsah.** Bez ukládání, stavu světa a questů a dialogů z dat
   nelze psát příběh s následky. Je to fáze 1 v `design/systems.md`. Odhad: první velký krok práce.
3. **Tři velké volby za kapitolu, ne víc.** Následky hlavně přes malé viditelné změny (repliky,
   kdo kde stojí, tábor, obchod, jméno na desce), ne přes celé nové zóny. Jinak obsah exploduje.
4. **Příběhové hodiny místo reálného času.** Svět jde dál po beatech, deník dává náznaky. Dává to
   tlak a váhu volbám, ale nikoho to netrestá za pomalé hraní.
5. **Ukládání jen automaticky** na beatech a při odchodu, plus jedna záloha pro pád hry. Volby
   musí být závazné, jinak hráč přestane přemýšlet a začne zkoušet.
6. **Temnotu ukazovat, ne vysvětlovat.** Šibenice, obojky, hroby, Memorial Wall. Hráč si sám všimne.
   Krutá cesta je vždy volba, nikdy jediná možnost. Sexuální násilí ne, smrt dětí jen mimo obraz.
7. **Začátek v kleci.** Silnější háček než probuzení na louce. Hned ukáže lovce, otroctví, Skyfallen
   jako zboží a goblinské železo, ze kterého vyroste kapitola 1.
8. **Společníci až po aktu I.** Velká funkce, příběh aktu I bez nich funguje.

---

## 4. Varianty celkového pojetí

Zvažoval jsem tři směry. Doporučuji B.

### A. Lehký isekai (Konosuba, Slime)
Veselý svět, Guilda jako parta, politika na pozadí, zlo je jasné.
- **Plus:** jednodušší psaní, přístupné, méně obsahu kolem voleb.
- **Mínus:** nesplní přání „hráč musí přemýšlet", žádná váha rozhodnutí.

### B. Temná středověká fantasy s isekai základem (doporučeno)
To, co je popsané v těchto souborech. Teplý začátek, temnějící svět, šedé frakce, trvalé smrti,
otroctví a politika jako normál.
- **Plus:** volby mají váhu, svět drží pohromadě (krystaly, síť a Blight vysvětlují i herní
  mechaniky), hodně háčků na budoucnost.
- **Mínus:** hodně textu a větvení, náročné na konzistenci. Řeší se rozsahem (akt I napřed) a daty.

### C. Grimdark (Berserk, Goblin Slayer)
Beznaděj, každá volba špatná, mrtví všude.
- **Plus:** silná atmosféra.
- **Mínus:** únavné, odpuzuje, ztratí se teplo vesnice, které dává smrti váhu. Bez naděje se nemá
  o co bojovat.

### Varianty hlavní záhady
| Varianta | Hodnocení |
|---|---|
| **Síť způsobuje Blight** (lék = nemoc) | **doporučeno**: skutečné dilema, spojuje Skyfallen, krystaly, ekonomiku a hrozbu do jednoho |
| Blight je drak nebo démon | jednoduché zlo, žádné dilema |
| Svět je simulace nebo hra | zabije sázky |
| Skyfallen jsou klony bez duše | zajímavé, ale hráč by ztratil identitu a důvod chtít něco změnit |

### Varianty struktury
| Varianta | Hodnocení |
|---|---|
| Lineární kapitoly | jednoduché, ale málo svobody |
| **Hybrid: kapitola 1 lineární, 2 a 3 volně, 4 spouští čas** | **doporučeno** |
| Úplně otevřený svět | příběh i následky se rozpadnou |

---

## 5. Otevřené otázky

- Jméno vesnice a světa: Hearthmere a Velmora (návrh). Přejmenovat Meadow Village ve hře?
- Úvodní video: 2D pixel art obrázky (levné, sedí ke stylu), nebo scéna ve 3D enginu (dražší)?
- Beastkin: nová rasa znamená uši a ocas na anime rig a nové barvy. Chceme je už v aktu I (karavany
  do vesnice), nebo až ve městech v aktu II?
- Romantické linky: zatím ne, později možné (Lisbet, Vashra, Corran, Sora).
