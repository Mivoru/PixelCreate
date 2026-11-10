"""Generate the spoken lines of the people of the world with the ElevenLabs text-to-speech API.

Usage: python asset-pipeline/generate_voices.py [--dry] [--force] [--voices] [--model <id>] [npc ...]

A person speaks once world/people.json gives them a "voice": { "id": "<ElevenLabs voice id>", ... }.
Their lines are the texts of their dialogue nodes, what they say about others, their passing lines, and
the rumours they tell.
Each line becomes client/assets/voices/<npc>/<hash of the text>.mp3, which the client finds by the same
hash, so a line that changes simply has no recording until this runs again. Existing files are kept: a
run only pays for lines that are new, changed, or whose voice or model changed (see manifest.json).

The recordings are not in git (the folder is ignored): run this to make them. --dry counts characters
without calling the API, --voices lists the voices the account can use. The key is found as in
generate_sounds.py and never goes into this repository.
"""
import hashlib
import json
import sys
from pathlib import Path

from generate_sounds import find_key, request

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "client" / "assets" / "voices"
MANIFEST = OUT / "manifest.json"
DEFAULT_MODEL = "eleven_multilingual_v2"


def line_key(text):
    """The file name of a line: the client computes the same (Voices.Key)."""
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:16]


def spoken(text):
    """Whether a line can be recorded: no {gold} to fill in, and not a stage direction in brackets."""
    text = text.strip()
    return bool(text) and "{" not in text and not (text.startswith("(") and text.endswith(")"))


def lines_of(npc, person):
    lines = []
    dialogue = ROOT / "world" / "dialogue" / f"{npc}.json"
    if dialogue.exists():
        nodes = json.loads(dialogue.read_text(encoding="utf-8"))["nodes"]
        lines += [node["text"] for node in nodes.values()]
    lines += list(person.get("about", {}).values())
    lines += [bark["text"] for bark in person.get("barks", [])]
    # Rumours: their own telling of it, or the common one when they are named as a teller.
    for rumor in json.loads((ROOT / "world" / "rumors.json").read_text(encoding="utf-8")):
        if npc in rumor.get("by", {}):
            lines.append(rumor["by"][npc])
        elif npc in rumor.get("from", []):
            lines.append(rumor["text"])
    seen = set()
    return [line for line in lines if spoken(line) and not (line in seen or seen.add(line))]


def main():
    arguments = sys.argv[1:]
    dry = "--dry" in arguments
    force = "--force" in arguments
    model = arguments[arguments.index("--model") + 1] if "--model" in arguments else DEFAULT_MODEL
    names = [a for i, a in enumerate(arguments) if not a.startswith("--") and (i == 0 or arguments[i - 1] != "--model")]

    if "--voices" in arguments:
        for voice in json.load(request(find_key(), "/voices"))["voices"]:
            labels = voice.get("labels") or {}
            print(f'{voice["voice_id"]}  {voice["name"]:<28} {labels.get("gender", ""):<7} {labels.get("age", ""):<12} '
                  f'{labels.get("accent", ""):<12} {labels.get("description", "")}')
        return

    people = json.loads((ROOT / "world" / "people.json").read_text(encoding="utf-8"))
    speakers = {npc: person for npc, person in people.items() if "voice" in person and (not names or npc in names)}
    if not speakers:
        raise SystemExit("Nobody to record: give someone a \"voice\" in world/people.json, or check the names.")

    OUT.mkdir(parents=True, exist_ok=True)
    # Godot must not import these: the client reads the files itself, and they are not in git.
    (OUT / ".gdignore").touch()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8")) if MANIFEST.exists() else {}
    todo = []
    for npc, person in speakers.items():
        voice = person["voice"]
        for text in lines_of(npc, person):
            name = f"{npc}/{line_key(text)}.mp3"
            made = manifest.get(name)
            current = made is not None and made["voice"] == voice and made["model"] == model and (OUT / name).exists()
            if force or not current:
                todo.append((npc, name, text, voice))

    characters = sum(len(text) for _, _, text, _ in todo)
    print(f"{len(todo)} lines to record, {characters} characters, model {model}")
    for npc in speakers:
        mine = [text for who, _, text, _ in todo if who == npc]
        print(f"  {npc}: {len(mine)} lines, {sum(map(len, mine))} characters")
    if dry or not todo:
        return

    key = find_key()
    for count, (npc, name, text, voice) in enumerate(todo, 1):
        settings = {k: v for k, v in voice.items() if k in ("stability", "similarity_boost", "style", "speed", "use_speaker_boost")}
        body = {"text": text, "model_id": model}
        if settings:
            body["voice_settings"] = settings
        audio = request(key, f"/text-to-speech/{voice['id']}?output_format=mp3_44100_128", body).read()
        (OUT / npc).mkdir(exist_ok=True)
        (OUT / name).write_bytes(audio)
        manifest[name] = {"voice": voice, "model": model, "text": text}
        MANIFEST.write_text(json.dumps(manifest, indent=1, ensure_ascii=False), encoding="utf-8")
        print(f"[{count}/{len(todo)}] {name}  {text[:60]}")


if __name__ == "__main__":
    main()
