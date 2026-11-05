# Live Coexistence Embedded Signup Validation

Validation date: 2026-11-05
Scope: WhatsApp Business App Coexistence only. Standard is not a substitute.
Status: Blocked before onboarding; T134 remains unchecked.
Implementation status: T134 implementación local completa / validación live pendiente.

## T134 Criterion

`specs/001-whatsapp-business-bot-poc/tasks.md` defines T134 as:

> Implement and validate Coexistence Embedded Signup with Meta real; done when an eligible
> WhatsApp Business App number completes onboarding without Standard `/register`, explicit history
> consent imports only Meta-delivered historical messages idempotently without bot/outbox work,
> groups are skipped, `smb_message_echoes` records human outbound activity with Human priority,
> and available history limits are evidenced.

## Read-Only Audit

- The only integration row in the development database is IntegrationId `08FFC990-3C9F-4033-81CD-D74E2F02B61D` for TenantId `3B5776B5-E44A-479B-BCBA-4772254D3DEB`.
- The current number is `+57 321 5644731`, Phone Number ID `1346146225253204`, WABA `937023445764944`, `ConnectionState=Connected`, and `OnboardingType=Standard`.
- The current Graph API phone metadata is valid and reports `platform_type=CLOUD_API`, `quality_rating=GREEN`, and `code_verification_status=EXPIRED`. These fields do not prove that the number is currently active in WhatsApp Business App or eligible for Coexistence.
- No separate Coexistence candidate number is configured or persisted in the repository/database. The intended candidate under the requested same-number scenario would therefore be the currently connected Standard number, but using it would require changing the active integration.
- The current Admin page exposes only `Desconectar WhatsApp` because the integration is complete. The server-side `WhatsAppIntegrationService` also rejects a new signup while `HasCompleteConnection` is true and requires disconnecting the current number first.
- `Meta:CoexistenceEmbeddedSignupConfigId` is not configured as a dedicated secret. The application now refuses to start the Coexistence path without that dedicated configuration; Meta-side enablement and eligibility are not confirmed by configuration presence alone.
- The current WABA subscription and webhook are operational, but that is evidence for the existing Standard connection, not evidence of Coexistence onboarding.

## Coexistence Implementation Boundary

- The Admin JavaScript sends `featureType=whatsapp_business_app_onboarding` and expects `FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING` for Coexistence.
- The server requires the dedicated Coexistence configuration and never reuses the Standard configuration.
- The adapter validates the WABA/Phone Number relationship, obtains phone details, skips Standard `/register`, subscribes the WABA, stores only a secret reference, and persists `OnboardingType=Coexistence` only after confirmation.
- Automated integration coverage verifies that the Coexistence branch does not call `register-phone`.
- `history` is accepted only after persisted consent and uses a separate idempotent, progress-tracked path with no bot/outbox work. `smb_message_echoes` is accepted as outbound human activity and moves the conversation to Humano priority. `smb_app_state_sync` remains ignored.

## Actions Not Performed

- No Embedded Signup flow was launched.
- No authorization code was obtained or submitted.
- No Coexistence callback was fabricated.
- No `/register` call was made.
- No WABA, Phone Number, credential, database row, chat, or Standard connection was modified.
- No mobile-app, customer, bot, delivery, AttentionMode, or echo scenario was claimed as validated.

## Blocking Decision

T134 remains unchecked. The live Coexistence validation cannot safely proceed with the current state because the only available number is already connected as Standard and the implementation intentionally prevents replacing a complete connection. The current Graph response also does not establish Business App registration or Coexistence eligibility. Local implementation and automated coverage are not live evidence.

Before any onboarding attempt, a manual decision is required: confirm an eligible WhatsApp Business App number and Meta-side Coexistence enablement, and provide a safe integration/Tenant slot that does not alter the current Standard connection. If the intended candidate is the current number, the required Meta/application transition must be explicitly authorized before any irreversible action; it will not be performed implicitly.
