"""Teach and assess all 33 written Russian letters; not a graded listening claim."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
letters = json.loads((ROOT / "frontend/public/teaching/russian-alphabet.json").read_text(encoding="utf-8"))
path = ROOT / "backend/LanguageLearning.Infrastructure/Content/Starter/ru.json"
pack = json.loads(path.read_text(encoding="utf-8"))
lessons = []
previous = "cyrillic-first-letters"
for group in range(3):
    rows = letters[group * 11:(group + 1) * 11]
    questions = []
    notes = []
    for index, row in enumerate(rows):
        notes.append(f"{row['upper']} {row['lower']} — letter name: {row['name']}; example: {row['example']} ({row['meaning']})")
        others = [candidate for candidate in letters if candidate["lower"] != row["lower"]]
        distractors = [others[(group * 11 + index + offset) % len(others)]["lower"] for offset in (0, 7, 15)]
        for prompt in (f"Choose the lowercase form of {row['upper']}.", f"Which letter has the Russian name «{row['name']}»?"):
            questions.append({"type": "Alphabet", "prompt": prompt, "answers": [row["lower"]],
                              "options": [row["lower"], *distractors], "contextText": None,
                              "explanation": f"{row['upper']} {row['lower']} is named {row['name']}. Example: {row['example']} ({row['meaning']})."})
    key = f"cyrillic-written-{group + 1}"
    lessons.append({"key": key, "title": f"Read the Russian alphabet · Part {group + 1}",
                    "objective": "Match capital and lowercase Cyrillic letters and recognize their written names.",
                    "explanation": "Your new letters\n\n" + "\n".join(notes) + "\n\nThese are letter names, not a claim that each letter has one fixed sound. Ь and Ъ are signs; they have no independent vowel sound. Russian spelling stays Cyrillic, and Е and Ё are distinct. Read the examples before checking your recognition.",
                    "exercises": questions, "unitKey": "cyrillic-basics", "unitTitle": "Cyrillic basics",
                    "unitDescription": "Recognize Russian letters and connect reviewed recordings with written forms.",
                    "difficulty": "A1", "contentVersion": 1, "prerequisiteKeys": [previous]})
    previous = key
lessons.append({"key": "check-cyrillic-written", "title": "Unit check · Written Cyrillic",
                "objective": "Review letter shapes and names across the complete Russian alphabet.",
                "explanation": "Complete all three written-letter lessons before this check. Graded sound associations are a separate activity.",
                "exercises": [question.copy() for lesson in lessons for question in lesson["exercises"][::5]],
                "unitKey": "cyrillic-basics", "unitTitle": "Cyrillic basics", "difficulty": "A1",
                "contentVersion": 1, "isCheckpoint": True,
                "prerequisiteKeys": [lesson["key"] for lesson in lessons]})
existing = {lesson["key"] for lesson in pack["lessons"]}
pack["lessons"].extend(lesson for lesson in lessons if lesson["key"] not in existing)
path.write_text(json.dumps(pack, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Russian written recognition: 33 letters, 66 teaching questions and 15 review questions.")
