# Simulated subscription renewals and account inbox

This slice implements the original BillingJobExecution and Notification models and the original inbox/read API operations. Payments use existing test-card balances only. It introduces no payment provider, real charge, paid service, external email delivery or privileged role assignment.

## Consent and user controls

Renewal processing is disabled by default. Configure `BillingRenewals:Enabled=true` only after applying AddSubscriptionRenewals and verifying the simulator. The local development instance is enabled after verification; production activation remains subject to the release gates.

Earlier `AutoRenew=true` preferences are drafts, not permission for the newly available processor. The migration leaves their new consent fields empty. While a paid period is active, the learner explicitly saves renewal ON again. This records the owned subscription and consent time together with the selected owned, active, matching-currency test card. The interface explains the future simulated charge and never enables renewal on page load. A fresh manual purchase starts with renewal OFF and clears previous consent.

Cancellation takes precedence over renewal consent. Before period end, cancel/resume preserves the paid period and the raw saved renewal preference. After period end, an owned pending renewal can still be stopped using cancellation or renewal OFF; resume, new opt-in and scheduling an ended period are rejected. An old subscription cannot alter a newer active subscription. Late cancellation also clears consent. The settings business quota never blocks cancellation or renewal OFF; ordinary authenticated HTTP limits remain in force.

## Due-period processing

The hosted worker polls every 30 seconds and selects up to 50 due subscriptions per pass. Only active, email-confirmed accounts are eligible. Each candidate has its own SQL transaction and the same account-first lock used by cards, purchases and preferences. Candidate state is reread after locking; concurrent workers therefore cannot charge the same due period twice.

`periodKey` is the prior UTC period-end instant. BillingJobExecution and the existing renewal-order index independently enforce one job/order per subscription and period. The selected price is either the explicitly scheduled price or the existing price version; it must still be valid at the due instant and belong to the active subscription plan. A retired price stops renewal rather than silently substituting a different amount.

With valid consent/card/funds, the processor debits once, increments the card version, records a successful PaymentAttempt, immutable debit, receipt, Renewed event, system AuditLog, successful job and inbox notification. It updates the same subscription to the next calendar month/year, starting at the prior period end, and clears the applied scheduled choice. Current-period prices are never rewritten. All artifacts and the period change commit together.

The default grace window is 15 minutes (validated configuration range 1–60). Modest worker latency retains the calendar billing boundary. A later run expires the old period without catch-up charges. Free access is already determined by UTC period bounds even before the worker updates the stored status.

## Failure and recovery policy

User-approved October29 policy: completed failed or substantially missed renewals require manual retry, without repeated automatic charges. Manual retry is a newly reviewed purchase through the existing checkout; the failed renewal and its evidence remain intact, and the new period starts at payment time with renewal OFF.

- Blocked cards, insufficient funds and currency mismatch create a failed attempt and closed renewal order, without debit, receipt or paid extension.
- Cancellation, renewal OFF, absent consent/card, retired price or a missed window create a skipped job without a payment. The period expires; consent is cleared.
- Failed/skipped periods receive an owned inbox explanation and retain learning progress. `NextAttemptAt` remains null under this approved policy.
- Infrastructure exceptions roll back the complete transaction, including notification, job and debit. An uncommitted transaction can be attempted safely on a later poll. A committed failed job is never automatically retried. Commit-response loss cannot duplicate a committed due-period charge.
- One candidate failure is logged by exception type only and does not prevent the rest of the batch from running. Logs contain no card balance, email address, connection details or recording.

Deferred SQL checks require successful payments to have their matching debit/subscription/event/receipt and require each renewal job to agree with its order/payment result. Completed jobs cannot be edited/deleted. Downgrading this migration refuses to discard existing job or notification evidence. A backup/recovery procedure still needs production verification.

## Private account inbox

`GET /api/v1/me/notifications` returns only the authenticated account's notifications, with stable time/UUID cursor pagination (default30, maximum50). Cursor dates normalize to UTC. `PATCH /api/v1/me/notifications/{id}/read` is owned and idempotent; another account's or an unknown ID returns the same404 response. Responses are private/no-store.

The `/notifications` page uses the existing retro theme, escaped text, loading/error/empty/read states and explicit pagination. Account switches and destruction discard late responses and private state. Rendering never marks a message read. Links are available from Account and Subscription. No outgoing email is sent.

## Verification and remaining scope

For a repeatable local demo, initialize the application database with
`./scripts/Initialize-Development.ps1`. The billing migrations seed the
Free and Premium plans and the monthly and annual prices. The examples
below use temporary PostgreSQL and synthetic test cards. The hosted worker
test enables renewal only in its test application.

```powershell
dotnet test backend/LanguageLearning.Tests --filter 'FullyQualifiedName~HostedWorkerActuallyProcessesADuePeriodWithoutAClientPaymentRequest|FullyQualifiedName~DeclinesExpireWithoutDebitAndNeverRetryAfterTheCardIsFixed'
```

- Successful renewal: an explicitly enabled subscription with sufficient
  test-card funds advances one period. Check the debit, receipt, renewal
  event and private inbox notice.
- Declined renewal: the fixtures cover a blocked card, insufficient funds
  and currency mismatch. Check that the paid period is not extended, the
  balance is unchanged, and fixing the card does not silently retry the
  charge.

Focused purchase/management/renewal/inbox checks and Angular inbox/confirmation tests have passed; final full-suite, hosted-worker and runtime evidence is recorded in PROJECT_MEMORY.md at handoff. Isolated PostgreSQL fixtures contain all generated test accounts/cards; tests do not seed actual learner data or permissions.

Refunds, billing administration and MFA/permissions, complete financial/subscription history, catalog administration/version overlap rules, other notification producers/reminders, physical-device/accessibility checks and production recovery/security gates remain required. Other Premium learning features remain separately marked planned. The full original70-model/122-operation baseline remains unchanged.
