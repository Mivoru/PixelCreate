# Etapa 3: Prolog „Fallen"

Cíl: hratelný prolog od úvodního videa po rank F, 25 až 35 minut. Dny 1 a 2 kalendáře.
Návaznost: `design/plot.md` (prolog), `design/systems.md` (tutoriál), etapy 1 a 2 (data, kalendář,
dialogy, mimika, úprava vzhledu).

Repliky jsou anglicky (texty ve hře), poznámky česky. Repliky jsou první verze, ladí se při hraní.

---

## 1. Přehled

| # | Scéna | Místo | Čas | Hratelnost |
|---|---|---|---|---|
| 1 | Úvodní obrázky | — | — | 2D pixel art obrázky, jméno, úprava vzhledu |
| 2 | Pád | Mirror Lake (70, 132) | den 1, 9:00 | dopad do mělčiny, vybrodit, první volba tónu, síť |
| 3 | Klec | tábor lovců u cesty (asi 80, 80) | den 1, 19:00 | rozhovory v kleci, prohlédnout mříže |
| 4 | Přepadení | totéž | den 1, 21:00 | goblini, Haruto zemře, Rusty Dagger, první boj |
| 5 | Noční cesta | cesta na západ k Mill Hill | den 1, noc | Nell jde s hráčem, slimáci, těžký útok, uhýbání, probuzení Marku |
| 6 | Pip | pole u Mill Hill (34, 8) | noc | chůze kolem zvířat, druhá volba tónu |
| 7 | Elda | pekárna, Hearthmere | před svítáním | jídlo léčí, spánek (stmívačka do rána) |
| 8 | Bram | studna, Hearthmere | den 2, 7:00 | třetí volba tónu, cesta ke Guildě |
| 9 | Guilda | interiér Guildy | den 2, 8:00 | Mira, Nell do skladu, Anchor Stone |
| 10 | Zkouška | aréna | den 2, dopoledne | tři kola: blok a parry, dash, souboj |
| 11 | Konec prologu | Guilda | den 2, večer | rank F, háček na Haruta a na Nell |

Souřadnice jsou z dnešní zóny (oblasti `Mirror Lake`, `Old Ruins`, `Mill Hill`, `Meadow Village`).
Přesná místa se vyberou při stavbě: rovná zem, mimo tábory nepřátel, kontrola sklonu před umístěním.

---

## 2. Scény a repliky

### Scéna 1: Úvodní obrázky
Sedm obrázků 320×180 (Pixelwright, export do `src/`, PNG do klienta), stmívačky, zvuk deště.
Žádný hlas, jen pár slov.

1. Noční město v dešti, přechod, semafor bliká.
2. Malé dítě vyběhne do silnice za míčem.
3. Světla náklaďáku, klakson.
4. Postava odstrkuje dítě zpět na chodník.
5. Bílá.
6. Černá. Uprostřed se pomalu objeví podivné znaky (auréjské písmo), pod nimi: *„Unfinished."*
   Pak: *„Bound."*
7. Pád mraky, na nebi síť světelných čar (Lattice), pod ní krajina. Přes obraz název **Forty Crowns**.

Pak obrazovka jména: *„What was your name?"* a úprava vzhledu: *„Remember yourself."* (etapa 2).

### Scéna 2: Pád
Kamera shora, hráč dopadne do mělčiny u břehu Mirror Lake (bez plavání). Hráč dostane ovládání,
vybrodí se. Na břehu vůz s klecí a čtyři muži: **Vosk** a tři lovci (**Gil**, **Hanne**, **Corb**).

> **Gil:** Told you. Saw the light come down over the ridge.
> **Vosk:** Easy now, Lightborn. Easy. Nobody wants you hurt. Hurt ones sell cheap.

Volba (první volba tónu, zapíše se jako první dojem u Vosky):
- *„Where am I?"* → **Vosk:** Somewhere you don't belong. That makes you mine.
- *„Stay back."* → **Vosk:** Spirit. Good. Spirit fetches a better price.
- *[Say nothing.]* → **Vosk:** Quiet one. Even better.

