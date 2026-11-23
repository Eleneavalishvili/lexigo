"""Prepare attributed public speech clips for local recognition tests, never course publishing."""

import hashlib
import json
from pathlib import Path
import random
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import wave

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "work/speech-test-python"))
import numpy as np
import soundfile

OUTPUT = (ROOT / "work/speech-human").resolve()
if not OUTPUT.is_relative_to(ROOT):
    raise ValueError("Fixture directory must stay inside the workspace.")
OUTPUT.mkdir(parents=True, exist_ok=True)
SOURCES = json.loads((ROOT / "config/speech-test-fixtures.json").read_text(encoding="utf-8"))
HEADERS = {"User-Agent": "LexiGoSpeechResearch/1.0 (local academic evaluation)"}


def fetch(url, maximum_bytes):
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(request, timeout=30) as response:
                data = response.read(maximum_bytes + 1)
            if len(data) > maximum_bytes:
                raise ValueError("Fixture download exceeded its bound.")
            return data
        except urllib.error.HTTPError as error:
            if error.code != 429 or attempt == 2:
                raise
            wait = error.headers.get("Retry-After", "5")
            time.sleep(max(2, min(60, int(wait) if wait.isdigit() else 5)))
    raise RuntimeError("Fixture download did not complete.")


def output_path(name):
    path = OUTPUT / name
    if path.is_symlink() or not path.resolve().is_relative_to(OUTPUT):
        raise ValueError("Fixture setup does not follow output links.")
    return path


def write_wave(path, samples):
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(16000)
        output.writeframes(samples)


titles = "|".join("File:" + source["file"] for source in SOURCES)
query = urllib.parse.urlencode({
    "action": "query", "format": "json", "prop": "imageinfo",
    "iiprop": "url", "titles": titles,
})
metadata = json.loads(fetch("https://commons.wikimedia.org/w/api.php?" + query, 200000))
urls = {page["title"][5:]: page["imageinfo"][0]["url"] for page in metadata["query"]["pages"].values()}
cases = {}
receipts = []
for source in SOURCES:
    language = source["language"]
    if language not in {"es", "it", "de", "ru"}:
        raise ValueError("Unexpected fixture language.")
    name = language + ("-wrong" if source["negative"] else "")
    ogg = output_path(name + ".ogg")
    if not ogg.exists():
        url = urls[source["file"]]
        if urllib.parse.urlparse(url).hostname != "upload.wikimedia.org":
            raise ValueError("Unexpected public fixture host.")
        time.sleep(2)
        data = fetch(url, 1024 * 1024)
        if hashlib.sha256(data).hexdigest() != source["sha256"]:
            raise ValueError("Fixture changed; review provenance before using it.")
        ogg.write_bytes(data)
    if hashlib.sha256(ogg.read_bytes()).hexdigest() != source["sha256"]:
        raise ValueError("Cached fixture integrity check failed.")
    samples, rate = soundfile.read(ogg, dtype="int16", always_2d=True)
    mono = samples.astype(np.float64).mean(axis=1)
    # Linear resampling is test preparation only; browser capture uses Web Audio resampling.
    positions = np.arange(int(len(mono) * 16000 / rate)) * rate / 16000
    pcm = np.rint(np.interp(positions, np.arange(len(mono)), mono)).astype("<i2").tobytes()
    wav = output_path(name + "-16k.wav")
    write_wave(wav, pcm)
    receipts.append({**source, "convertedSha256": hashlib.sha256(wav.read_bytes()).hexdigest()})
    if not source["negative"]:
        cases[language] = source["expected"]

noise = random.Random(28102026)
for language in ("es", "it", "de", "ru"):
    samples = np.array([noise.randint(-3000, 3000) for _ in range(16000)], dtype="<i2")
    write_wave(output_path(language + "-noise-16k.wav"), samples.tobytes())
output_path("cases.json").write_text(json.dumps(cases, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
output_path("SOURCES.json").write_text(json.dumps(receipts, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Prepared four positive clips, four different-word clips and four noise checks with attribution.")
