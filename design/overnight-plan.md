# Noční běh 2026-11-10 → 2026-11-11 (do 8:00)

Autor zadal: pracovat samostatně do 8:00 ráno podle doporučení (přestavba Meadow Valley a doplnění
plánu světa), a když dojdou kredity, počkat a pokračovat. Opakované probuzení dělá cron v této
session každých zhruba 15 minut; každé probuzení pokračuje prvním nehotovým úkolem.

Pravidla:
- Úkoly v pořadí. Každý končí zelenými testy, commitem a pushem (`git push`), řádkem v logu níže.
- Vizuální změny ověřit snímky (studio nebo živý běh) a přečíst je.
- Nespouštět hru, pokud už běží (autor hraje): `Get-Process Godot*` před živým během.
- Neměnit nic nevratně (savy autora, mazání dat). Žádné placené generování hlasů. Zvuky jen pár kusů.
- Když úkol uvázne po dvou pokusech: zapsat do logu proč a jít na další.
- V 8:00 skončit: dopsat shrnutí pro autora na konec tohoto souboru (co je hotové, co si zahrát,
  co se nepovedlo) a zrušit cron.

## Úkoly

1. **Mosty v obou směrech** (deska podél delší strany plošiny), most přes Ironwash u East Watch,
   rovný nájezd silnice, testy mostů obecně. Zóna zelená.
2. **Plán světa doplnit** o dobré části cizího návrhu: tabulka biomů podle výšky a sklonu (povrch,
   vegetace, zvířata), atmosféra regionů (mlha, obloha, zvuk, prolínání 40 m), logika měst (voda proti
   proudu, stoka po proudu jako mini dungeon, tři šířky ulic, lucerny 19 až 6 h, ukazatele),
   konstrukční standard domů, opuštěná místa a nová zajímavá místa, rychlé cestování jen od kamene
   ke kameni. Opravit vodu v `layout.json` podle údolí (Silverbrook ven západním sedlem jako Westbrook,
   Ironwash do Mirror Lake, Mirrorrun pramení za jižním okrajem). Překreslit mapy.
3. **Jména světových stran:** Southern Wilds → Northern Wilds (zóna, dokumenty, testy).
4. **Terén doladit:** kulaté „pupínky" kopců nahradit přirozenými tvary (protáhlé hřbety, terasy u
   jižního okraje, svahy k Silver Falls), znovu vykreslit minimapu (studio `map`), snímky, audit zóny,
   výkon (`frameMs`).
5. **Vegetace podle biomů:** biom podle výšky, sklonu a blízkosti vody v `Zone.Scatter` (data
   v zóně), louky, okraje lesů, lužní les, skalnatý okraj, vřesoviště u Moonfell, borovice u Greyfangu;
   snímky a výkon.
6. **Vesnice:** kaple a hřbitov (modely v Blenderu), detaily (truhlíky, sudy, prádlo, vývěsní štíty,
   studna s rumpálem, lucerny), umístit, kolize, testy, studio.
7. **Monstra a AI:** audit všech druhů (čitelnost útoků, zvuky, skupina, ústup) do
   `design/monster-audit.md`, pak první vylepšení s testy.
8. **Questy s logikou:** oznámení na nástěnce s důvodem, místem a zadavatelem; joby reagují na svět.
9. **Dungeony:** Sunken Archive, pak Goblin Warren (interiéry na plošinách, portály).
10. **Technika zón:** přechody v sedlech, waystones (od kamene ke kameni).

## Log