> **Vosk:** Gil. The net.

Síť (scéna, neuhnutelná), rána, tma. Hráč nic neztrácí, žádný boj.

### Scéna 3: Klec
Probuzení v kleci na stojícím voze. Tábor lovců u cesty, ohniště, soumrak. Hráč se v kleci smí
rozhlížet a mluvit. Cesta vozem se neukazuje (stmívačka): hráč nejede uvnitř pohybujícího se vozu,
to by komplikovalo predikci pohybu.

**Nell** (13, výraz `afraid`):
> **Nell:** Don't shout. They beat the last one who shouted.

Možnosti (lze projít všechny):
- *„Who are you?"* → **Nell:** Nell. From Westwood. My da owed the Company. Now I'm what he owed.
- *„Who are they?"* → **Nell:** Finders. Company men. They wait at the lake for your kind. Lightborn sell for forty crowns. I sell for fifteen.
- *„Where are we?"* → **Nell:** The March. The far end of the kingdom, where the Company does what it likes.
- *„We'll get out of here."* (soucitná) → **Nell:** ...You don't know that. But thank you for saying it. (důvěra +)

**Haruto** (sedí v rohu, `tired`, mumlá):
> **Haruto:** ...the nine o'clock train... I have to call the office... the presentation...
> *(k hráči)* You too? You died too? ...Then where is this? Where IS this?

Prohlédnout mříže (interakce):
> *The lock is new iron. The bars are old wood. One of them is cracked near the floor.*
(flag `prologue.bar_seen`; při přepadení se klec rozbije právě tam)

**Vosk** přijde ke kleci:
> **Vosk:** *(k Gilovi)* Forty crowns, that one. Don't damage the face.
> **Vosk:** *(k Nell)* And you, girl. Quiet. Fifteen crowns doesn't need a tongue.

Stmívačka do noci.

### Scéna 4: Přepadení
Ze tmy šípy. Goblinský křik. Lovci bojují s goblíny (skriptovaně, padnou Corb a Hanne). Goblin
s palicí buší do klece, prasklá mříž povolí.

Haruto se vypotácí ven, goblin ho probodne. Haruto se rozpadne ve světlo, které stoupá a letí
k severovýchodu (k jezeru).
> **Nell:** He— he's gone. Where did he go?

Mrtvý lovec u klece: **Rusty Dagger** (výzva k sebrání). **První boj:** dva goblini
(`goblin_skirmisher`). Nápověda: lehký útok. Goblini mají nové železné zbraně: kamera se na chvíli
zastaví na čepeli (hráč si to zapamatuje).

> **Vosk:** *(na koni, ve tmě)* Not worth dying for! Fall back! Fall back!

Vosk a Gil utečou. Kořist u lovců: chléb, Minor Healing Potion, **Nell's Bond** (nový předmět,
druh `document`, dá se přečíst):
> *Bond of Nell, daughter of Aron of Westwood. Fifteen crowns, to the Halveth Trading Company,
> East Watch warehouse. Signed: Aron (his mark). Witnessed: B. Vosk.*

