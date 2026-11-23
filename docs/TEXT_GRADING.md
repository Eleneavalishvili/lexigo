# Text answer grading

Grading runs on the server against the private accepted alternatives in the saved exercise snapshot. Learner text never supplies a score or answer key.

Normalization applies Unicode NFC and collapses whitespace. Curly apostrophes U+2018/U+2019 and the ordinary keyboard apostrophe U+0027 compare equally. The apostrophe itself is not removed: `l’acqua` and `lacqua` remain different.

The exercise’s existing case flag controls letter case. Its punctuation flag controls terminal `. ! ? …` and Spanish opening `¿ ¡`. Strict punctuation exercises still require their opening/closing marks. Internal commas and punctuation remain significant.

For typed answers in the Spanish, Italian and German courses, a plain Latin vowel may stand in for the same vowel with a diacritic in an accepted answer. The fallback is directional and the feedback shows the standard spelling. A different vowel or an incorrectly accented vowel still differs. Distinct letters such as ñ and ß keep their spelling. The Russian course keeps strict letter matching, including ё and е. Speaking, word tiles and choices use their own existing checks.

Examples with punctuation ignored:

| Accepted expression | Typed response | Result |
| --- | --- | --- |
| Dov’è la stazione? | Dov'è la stazione? | Correct |
| ¿Qué hora es? | Qué hora es | Correct |
| ¿Qué hora es? | Que hora es | Correct in Spanish |
| l’acqua | lacqua | Incorrect: missing apostrophe |
| schön | schon | Correct in German |
| für | far | Incorrect |
| ёлка | елка | Incorrect in Russian |

Existing recorded answers/results are not regraded. New submissions, including in saved open sessions, use the corrected evaluator with their own saved case/punctuation flags. Acceptable synonyms still need explicit private alternatives; this change does not implement semantic AI grading.

Tests include typography, NFC, whitespace, strict punctuation/case, meaningful-letter denials and API checks of the resulting hearts and feedback after reload. See TextAnswerEvaluatorTests and LearningTests.KeyboardVariantsAreGradedOnTheServerWithoutDiscardingMeaningfulLetters.
