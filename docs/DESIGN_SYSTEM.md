# LexiGo visual identity

User direction (2026-10-27): original, retro/vintage, cool, with intentional typography and colors rather than generic application styling.

## Direction

A vintage language/travel club: warm paper, editorial serif headlines, postcards, conversation fragments and restrained print details. Distinctive without copying Duolingo. No fabricated testimonials, usage statistics or course completion claims.

## Palette

- Paper: #F7F1E3
- Ink blue: #213E62
- Tomato red: #BD432D
- Golden yellow: #F1BD4B
- Sage: #BECBB0
- Muted text: #56616A

Tokens live in frontend/src/styles.scss. Use ink for text, red for emphasis and actions, and pastel surfaces for language identities. Keep readable contrast and text/focus indicators; color alone must not communicate a state.

## Typography and assets

Fraunces for display/wordmark; DM Sans for body/UI. Fonts are locally bundled through pinned @fontsource packages, so page rendering does not call Google Fonts. Font licenses are retained in public/brand/licenses. System fallbacks cover additional scripts; review language-specific font coverage as curriculum pages are built.

Original vector L/spark logomark, underline wordmark and hello-world collage are in frontend/public/brand. lexigo-mark.svg is the favicon; lexigo-logo.svg is the standalone wordmark with embedded licensed font. Do not alter font licenses or claim the font itself was designed for LexiGo.

## Components

Home orchestrates navigation/footer. HomeHero, LanguageExplorer and HomeStory own their respective content and styles. BrandLogo is reusable. Shared design tokens/page width/typography/buttons live in global styles; business functionality remains separate.

## Verified in this milestone

- Production Angular build within existing component style budgets, with no warnings.
- Formatting check and git diff whitespace check.
- Desktop visual inspection at approximately 1264px, narrow layout at 319px and tablet layout at 700px; corrected Russian card typography and narrow header spacing.
- Local fonts loaded and illustration had no broken image source.
- Italian and Russian course selection updated their greeting, topics and pressed state through Angular Signals.
- Anchor navigation, keyboard focus, skip link and reduced-motion styles provided. This is not a full accessibility audit.

## Scope boundary

Course cards are informational previews. They do not start lessons, award XP or imply registration/authentication is complete. All learning/backend/security requirements remain in the full project checklist. This design is subject to user feedback and will guide later screens once accepted.

## Contact and password controls

Contact Us extends the approved travel-club identity with an original vector postcard, ink-blue/red typography, sage offset shadow and a two-column inbox/company section. Shared PasswordToggle uses custom engraved-eye artwork, gold iris, red rays and a star pupil; open/closed states expose accessible Show/Hide labels, pressed state and the controlled input. Links are available in the home navigation/footer and all account-form footers. Authenticator settings reuse the same account shell.
