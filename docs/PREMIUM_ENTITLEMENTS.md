# Free and Premium learning

Premium in the current checkout is activated by simulated card payments.
The API resolves eligibility from the current subscription period and the
plan's feature links. A browser control or test-card balance alone cannot
grant access.

| Capability | Free | Premium | Delivery |
| --- | --- | --- | --- |
| Core courses, lessons and in-lesson listening | Included | Included | Available |
| Personal vocabulary, basic practice and progress | Included | Included | Available |
| Weekly learning summary and basic mistake practice | Included | Included | Available |
| Unlimited hearts | Finite hearts | Included | Available |
| Week-to-week learning comparison | Weekly summary | Included | Available |
| Extra situational dialogues | Four core conversations per language | Includes an additional lost-and-found conversation per language | API access verified with simulated checkout |
| Advanced personal practice | Mixed mistake and scheduled review | Adds dedicated repeated-mistake and due-review sessions | Available with simulated Premium |
| Dedicated listening and speaking practice | In-lesson listening and a standalone recorder | Guided starter rounds in Spanish, Italian, German and Russian | API and Angular tests passed; device walkthrough pending |
| Live character conversations | Core dialogue exercises | Planned add-on | Provider, privacy, and free usage rules pending |
| Extra study with a teacher | Core courses | Included when the teacher accepts | Signed-in two-participant walkthrough pending |

The Premium week comparison is available under **My learning week** on the
learning path. It compares XP, first lesson completions, study days and
graded answer accuracy over the chosen week and the preceding week. It labels
the current week as in progress. The API checks `AdvancedAnalytics` on each
request and reads both weeks from the signed-in learner's own records using
one PostgreSQL snapshot.

Focused practice is available under **Review the tricky bits** on each course
path. **Repeated mistakes** selects unresolved questions missed at least twice.
**Due for review** selects previously correct questions whose review date has
arrived. Each mode resumes its own saved session. Both use the learner's own
history and follow the same zero-XP, zero-heart-loss rules as basic practice.
An active `AdvancedPractice` entitlement is required to start or continue
these sessions. Finished results stay available to their owner.
