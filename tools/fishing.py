"""Writes world/dialogue/<id>.json for every fishing spot of a zone: cast a line, and a fish bites a few seconds later.

A fishing spot is an NPC of role "fishing" in the zone file (no figure; at a pier's water end), with an optional
`note`. "Cast a line." runs the server action fish (GameServer.StepFishing): the player must stay still until the
bite, and gets a perch, a trout or now and then a mirefin. Run it after adding a spot:

    python tools/fishing.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZONE = ROOT / "world" / "zones" / "meadow-valley.json"

DEFAULT_NOTE = "Dark water below the boards. Now and then something turns down there and the surface shivers."


def dialogue(spot):
    return {
        "npc": spot["id"],
        "entries": [{"node": "water"}],
        "nodes": {
            "water": {
                "text": spot.get("note", DEFAULT_NOTE) + " Cast a line and keep still until something bites.",
                "options": [
                    {"text": "Cast a line.", "effects": [{"action": "fish"}]},
                    {"text": "Leave it."},
                ],
            },
        },
    }


def main():
    zone = json.loads(ZONE.read_text(encoding="utf-8"))
    spots = [npc for npc in zone["npcs"] if npc.get("role") == "fishing"]
    for spot in spots:
        path = ROOT / "world" / "dialogue" / f"{spot['id']}.json"
        path.write_text(json.dumps(dialogue(spot), indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(spots)} fishing spots: " + ", ".join(spot["id"] for spot in spots))


if __name__ == "__main__":
    main()
