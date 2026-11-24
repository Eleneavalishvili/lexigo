"""Write the original lesson-linked vocabulary expansion as a readable resource."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
KEYS = ["coffee", "milk", "tea", "water", "mother", "father", "boy", "girl",
        "monday", "january", "student", "teacher", "school", "ticket", "sun", "three"]
ENGLISH = ["coffee", "milk", "tea", "water", "mother", "father", "boy", "girl",
           "Monday", "January", "student", "teacher", "school", "ticket", "sun", "three"]
TOPICS = ["food"] * 4 + ["family"] * 4 + ["numbers"] * 2 + ["family"] * 3 + ["travel", "routines", "numbers"]
ROWS = {
    "es": [
        ("el café", "El café está caliente."), ("la leche", "La leche está fría."),
        ("el té", "El té está caliente."), ("el agua", "El agua está fría."),
        ("la madre", "Mi madre trabaja hoy."), ("el padre", "Mi padre está en casa."),
        ("el niño", "El niño bebe agua."), ("la niña", "La niña bebe leche."),
        ("lunes", "La clase es el lunes."), ("enero", "Enero es un mes."),
        ("el estudiante", "El estudiante estudia inglés."),
        ("el profesor", "El profesor trabaja en una escuela."),
        ("la escuela", "La escuela está cerca."), ("el billete", "Necesito un billete."),
        ("el sol", "Hoy hace sol."), ("tres", "Tengo tres libros."),
    ],
    "it": [
        ("il caffè", "Il caffè è caldo."), ("il latte", "Il latte è freddo."),
        ("il tè", "Il tè è caldo."), ("l'acqua", "L'acqua è fredda."),
        ("la madre", "Mia madre lavora oggi."), ("il padre", "Mio padre è a casa."),
        ("il bambino", "Il bambino beve acqua."), ("la bambina", "La bambina beve latte."),
        ("lunedì", "La lezione è lunedì."), ("gennaio", "Gennaio è un mese."),
        ("lo studente", "Lo studente studia inglese."),
        ("il professore", "Il professore lavora in una scuola."),
        ("la scuola", "La scuola è vicina."), ("il biglietto", "Ho bisogno di un biglietto."),
        ("il sole", "Oggi c'è il sole."), ("tre", "Ho tre libri."),
    ],
    "de": [
        ("der Kaffee", "Der Kaffee ist heiß."), ("die Milch", "Die Milch ist kalt."),
        ("der Tee", "Der Tee ist heiß."), ("das Wasser", "Das Wasser ist kalt."),
        ("die Mutter", "Meine Mutter arbeitet heute."), ("der Vater", "Mein Vater ist zu Hause."),
        ("der Junge", "Der Junge trinkt Wasser."), ("das Mädchen", "Das Mädchen trinkt Milch."),
        ("der Montag", "Der Unterricht ist am Montag."), ("der Januar", "Der Januar ist ein Monat."),
        ("der Student", "Der Student lernt Englisch."),
        ("der Lehrer", "Der Lehrer arbeitet in einer Schule."),
        ("die Schule", "Die Schule ist in der Nähe."), ("die Fahrkarte", "Ich brauche eine Fahrkarte."),
        ("die Sonne", "Heute scheint die Sonne."), ("drei", "Ich habe drei Bücher."),
    ],
    "ru": [
        ("кофе", "Кофе горячий."), ("молоко", "Молоко холодное."),
        ("чай", "Чай горячий."), ("вода", "Вода холодная."),
        ("мама", "Моя мама работает сегодня."), ("папа", "Мой папа дома."),
        ("мальчик", "Мальчик пьёт воду."), ("девочка", "Девочка пьёт молоко."),
        ("понедельник", "Урок в понедельник."), ("январь", "Январь — месяц."),
        ("студент", "Студент изучает английский."), ("учитель", "Учитель работает в школе."),
        ("школа", "Школа рядом."), ("билет", "Мне нужен билет."),
        ("солнце", "Сегодня светит солнце."), ("три", "У меня три книги."),
    ],
}

STARTER = {
    "es": [("hello", "hola", "hello", "¡Hola! ¿Cómo estás?"), ("two", "dos", "two", "Tengo dos entradas."),
           ("sister", "la hermana", "sister", "La hermana de Ana vive aquí."), ("bread", "el pan", "bread", "El pan está en la mesa."),
           ("station", "la estación", "station", "La estación está cerca."), ("rain", "la lluvia", "rain", "La lluvia moja las calles.")],
    "it": [("hello", "ciao", "hello", "Ciao, come stai?"), ("two", "due", "two", "Ho due biglietti."),
           ("sister", "la sorella", "sister", "La sorella di Anna abita qui."), ("bread", "il pane", "bread", "Il pane è sul tavolo."),
           ("station", "la stazione", "station", "La stazione è vicina."), ("rain", "la pioggia", "rain", "La pioggia bagna le strade.")],
    "de": [("hello", "Hallo", "hello", "Hallo, wie geht es dir?"), ("two", "zwei", "two", "Ich habe zwei Fahrkarten."),
           ("sister", "die Schwester", "sister", "Die Schwester von Anna wohnt hier."), ("bread", "das Brot", "bread", "Das Brot liegt auf dem Tisch."),
           ("station", "der Bahnhof", "train station", "Der Bahnhof ist ganz in der Nähe."), ("rain", "der Regen", "rain", "Der Regen macht die Straßen nass.")],
    "ru": [("hello", "привет", "hello", "Привет! Как дела?"), ("two", "два", "two", "У меня два билета."),
           ("sister", "сестра", "sister", "Моя сестра живёт здесь."), ("bread", "хлеб", "bread", "Хлеб лежит на столе."),
           ("station", "вокзал", "train station", "Вокзал находится рядом."), ("rain", "дождь", "rain", "На улице идёт дождь.")],
}
STARTER_TOPICS = ["greetings", "numbers", "family", "food", "travel", "routines"]
packs = []
for language, rows in ROWS.items():
    if len(rows) != len(KEYS):
        raise ValueError("Every vocabulary row needs a matching key and English meaning.")
    items = [
        {"key": KEYS[index], "word": word, "englishTranslation": ENGLISH[index], "example": example,
         "partOfSpeech": "numeral" if KEYS[index] == "three" else "noun", "topic": TOPICS[index]}
        for index, (word, example) in enumerate(rows)
    ]
    items.extend({"key": key, "word": word, "englishTranslation": english, "example": example,
                  "partOfSpeech": "interjection" if key == "hello" else "numeral" if key == "two" else "noun",
                  "topic": STARTER_TOPICS[index]}
                 for index, (key, word, english, example) in enumerate(STARTER[language]))
    packs.append({"code": language, "items": items})
path = ROOT / "backend/LanguageLearning.Infrastructure/Vocabulary/Starter/words.json"
path.parent.mkdir(parents=True, exist_ok=True)
# Preserve separately authored additions when rebuilding the original resource.
if path.exists():
    previous = {pack["code"]: pack["items"] for pack in json.loads(path.read_text(encoding="utf-8"))}
    for pack in packs:
        keys = {item["key"] for item in pack["items"]}
        pack["items"].extend(item for item in previous.get(pack["code"], []) if item["key"] not in keys)
path.write_text(json.dumps(packs, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("Wrote 88 vocabulary entries: 24 existing starter words and 64 additions.")
