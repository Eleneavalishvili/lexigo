# LexiGo interaction refresh — 2026-11-03

The user approved adopting learning-app interaction patterns while preserving LexiGo's existing logo, Fraunces/DM Sans fonts, cream/navy/terracotta/gold palette and retro identity. Desktop website and mobile are both required. This is a first implementation pass; the complete supplied list remains required.

## Implemented in this pass

- Shared raised controls: four-pixel depth, pressed movement, primary/secondary/danger classes and native disabled state. Existing colours/fonts retained. Text actions have consistent styling.
- Landing-page Get started and existing-account links, retaining the original hero illustration, copy and language picker.
- Real course path: raised circular buttons, current START bounce, completed gold and locked gray states, gold chapter banners, winding placement, collapsible details and keyboard focus. Buttons retain server prerequisite/ownership rules; no client unlocks.
- Lesson close X, progress-fill transition, fixed Check action, sliding correct/incorrect feedback and short wrong-answer shake. Existing target text, picture choices, word banks and audio controls retained.
- Focused result-stat component showing the existing server result. Confetti is decorative and restricted to a passed lesson with an actual positive XP award. It does not create rewards.
- Three original native SVG/CSS human companions: Mira, Nico and Zara, including course selection, blink/wave/feedback/playback reactions and pause/reduced-motion support. These are simplified SVG adaptations of the concept, not identical raster cutouts or phoneme lip-sync.
- Reduced-motion rules limit duration and iteration count; confetti is hidden under OS/saved motion preferences.

## Verification

248 frontend tests / 66 files passed after the functional changes. Final production build passed without warnings. The final build output is `work/interaction-final`; logs are `work/interaction-final-build.log` and `work/interaction-tests-final.log`. Formatting check initially found one pre-existing billing test formatting issue; normalized that test with Prettier before the final check.

Actual browser checks: desktop landing page at 1280×900; authenticated saved unsuccessful lesson showing real 77% accuracy and zero XP without confetti; real Spanish path at desktop and 390×844 iPhone width. Mobile document width 375 ≤ viewport 390 with the scrollbar: no horizontal overflow. Keyboard focus on a lesson summary is visible. Current and locked nodes rendered correctly. The cast demo's Hola playback changed Mira's accessible reaction to speaking.

Saved screenshots:

- `outputs/character-concepts/lexigo-desktop-interactions.png`
- `outputs/character-concepts/lexigo-path-desktop.png`
- `outputs/character-concepts/lexigo-path-mobile.png`
- `outputs/character-concepts/mira-installed-mobile.png`

Final browser preview is the real course page, not a mockup. Local frontend/API listen on loopback 4200/5187; API readiness returned 200. Existing PostgreSQL/mail/storage containers were restored without deleting data. External email sending stays disabled.

## Still required from the supplied list

- Dedicated desktop learning sidebar/top statistics with verified streak, total XP, hearts and active language.
- Complete onboarding presentation across language, daily goal and level. The existing goal/start wizard and placement work; language selection currently enters through the home/catalog cards.
- Social profile/avatar, achievement grid and friends integrated with real permissions/data.
- Weekly leagues with ranking/promotion/demotion and real membership/privacy rules.
- Real achievement/level-up toasts; optional sound effects with mute preference.
- Full cross-format idle/selected/correct/incorrect and device/accessibility verification. Existing recorded/local pronunciation and speaking services remain; browser speech recognition must not be presented as precise pronunciation assessment.
- Full original curriculum, baseline/bonus/security/release requirements. This pass does not complete those items.

No fake people, localStorage reward engine, client-side scoring, new payment integration, paid service or public deployment was introduced. No new API/schema/model: original registers remain 70 models/122 operations plus five supporting models/44 operations. No user lesson answers were submitted during browser verification.

## Files changed

All source paths below are under `frontend/src/`:

- `styles.scss`: shared control tokens, button states, text actions and reduced motion.
- `app/shared/character-cue/character-cue.{ts,html,scss,spec.ts}`: human cast and reaction tests.
- `app/shared/lesson-celebration.ts`: bounded decorative celebration.
- `app/features/companions/companions.{html,scss,spec.ts}`: three-character demo page.
- `app/features/home/home-hero.{ts,html,scss}` and `app/features/home/home.html`: landing actions and cast link.
- `app/features/learning/course-start.ts` and `lesson-intro.ts`: shared course-to-character mapping.
- `app/features/learning/lesson-player.{ts,html,scss,spec.ts}`: lesson shell, characters and completion composition.
- `app/features/learning/lesson-result-stats.ts`: real result cards.
- `app/features/learning/answer-feedback-panel.ts`: animated feedback sheet.
- `app/features/learning/choice-answers.scss`: press interaction.
- `app/features/learning/journey.{ts,html,scss}`: accessible course nodes and compact details.
- `app/features/billing/heart-checkout.html` and `subscription-checkout.html`: new character names only.
- `app/features/billing/billing-administration.spec.ts`: formatting only.

Documentation: `docs/PROJECT_MEMORY.md`, `docs/REQUIREMENTS.md`, `docs/CHARACTER_DESIGN.md` and this file. Concept images and actual browser captures are in `outputs/character-concepts/`.

Review the in-browser character shapes and path density. No credentials, payment account, paid provider or additional artwork purchase is needed for this pass. Independent native-language review remains needed for curriculum/audio quality.
