"""Append original dictionary entries; preserve existing stable keys and recordings."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = json.loads(Path(__file__).with_name("vocabulary-foundations.json").read_text(encoding="utf-8"))
PATH = ROOT / "backend/LanguageLearning.Infrastructure/Vocabulary/Starter/words.json"
packs = json.loads(PATH.read_text(encoding="utf-8"))
for pack in packs:
    existing = {item["key"] for item in pack["items"]}
    spellings = {item["word"].casefold() for item in pack["items"]}
    added = 0
    for index, (word, example) in enumerate(DATA[pack["code"]]):
        key = DATA["keys"][index]
        if key in existing or word.casefold() in spellings:
            continue
        pack["items"].append({
            "key": key, "word": word, "example": example,
            "englishTranslation": DATA["english"][index], "partOfSpeech": "noun",
            "topic": DATA["topics"][index],
        })
        added += 1
    print(f"{pack['code']}: added {added} entries")
PATH.write_text(json.dumps(packs, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
