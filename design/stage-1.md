# Etapa 1: Základy živého světa

Cíl: hra funguje jako dnes, ale z dat, s kalendářem, ukládáním a stavem světa. Žádný nový příběhový
obsah. Všechno, co je tu popsané, musí běžet v `shared-core` bez enginu a projít `dotnet test`.

Návaznost: `design/stages.md` (etapa 1), `design/systems.md` (kapitoly 3, 5, 5a, 5b).

---

## 1. Co se změní v kódu (přehled)

| Dnes | Po etapě 1 |
|---|---|
| Stav hráče jen v paměti `GameServer.Player`, po restartu pryč | uložený v souboru, načtený při startu |
| `GuildRank` Newcomer, InTrial, Adventurer | zůstává; podmínka `rank` dává 0 před registrací a 1 po ní. Ranky F až S přijdou v etapě 4 |
| Jeden aktivní quest (`QuestId`, `QuestProgress`), pole `Guild.Quests` | víc aktivních questů, stav každého (krok, postup), questy z `world/quests.json` |
| Repliky a větvení v `Dialogues.cs` (`switch` podle role) | dialogy z `world/dialogue/*.json`, klient je jen vykresluje |
| Příkazy `AcceptSlimeQuest`, `AcceptGoblinQuest`, `QuestCommands`, `TurnInQuest` | obecné `Command.Choose` (volba v dialogu) |
| Čas neexistuje (jen ticky) | kalendář: den, minuta, roční doba; denní světlo |
| Žádný stav světa | `WorldState`: flagy, pověsti, důvěra, osudy NPC, členství, kroky plánů frakcí |

Obchody, truhla, prodej a mastery zůstávají, jak jsou. Jen jejich spouštění přejde z kódu dialogů
do efektů v datech (`"action": "sellCrystals"` a podobně).

---

## 2. Kalendář

### Úprava návrhu
V `design/timeline.md` je „čas běží jen akcí". Při rozpracování jsem zjistil, že to nesedí se
střídáním dne a noci: když hráč běhá po světě, světlo by stálo. **Doporučuji a beru:** čas běží
plynule, když hráč hraje, a spánek přeskočí noc.

- **1 herní hodina = 26 s reálného času.** Den od 6:00 do 22:00 trvá asi 7 minut, noc asi 3,5 minuty.
  70 dní ≈ 9 až 12 hodin hraní (podle toho, kolik hráč spí). Sedí na odhad délky aktu I.
- **Spánek** (postel v hostinci nebo doma, později táborák) přeskočí na 6:00 dalšího dne. Spát jde
  od 20:00. Ráno = automatické uložení.
- **Pauza:** čas stojí v menu a v dialogu (hra pro jednoho; v co-opu později jen když všichni).
- **Joby** nemají délku. Čas plyne sám.
- Konstanty v `Tuning`: `GameMinuteTicks`, `DayStartMinute` (360), `SleepFromMinute` (1200).

### Datový model (`shared-core/Calendar.cs`)
```csharp
public readonly record struct GameTime(int Day, int Minute)   // Day od 1, Minute 0..1439
{
    public Season Season => Day <= 20 ? Season.Thaw : Day <= 40 ? Season.Highsun : Day <= 58 ? Season.Harvest : Season.Frost;
    public DayPart Part => ...;  // Morning 6-12, Afternoon 12-18, Evening 18-22, Night 22-6
}
```
- Server drží `GameTime`, posouvá ho podle ticků, posílá ho v každém snapshotu (`ushort Day`,
  `ushort Minute`, 4 bajty).
- Klient podle `Minute` nastaví slunce, barvu oblohy a mlhy (`GameClient` / světlo scény).
  Noc musí být tmavší, ale hratelná.

---

## 3. Stav světa (`shared-core/WorldState.cs`)

```csharp
public sealed class WorldState
{
    public GameTime Time;
    public HashSet<string> Flags;                    // "ch1.ledger.margravine", "prologue.done"
    public Dictionary<string, int> Reputation;       // "company" -> -100..100
    public Dictionary<string, int> Trust;            // "mira" -> -100..100
    public Dictionary<string, NpcFate> Fates;        // "hollis" -> Dead
    public Dictionary<string, Membership> Members;   // "company" -> (Rank 0..3, Suspicion 0..100)
    public HashSet<string> AgendaDone;               // "company.d9.poison_hollis"
    public HashSet<string> AgendaFoiled;
    public Dictionary<string, QuestState> Quests;    // "slime_cull" -> (Step, Progress, Status)
}
public enum NpcFate : byte { Alive, Dead, Gone, Moved }
```
- Jeden `WorldState` na svět (save). Hráčská data zvlášť (viz ukládání).
- Klíče jsou řetězce v datech. Pro síť se překládají na čísla: při načtení dat se sestaví **registr**
  všech jmen flagů, frakcí, postav a questů (seřazený), stejný na klientu i serveru (oba čtou stejné
  soubory z `world/`).

