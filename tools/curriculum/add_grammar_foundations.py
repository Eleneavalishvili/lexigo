"""Add original, teach-first page-5 grammar practice without rewriting old lessons."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKS = ROOT / "backend/LanguageLearning.Infrastructure/Content/Starter"
DATA = json.loads(Path(__file__).with_name("grammar-foundations.json").read_text(encoding="utf-8"))

for code, source in DATA.items():
    path = PACKS / f"{code}.json"
    pack = json.loads(path.read_text(encoding="utf-8"))
    shared = {
        "unitKey": "grammar-foundations", "unitTitle": "How the language works",
        "unitDescription": "Use articles, noun forms, sentence patterns and the language's special letters.",
        "difficulty": "A1", "contentVersion": 1,
    }
    examples = "\n".join(f"{target} — {english}" for target, english in source["pairs"])
    questions = []
    for target, english in source["pairs"]:
        questions.extend([
            {"type": "MultipleChoice", "prompt": f'Choose the expression for "{english}".',
             "answers": [target], "options": [pair[0] for pair in source["pairs"]],
             "explanation": f"{target} — {english}", "contextText": None},
            {"type": "Translation", "prompt": f"Translate: {english}", "answers": [target],
             "options": [], "explanation": f"Use {target}", "contextText": None},
            {"type": "Translation", "prompt": f"Translate into English: {target}", "answers": [english],
             "options": [], "explanation": f"{target} — {english}", "contextText": None},
        ])
    meaning = {
        **shared, "key": "grammar-forms", "title": source["title"],
        "objective": "Recognize and use the grammatical forms taught in these examples.",
        "explanation": f"New words and phrases\n\n{examples}\n\n{source['note']}",
        "prerequisiteKeys": ["greetings"], "exercises": questions,
    }
    symbol_questions = [{"type": "MultipleChoice", "prompt": prompt, "answers": [answer],
                       "options": options, "explanation": source["note"], "contextText": None}
                      for prompt, answer, options in source["symbols"]]
    meaning["exercises"] = questions + symbol_questions
    check = {
        **shared, "key": "check-grammar-foundations", "title": "Unit check · Language foundations",
        "objective": "Apply the grammar and spelling distinctions from this unit.",
        "explanation": "Review the teaching lesson before taking this check.",
        "prerequisiteKeys": ["grammar-forms"], "isCheckpoint": True,
        "exercises": [question.copy() for question in questions[1::3] + symbol_questions + questions[2::3][:2]],
    }
    existing = {lesson["key"] for lesson in pack["lessons"]}
    # This authoring source owns these keys. Published edits require a contentVersion bump;
    # the database installer never overwrites an existing learner version.
    authored = {lesson["key"]: lesson for lesson in (meaning, check)}
    pack["lessons"] = [authored.get(lesson["key"], lesson) for lesson in pack["lessons"]]
    additions = [lesson for lesson in (meaning, check) if lesson["key"] not in existing]
    index = next(index for index, lesson in enumerate(pack["lessons"]) if lesson.get("unitKey") == "everyday")
    pack["lessons"][index:index] = additions
    path.write_text(json.dumps(pack, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{code}: added {len(additions)} steps")
