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
DEFAULT_KEY_FILE = r"D:\AIvidea\ElevenLabsTextToSpeach&SoundEfektsAPI\apiElevenLabs.old.txt"
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
    "hit": ("soft low dull thud of a wooden stick hitting thick mud, deep muffled, warm, no high frequencies", 0.5, 0.3, -9),
    "hit_heavy": ("deep muffled heavy thump into thick mud, low bass impact, warm, no high frequencies", 0.7, 0.5, -7),
    "hurt": ("short male grunt of pain, fantasy game character taking a hit", 0.5, 0.4, -6),
    "dodge": ("soft low airy whoosh, gentle, warm, no high frequencies", 0.5, 0.3, -12),
    "level_up": ("triumphant fantasy RPG level up jingle, bright magical chime rising with sparkle", 2.0, 2.0, -5),
    "slime_hop": ("low soft wet blob bounce, deep gooey plop, muffled", 0.5, 0.3, -13),
    "slime_charge": ("wet gooey slime squeezing and building up pressure, rising squelch", 0.7, 0.6, -9),
    "slime_spit": ("low wet glob launched, deep soft gooey pop, muffled", 0.5, 0.35, -10),
    "slime_hit": ("low deep wet squish, thick mud being pressed, soft and muffled, no squeak, no high frequencies", 0.5, 0.3, -10),
    "slime_alert": ("low soft wet gurgle, deep bubbling mud, short", 0.5, 0.35, -12),
    "slime_splat": ("big heavy mud blob falling on the ground, deep low wet thump, muffled, no high frequencies", 0.8, 0.7, -7),
    "spit_splash": ("low soft wet mud splat, deep and muffled, no high frequencies", 0.5, 0.4, -9),
    "slime_die": ("thick mud slowly collapsing with a deep low wet sigh, soft bubbling, muffled, no high frequencies", 1.0, 0.9, -8),
    "goblin_alert": ("short low gruff goblin growl, fantasy creature, no words", 0.6, 0.5, -9),
    "goblin_hit": ("short low goblin grunt of pain, fantasy creature, no words", 0.5, 0.35, -9),
    "goblin_die": ("goblin dying with a low croaking groan, fantasy creature, no words", 0.9, 0.8, -8),
    "goblin_swing": ("quick low whoosh of a small blade swing, soft", 0.5, 0.3, -10),
    "club_slam": ("huge wooden club slamming the ground, deep heavy boom with low rumble", 0.8, 0.7, -5),
    "club_spin": ("heavy wooden club spinning through the air, deep low whoosh", 0.9, 0.8, -7),
    "bow_shot": ("wooden bow releasing an arrow, soft low twang and thump", 0.5, 0.35, -9),
    "bow_power": ("powerful longbow shot, deep heavy twang and fast low whoosh", 0.7, 0.6, -6),
    "arrow_hit": ("arrow thudding into wood, short low dull thunk", 0.5, 0.25, -9),
    "fireball": ("fireball launched, low whoosh of flames, warm roar", 0.7, 0.6, -7),
    "fire_wall": ("wall of fire rushing forward, deep roaring flames sweeping, low rumble", 1.5, 1.4, -6),
    "explosion": ("small fiery explosion, deep muffled boom, low rumble", 0.8, 0.7, -6),
    "crystal": ("soft warm glass chime, one gentle low note, calm, magical", 0.6, 0.5, -11),
    "cow_moo": ("a single cow mooing once in a quiet meadow, long low moo, outdoors, no other animals", 2.0, 1.7, -11),
    "cow_hurt": ("a cow bellowing once in alarm and pain, short distressed moo, outdoors", 1.0, 0.8, -8),
    "sheep_baa": ("a single sheep bleating once, soft baa, outdoors, close", 1.2, 1.0, -12),
    "sheep_hurt": ("a sheep crying out once in fright, sharp short bleat", 0.8, 0.6, -9),
    "goat_bleat": ("a single goat bleating once, nasal meh, outdoors, close", 1.0, 0.9, -12),
    "goat_hurt": ("a goat crying out once in fear, short sharp bleat", 0.8, 0.6, -9),
    "pig_grunt": ("a wild boar grunting and snuffling softly while rooting in the ground, close, calm", 1.2, 1.0, -12),
    "pig_angry": ("an angry wild boar snorting and grunting aggressively, about to charge", 1.0, 0.9, -7),
    "pig_hurt": ("a wild boar squealing once in pain, short loud squeal", 0.8, 0.6, -8),
    "chicken_cluck": ("a hen clucking softly, a few calm clucks, close, no rooster", 1.2, 1.0, -14),
    "chicken_squawk": ("a chicken squawking in panic with flapping wings, short", 1.0, 0.8, -9),
    "deer_alarm": ("a red deer giving one short sharp alarm bark, outdoors in a meadow", 0.8, 0.6, -10),
    "orc_hit": ("a big brutish orc grunting in pain, deep guttural male growl, one short grunt", 0.6, 0.5, -7),
    "orc_die": ("a huge orc warrior dying, deep guttural groan fading out", 1.2, 1.0, -6),
    "orc_alert": ("a deep orc war cry, guttural male roar of challenge, short", 1.2, 1.0, -6),
    "orc_f_hit": ("a fierce female orc warrior grunting in pain, rough low voice, short", 0.6, 0.5, -7),
    "orc_f_die": ("a fierce female orc warrior crying out as she falls, rough low voice", 1.0, 0.9, -6),
    "orc_f_alert": ("a fierce female warrior battle cry, rough and loud, short", 1.0, 0.9, -6),
    "orc_swing": ("a heavy iron weapon swung hard, deep whoosh, powerful", 0.6, 0.5, -6),
    "air_slash": ("a sharp blade of wind cutting through the air, fast rising whoosh with a ringing steel edge", 0.9, 0.7, -6),
    "spear_throw": ("a heavy spear thrown hard, fast whistling whoosh through the air", 0.7, 0.5, -7),
    "earth_crack": ("a huge axe smashing into the ground, the earth cracking and splitting apart, rocks breaking, deep rumble", 1.5, 1.2, -5),
    "werewolf_roar": ("a huge werewolf roaring, deep monstrous snarling roar, terrifying, short", 1.5, 1.3, -5),
    "werewolf_hit": ("a monstrous beast snarling in pain, deep wolf growl, one short burst", 0.6, 0.5, -6),
    "werewolf_die": ("a giant werewolf dying, a howl that breaks into a whimper, deep", 2.0, 1.8, -6),
    "werewolf_swipe": ("huge claws tearing through the air, fast vicious swipe with a snarl", 0.6, 0.5, -6),
    "werewolf_howl": ("a werewolf howling at the full moon, huge deep eerie howl echoing over a dark village", 3.5, 3.2, -9),
    "wolf_howl": ("a lone wolf howling in a cold forest at night, long rising mournful howl, distant echo", 3.0, 2.6, -11),
    "wolf_growl": ("a wolf snarling and growling low and menacing, teeth bared, about to attack, close", 1.2, 1.0, -7),
    "wolf_hurt": ("a wolf yelping sharply in pain, one short high yelp", 0.6, 0.5, -8),
    "deer_hurt": ("a deer bleating once in pain, short high cry", 0.8, 0.6, -9),
    "hooves": ("loud close hoofbeats of several deer galloping fast over hard dry ground, clear thudding gallop", 1.5, 1.3, -12),
    "animal_fall": ("a large animal body falling onto soft grass, one heavy muffled thud", 0.8, 0.6, -9),
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
    filters = (f"silenceremove=start_periods=1:start_threshold=-45dB,lowpass=f=4800,lowpass=f=7000,atrim=0:{keep},"
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