### Posílání klientovi
Stejně jako batoh: změněné položky přednostně, k tomu kurzor, který postupně znovu posílá všechno
(klient tak vždy dorovná ztracený paket).
```
StoryEntry(byte Kind, ushort Key, short Value)   // 5 bajtů
Kind: Flag (Value 0/1), Reputation, Trust, Fate, QuestStep, QuestProgress, QuestStatus, Member, Suspicion
```
- Až 8 položek na snapshot (40 bajtů). Hlídat `MaxSnapshotBytes` (`GameServer.Fit` už umí krátit).
- Klient drží kopii `WorldState` sestavenou z položek. Používá ji pro dialogy, deník a vzhled světa.

---

## 4. Podmínky a efekty (společné pro questy, dialogy, plány a drby)

Jeden malý jazyk v JSON. Vyhodnocuje ho `shared-core/Conditions.cs` a `Effects.cs`.

### Podmínky
```json
{ "flag": "prologue.done" }
{ "notFlag": "hollis.warned" }
{ "day": { "min": 5, "max": 12 } }
{ "time": { "from": 1080, "to": 1320 } }
{ "rank": { "min": 1 } }
{ "rep": { "company": { "min": 20 } } }
{ "trust": { "mira": { "min": 30 } } }
{ "member": { "company": { "min": 1 } } }
{ "fate": { "hollis": "alive" } }
{ "item": { "id": "goblin_tooth", "count": 4 } }
{ "gold": { "min": 15 } }
{ "attr": { "Intelligence": 6 } }
{ "quest": { "slime_cull": "active" } }          // none | active | ready | done | failed
{ "questStep": { "teeth_iron": 2 } }
{ "agenda": { "company.d9.poison_hollis": "done" } }   // done | foiled | pending
{ "near": "receptionist" }                        // jen tam, kde to dává smysl (server)
{ "all": [ ... ] }  { "any": [ ... ] }  { "not": { ... } }
```

### Efekty
```json
{ "setFlag": "hollis.warned" }        { "clearFlag": "..." }
{ "rep": { "company": -10 } }        { "trust": { "mira": 5 } }
{ "fate": { "hollis": "dead" } }
{ "member": { "company": 1 } }       { "suspicion": { "company": 15 } }
{ "gold": -15 }                      { "give": { "item": "rusty_dagger", "count": 1 } }
{ "take": { "item": "slime_gel", "count": 6 } }
{ "xp": 60 }
{ "quest": { "start": "teeth_iron" } }   { "quest": { "step": ["teeth_iron", 2] } }
{ "quest": { "complete": "slime_cull" } }   { "quest": { "fail": "..." } }
{ "agenda": { "foil": "company.d11.take_nell" } }
{ "action": "sellCrystals" }   // napojení na stávající kód serveru, seznam níže
{ "scene": "prologue_cage" }   // od etapy 3
```

Akce na stávající kód: `beginTrial`, `sellCrystals`, `sellLoot`, `resetAttributes`, `openShop:<role>`,
`openChest`, `sleep`. Nové akce přidávat jen tam, kde efekt neumí obecný jazyk.

---

## 5. Questy (`world/quests.json`)

```json
{
  "id": "slime_cull",
  "name": "Clear the Slimes",
  "kind": "job",                       // job | story | personal | faction | contract
  "giver": "board",
  "repeatable": true,
  "available": { "rank": { "min": 1 } },
  "steps": [
    { "text": "Kill 5 slimes on the road.", "goal": { "kill": "slime", "count": 5 } },
    { "text": "Report to Mira.", "goal": { "talk": "mira" } }
  ],
  "rewards": [ { "gold": 40 }, { "xp": 60 } ],
  "deadline": null,                    // nebo { "day": 30, "onExpire": [ efekty ] }
  "journal": "The road south is crawling with slimes again."
}
```
Cíle kroků: `kill` (id nebo prefix s `*`), `bring` (item a počet, odebere se při odevzdání),
`talk` (NPC), `reach` (oblast ze zóny), `choose` (konkrétní volba v dialogu), `survive` (do času),
`escort` (od etapy 3). Krok se posune sám, když je cíl splněný.