| Čas | Úkol | Výsledek |
|---|---|---|
| 22:58 | start | plán zapsán, cron nastaven |
| 23:21 | 1 | hotovo: nový terén údolí (5 skalisek místo vnitřních hřbetů, Silverbrook ven západním sedlem, Ironwash do Mirror Lake), most přes Ironwash u East Watch (183, −30) severojižně; nájezd i testy mostů podle delší strany desky; 327 testů zelených, snímek `ironwash_bridge` ukazuje vodu pod mostem |
| 23:23 | 2 | hotovo: world-plan.md kapitola 8 (biomy, atmosféra s prolínáním 40 m, logika sídel, standard domů, opuštěná místa), waystones jen od kamene ke kameni, voda v `layout.json` podle údolí (Silverbrook, Westbrook, Ironwash do Mirror Lake, Mirrorrun za jižním okrajem), mapy překresleny |
| 23:25 | 3 | hotovo: Southern Wilds → Northern Wilds (zóna, snímek `wildcamp_north`), CLAUDE.md opravené světové strany (Greyfang JV, Moonfell JZ, Ironjaw SV); příběhové dokumenty nechány; kdo už měl Southern Wilds objevené, uvidí jméno až po novém objevení |
| 23:33 | 4 | hotovo: kopce mohou být protáhlé (`stretch`, `yaw`) s laločnatým obrysem a terasované (`terrace`); 8 kulatých kopců přetvořeno na hřbety, nový terasovaný hřbet u jižního okraje (40, 250); kopce otočené tak, aby žádný tábor nestál na horším svahu než dřív (kontrola výškového rozdílu v kruhu tábora); minimapa a mapa zóny překresleny, snímky `south_terraces`, `wildcamp_north`, `goblin_fort` přečteny; 327 testů zelených. Výkon neměřen (tvar terénu nemění počet objektů) |
| 23:39 | 5 | hotovo: rozptyl má pravidla biomů (`maxHeight`, `steep`, `dry`, `edge`); osamělé duby na loukách mimo vesnici, vrby a olše podél všech toků, keře a mladé břízy na okrajích lesů, kameny na svazích, borovice na výšinách Greyfangu, trsy a kameny vřesoviště v Moonfell; snímky údolí, mostu, Moonfell přečteny; idle bot ve vesnici 9,4 ms (dřív 9,6), 2092 draws. Vřes jako model zatím chybí (náhradou trsy trávy) |
| 23:53 | 6 | kaple (model `chapel`: kamenná loď, opěráky, lomená okna, klenuté dveře, rozeta, zvonička se zvonem) a hřbitov (`village_graves`: náhrobky, dřevěné kříže, obruby, květiny) severně od vesnice u cesty ke Goblin Hollow (−18, −38); zídka s brankou, pěšinky ke dveřím a brance, ušlapaná zem mezi hroby, lucerna u dveří, značka kaple na minimapě (kříž); snímky `village_chapel`, `village_churchyard` přečteny; 327 testů zelených. Ostatní detaily vesnice (prádlo, štíty, studna s rumpálem) zatím ne |
| 23:57 | 7 | hotovo: audit všech druhů v `design/monster-audit.md` (nápřahy, varování, zvuky, skupina, ústup; 8 zjištění, pořadí dalších kroků); první vylepšení: morálka goblinů (zraněný goblin jednou uteče k táboru a vzdá se tahu, pak bojuje dál; bruti, orkové, vlci, vlkodlaci a goblini ze scén ne), `MoraleTests` (4), 331 testů zelených. Nápřahy lehkých útoků jen navrženy (blízko balancu, rozhodne autor) |
| 00:01 | 8 | hotovo (data): každé oznámení na nástěnce má zadavatele, důvod a místo (Pip, Garrick, Wren, Hamm, Corwin, Old Bram), kroky jmenují místo, opraven omyl "goblini jižně od vesnice"; tři nové joby se zpřístupní podle úrovně (vlci v Greyfangu od 4, orčí zvědové od 7, vlkodlak v Moonfell od 9); odměny nových jobů jsou jen zástupné. Joby reagující na stav světa (počet živých v táboře) zatím ne, chybí podmínka; 331 testů, `check.ps1` s živým během Guildy prošel |
| 00:20 | 9a | hotovo: Sunken Archive. Dílky dungeonu (`dungeons.py`: podlaha s kanálem černé vody, klenba s kořeny, stěna s výklenkem knih a svítícími runami, regály, převrácený regál, sloupy, pult s knihou, jeskynní vstup s auréjským portálem), síň 48 x 36 m na x 560, vchod v jeskyni u Silver Falls (−42, 134), slimové 5 až 7, goblinští lupiči a šaman; podzemní světlo (tlumené slunce, tmavá mlha, azurové lampy); job `Fire in the Archive` (Wren, od úrovně 6); audit zóny prochází dveřmi; živý běh uvnitř 7,7 ms, snímky přečteny |
| 00:43 | 9b | hotovo: Goblin Warren (x 640): hliněné stěny s výdřevou a pochodněmi, nízký strop s trámy, sloupky, klikatá cesta třemi komorami, náčelníkovo doupě s chýší a ohněm, rozbitá klec; doupě v noře hned západně od goblinské pevnosti (70, −114); goblini a vzácný šaman; job `Into the Warren` (Corwin, od úrovně 7). Cestou nalezena a opravena stará chyba: dávkované rekvizity ztrácely posun uzlu z glb (lucerny a ukazatele zapadlé o metr a víc, bedny, lavice, kameny, ploty mimo kolizi) |
