# Etapa 2: Hráčská postava a lidé

> **Pozor (2026-11-10).** Hra už není příběhová. Mimika, portrét a úprava vzhledu platí dál; části o rozhovorech a postavách příběhu neplatí. Směr: `design/direction.md`.

Cíl: lepší postava, čitelná mimika a úprava vzhledu. Hráč je vždy muž (rozhodnuto). Všechno, co tu
vznikne, používají i NPC.

Návaznost: `design/stages.md` (etapa 2). Kód: `client/src/PlayerRig.cs` (rig, `BuildEyes`,
`BuildHair`, mrkání), `client/src/Dialogues.cs` (`NpcLooks`), modely `anime_*` z Blenderu,
`toon.gdshader`, `ClothChain`.

---

## 1. Výchozí stav (co dnes je)

- Hlava `anime_head.glb`. Oči, řasy, obočí, nos a ústa jsou ploché destičky (`Flat`) přidané
  v kódu (`BuildEyes`). Oči mrkají (zmenšení na 10 % výšky).
- Ústa jsou jedna destička, bez výrazu. Obočí stojí.
- 15 účesů (`anime_hair_*`), některé s volnou částí přes `ClothChain` (cop, culík, copánky).
- Doplňky: plášť, vesta, zbroj, zástěra, róba, vousy, čelenka. Barvy přes `Palette` podle materiálů.
- Ruce jsou předloktí bez prstů (`anime_forearm`).

Ploché destičky jsou pro mimiku vlastně výhoda: dají se posouvat, točit, měnit a přepínat bez
deformace sítě. Na nich mimiku postavím.

---

## 2. Obličej a mimika (`client/src/FaceRig.cs`)

Nová třída, kterou vytvoří `PlayerRig` místo `BuildEyes`. Drží části obličeje a nastavuje je podle
stavu obličeje.

