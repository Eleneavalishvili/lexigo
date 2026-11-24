# Graded language foundations — 2026-11-24

Page 5 of the Georgian PDF requires language-specific grammar and symbols,
written Cyrillic recognition, practical topic vocabulary and pronunciation.
This slice turns the existing reference notes into real graded practice.

Each course gains **How the language works**: a 22-question teaching lesson
followed by a 12-question unit check. English notes introduce the exact forms
before recognition, target-language translation and English recall questions.

| Course | Material practised |
| --- | --- |
| Spanish | el/la, noun gender, soy/estoy, hablo, adjective agreement, ñ and opening question marks |
| Italian | il/la/lo/l’, essere/parlare, adjective agreement and è/e |
| German | der/die/das, accusative den, dative dem after mit, verb-second order and umlauts |
| Russian | gender, present-tense forms, nominative/accusative/genitive/prepositional book forms and Е/Ё |

Russian also gains three written-letter teaching lessons (11 letters and 22
questions each), followed by a 15-question check. All 33 capital/lowercase forms
and their written names are assessed. Ь/Ъ are explained as signs without an
independent sound. These text-recognition exercises do not masquerade as graded
listening or independent approval of recordings. The existing reviewed sound-
association publication gate remains in place.

The dictionary adds 12 entries per language: book, house, city, bus, hotel, room,
shop, street, table, chair, train and airport. Each entry has an English meaning,
example, noun classification and topic. There are now 136 entries, 34 per course.
The installer preserves earlier word IDs, staff edits, saved words and schedules.

Source authoring files are `tools/curriculum/grammar-foundations.json`,
`vocabulary-foundations.json` and the complete Russian reference. Their authoring
commands maintain readable embedded JSON resources. New stable lesson keys and
content-version 1 do not overwrite earlier published lessons or learner snapshots.
Future edits to installed versions must increment `contentVersion`.

The local speech generator skips punctuation-only text and standalone Russian
signs. It writes clips atomically and validates their WAV frames before replacing
the destination. A failed generation cannot replace a working manifest or leave
a new truncated destination clip. Generated speech remains synthetic, not
human-recorded or guaranteed to have native-level pronunciation.

The earlier content review remains relevant: broader vocabulary and subtopic
coverage still need review, and independent linguistic/audio approval is open.
No complete A1–C2 or Duolingo-depth claim is made.
