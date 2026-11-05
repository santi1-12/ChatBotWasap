# Live Standard Embedded Signup Validation

Validation date: 2026-11-05
Scope: Standard Cloud API only. Coexistence was not exercised.
Status: Blocked for strict T133 completion.

## T133 Criterion

`specs/001-whatsapp-business-bot-poc/tasks.md` defines T133 as:

> Validate Standard Embedded Signup with Meta real; done when authorization, Phone Number ID validation, registration, WABA subscription and truthful Connected state are evidenced.

## Live Evidence Revalidated

The existing integration was not disconnected, deleted, replaced, or modified.

- LocalDB `dbo.sp_WhatsAppIntegration_GetCurrent` after the controlled API/Admin restart returned IntegrationId `08FFC990-3C9F-4033-81CD-D74E2F02B61D`, TenantId `3B5776B5-E44A-479B-BCBA-4772254D3DEB`, WABA `937023445764944`, Phone Number ID `1346146225253204`, business number `+57 321 5644731`, display name `AsnetBot`, `ConnectionState=Connected`, `OnboardingType=Standard`, `IsActive=1`, and `LastConnectionErrorCode=NULL`.
- The persisted connection timestamp remains `2026-10-22 17:52:29.981 UTC`; no new signup timestamp was created during this validation.
- The effective Tenant is `Initial Tenant`, active, with `TenantId=3B5776B5-E44A-479B-BCBA-4772254D3DEB`.
- The persisted bot configuration remains enabled with reply mode `FaqThenAi`.
- Meta Graph API `v26.0` returned HTTP `200` for WABA details and the expected WABA ID.
- Meta Graph API `v26.0` returned HTTP `200` for WABA phone numbers, including Phone Number ID `1346146225253204`, `+57 321 5644731`, and verified name `AsnetBot`.
- Meta Graph API `v26.0` returned HTTP `200` for WABA subscribed apps; the WhatsApp Business API app ID matched the configured Meta app.
- Local webhook verification returned invalid-token `403`, valid challenge `200`, and signed no-op POST `200`.
- Public webhook verification through `https://flail-salvation-posh.ngrok-free.dev` returned invalid-token `403`, valid challenge `200`, and signed no-op POST `200`.
- API Development restarted cleanly on `https://localhost:7001`; Swagger returned HTTP `200` after restart.
- Admin Development restarted cleanly on `https://localhost:7002`; both `/Settings/WhatsApp` and `/Dashboard` are reachable and redirect unauthenticated requests to `/Account/Login` as expected. The authenticated Admin screen supplied during this validation showed `Conectado`, `Standard`, `Bot Activo`, and the persisted WABA/Phone Number values.
- After the final Admin-only Debug rebuild/restart, the Admin session was renewed and the operator visually confirmed that `/Dashboard` and `/Settings/WhatsApp` loaded correctly, continued to show the operational Standard integration, and required no disconnect or replacement.
- The transient Admin errors before session renewal were caused by the Admin in-memory session invalidated by the restart, not by a Meta, WABA, Phone Number, webhook, or persistence failure.

## Historical Embedded Signup Evidence

- The current database row is the result of the historical Standard completion persisted on `2026-10-22`.
- Its persisted `Connected` state, Standard onboarding type, secret references, WABA, Phone Number ID, and subsequent live Graph/webhook checks provide evidence of the resulting operational connection.
- No authorization code, callback payload, or registration request from the 2026-10-22 flow is available as a safe repository artifact.

## Strict Completion Decision

T133 remains unchecked in `specs/001-whatsapp-business-bot-poc/tasks.md`.

The live resulting state is revalidated, but a new Standard Embedded Signup authorization callback and its registration step were not repeated. Repeating them would require disconnecting, deleting, replacing, or otherwise altering the operational integration, which is explicitly prohibited for this validation. Therefore the missing new callback is the only remaining T133 criterion under the strict interpretation; no other failure or runtime issue was found.
