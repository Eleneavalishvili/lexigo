# Receipt recovery and unknown addresses

The receipt page distinguishes a temporary read failure from a missing/foreign
payment. Temporary failures offer a read retry. Pending payments have an explicit
status refresh; neither operation submits a charge. Failed payments explain that
no simulated funds moved and link to the test wallet.

The page shows payment/order references, request time and the original order's
period and quoted amount/currency. It does not substitute current catalog prices
or current subscription status. A failed order-detail read retains the payment
and successful-receipt download, with a separate retry. Late payment/order
responses after route or account changes are ignored. New reads are refused
after the original account changes.

Existing receipt download, payment, order ownership and server financial rules
remain unchanged. No backend API, persistent model or schema was added.

The Angular wildcard route now shows a branded recovery screen with links to
courses, help and home. It does not echo the visited path/query. This is a
client-side not-found screen; the development SPA server still serves its
application shell with HTTP 200. It does not establish HTTP 404 behavior for
production hosting or API routes.

## Verification — November 6

Seventeen frontend receipt/checkout/BillingStore cases and one router recovery
case passed. Receipt tests cover network retry, foreign/missing denial, partial
order failure, pending refresh, route changes and account changes. The router
test navigated from an unknown address back to courses without rendering query
data. Three PostgreSQL cases verified monthly/annual purchase receipts and
cross-account order/card/receipt/recovery denial. Production build and formatting
passed. Authenticated browser/mobile/download walkthrough remains pending.

Changed source: billing/payment-receipt.ts, .html and .spec.ts; navigation/
not-found.ts, .html, .scss and .spec.ts; app.routes.ts. Engineering evidence and
requirement/model/API records were updated without completing broader release
requirements.
