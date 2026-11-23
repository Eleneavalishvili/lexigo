# Calendar dates, work and study

The Georgian specification, page 5, requires dates, professions and study
alongside the existing greetings, numbers, family, food, travel and routine
topics. This slice adds original material in all four course directions.

Each course gains an A1 unit, **Dates, work and study**, between First
conversations and Everyday essentials:

| Lesson | Teaching and practice |
| --- | --- |
| Days and calendar dates | Monday, January, a spoken calendar date and the weekday of a lesson; 12 recognition and translation questions. |
| People at school and work | Student and teacher vocabulary, studying English and working in a school; 12 recognition and translation questions. |
| Unit check | 12 mixed review questions drawn from the two teaching lessons; both are prerequisites. |

English notes explain the examples before practice, including Spanish and
Italian lowercase days/months, German capitals and ordinal dates, and Russian
date/weekday case forms. School/work notes distinguish gendered nouns and
introduce the location forms in the sample sentences. These notes are not a
claim of complete grammar coverage.

The authoring data is `tools/curriculum/baseline-topics.json`; the repeatable
authoring command is `tools/curriculum/add_baseline_topics.py`. It adds only
missing stable lesson keys. Later edits to published content must increment
the lesson content version, rather than overwrite an existing published
version. Runtime lesson packs remain under
`backend/LanguageLearning.Infrastructure/Content/Starter`.

The installer holds the existing course lock and shifts following unit
positions in descending order to respect PostgreSQL's unique ordering
constraint. Existing unit/lesson identifiers, lesson versions, prerequisites
and learner progress are retained. Existing lessons are not silently given
new prerequisites. Repeating installation must not move units again.

New pronunciation clips are generated locally with the existing pinned Piper
voices and served as static WAVs. No cloud account, paid quota or learner
recording upload is involved. Audio availability/playback checks do not
replace independent linguistic quality review.

This slice expands REQ-032/033. It does not establish that every remaining
topic, grammar point, recording or grouped baseline requirement is complete.
Public hosting remains the final phase at the user's request.

## Verified delivery — 2026-11-23

- Installed 12 new steps into the normal local database, without resetting it.
  Public APIs report 40 steps/8 units for Spanish, Italian and German and
  41 steps/9 units for Russian. The new unit is position 3 in every course.
- Current total: 161 steps, 2,148 questions and 33 units. The 32 review checks
  reuse taught material; they are not counted as additional vocabulary breadth.
- Nine course/revision/installation cases passed in
  `work/test-results-20261123/baseline-topics-20261123.trx`, covering complete
  grading across all four packs and old-session preservation. Twelve placement
  cases passed separately. Five final alternative-answer/installation cases
  passed in `baseline-topic-alternatives-20261123.trx` after the final edits.
- Eight isolated browser cases passed in
  `work/learner-browser-0bf30e901c4d40b69fe565fb2158d0a1/browser-results.log`.
  All eight also passed on the actual site at port 4200:
  `work/baseline-topics-local-browser-20261123.log`.
- Local synthesis and the frontend production build passed. The final
  manifest contains 3,339 clips, 134 more than the preceding release,
  including English alternative answers. Reference playback was exercised
  for the new date sentences in every language at desktop and phone widths.
- Common English equivalents such as “the teacher,” “I am studying English”
  and “Today is the second of May” are accepted where appropriate. Russian
  noun translations also accept context-appropriate English articles.
