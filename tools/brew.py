"""Writes world/dialogue/wren.json: Wren sells potions and brews them from herbs the player gathers.

Each brew of BREWS is two options, of which one shows: "Brew ..." while bag and purse hold enough (takes the herbs
and gold, gives the potion) or the same brew marked as short. Herbs grow where the zone's `gathers` put them.
Amounts and prices are guesses, not measured balance. Run it after changing a brew:

    python tools/brew.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# (potion, {herb: count}, gold)
BREWS = [
    ("minor_healing_potion", {"meadow_sage": 3}, 4),
    ("healing_potion", {"glowcap": 2, "moonpetal": 1}, 10),
    ("warding_draught", {"glowcap": 1, "moonpetal": 1, "fin_spine": 1}, 12),
]

HERBS = ("Bring me sage from the meadows, glowcaps from the deep woods or moonpetal from the old stones, "
         "and I'll brew them for a few coins.")


def main():
    items = {item["id"]: item for item in json.loads((ROOT / "world" / "items.json").read_text(encoding="utf-8"))}
    brews = []
    for potion, herbs, gold in BREWS:
        assert items[potion]["kind"] == "potion", potion
        cost = ", ".join(f"{count} {items[h]['name']}" for h, count in herbs.items()) + f", {gold} gold"
        enough = {"all": [{"item": {"id": h, "count": c}} for h, c in herbs.items()] + [{"gold": {"min": gold}}]}
        effects = [{"take": {"item": h, "count": c}} for h, c in herbs.items()]
        effects += [{"gold": -gold}, {"give": {"item": potion, "count": 1}}]
        name = items[potion]["name"]
        brews.append({"text": f"Brew a {name} ({cost}).", "when": enough, "effects": effects, "next": "brewed"})
        brews.append({"text": f"Brew a {name}: {cost}. (Not enough yet.)", "when": {"not": enough}, "next": "short"})
    goodbye = {"text": "Goodbye"}
    again = [{"text": "Show me the rest.", "next": "shop"}, goodbye]
    dialogue = {
        "npc": "wren",
        "entries": [{"when": {"gold": {"min": 1}}, "node": "shop"}, {"node": "broke"}],
        "nodes": {
            "shop": {
                "text": "Potions, fresh from the cauldron! One heals you at once, but give your body three breaths before the next. "
                        + HERBS + " You have {gold} gold.",
                "shop": "alchemist",
                "options": brews + [goodbye],
            },
            "broke": {
                "text": "Potions, fresh from the cauldron! Come back when the Guild has paid you; I do not give them away. " + HERBS,
                "shop": "alchemist",
                "options": brews + [goodbye],
            },
            "brewed": {
                "text": "There, still warm. Shake it before you drink it, and don't drink it all at once in a fight. Well, do.",
                "options": again,
            },
            "short": {
                "text": "That's not enough to fill a flask. The herbs grow back if you leave the roots; go and look again.",
                "options": again,
            },
        },
    }
    path = ROOT / "world" / "dialogue" / "wren.json"
    path.write_text(json.dumps(dialogue, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(BREWS)} brews for Wren")


if __name__ == "__main__":
    main()
