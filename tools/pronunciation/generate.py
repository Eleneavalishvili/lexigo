"""Build public starter pronunciation clips locally; never upload learner data."""
import hashlib
import json
import re
import unicodedata
import urllib.request
import wave
from pathlib import Path

from piper import PiperVoice

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "config/voice-models.json"
MODELS = ROOT / "work/tts-models"
OUTPUT = ROOT / "frontend/public/pronunciation"


def canonical(text):
    return " ".join(unicodedata.normalize("NFC", text).strip().lower().split()).strip(".!?¡¿")


def download(url, target, checksum=None):
    if target.exists() and (checksum is None or hashlib.sha256(target.read_bytes()).hexdigest() == checksum):
        return
    temporary = target.with_suffix(target.suffix + ".download")
    urllib.request.urlretrieve(url, temporary)
    if checksum and hashlib.sha256(temporary.read_bytes()).hexdigest() != checksum:
        raise ValueError("Voice model checksum does not match the official pinned file.")
    temporary.replace(target)


def collect():
    texts = {language: {} for language in ["en", "es", "it", "de", "ru"]}

    def add(language, text):
        if language == "ru" and canonical(text) in {"ъ", "ь"}:
            return  # These signs have names but no independent sound.
        if text and len(text) <= 1000 and re.search(r"\w", text, flags=re.UNICODE):
            texts[language].setdefault(canonical(text), text)
            if language != "en":
                for word in re.findall(r"[\w’']+", text, flags=re.UNICODE):
                    texts[language].setdefault(canonical(word), word)

    for path in (ROOT / "backend/LanguageLearning.Infrastructure/Content/Starter").glob("*.json"):
        pack = json.loads(path.read_text(encoding="utf-8"))
        language = pack["code"]
        for lesson in pack["lessons"]:
            for question in lesson["exercises"]:
                prompt = question["prompt"]
                add("en", prompt.replace("____", "blank"))
                response_language = language
                if question["type"] in ("Translation", "SentenceBuilder") and ("into English:" in prompt or "in English:" in prompt):
                    response_language = "en"
                if question["type"] == "Reading":
                    add(language, question["contextText"])
                    if (not prompt.startswith("Which sentence means")
                            and prompt != "Which greeting does the second speaker use?"
                            and prompt != "What is the speaker’s name?"):
                        response_language = "en"
                for text in question["options"] + question["answers"]:
                    add(response_language, text)
                # New vocabulary is deliberately public teaching material, not an exercise answer-key map.
            for line in lesson["explanation"].splitlines():
                if " — " in line:
                    target, english = line.split(" — ", 1)
                    add(language, target)
                    add("en", english)
    for language, greeting in {"es": "Hola, mundo.", "it": "Ciao, mondo.", "de": "Hallo, Welt.", "ru": "Привет, мир."}.items():
        add(language, greeting)
    vocabulary = ROOT / "backend/LanguageLearning.Infrastructure/Vocabulary/Starter/words.json"
    if vocabulary.exists():
        for pack in json.loads(vocabulary.read_text(encoding="utf-8")):
            for word in pack["items"]:
                add(pack["code"], word["word"])
                add(pack["code"], word["example"])
                add("en", word["englishTranslation"])
    alphabet = ROOT / "frontend/public/teaching/russian-alphabet.json"
    if alphabet.exists():
        for letter in json.loads(alphabet.read_text(encoding="utf-8")):
            add("ru", letter["audioText"])
            add("ru", letter["example"])
    for language, text in dialogue_lines():
        add(language, text)
    return texts


def dialogue_lines():
    content = ROOT / "backend/LanguageLearning.Infrastructure/Content"
    literal = r'"(?:[^"\\]|\\.)*"'
    scenario = r'new\(("(?:cafe|hotel|tickets|directions|lost-and-found)"),'
    for code in ("es", "it", "de", "ru"):
        source = (content / f"StarterDialogues.{code.title()}.cs").read_text(encoding="utf-8")
        sections = re.split(scenario, source)
        if len(sections) != 11:
            raise ValueError(f"Expected five starter dialogues for {code}")
        for body in sections[2::2]:
            values = [json.loads(value) for value in re.findall(literal, body)]
            if len(values) != 11:
                raise ValueError(f"Unexpected starter dialogue layout for {code}")
            # Skip the English title and coaching note.
            for line in values[1:-1]:
                yield code, line
    installer = (content / "StarterDialogueInstaller.cs").read_text(encoding="utf-8")
    corrections = re.findall(rf'"(es|it|de|ru)"\s*=>\s*\(({literal}),\s*({literal})\)', installer)
    if len(corrections) != 4:
        raise ValueError("Expected four localized dialogue corrections")
    for code, response, ending in corrections:
        yield code, json.loads(response)
        yield code, json.loads(ending)


def main():
    settings = json.loads(CONFIG.read_text(encoding="utf-8"))
    MODELS.mkdir(parents=True, exist_ok=True)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    manifest = {"version": 1, "source": "Locally generated synthetic pronunciation", "clips": {}}
    for language, texts in collect().items():
        model = settings["voices"][language]
        filename = model["name"] + ".onnx"
        base = "https://huggingface.co/rhasspy/piper-voices/resolve/" + settings["revision"] + "/" + model["directory"] + "/"
        download(base + filename, MODELS / filename, model["sha256"])
        download(base + filename + ".json", MODELS / (filename + ".json"), model["configSha256"])
        voice = PiperVoice.load(str(MODELS / filename))
        for key, text in texts.items():
            stem = hashlib.sha256((language + "\n" + key).encode("utf-8")).hexdigest()[:24]
            target = OUTPUT / (stem + ".wav")
            if not target.exists():
                temporary_clip = target.with_suffix(".wav.tmp")
                try:
                    with wave.open(str(temporary_clip), "wb") as recording:
                        voice.synthesize_wav(text, recording)
                    with wave.open(str(temporary_clip), "rb") as recording:
                        if recording.getnframes() == 0:
                            raise ValueError(f"No audio generated for {language}: {text!r}")
                    temporary_clip.replace(target)
                finally:
                    temporary_clip.unlink(missing_ok=True)
            with wave.open(str(target), "rb") as recording:
                if recording.getnframes() == 0:
                    raise ValueError("A pronunciation clip is empty.")
            manifest["clips"][language + "|" + key] = {"file": stem + ".wav", "text": text, "language": language}
        print(language + ": " + str(len(texts)) + " pronunciation clips ready", flush=True)
    temporary = OUTPUT / "manifest.json.tmp"
    temporary.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(OUTPUT / "manifest.json")
    print("Ready: " + str(len(manifest["clips"])) + " real WAV clips", flush=True)


if __name__ == "__main__":
    main()
