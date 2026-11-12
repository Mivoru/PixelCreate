"""Writes world/dialogue/<id>.json for every hidden cache of a zone: an old chest the player finds and opens once.

A cache is an NPC of role "cache" in the zone file with a `reward` ({"gold": n, "items": {"id": count}}) and a
`note` (what the chest looks like). One with `requires` (an item id) and `locked` (what the player reads without
it) opens only with that key, which it takes. Opening it gives the reward and sets the flag cache.<id>; after that it is
empty. Run it after adding or changing a cache:

    python tools/caches.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ZONE = ROOT / "world" / "zones" / "meadow-valley.json"


def dialogue(cache):
    opened = f"cache.{cache['id']}"
    reward = cache.get("reward", {})
    effects = [{"setFlag": opened}]
    if reward.get("gold"):
        effects.append({"gold": reward["gold"]})
    for item, count in reward.get("items", {}).items():
        effects.append({"give": {"item": item, "count": count}})
    if reward.get("xp"):
        effects.append({"xp": reward["xp"]})
    entries = [{"when": {"flag": opened}, "node": "empty"}]
    extra = {}
    if cache.get("requires"):
        # A locked chest: it opens only with its key, which it keeps.
        key = cache["requires"]
        entries.append({"when": {"not": {"item": {"id": key}}}, "node": "locked"})
        effects.insert(0, {"take": {"item": key, "count": 1}})
        extra["locked"] = {"text": cache["locked"], "options": [{"text": "Leave it."}]}
    entries.append({"node": "closed"})
    return {
        "npc": cache["id"],
        "entries": entries,
        "nodes": {
            **extra,
            "closed": {"text": cache["note"], "options": [
                {"text": "Open it.", "effects": effects, "next": "found"},
                {"text": "Leave it."}]},
            "found": {"text": "Inside, wrapped in oilcloth: " + cache["found"], "options": [{"text": "Take it all."}]},
            "empty": {"text": "The chest stands open and empty. Someone got here first. You did.", "options": [{"text": "Leave"}]},
        },
    }


def main():
    zone = json.loads(ZONE.read_text(encoding="utf-8"))
    caches = [npc for npc in zone["npcs"] if npc.get("role") == "cache"]
    for cache in caches:
        path = ROOT / "world" / "dialogue" / f"{cache['id']}.json"
        path.write_text(json.dumps(dialogue(cache), indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(caches)} caches: " + ", ".join(cache["name"] for cache in caches))


if __name__ == "__main__":
    main()
