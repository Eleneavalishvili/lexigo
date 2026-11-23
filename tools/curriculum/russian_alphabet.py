"""Build the complete, ungraded Russian alphabet reference."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ROWS = [
    ("А", "а", "арбуз", "watermelon"), ("Б", "бэ", "банк", "bank"),
    ("В", "вэ", "вода", "water"), ("Г", "гэ", "город", "city"),
    ("Д", "дэ", "дом", "house"), ("Е", "е", "ель", "spruce tree"),
    ("Ё", "ё", "ёлка", "Christmas tree"), ("Ж", "жэ", "жук", "beetle"),
    ("З", "зэ", "зима", "winter"), ("И", "и", "игра", "game"),
    ("Й", "и краткое", "йогурт", "yogurt"), ("К", "ка", "кот", "cat"),
    ("Л", "эль", "лампа", "lamp"), ("М", "эм", "мама", "mother"),
    ("Н", "эн", "нос", "nose"), ("О", "о", "окно", "window"),
    ("П", "пэ", "парк", "park"), ("Р", "эр", "рыба", "fish"),
    ("С", "эс", "суп", "soup"), ("Т", "тэ", "три", "three"),
    ("У", "у", "утка", "duck"), ("Ф", "эф", "фото", "photo"),
    ("Х", "ха", "хлеб", "bread"), ("Ц", "цэ", "цветок", "flower"),
    ("Ч", "че", "чай", "tea"), ("Ш", "ша", "школа", "school"),
    ("Щ", "ща", "щётка", "brush"), ("Ъ", "твёрдый знак", "подъезд", "building entrance"),
    ("Ы", "ы", "сыр", "cheese"), ("Ь", "мягкий знак", "день", "day"),
    ("Э", "э", "это", "this"), ("Ю", "ю", "юг", "south"),
    ("Я", "я", "яблоко", "apple"),
]
NOTES = {
    "Е": "Е and Ё are different letters. Listen to each name and compare their examples.",
    "Ё": "Keep both dots: Ё is a separate letter from Е, including in typed answers.",
    "Й": "Й and И are different letters. The short mark belongs to Й.",
    "Щ": "Щ is different from Ш. Compare their names and example words.",
    "Ъ": "The hard sign has no independent sound. It separates a consonant from a following vowel in words such as подъезд.",
    "Ы": "Ы is a vowel distinct from И. Compare their names and examples.",
    "Ь": "The soft sign has no independent sound. It often marks the preceding consonant as soft.",
}
letters = [
    {"upper": upper, "lower": upper.lower(), "name": name,
     "audioText": f"Это {name}." if upper in "ЪЬ" else f"Это буква {name}.",
     "example": example, "meaning": meaning, "note": NOTES.get(upper, "")}
    for upper, name, example, meaning in ROWS
]
if "".join(item["upper"] for item in letters) != "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ":
    raise ValueError("The reference must contain the complete alphabet in order.")
path = ROOT / "frontend/public/teaching/russian-alphabet.json"
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(letters, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Wrote 33 Russian letters with names and examples.")
