# Brevo preparation and activation

## Current status

Brevo Free onboarding and sender verification are complete. A dedicated Standard SMTP key named LexiGo local development expires January 27, 2027 and is saved in local .NET User Secrets. The user approved one registration email, created their own account and confirmed inbox receipt. SQL shows the email is confirmed; the outbox records SMTP acceptance on its first attempt. **DeliveryEnabled=false** again after this single approved test. No additional email was sent for the confirmation-screen fix.

The key value was never printed in chat, source, process arguments or a screenshot. A one-time encrypted local transfer kept its private transfer key only in process memory; only ciphertext crossed the temporary transfer files, which were removed afterward. Local User Secrets remains development storage, not an encrypted production vault.

The user requires free services only. Do not choose a paid plan, paid add-on, domain purchase or card-required trial. Brevo warns that a freemail sender domain is not recommended; no owned-domain authentication or production-delivery claim is made.

Confirmation uses an expiring one-use **link**, not a six-digit code. Brevo transports the existing email; Identity creates the token. The account owner entered their own password during the test. A valid, still-unexpired used link now gives a helpful already-confirmed response and sign-in link; invalid and expired tokens remain rejected. Temporary service failures preserve the link for a retry. This local test does not establish production deliverability.

Registration → Identity confirmation token → transactional SQL outbox → SMTP worker → Brevo → recipient inbox → confirmation link and explicit confirmation action. Reset and security-notification emails use the same transport. Sender acceptance is not a guarantee of inbox placement.

## Account steps, when ready

1. Create/sign in to the intended Brevo account. The account owner completes signup, terms and any requested verification.
2. Add the sender LexiGo and verify its sending address. The proposed support address is lexigolanguage@gmail.com. Brevo determines sender eligibility; listing an address on Contact Us does not verify ownership or make it an approved sender.
3. For the public website, use an owned sending domain and authenticate it using the DNS records Brevo supplies. We cannot authenticate gmail.com because we do not own its DNS. If Brevo permits a verified Gmail sender for initial testing, use only that approved setup; do not promise it is suitable for production delivery.
4. In Settings → SMTP & API → SMTP, obtain the SMTP login and generate an SMTP key. This differs from an API key and the account password. Keep the key private; do not paste it into chat or commit it.

## Local credential configuration

From the repository root, after the sender is approved:

```powershell
./scripts/Configure-Brevo.ps1 -SmtpLogin 'YOUR_SMTP_LOGIN' -SenderAddress 'YOUR_VERIFIED_SENDER'
```

The script prompts for the SMTP key using hidden input and passes settings to dotnet User Secrets through stdin. It configures smtp-relay.brevo.com:587; the worker requires STARTTLS. It leaves external delivery disabled. User Secrets is a local development store, not an encrypted production vault. Production credentials belong in the deployment's protected secret mechanism.

Do not print `dotnet user-secrets list` into chat/logs. To rotate a key, rerun the script with a newly generated SMTP key, then revoke the old one in Brevo after the replacement is verified.

## Activation, separate from preparation

- Confirm the Brevo account/sender are approved and the intended recipient explicitly agrees to a test. No new paid service or purchase is required by this preparation.
- Review the pending outbox before enabling: old test addresses and expired confirmation emails must not be sent accidentally. Do not delete queued data silently. The worker retries pending unsent rows when enabled.
- Stop the API before changing transport. Enable `Smtp:DeliveryEnabled` in local secrets only when the sender, credentials and test recipient are ready, then restart. Do not start a competing API in Visual Studio.
- Register the agreed test address and check actual receipt plus confirmation. Never infer delivery merely from a successful registration response. Inspect Brevo delivery results without exposing keys, links or recipient data.
- The current WebOrigin is http://localhost:4200. A received link works only on a computer running this local frontend. Before inviting outside users, set AccountEmail:WebOrigin to the deployed HTTPS origin and verify that flow there. Do not expose the development server publicly to work around this.

## Return to Mailpit

Set only the following non-secret overrides, then restart the Development API:

```powershell
dotnet user-secrets set 'Smtp:Host' 'localhost' --project backend/LanguageLearning.Api
dotnet user-secrets set 'Smtp:Port' '1025' --project backend/LanguageLearning.Api
dotnet user-secrets set 'Smtp:SenderAddress' 'hello@lexigo.test' --project backend/LanguageLearning.Api
dotnet user-secrets remove 'Smtp:Username' --project backend/LanguageLearning.Api
dotnet user-secrets remove 'Smtp:Password' --project backend/LanguageLearning.Api
dotnet user-secrets set 'Smtp:DeliveryEnabled' 'true' --project backend/LanguageLearning.Api
```

External configuration is validated on startup: hostname, port, sender and credentials must be valid before delivery is enabled; test-domain senders and production loopback SMTP are rejected. Validation messages never include credential values. Existing retries, redacted logs and one-use confirmation tokens remain in place.

References: [Brevo SMTP configuration](https://help.brevo.com/hc/en-us/articles/7924908994450-Send-transactional-emails-using-Brevo-SMTP), [SMTP keys](https://help.brevo.com/hc/en-us/articles/7959631848850-Create-and-manage-your-SMTP-keys), [sender and delivery troubleshooting](https://help.brevo.com/hc/en-us/articles/115000188150-Troubleshooting-Issues-with-Brevo-SMTP).
