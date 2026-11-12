"""Writes world/dialogue/<id>.json for every cooking fire of a zone: raw food in, cooked food out.

A cooking fire is an NPC of role "hearth" in the zone file (no figure; it stands on a camp_fire prop or a hearth),
with an optional `note` (what the fire looks like). Each recipe of RECIPES is an option shown only while the bag
holds the raw item; choosing it takes one and gives the cooked one, and the dialogue stays at the fire so the
player can cook again. Run it after adding a fire or a recipe:

    python tools/cooking.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZONE = ROOT / "world" / "zones" / "meadow-valley.json"

# (raw item, cooked item, what the option says)
RECIPES = [
    ("raw_chicken", "roast_chicken", "Turn a chicken on the spit."),
    ("hare_meat", "game_skewer", "Skewer the hare meat."),
    ("goat_meat", "game_skewer", "Skewer the goat meat."),
    ("mutton", "roast_mutton", "Roast the mutton."),
    ("boar_meat", "boar_roast", "Roast the boar meat."),
    ("venison", "venison_steak", "Sear the venison."),
    ("raw_beef", "seared_steak", "Sear the beef."),
    ("mirefin_fillet", "grilled_fillet", "Grill the mirefin fillet on a hot stone."),
    ("river_perch", "fried_perch", "Fry the perch in the pan."),
    ("silver_trout", "baked_trout", "Bake the trout in the embers."),
]

DEFAULT_NOTE = "The fire burns low and steady. A spit and a blackened pan wait over the embers."


def dialogue(fire):
    options = [
        {
            "text": text,
            "when": {"item": {"id": raw}},
            "effects": [{"take": {"item": raw, "count": 1}}, {"give": {"item": cooked, "count": 1}}],
            "next": "fire",
        }
        for raw, cooked, text in RECIPES
    ]
    options.append({"text": "Leave the fire."})
    return {
        "npc": fire["id"],
        "entries": [{"node": "fire"}],
        "nodes": {
            "fire": {
                "text": fire.get("note", DEFAULT_NOTE) + " Raw meat or fish from the bag cooks here, one piece at a time.",
                "options": options,
            },
        },
    }


def main():
    items = {item["id"] for item in json.loads((ROOT / "world" / "items.json").read_text(encoding="utf-8"))}
    for raw, cooked, _ in RECIPES:
        assert raw in items and cooked in items, f"unknown item in recipe {raw} -> {cooked}"
    zone = json.loads(ZONE.read_text(encoding="utf-8"))
    fires = [npc for npc in zone["npcs"] if npc.get("role") == "hearth"]
    for fire in fires:
        path = ROOT / "world" / "dialogue" / f"{fire['id']}.json"
        path.write_text(json.dumps(dialogue(fire), indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(fires)} fires: " + ", ".join(fire["id"] for fire in fires))


if __name__ == "__main__":
    main()
