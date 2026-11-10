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
| 23:40 | 1 | hotovo: nový terén údolí (5 skalisek místo vnitřních hřbetů, Silverbrook ven západním sedlem, Ironwash do Mirror Lake), most přes Ironwash u East Watch (183, −30) severojižně; nájezd i testy mostů podle delší strany desky; 327 testů zelených, snímek `ironwash_bridge` ukazuje vodu pod mostem |
| 23:55 | 2 | hotovo: world-plan.md kapitola 8 (biomy, atmosféra s prolínáním 40 m, logika sídel, standard domů, opuštěná místa), waystones jen od kamene ke kameni, voda v `layout.json` podle údolí (Silverbrook, Westbrook, Ironwash do Mirror Lake, Mirrorrun za jižním okrajem), mapy překresleny |
