# Plan catalogue administration

The catalogue at `/admin/plans` reads plans through the original `GET /api/v1/admin/plans`. SuperAdmin or an explicitly delegated Admin needs current active, confirmed, MFA-enrolled account/session access. Other staff roles do not inherit financial permissions. Responses use no-store caching.

The admin sees active and retired plans, creation dates, all recorded price periods including expired/future versions, feature descriptions and limits. Planned features remain marked unavailable; including one in a plan does not enable its service. The public pricing endpoint still excludes retired plans and exposes only currently effective prices. A catalogue read never publishes a plan or changes an entitlement.

Queries use a repeatable-read snapshot, stable code/ID ordering, validated20-row pages and an optional active flag. Three bulk projections avoid one query per plan and exclude learner identities, balances, passwords and session information. Client state discards older filter/page responses and clears across accounts; the existing billing route guard protects navigation and the server independently enforces access.

Verified2026-10-30:383 backend tests,206 frontend tests and production build pass. PostgreSQL tests cover both permitted financial roles, retired/public separation, complete price histories, limits/unavailable labels, malformed filters/pages, no-store and absence of private account data. Existing staff/MFA tests now include the new endpoint. Client tests exercise delayed responses and account changes.

## Future simulated prices

An authorized billing administrator can schedule a future version from an
open-ended price. Currency and billing period are inherited. The operation
requires a current password, a recent MFA sign-in, and a reason. The previous
window ends at the exact instant the new one begins. PostgreSQL commits the
window change, the new price, audit entry and retry evidence together. Existing
quotes and purchase records retain their saved amounts.

The browser converts the administrator's local date and time to UTC. An
uncertain response can be retried with the same request key and a newly
entered password. Billing Audit lists the change. A renewal whose consented
price has retired requires manual retry under the existing renewal rules.

## Draft creation and renaming

The original POST operation creates an inactive plan with a unique, normalized
code. The original PUT operation renames a plan using the name the administrator
last loaded, so a stale form cannot silently overwrite another name. Both
operations require billing authorization, fresh MFA, the current password and
a reason. Retry evidence and the audit event commit with the change.

Drafts stay out of public pricing. The admin catalogue can show them under
**Inactive plans**. Renaming preserves plan IDs, codes, prices, features and
paid access.

Inactive drafts without prices or purchase history can also select catalogue
features and record optional positive whole-number limits. The saved feature
set is checked before applying an edit, and changes commit with their audit
and retry records. Existing Free and Premium feature assignments are protected.
Draft feature limits are catalogue settings and do not grant learner access.

An authorized administrator can retire the built-in Premium plan from new
sales and reopen it. Retirement hides it from public pricing and prevents
new quotes. Valid existing quotes and paid access retain their terms.
Automatic renewal while the plan is retired follows the existing
unavailable-price outcome. The specification on PDF p. 20 defines two public
tiers, Free and Premium, with monthly and annual Premium periods. PDF p. 22
lists plan create and edit operations. Private drafts support those operations.
Publishing another tier would require a scope decision about prices,
entitlements, and transitions from existing subscriptions.

Twenty-six backend catalogue, price-window and checkout cases passed. The
desktop Edge walkthrough used a disposable SuperAdmin with MFA, created and
renamed a draft, reloaded the catalogue and verified public exclusion. The
form test, formatting, lint and production frontend build also passed.

The follow-up passed 27 backend tests and a desktop Edge walkthrough for
draft feature limits and Premium retirement and reopening. PostgreSQL also
verified that a valid quote still purchases Premium after retirement, an
existing paid entitlement survives and a new quote is available after reopening.
