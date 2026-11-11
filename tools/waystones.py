"""Writes world/dialogue/waystone_<id>.json for every waystone of a zone.

Each stone binds with "Lay your hand on the stone" (flag waystone.<id>) and offers the trip to every
other stone for the fare. Run it after adding or renaming a waystone NPC in the zone file:

    python tools/waystones.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZONE = ROOT / "world" / "zones" / "meadow-valley.json"
FARE = 15
TEXT = ("An Auren waystone, cold and humming. Azure runes run up its faces, and beneath them the names of "
        "other stones glow faintly, those you have touched brightest.")


def dialogue(stone, stones):
    bound = f"waystone.{stone['id']}"
    options = [{"text": "Lay your hand on the stone.", "when": {"notFlag": bound},
                "effects": [{"setFlag": bound}], "next": "stone"}]
    for other in stones:
        if other["id"] == stone["id"]:
            continue
        options.append({
            "text": f"Travel to the {other['name']} ({FARE} gold)",
            "when": {"all": [{"flag": bound}, {"flag": f"waystone.{other['id']}"}, {"gold": {"min": FARE}}]},
            "effects": [{"gold": -FARE}, {"action": f"travel:{other['id']}"}],
        })
    options.append({"text": "Leave"})
    return {"npc": stone["id"], "entries": [{"node": "stone"}], "nodes": {"stone": {"text": TEXT, "options": options}}}


def main():
    zone = json.loads(ZONE.read_text(encoding="utf-8"))
    stones = [npc for npc in zone["npcs"] if npc.get("role") == "waystone"]
    for stone in stones:
        path = ROOT / "world" / "dialogue" / f"{stone['id']}.json"
        path.write_text(json.dumps(dialogue(stone, stones), indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(stones)} waystones: " + ", ".join(stone["name"] for stone in stones))


if __name__ == "__main__":
    main()
