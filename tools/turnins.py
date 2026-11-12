"""Gives Mira a hand-in for every job on the Guild board, in world/dialogue/mira.json.

For each quest of kind "job" it adds an entry (when the job is ready, open turn_in_<id>) before Mira's other
entries that come after registering, and a node that pays the job and offers the crystal sale. Run it after adding
a job:

    python tools/turnins.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MIRA = ROOT / "world" / "dialogue" / "mira.json"
QUESTS = ROOT / "world" / "quests.json"


def main():
    mira = json.loads(MIRA.read_text(encoding="utf-8"))
    data = json.loads(QUESTS.read_text(encoding="utf-8"))
    quests = data if isinstance(data, list) else data["quests"]
    jobs = [q for q in quests if q.get("kind") == "job"]
    entries = mira["entries"]
    added = 0
    for job in jobs:
        node = f"turn_in_{job['id']}"
        if node not in mira["nodes"]:
            mira["nodes"][node] = {
                "text": f"Well done! Here is your pay for {job['name']}: {job['gold']} gold.",
                "face": "smile",
                "options": [
                    {"text": "Hand in the job", "effects": [{"quest": {"complete": job["id"]}}]},
                    {"text": "Sell all crystals ({crystalWorth} gold)", "effects": [{"action": "sellCrystals"}]},
                    {"text": "Goodbye"},
                ],
            }
        if not any(e.get("node") == node for e in entries):
            # Hand-ins go right after the existing ones, before the trial offer and the default.
            last = max((i for i, e in enumerate(entries) if e.get("node", "").startswith("turn_in_")), default=0)
            entries.insert(last + 1, {"when": {"quest": {job["id"]: "ready"}}, "node": node})
            added += 1
    MIRA.write_text(json.dumps(mira, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(jobs)} jobs, {added} hand-ins added")


if __name__ == "__main__":
    main()