(Smlouva je v batohu hráče. V oblouku B je to klíč: kdo ji drží, může ji spálit, prodat, nebo
před markraběnkou ukázat, že ji „svědčil" sám lovec.)

> **Nell:** We can't stay. More of them come at night. There's a village west. Hearthmere. They have a Guild.

Začne doprovod: Nell jde za hráčem.

### Scéna 5: Noční cesta
Nell jde 2 až 3 m za hráčem, nepřátelé ji ignorují (žádná frustrace z doprovodu). Při ztrátě se
k hráči přenese. Na cestě tři slimáci (svítí ve tmě).

> **Nell:** Slimes. They jump where the ground glows. When it glows, get out of the way!
> **Nell:** Hit it hard when it lands. It's soft right after.

Nápovědy: uhýbání (dash), těžký útok. **První zabité monstrum:** světlo z mrtvého slimáka vletí do
hráče, rozhraní se rozsvítí (úroveň, zkušenosti, krystal). Do té doby je vidět jen zdraví.

> **Nell:** *(couvne, `afraid`)* Your eyes. They lit up. You're one of them. The Lightborn.

Volba:
- *„I don't know what I am."* (soucitná) → **Nell:** ...The Guild knows. Mira knows everything. (důvěra +)
- *„Does it matter? We need to move."* (pragmatická) → **Nell:** No. I suppose not. West, then.
- *„Afraid of me?"* (tvrdá) → **Nell:** Everyone is. You'll see. (důvěra −)

### Scéna 6: Pip
Pole u Mill Hill, noc. **Pip** s lucernou a vidlemi hlídá pole před kanci.

> **Pip:** Stop there! Who walks the fields at night? ...Gods. You're one of them. A shine. Get away from my cows!
> **Nell:** Pip! It's me, Nell. The tinker's girl from the summer fair. He got me out.
> **Pip:** ...Got you out. A shine did. *(k hráči)* Walk slow past the cows. They scare. Elda's lit; she bakes before dawn.

Volba:
- *„Thank you, Pip."* → **Pip:** Don't thank me. Just don't run.
- *„Why do you hate us?"* → **Pip:** One of you burned my barn last year. With my dog in it. For a *quest*, he said. (háček na Dexe)
- *„I'll go where I like."* → **Pip:** Then the cows'll trample you, and good riddance. (pověst Hearthmere −)

Krávy se plaší z běhu (to už ve hře je). Nápověda: chůze.

### Scéna 7: Elda
Pekárna, před svítáním. Teplé světlo pece.

> **Elda:** Another one who fell from the sky? You are the third this year. Sit. Eat. Both of you.
> **Elda:** *(když je hráč zraněný)* Eat something when you're hurt. A good steak mends more than any potion.

Dá chléb a Raw Steak. Nápověda: jídlo léčí (použití z batohu, `Command.UseItem`).
> **Elda:** Sleep by the oven. Nobody comes looking in a bakery.

Stmívačka, den 2, 6:00, automatické uložení. (Skutečný spánek v posteli se učí později u Guildy.)

### Scéna 8: Bram
Ráno u studny. **Bram** (existující replika jako první věta).

> **Bram:** You have a strange look, stranger.

Volba:
- *„I fell from the sky. Do you know what that means?"* → **Bram:** *(dlouhý pohled)* I know what it meant once.
- *„Who's asking?"* → **Bram:** An old man who's seen your kind before.
- *„Just passing through."* → **Bram:** Nobody passes through Hearthmere. They fall into it.

Pak vždy:
> **Bram:** Register at the Guild before sundown, boy. Before they find you again.
> **Bram:** And leave the girl with Mira. Not with me. Not with anyone the Company knows.

(Brama to stojí trochu důvěry, ať hráč odpoví jakkoli. Je první, kdo hráči něco skutečně dá.)

### Scéna 9: Guilda
Dveře Guildy (portál). Uvnitř Mira, Hollis, Sora a Kai.

> **Mira:** Welcome to the Adventurers Guild! ...Oh. Oh, you're Lightborn. And you're— Nell? The Company's Nell?

Volba:
- *„She's with me. She needs help."* → **Mira:** Then we'd better be quick and quiet. Into the storeroom, Nell. Nobody goes there but Hollis, and Hollis doesn't talk.
- *„The Company bought her. I don't know what to do."* → **Mira:** Nobody here knows what to do about the Company. But not today. Storeroom, Nell.

> **Mira:** Registration makes you ours in the eyes of the law. Finders can't touch a registered adventurer.
> **Mira:** And the Anchor Stone will know you. Touch it. If you fall, you'll wake here, not at the lake.

Hráč se dotkne Anchor Stone (nový objekt v Guildě, svítí). Zvuk, záblesk. Od teď respawn u Guildy.

> **Mira:** But first, the trial. Rhea will test you. Everyone goes through Rhea.

Volitelné rozhovory: Sora (`„I came from another world too..."`) a Kai (`„Rhea once threw me across
the whole arena..."`): stávající repliky, přepsané do dat.

### Scéna 10: Zkouška u Rhey (tři kola)
Stávající zkouška se rozdělí na kola. Server hlídá cíl kola (počítadla `Blocks`, `Parries`, `Dodges`
už existují).

**Kolo 1: blok a parry.** Rhea jen útočí, pomalu.
> **Rhea:** Round one. You don't strike. You hold. Hold the wheel and take my blows. Then catch one at the very last moment.
Cíl: 3 bloky a 1 parry.
> **Rhea:** Good. A guard is a wall. A parry is a door you slam in their face.

**Kolo 2: dash přes těžký úder.**
> **Rhea:** Round two. When I lift the blade high, the ground goes red. Don't block it. Go through it.
Cíl: 3 úniky dashem přes těžký úder.
> **Rhea:** You moved before you thought. Keep that.

**Kolo 3: souboj** (dnešní chování `duelist`).
> **Rhea:** Last round. Everything you have. I won't hold back much.
- Výhra: **Rhea:** ...Not bad. For someone who was dead two days ago.
- Prohra: hráč se probudí u Anchor Stone (bez ztráty v prologu).
  **Mira:** Welcome back. That's what the stone is for. Rhea's waiting. (kolo 3 znovu)

### Scéna 11: Konec prologu
> **Mira:** Adventurer, rank F. Welcome. *(podá odznak)*
> **Mira:** And now the hard part. Sooner or later the Company will come asking about a girl.

Pokud hráč mluvil s Haruto v kleci:
> **Nell:** *(ze dveří skladu)* The man in the cage. When they vanish like that... where do they go?
> **Mira:** Unbound ones wake where they fell. At the lake. *(ztiší hlas)* Where the finders wait.

Zapíše se úkol **The Second One** (deník) a flag `prologue.done`. Den 2, večer. Od dne 3 je svět otevřený
(tabule, joby, kalendář a plány frakcí běží).

---

## 3. Co je potřeba postavit

### Systémy
| Systém | Popis |
|---|---|
| **Scény** (`world/scenes/*.json`, `shared-core/Scenes.cs`) | Kroky: `slides`, `camera`, `say` (mluvčí, text, výraz, volby = dialog z etapy 1), `move` (herec po bodech), `pose`, `spawn`, `despawn`, `fade`, `wait`, `waitFor` (podmínka: nepřátelé mrtví, hráč v oblasti, flag), `lockInput`, `effects`. Server scénu řídí (herci, efekty, podmínky), klient ukazuje kameru, obrázky a text. Ve snapshotu `SceneCue` (scéna, krok, tick začátku). `Command.SceneNext` přeskočí text. |
| **Herci (pohyblivá NPC)** | NPC se dnes nehýbou (pevně v zóně). Nově mají pozici, směr a pózu na serveru a jdou ve snapshotu (`NpcView`). Hodí se i pro denní rozvrhy v etapě 4. Pohyb jen po bodech a s kolizí proti `World`, žádná fyzika Godotu. |
| **Doprovod** | Nell jde za hráčem (řízení k bodu za hráčem, kolize, přenos při vzdálenosti nad 30 m). Nepřátelé ji ignorují. |
| **Postupné rozhraní** | Části HUD se zapínají flagy: na začátku jen zdraví; `hud.mark` (úroveň, zkušenosti, krystaly, skilly) po prvním monstru; batoh hned (jídlo u Eldy). |
| **Kola zkoušky** | Stav zkoušky na serveru s cílem kola. Rheino chování podle kola (kolo 1 jen útoky, kolo 2 jen těžké údery, kolo 3 dnešní). |
| **Anchor Stone** | Objekt v Guildě, flag `anchor.bound`. Respawn: před vázáním u jezera (jen teoreticky; v prologu hráč nezemře před Guildou), po vázání v Guildě. |
| **Dokumenty** | Druh předmětu `document` s textem, `Command.UseItem` ho otevře ke čtení. |
| **Nová hra** | Nový save začne prologem. `PIXEL_TEST=1` a `PIXEL_SKIP_PROLOGUE=1` prolog přeskočí (registrovaný hráč v Guildě, jako dnes), aby ostatní boti fungovali. |

### Obsah
| Druh | Co |
|---|---|
| Obrázky | 7 obrázků úvodu (320×180, Aseprite v `src/intro/`) |
| Postavy | Nell, Vosk, Gil, Hanne, Corb, Haruto (z voleb etapy 2) |
| Modely | vůz s dřevěnou klecí (a rozbitá varianta), tábor lovců (stany, ohniště, bedny), Anchor Stone, kůň (Vosk; stačí jednoduchý `beast_horse` z `beasts.py`) |
| Předměty | `nells_bond` (document), ikona |
| Dialogy a scény | `world/scenes/prologue_*.json`, `world/dialogue/` pro Nell, Vosku, Pipa, Eldu, Brama, Miru, Rheu (prologové vstupy) |
| Deník | úkol prologu, úkol The Second One, poznámky (goblini se železem, Nellina smlouva) |
| Zvuky | déšť, klakson (úvod), síť, praskání klece, světlo duše (ElevenLabs, `generate_sounds.py`) |
| Zóna | spawn nové hry v mělčině Mirror Lake, tábor lovců, místo přepadení, slimáci na cestě, Pip u Mill Hill |

Lovci v prologu **nebojují s hráčem** (jen skriptovaně s goblíny). Lovec jako nepřítel se sítí
přijde v etapě 5 (osvobození Haruta). Tím se prolog zjednoduší.

---

## 4. Testy a kontrola

- `PrologueTests` (net-tests, bez klienta): scéna po scéně přes server: síť, klec, přepadení, smrt
  goblinů posune scénu, doprovod, první zabití zapne `hud.mark`, Guilda, kola zkoušky s počítadly,
  konec s flagem `prologue.done`, den 2.
- `ZoneAuditTests`: nová místa dosažitelná, tábor lovců dost daleko od vesnice.
- Bot `PIXEL_BOT=prologue`: projde celý prolog přes skutečné rozhraní (volby, boj, zkouška) a vypíše
  `BOT` fáze. `scripts/check.ps1` ho spustí místo bota `guild` (bot `guild` s `PIXEL_SKIP_PROLOGUE=1`
  zůstane).
- Studio: `models` (vůz, klec, tábor, Anchor Stone, kůň), `people` (nové postavy), nové záběry
  `world` (tábor lovců, místo přepadení) do `client/studio/shots.json`.
- Čas: prolog dohraný za 25 až 35 minut (bot měří, ruční hraní potvrdí).

---

## 5. Pořadí práce

1. Herci a `NpcView` (pohyblivá NPC), stávající NPC jako herci bez pohybu.
2. Systém scén (server a klient), jednoduchá testovací scéna.
3. Doprovod.
4. Postupné rozhraní a Anchor Stone.
5. Kola zkoušky.
6. Modely a postavy, tábor a místa v zóně.
7. Scény 2 až 11 a dialogy, předmět smlouvy, deník.
8. Úvodní obrázky, jméno a úprava vzhledu na začátku nové hry.
9. Zvuky.
10. `PrologueTests`, bot `prologue`, studio, ruční hraní, úprava textů.

---

## 6. Hotovo když

- Nová hra začne úvodem a dojde až k ranku F bez zaseknutí.
- Bot `prologue` a `PrologueTests` projdou, prolog trvá 25 až 35 minut.
- Každá volba v prologu se projeví (důvěra, flag, replika později).
- Studio ukáže nové modely a místa. Ty si prolog zahraješ a texty upravíme podle toho.
