# LexiGo Home Screen version

Approved by the owner on 2026-11-24. This is a free Home Screen web app,
not an Apple App Store publication or an offline application. It uses the
same website, account, server-side progress and Free/Premium entitlements.

On iPhone, open https://206.189.25.5/ in Safari. Open Share, choose Add to
Home Screen, keep Open as Web App enabled if offered, and tap Add. Launch
the LexiGo icon and sign in with your existing account if asked. Instructions
are also at https://206.189.25.5/help#install-app . Internet is required.
Availability depends on the existing temporary hosting and its expiry;
installation does not extend the server lifetime or create a new hosting fee.

The manifest requests standalone display and uses the existing brand colors.
Apple and general app PNGs are exports of the unchanged original SVG mark,
with mask-safe padding. Reproduce them with
`node tools/assets/generate-app-icons.mjs msedge` (omit msedge when using
Playwright's installed Chromium). No service worker caches account/API data.

The icons, metadata, instructions and accessibility passed Chromium and
WebKit desktop/phone-sized checks locally. The real HTTPS manifest and Apple
icon return 200 with their correct content types. The published Chromium
Home Screen browser checks passed. Physical Home Screen installation still
requires the owner to perform the iPhone steps; it cannot be claimed from
browser emulation. No Apple account or membership was created or purchased.
