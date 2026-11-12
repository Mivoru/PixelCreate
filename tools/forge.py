"""Writes world/dialogue/smith.json: Tess forges gear from monster materials and coin.

Each recipe of RECIPES is two options, of which one shows: "Forge ..." while the bag and purse hold enough (it
takes the materials and gold and runs the server action forge:<item>, a rare piece of the player's level), or the
same recipe marked as short, which only says what is missing. Materials and prices are guesses, not measured
balance. Run it after changing a recipe:

    python tools/forge.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# (item forged, {material: count}, gold)
RECIPES = [
    ("twin_daggers", {"wolf_fang": 4, "iron_scrap": 2}, 40),
    ("hunters_longbow", {"vine_fiber": 2, "antler": 1, "hawk_feather": 2}, 50),
    ("spiked_club", {"boar_tusk": 2, "iron_scrap": 3}, 30),
    ("bone_staff", {"slime_core": 1, "crow_eye": 3, "wisp_ember": 1}, 60),
    ("iron_helm", {"iron_scrap": 6, "leather_scrap": 2}, 40),
    ("leather_jerkin", {"deer_hide": 3, "leather_scrap": 2}, 30),
    ("boar_boots", {"boar_hide": 3}, 20),
    ("chain_leggings", {"iron_scrap": 8, "stone_scute": 1}, 60),
]


def main():
    items = {item["id"]: item for item in json.loads((ROOT / "world" / "items.json").read_text(encoding="utf-8"))}
    options = []
    for forged, materials, gold in RECIPES:
        assert items[forged]["kind"] == "equipment", forged
        for material in materials:
            assert material in items, material
        cost = ", ".join(f"{count} {items[m]['name']}" for m, count in materials.items()) + f", {gold} gold"
        enough = {"all": [{"item": {"id": m, "count": c}} for m, c in materials.items()] + [{"gold": {"min": gold}}]}
        effects = [{"take": {"item": m, "count": c}} for m, c in materials.items()]
        effects += [{"gold": -gold}, {"action": f"forge:{forged}"}]
        options.append({"text": f"Forge the {items[forged]['name']} ({cost}).", "when": enough, "effects": effects, "next": "forged"})
        options.append({"text": f"{items[forged]['name']}: {cost}. (Not enough yet.)", "when": {"not": enough}, "next": "short"})
    options.append({"text": "Goodbye."})
    dialogue = {
        "npc": "smith",
        "entries": [{"node": "forge"}],
        "nodes": {
            "forge": {
                "text": "Bring me what the monsters leave behind and coin for the coal, and I'll make you something better than the junk goblins carry.",
                "face": "neutral",
                "options": options,
            },
            "forged": {
                "text": "There. Mind the edge, it's still warm. Anything else?",
                "face": "smile",
                "options": [{"text": "Show me the rest.", "next": "forge"}, {"text": "Goodbye."}],
            },
            "short": {
                "text": "Not with that. Come back when your bag and your purse are heavier.",
                "options": [{"text": "Show me the rest.", "next": "forge"}, {"text": "Goodbye."}],
            },
        },
    }
    path = ROOT / "world" / "dialogue" / "smith.json"
    path.write_text(json.dumps(dialogue, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(RECIPES)} recipes for Tess")


if __name__ == "__main__":
    main()
