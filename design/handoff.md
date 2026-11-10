# Předávka pro další session (stav 2026-11-10 večer)

Čti v tomto pořadí: tento soubor, `design/repair-plan.md`, v `CLAUDE.md` oddíl „Done means".

## Kde jsme

- Větev `main`, vše commitnuto a pushnuto. Poslední commity dne:
  - `4665c9f` 11 nových lidí, třetí stůl na dvorku hospody, body `by` v rozvrzích.
  - `d676490` zkouška hlasů (Bram, Mira, Corwin), `generate_voices.py`, `Voices.cs`.
  - `1351a0e` drby („Heard anything?"), události dnů 3 až 10, `VillageTests`.
  - poslední commit: pravidla „Done means", plán oprav, tato předávka.
- Testy: `dotnet test net-tests` 309 zelených, `python tests/test_tools.py` OK (stav po `1351a0e`).

## Co se stalo

Autor poprvé prošel prolog a vesnici jako hráč a označil začátek za otřesný. Jeho výhrady jsou
v paměti (`playtest-quality-feedback`) a rozepsané v `design/repair-plan.md`. Podstata: testy a
poslušný bot procházely, ale hráč, který udělal něco jiného, příběh rozbil; rozhovory byly výplň;
hráč nevěděl, kam jít.

## Rozhodnutí autora z dneška

- Zajetí lovci u jezera je **nevyhnutelné** (síť, když hráč utíká nebo otálí). Větev útěku se nestaví.
- Nahrávky hlasů jsou **mimo git** (`client/assets/voices/`, v `.gitignore`).
- Pravidla „Done means" v `CLAUDE.md` platí.
- Úvod chce nahradit dvěma videi z Gemini (2 × 10 s). Prompty dostal; videa přijdou do `refs/`.
- Chce velkou mapu na M, vlastní body a cestu vyznačenou ve světě (jako Witcher 3).

## Co dělat jako první

**R1 je postavená (2026-11-10), čeká na zahrání autorem.** Bot `prologue` i všechny čtyři
`-Contrary` běhy došly do `BOT done`; testy 314 zelených. Neověřeno očima: snímky sítě, Eldy, která
jde k hráči, a cedule u Guildy. Další fáze (R2 mapa, R3 kamera, R5 lovci) až po autorově odpovědi
na R1. Nový obsah etapy 4 je zastavený. Videa úvodu: v `refs/` je zatím jedno
(`isekai_inkprint_v1.mp4`), R6 chce dvě.

Neřešeno a čeká na autora:
- Poslechl si hlasy? Rozšířit na další postavy až po přepsání textů (R4).
- Videa úvodu (R6) až je dodá.

## Co je ověřené a co ne

Potvrzeno v datech: útěk od lovců, Mira a Nell, Nell jde donekonečna (příčina: scéna 8 chce
`prologue.slept`), nejasný cíl kroku questu, „da", Bram jen „What do you make of", lovci stejní.

Jen hlášení autora, příčina je odhad: kamera a portrét v rozhovoru (viz R3).

## Na co si dát pozor (zjištěno dnes)

- **Skripty přes heredoc v Bash nástroji se rozbíjejí** na uvozovkách a delším obsahu. Jednorázový
  Python piš nástrojem Write do scratchpadu a pak ho spusť.
- Soubory mají smíšené konce řádků; při úpravě skriptem zachovej, co v souboru je.
- `scripts/play.ps1 -Capture` chce absolutní cestu k existující složce.
- Proměnné `PIXEL_DAY`, `PIXEL_TIME`, `PIXEL_SKIP_PROLOGUE` platí jen pro nový save.
- Save autora je smazaný (na jeho žádost), objevená místa na mapě také.
- Skrytá podmíněná rekvizita si drží kolizi: plot nejde „spálit" skrytím.
- `ActorSlice` posílá nejvýš 10 chodců; rozvrhy proto mají rozložené hodiny odchodů.
- Studio světové snímky staví lidi na místa ze zóny ve stoje (rozvrh a sezení neukážou). Sezení
  ověřuj živým během (`PIXEL_TIME=20`, `PIXEL_SPAWN=28.6,-7.6`, `PIXEL_BOT=idle`, `PIXEL_YAW=90`).
- ElevenLabs: zbývalo asi 84 000 znaků kreditu. Na účtu jsou jen výchozí hlasy.

## Užitečné příkazy

```powershell
powershell -ExecutionPolicy Bypass -File scripts/check.ps1            # testy + živý průchod Guildou
powershell -ExecutionPolicy Bypass -File scripts/prologue.ps1         # bot projde prolog
powershell -ExecutionPolicy Bypass -File scripts/studio.ps1 -Jobs people
python asset-pipeline/generate_voices.py --dry                         # kolik znaků by stály hlasy
```

Živý běh s botem u Corwina, den 6 večer, ptá se na drby:

```powershell
$env:PIXEL_DAY="6"; $env:PIXEL_TIME="20"; $env:PIXEL_SPAWN="25.7,3.0"; $env:PIXEL_BOT="rumor"
powershell -ExecutionPolicy Bypass -File scripts/play.ps1 -Test -Capture D:/PixelCreate/export/previews/live-yard
```
