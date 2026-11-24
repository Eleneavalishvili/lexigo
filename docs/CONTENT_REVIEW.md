# Baseline content review — 2026-11-24

The scope is page 5 of the original Georgian specification. Reference notes,
graded lessons and independent pronunciation approval are different evidence.
No complete-course or native-speaker approval is claimed by this review.

## Language-specific foundations

All four catalog details now link to `/basics/{code}`. These public reference
guides use the existing palette, fonts and logo, require no enrollment, and
do not award XP or change progress. Each example has its language attribute.

| Language | Reference content added |
| --- | --- |
| Spanish | Masculine/feminine articles, the feminine *agua* exception, ser/estar, regular verb example, ñ, stress accents and opening question marks. |
| Italian | il/lo/la/l’, un/uno/una/un’, gender, essere, regular verbs, adjective agreement, è/e and everyday expressions. |
| German | der/die/das, noun capitalization, nominative/accusative/dative examples, verb-second statements, questions, umlauts and ß. |
| Russian | Cyrillic look-alikes, Е/Ё and signs, gender exceptions, no articles/present copula, verb forms and four case examples; links to the complete 33-letter audio reference. |

The examples and explanations are original summaries. Checked references:

- [RAE: agua](https://www.rae.es/dpd/agua), for feminine agreement and the directly preceding singular article.
- [Treccani: un or un’?](https://www.treccani.it/enciclopedia/un-o-un_(La-grammatica-italiana)/), for masculine un and feminine elision.
- [Goethe-Institut grammar](https://www.goethe.de/ins/de/de/m/prf/grm.html), for main-clause verb position.
- [Russian noun grammar](https://russky.info/grammar/nouns?hl=en), for gender and case patterns.

These references do not establish independent review of every lesson,
translation, recording or new reference paragraph. They do not license copying
those sources' exercises; no source exercises were copied.

## Topic coverage inspected

The same stable teaching keys exist in all four source packs:

| PDF family | Teaching keys and remaining limitation |
| --- | --- |
| Alphabet, pronunciation, greetings and introductions | `greetings`, `introductions`, picture-first words; Russian `cyrillic-first-letters` plus the full reference. Graded Russian sound recording approval remains open. |
| Numbers, time, dates, colours and objects | `numbers`, `time`, `calendar-dates`, `colours`, picture-first food/drinks and `home`. This establishes examples, not exhaustive vocabulary breadth. |
| Family, people, professions and study | `family`, `picture-people`, `work-and-study`; profession range remains limited. |
| Food, cafes, shopping, prices and ordering | `food`, `picture-food`, `picture-drinks`, `shopping`; broader menus and transactions remain a content-review task. |
| City, transport, hotel and directions | `travel`, `directions`, `missed-connection`, `resolving-problem`; full hotel-specific coverage still needs explicit exercise-level reconciliation. |
| Routines, hobbies, weather and plans | `routines`, `hobbies`, `plans`, `future`; weather has written examples and comprehension. |

The vocabulary library remains 88 entries, 22 per language. It covers all six
existing library topic labels, but is narrower than the lesson bank. No new
dictionary entries or graded lessons are claimed in this slice. Published
lesson versions and learner snapshots were not overwritten.

## Verification

- Frontend lint and production build passed.
- Four isolated browser checks passed, then all four passed on the real public
  HTTPS website: `work/deployment/language-basics-live-20261124.log`.
- The browser checks inspect all four guides at 320/390/768/1440px with axe
  WCAG 2/2.1 A/AA rules, overflow, language attributes, links, refreshed topic
  fragments and unsupported/inherited-object route names.
- The first run caught a base-href fragment error that returned to the home
  page after reload. Router fragment links corrected it; final runs passed.
- The six existing public/learner/staff accessibility journeys passed again:
  `work/deployment/accessibility-release-20261124.log`.

REQ-032/033/045/057 and full accessibility requirements stay partial pending
their remaining graded coverage, independent review and physical-device checks.