### Části
| Část | Jak se hýbe |
|---|---|
| Obočí (L, P) | výška, sklon (vnitřní konec nahoru = smutek, dolů = hněv), přepínání tloušťky |
| Horní víčko (L, P) | nová destička v barvě pleti nad okem: zakrývá oko shora (otevřené, přivřené, zavřené) |
| Spodní víčko (L, P) | zvedá se při úsměvu (oči „smějící se") a při hněvu |
| Oko | zornička a duhovka se posunou (pohled stranou, dolů); tvar „^ ^" při velkém smíchu (přepnutí na oblouček) |
| Ústa | sada tvarů (přepínání destiček): rovná, úsměv, široký úsměv, otevřená (A), malé O, zamračená, zuby (zatnuté), křivý úsměv |
| Tváře | ruměnec (destička, zapíná se) |
| Pot a slza | malé destičky pro únavu a smutek (volitelné) |

### Stav obličeje
```csharp
public struct FaceState
{
    public float BrowRaise;    // -1 dolů .. 1 nahoru
    public float BrowTilt;     // -1 hněv .. 1 smutek
    public float LidUpper;     // 0 otevřené .. 1 zavřené
    public float LidLower;     // 0 .. 1 (úsměv, mžourání)
    public Vector2 Gaze;       // kam se dívá
    public MouthShape Mouth;
    public bool Blush;
}
```
- Plynulé přechody: obličej se blíží k cíli jako zbytek rigu (stejný princip jako `_pose`/`_target`).
- Mrkání zůstane, jen přes `LidUpper`.

### Výrazy (předvolby)
| Výraz | Použití |
|---|---|
| `neutral` | výchozí |
| `smile` | pozdrav, přátelské repliky |
| `laugh` | smích, oči „^ ^", otevřená ústa |
| `angry` | hádka, výhrůžka, boj |
| `sad` | smutné zprávy, smrt |
| `surprised` | odhalení, leknutí |
| `afraid` | strach (Nell v kleci) |
| `pain` | zásah v boji, zranění |
| `effort` | těžký útok, zatnuté zuby |
| `tired` | málo staminy, přivřené oči, pot |
| `smirk` | křivý úsměv (Corran, Dex) |
| `asleep` | zavřené oči (spánek, bezvědomí ve scénách) |

Předvolby jsou data (`world/faces.json`), aby šly ladit bez kódu.

### Kdy se výraz mění
- **Dialog:** uzel dialogu má `"face": "angry"` (formát z etapy 1). Mluvčí ho drží, dokud uzel trvá.
- **Mluvení:** během vypisování repliky se ústa střídají mezi zavřenými a otevřenými (A, O) v rytmu
  písmen. Pauza na čárce a tečce.
- **Boj (hráč i nepřátelé lidé):** zásah = `pain` na 0,4 s, těžký útok = `effort`, stamina pod 25 % =
  `tired`, blok = zatnuté zuby.
- **Pohled:** NPC v dialogu se dívá na hráče, hráč na mluvčího. Mimo dialog se NPC občas rozhlédne.

### Čitelnost
Hra má pixelový vzhled a kameru zezadu. Výraz musí být čitelný:
- **v dialogu:** kamera přiblíží obličej mluvčího (nebo portrét v rámečku, viz níže),
- **ve hře:** jen velké výrazy (bolest, smích) jsou čitelné. To stačí.

**Portrét v dialogu:** doporučuji v okně dialogu malý živý portrét mluvčího (druhá kamera na hlavu
postavy, render do textury). Je čitelný vždy, bez střihů kamery, a výraz je vidět. Alternativa
(přibližovat herní kameru) je dražší na scénování a ruší hráče. **Beru portrét.**

---

## 3. Detail postavy

| Část | Dnes | Nově |
|---|---|---|
| Hlava | jeden tvar | 3 tvary obličeje (úzký, střední, široký) jako varianty `anime_head` z Blenderu |
| Nos | destička | 3 tvary (malý, rovný, orlí) jako destičky nebo malé díly |
| Uši | v modelu hlavy | zůstávají; později varianta pro beastkin (akt II) |
| Krk | v trupu | zůstává |
| Ruce | předloktí bez dlaně | dlaň s palcem a prsty v jednom kusu (palčák), dvě polohy: otevřená a sevřená (držení zbraně) |
| Oblečení | košile, kalhoty, plášť | vrstvy pro gear: košile, kabátec, opasek, boty se záložkou; hrudní zbroj z gear tvaru |
| Vousy | jedny | strniště, krátké, plné, kozí bradka, knír |
| Jizvy | ne | přes tvář, přes oko, přes nos (destičky) |

- Modely staví `asset-pipeline/build_meshes.py` (stejný postup jako dnešní `anime_*`). Kolize se
  neměří (postavy nemají kolizní boxy z modelů).
- **Vzhled gearu na postavě** (helma, zbroj, boty podle nošených předmětů) je samostatná velká věc.
  V této etapě jen připravit vrstvy oblečení tak, aby to šlo později. Doporučuji neudělat teď.

---

## 4. Účesy

Mužské účesy pro hráče (a NPC):

| Účes | Model | Volná část |
|---|---|---|
| Hero | `anime_hair_hero` (je) | — |
| Short | `anime_hair_short` (je) | — |
| Spiky | `anime_hair_spiky` (je) | — |
| Messy | nový | — |
| Undercut | nový | — |
| Long | `anime_hair_long` (je) | ano |
| Ponytail | čepice + `anime_ponytail` (je) | ano |
| Topknot | nový (drdol nahoře) | malá |
| Swept back | nový | — |
| Shaved | nový (jen stín na hlavě) | — |

Každý účes musí sedět pod helmy později (zatím neřešit, jen nechat místo).

---

## 5. Úprava vzhledu

### Kdy
1. Na začátku nové hry: po úvodním videu a jménu (etapa 3).
2. Později u holiče v Hearthmere (účes, vousy, barva vlasů) za malý poplatek. Holič přijde v etapě 4.

### Volby
| Volba | Možnosti |
|---|---|
| Tvar obličeje | 3 |
| Nos | 3 |
| Pleť | 6 odstínů |
| Oči | 8 barev, 2 tvary (ostré, kulaté) |
| Obočí | 3 tloušťky, barva podle vlasů nebo vlastní |
| Účes | 10 |
| Barva vlasů | 10 |
| Vousy | žádné, strniště, krátké, plné, kozí bradka, knír |
| Jizva | žádná, tvář, oko, nos |
| Výška | 0,95 až 1,05 (5 kroků) |

Barvy jsou z palety projektu (`palettes/`), ne volný výběr. Tím zůstane styl jednotný.

### Obrazovka
- Postava uprostřed na otočném podstavci, světlo jako ve hře, výraz se střídá (aby hráč viděl mimiku).
- Vlevo kategorie, vpravo volby jako šipky nebo mřížka. Tlačítko „Random".
- Potvrzení uloží vzhled do savu (`player.appearance`) a pošle ho serveru.

### Data
```json
"appearance": { "face": 1, "nose": 0, "skin": 2, "eyes": 3, "eyeShape": 0, "brows": 1,
                "browColor": -1, "hair": "messy", "hairColor": 4, "beard": "stubble",
                "scar": "none", "height": 2 }
```
- Možnosti v `world/appearance.json` (seznamy účesů, barev, vousů).
- Na server přes `Command.SetAppearance`: argument = vysoký bajt položka, nízký bajt hodnota
  (stejný vzor jako `SlotSkill`). Server uloží, klient vykreslí. V co-opu později přijde vzhled do
  `RemotePlayer`.

---

## 6. NPC

- `NpcLooks` přejde na stejné volby jako hráč (obličej, nos, účes, vousy, jizvy) plus ženská těla
  (ženské tělo a účesy už existují pro NPC; hráč je jen muž).
- Každé NPC dostane výchozí výraz (Mira `smile`, Rhea `neutral` s přísným obočím, Garrick unavený).
- Nové postavy prologu (Nell, Vosk, lovci, Haruto) se v etapě 3 složí z těchto voleb.

---

## 7. Studio

- Nová úloha `faces`: kontaktní arch všech výrazů pro hráče a tři NPC (Mira, Rhea, Bram), zepředu
  a ze tří čtvrtin.
- Nová úloha `creator`: mřížka účesů × barev, vousy, jizvy, tvary obličeje.
- Úloha `people` dál fotí všechna NPC (kontrola, že se nic nerozbilo).
- Číst archy po každé změně (pravidlo projektu).

---

## 8. Testy a kontrola

- Unit testy (shared-core): `appearance` v savu se uloží a načte; `SetAppearance` odmítne hodnoty mimo
  rozsah.
- Bot `PIXEL_BOT=creator`: projde obrazovku úpravy vzhledu, náhodně vybere, potvrdí, vyfotí.
- Bot `PIXEL_BOT=talk`: v dialogu vyfotí portrét s výrazy.
- `scripts/check.ps1 -Jobs people,faces,creator`.

---

## 9. Pořadí práce

1. `FaceRig` se současnými destičkami: víčka, obočí, tvary úst, výrazy z `world/faces.json`.
2. Studio `faces`. Naladit výrazy, dokud nejsou čitelné.
3. Portrét v dialogu a mluvení. Výraz z uzlu dialogu.
4. Výrazy v boji.
5. Modely: tvary obličeje, nosy, dlaně, nové účesy, vousy, jizvy (Blender, `build_assets.ps1`).
6. Obrazovka úpravy vzhledu, data, příkaz, uložení. Studio `creator`, bot `creator`.
7. `NpcLooks` na nové volby, kontrola `people`.

---

## 10. Hotovo když

- Hráč si upraví postavu a vzhled se uloží.
- Dvanáct výrazů je čitelných v portrétu dialogu, bolest a smích i ve hře.
- Postavy mluví (ústa) a dívají se na sebe.
- Studio ukáže výrazy, účesy a všechna NPC; nic nevypadá hůř než dnes.