Převod: pět současných jobů se přepíše 1:1 (stejné odměny). Test ověří, že hra dává stejné peníze
a zkušenosti jako dřív.

---

## 6. Dialogy (`world/dialogue/<npc>.json`)

```json
{
  "npc": "mira",
  "entries": [
    { "when": { "rank": { "max": 0 } }, "node": "register" },
    { "when": { "quest": { "slime_cull": "ready" } }, "node": "turn_in_slimes" },
    { "node": "default" }
  ],
  "nodes": {
    "register": {
      "text": "Welcome to the Adventurers Guild! To register you must pass a trial against one of our staff.",
      "face": "smile",
      "options": [
        { "text": "Take the trial", "tone": "neutral", "effects": [ { "action": "beginTrial" } ] },
        { "text": "Not yet" }
      ]
    },
    "default": {
      "text": "Good to see you. The Guild buys every magic crystal you bring.",
      "options": [
        { "text": "Sell all crystals ({crystalWorth} gold)", "effects": [ { "action": "sellCrystals" } ] },
        { "text": "[Intelligence] Your ledger has two different inks.", "when": { "attr": { "Intelligence": 6 } }, "next": "ledger" },
        { "text": "Goodbye" }
      ]
    }
  }
}
```
- Vstupy (`entries`) se zkoušejí shora, bere se první platný. Tak se replika mění podle stavu světa.
- Možnost má `when` (zobrazí se jen když platí), `tone` (soucitný, pragmatický, tvrdý, neutrální),
  `effects`, `next` (další uzel). Bez `next` se dialog zavře.
- `face` nastaví výraz mluvčího (od etapy 2; do té doby se ignoruje).
- Proměnné v textu: `{gold}`, `{crystalWorth}`, `{saleValue}`, `{resetCost}`, `{playerName}`.
- **Kdo co dělá:** klient čte dialog ze stejných dat a vyhodnocuje podmínky nad svou kopií stavu
  (rychlé zobrazení). Volba pošle `Command.Choose` s argumentem = číslo možnosti v registru. Server
  podmínky ověří znovu svým stavem (a že hráč stojí u NPC) a provede efekty. Server je pořád jediný
  vlastník stavu.
- Obchody (Wren) a truhla zůstanou jako dnes, dialog je jen otevře (`openShop:alchemist`, `openChest`).
- Převod: všechny repliky z `Dialogues.cs` do dat. `Dialogues.cs` zůstane jako vykreslování,
  proměnné a obchody.

---

## 7. Plány frakcí (`world/agendas.json`) a drby (`world/rumors.json`)

```json
{
  "id": "company.d9.poison_hollis",
  "faction": "company",
  "day": 9,
  "when": { "all": [ { "flag": "hollis.saw_mark" }, { "fate": { "hollis": "alive" } } ] },
  "effects": [ { "fate": { "hollis": "dead" } }, { "setFlag": "hollis.murdered" } ],
  "foiledBy": { "flag": "hollis.protected" },
  "rumor": "hollis_found_dead"
}
```
- Server vyhodnotí plány při každé herní hodině. Krok proběhne, když nastal den a platí `when`.
  Když platí `foiledBy`, je překažen. Krok proběhne jen jednou.
- Drb: text, podmínka, kde se šíří (tabule, hospoda, NPC), od kdy a do kdy.
- V etapě 1 jen formát a engine a **testovací osa**: plány z `design/timeline.md` bez obsahu
  (efekty jen na flagy), aby test simuloval 70 dní a ověřil výchozí konec. Skutečné plány přibydou
  s oblouky.

---

## 8. Ukládání (`shared-core/SaveGame.cs`)

- Formát JSON přes `System.Text.Json` (součást .NET, žádný balíček).
- Umístění: `%LOCALAPPDATA%/PixelCreate/saves/<slot>.json`, záloha `<slot>.bak.json`.
  Proměnná `PIXEL_SAVE` přepíše cestu (testy, boti). Testovací režim (`PIXEL_TEST=1`) neukládá.
