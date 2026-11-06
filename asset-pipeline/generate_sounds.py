"""Generate the game sound effects with the ElevenLabs Sound Effects API.

Usage: python asset-pipeline/generate_sounds.py [--force] [name ...]

Existing files are kept, so a run only pays for what is missing. Pass names, or --force, to redo them.
The API key comes from ELEVENLABS_API_KEY, or from the first key with credits left in the file named
by ELEVENLABS_KEY_FILE. Keys never go into this repository.
"""
import json
import os
import re
import subprocess
import sys
import urllib.request
from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "client" / "assets" / "sounds"

# Paid-for originals stay here, so changing the trimming or level costs no credits.
CACHE = Path(__file__).resolve().parent / ".sound-cache"
API = "https://api.elevenlabs.io/v1"
DEFAULT_KEY_FILE = r"D:\AIvidea\ElevenLabsTextToSpeach&SoundEfektsAPI\apiElevenLabs.txt"
MIN_CREDITS = 1500

# name: (prompt, seconds asked for, seconds kept, level in dB relative to full loudness)
SOUNDS = {
    "step_1": ("single footstep of a leather boot on soft grass, close, dry, one step only", 0.5, 0.3, -12),
    "step_2": ("one soft footstep on grass and dirt, leather boot, short, no reverb", 0.5, 0.3, -12),
    "step_3": ("one light boot step on a meadow, grass rustle, short and close", 0.5, 0.3, -12),
    "step_4": ("single footfall on earth with a little grass crunch, close, dry", 0.5, 0.3, -12),
    "dash": ("fast air whoosh of a quick dodge dash, short wind rush, video game", 0.6, 0.45, -6),
    "jump": ("short light jump effort with cloth rustle, video game character, no voice", 0.5, 0.3, -9),
    "land": ("boots landing on soft ground, short thud with a little dust", 0.5, 0.35, -8),
    "swing_light": ("quick light wooden sword swing, thin fast whoosh, video game", 0.5, 0.3, -7),
    "swing_heavy": ("slow powerful wooden sword swing, deep heavy whoosh, video game", 0.8, 0.6, -5),
    "hit": ("wooden sword hitting wet jelly, short slap and squish, video game impact", 0.5, 0.3, -5),
    "hit_heavy": ("heavy wooden club smashing into thick wet jelly, deep punchy splat, video game", 0.7, 0.5, -3),
    "hurt": ("short male grunt of pain, fantasy game character taking a hit", 0.5, 0.4, -6),
    "dodge": ("bright quick magical shimmer whoosh, successful dodge, video game", 0.5, 0.3, -11),
    "level_up": ("triumphant fantasy RPG level up jingle, bright magical chime rising with sparkle", 2.0, 2.0, -5),
    "slime_hop": ("small wet slime bounce, soft gooey boing, cartoon, short", 0.5, 0.3, -13),
    "slime_charge": ("wet gooey slime squeezing and building up pressure, rising squelch", 0.7, 0.6, -9),
    "slime_spit": ("wet slime spitting a glob of goo, quick wet ptooey projectile, video game", 0.5, 0.35, -7),
    "slime_hit": ("gooey slime creature hurt, wet squish with a tiny squeak, video game", 0.5, 0.3, -7),
    "slime_alert": ("small slime creature noticing the player, short wet burble, cute, video game", 0.5, 0.35, -10),
    "slime_splat": ("big slime blob slamming the ground and splattering, heavy wet splat with droplets", 0.8, 0.7, -4),
    "spit_splash": ("glob of slime bursting on impact, wet splash with small droplets", 0.5, 0.4, -6),
    "slime_die": ("slime monster bursting and melting away, long wet squelch collapsing into a puddle", 1.0, 0.9, -5),
}


def request(key, path, body=None):
    headers = {"xi-api-key": key}
    data = None
    if body is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(body).encode()
    return urllib.request.urlopen(urllib.request.Request(f"{API}{path}", data=data, headers=headers), timeout=120)


def find_key():
    key = os.environ.get("ELEVENLABS_API_KEY")
    if key:
        return key

    text = Path(os.environ.get("ELEVENLABS_KEY_FILE", DEFAULT_KEY_FILE)).read_text(encoding="utf-8", errors="ignore")
    for candidate in re.findall(r"sk_[A-Za-z0-9]{20,}", text):
        account = json.load(request(candidate, "/user/subscription"))
        if account["character_limit"] - account["character_count"] >= MIN_CREDITS:
            return candidate

    raise SystemExit("No ElevenLabs key has enough credits left.")


def download(key, name, prompt, seconds):
    body = {"text": prompt, "duration_seconds": seconds, "prompt_influence": 0.6}
    audio = request(key, "/sound-generation?output_format=mp3_44100_128", body).read()
    (CACHE / f"{name}.mp3").write_bytes(audio)


def convert(name, keep, level):
    """Removes silence at the start, keeps the useful part, fades the end and sets the level."""
    filters = (f"silenceremove=start_periods=1:start_threshold=-45dB,atrim=0:{keep},"
               f"afade=t=out:st={max(keep - 0.05, 0)}:d=0.05,loudnorm=I=-14:TP=-1:LRA=11,"
               f"volume={level + 5}dB,alimiter=limit=0.95:level=false")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(CACHE / f"{name}.mp3"), "-af", filters,
                    "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", str(OUT / f"{name}.wav")], check=True)


def main():
    arguments = sys.argv[1:]
    force = "--force" in arguments
    names = [a for a in arguments if a != "--force"]
    OUT.mkdir(parents=True, exist_ok=True)
    CACHE.mkdir(exist_ok=True)
    wanted = {n: SOUNDS[n] for n in names} if names else SOUNDS
    missing = {n: s for n, s in wanted.items() if force or names or not (CACHE / f"{n}.mp3").exists()}
    key = find_key() if missing else None
    for name, (prompt, seconds, _, _) in missing.items():
        download(key, name, prompt, seconds)
        print(f"generated {name}")

    for name, (_, _, keep, level) in wanted.items():
        convert(name, keep, level)


main()
