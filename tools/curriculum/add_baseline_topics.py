"""Add original PDF page 5 calendar/work/study material to the built-in packs."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKS = ROOT / "backend/LanguageLearning.Infrastructure/Content/Starter"
TOPICS = json.loads(Path(__file__).with_name("baseline-topics.json").read_text(encoding="utf-8"))
UNIT = "dates-work-study"


def english_answers(text, language):
    alternatives = {
        "Today is May 2.": ["Today is the second of May.", "Today is 2 May.", "Today is May second."],
        "The lesson is on Monday.": ["The class is on Monday."],
        "the male student": ["the student"],
        "the male teacher": ["the teacher"],
        "I study English.": ["I am studying English.", "I learn English.", "I am learning English."],
        "I work in a school.": ["I work at a school."],
    }
    answers = [text, *alternatives.get(text, [])]
    if language == "ru" and text.startswith("the male "):
        noun = text.removeprefix("the male ")
        answers.extend([noun, "a " + noun, "male " + noun, "a male " + noun])
    return answers


def lesson(key, title, objective, pairs, note, language):
    questions = []
    for target, english in pairs:
        questions.extend([
            {"type": "MultipleChoice", "prompt": f'Which expression means "{english}"?',
             "answers": [target], "explanation": f"{target} means {english}",
             "options": [pair[0] for pair in pairs], "contextText": None},
            {"type": "Translation", "prompt": f"Translate into English: {target}",
             "answers": english_answers(english, language), "explanation": f"{target} means {english}",
             "options": [], "contextText": None},
            {"type": "Translation", "prompt": f"Translate: {english}",
             "answers": [target], "explanation": f"Use {target}",
             "options": [], "contextText": None},
        ])
    examples = "\n".join(f"{target} — {english}" for target, english in pairs)
    return {
        "key": key, "title": title, "objective": objective,
        "explanation": f"New words and phrases\n\n{examples}\n\n{note}\n\nRead and listen to these examples before practising them.",
        "exercises": questions, "unitKey": UNIT,
        "unitTitle": "Dates, work and study",
        "unitDescription": "Read calendar dates, plan a lesson and talk about study and work.",
        "difficulty": "A1", "contentVersion": 1,
    }


for code, topic in TOPICS.items():
    path = PACKS / f"{code}.json"
    pack = json.loads(path.read_text(encoding="utf-8"))
    dates = lesson("calendar-dates", "Days and calendar dates",
                   "Recognize a weekday and month, read a date and say when a lesson takes place.",
                   topic["calendar"], topic["calendarNote"], code)
    dates["prerequisiteKeys"] = ["greetings"]
    study = lesson("work-and-study", "People at school and work",
                   "Recognize student and teacher vocabulary and describe studying and working.",
                   topic["study"], topic["studyNote"], code)
    study["prerequisiteKeys"] = [dates["key"]]
    check = {
        "key": "check-dates-work-study", "title": "Unit check · Dates, work and study",
        "objective": "Review the calendar and study expressions from this unit.",
        "explanation": "Review only: complete both teaching lessons before this mixed check. Open the lesson notes again if you need to revise.",
        "exercises": [source["exercises"][index].copy()
                      for source in (dates, study) for index in (0, 2, 4, 6, 8, 10)],
        "unitKey": UNIT, "unitTitle": dates["unitTitle"],
        "unitDescription": dates["unitDescription"], "difficulty": "A1",
        "contentVersion": 1, "isCheckpoint": True,
        "prerequisiteKeys": [dates["key"], study["key"]],
    }
    # Stable keys make this authoring command repeatable; installation preserves old versions.
    existing = {item["key"] for item in pack["lessons"]}
    additions = [item for item in (dates, study, check) if item["key"] not in existing]
    position = next(index for index, item in enumerate(pack["lessons"])
                    if item.get("unitKey") == "everyday")
    pack["lessons"][position:position] = additions
    path.write_text(json.dumps(pack, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{code}: added {len(additions)} lessons/checks")