- Obsah:
```json
{
  "version": 1,
  "world": { "time": { "day": 3, "minute": 360 }, "flags": [...], "reputation": {...}, "trust": {...},
             "fates": {...}, "members": {...}, "agendaDone": [...], "agendaFoiled": [...], "quests": {...} },
  "player": { "name": "...", "appearance": {...}, "level": 4, "xp": 120, "gold": 55, "rank": 1,
              "attributes": [...], "skills": [...], "skillSlots": [...], "mastery": {...},
              "bag": {...}, "gear": [...], "worn": [...], "storage": {...}, "storedGear": [...],
              "position": [x, y, z], "deaths": [ { "day": 3, "pos": [...], "level": 4, "weapon": "wooden_sword" } ] }
}
```
- Ukládá se každé ráno (po spánku nebo v 6:00) a při odchodu klienta. Před zápisem se starý soubor
  přejmenuje na zálohu. Zápis přes dočasný soubor a přejmenování, aby pád uprostřed nezničil save.
- `version` pro budoucí převody formátu.
- Server při startu načte slot (`PIXEL_SLOT`, výchozí `main`), nebo založí nový svět.

---

## 9. Smrt hráče (rozhodnuto podle návrhu)

- Hráč se probudí u Anchor Stone (dnes respawn). Ztratí **10 % neprodaných krystalů** (zaokrouhleno
  dolů, nejméně 0) a zapíše se **ozvěna** (den, místo, úroveň, zbraň) do `deaths` v savu.
- V prologu (před registrací) se nic neztrácí.
- Ozvěny se použijí až v aktu III. Teď se jen zapisují.

---

## 10. Deník (klávesa L)

- Dvě záložky: **Tasks** (aktivní questy, text aktuálního kroku, hotové šedě) a **Notes** (fakta:
  flagy s textem v `world/journal.json`, například `"hollis.saw_mark": "Hollis knew the mark on the
  goblin iron: East Watch."`).
- Nahoře den, roční doba a denní doba.
- Otevírá se jako ostatní okna (jedno najednou, `HudMenu`/okna batohu).
- `PIXEL_BOT=journal` okno otevře a vyfotí.

---

## 11. Testy (`net-tests`)

| Test | Ověří |
|---|---|
| `CalendarTests` | čas plyne podle ticků, spánek skočí na 6:00 dalšího dne, roční doby |
| `ConditionTests` | každá podmínka a efekt, `all`/`any`/`not` |
| `QuestDataTests` | pět převedených jobů dává stejné odměny; kroky se posouvají; opakovatelné joby |
| `DialogueDataTests` | každý dialog odkazuje na existující uzly, flagy, questy a NPC; každá akce existuje |
| `ChooseCommandTests` | server odmítne volbu, jejíž podmínka neplatí nebo hráč není u NPC |
| `SaveGameTests` | uložit a načíst vrátí stejný stav hráče i světa; záloha; poškozený soubor načte zálohu |
| `AgendaTests` | 70 dní bez hráče dojde k výchozímu konci z `timeline.md` kapitoly 3 (na testovací ose) |
| `StorySyncTests` | klient přes simulovanou síť se ztrátou paketů složí stejný `WorldState` jako server |
| `DeathTests` | ztráta 10 % krystalů, zápis ozvěny, v prologu bez ztráty |

Plus: `bash scripts/test_net_sim.sh`, `scripts/check.ps1` (bot `guild` musí projít přes nové dialogy).

---

## 12. Pořadí práce v etapě

1. `Calendar` a `WorldState` s testy.
2. Podmínky a efekty s testy.
3. Registr jmen a `StoryEntry` ve snapshotu, synchronizace s testem přes simulovanou síť.
4. Questy z dat, převod pěti jobů, odstranění starých příkazů.
5. Dialogy z dat, `Command.Choose`, převod `Dialogues.cs`. Bot `guild` musí projít.
6. Plány frakcí a drby (engine + testovací osa).
7. Ukládání a načítání, smrt hráče.
8. Deník.
9. Denní světlo na klientu.
10. Úprava `CLAUDE.md` (nové soubory, příkazy, proměnné `PIXEL_SAVE`, `PIXEL_SLOT`).

Každý bod je commit (a push) po zelených testech.

---

## 13. Hotovo když

- Hra se hraje stejně jako dnes, ale texty, joby a volby jsou v datech.
- Den a noc se střídají, spánek funguje, uložení a načtení vrátí stejný stav.
- Test 70 dní bez hráče prochází. Všechny testy a bot `guild` jsou zelené.
