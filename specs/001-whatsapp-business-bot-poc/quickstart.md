# Quickstart de validación: POC de Bot de WhatsApp Business

This guide distinguishes the repository behavior currently validated from the approved POC target.
The current local slice covers solution/bootstrap, bot configuration/FAQ, Meta Embedded Signup,
signed text-webhook/outbox processing and read-only Admin conversation visibility. The approved
POC target additionally includes OpenAI, audio, image, Bot/Humano attention, manual Admin replies,
conversation lifecycle control and tenant-aware isolation. Coexistence history synchronization
remains future work.

## Scope status

- **Implementado o validado localmente**: bootstrap, autenticación Admin, configuración del bot,
  respuestas frecuentes, Embedded Signup/estado de integración, webhook firmado, inbox/outbox,
  procesamiento de texto/FAQ y consulta de conversaciones.
- **Dentro de esta POC, pendiente de implementación**: OpenAI, audio, imagen, storage privado con
  expiración inicial de 30 días, toma/handoff, respuesta manual, devolución al bot,
  cierre/reapertura, auditoría, membresías de tenant y asignación simple.
- **Fase posterior explícita**: `history`, `smb_app_state_sync`, `smb_message_echoes`,
  sincronización de contactos, recuperación histórica, video, documentos, ubicación, contactos y
  otros tipos multimedia.

La documentación de objetivo no debe interpretarse como evidencia de que los elementos pendientes
ya existan en el código.

## Prerequisites

- .NET 10 LTS SDK (latest supported 10.0.x servicing patch).
- ASP.NET Core HTTPS development certificate trusted on the local machine
  (`dotnet dev-certs https --trust`) so Admin can call Api over HTTPS.
- SQL Server LocalDB and `sqlcmd` for local Development. Production uses its configured SQL
  Server environment; LocalDB is not a production requirement.
- Meta Developer app with WhatsApp Cloud API, Embedded Signup v4 configuration, HTTPS webhook,
  required permissions/advanced access, approved test number and permitted test recipients.
- OpenAI API account/key and configured text/vision and transcription model identifiers.
- FFmpeg available to the API worker for OGG/Opus normalization; private media storage.
- User Secrets locally; production secret-store adapter configured before any live customer
  data is processed.
- Owner approval for the initial 30-day media retention, Meta opt-in/message policy, and AI
  provider data processing before real customer data is processed.

## Estado de validación real (2026-09-30)

La configuración real de Meta ya está preparada: Developer App, WhatsApp Cloud API, Facebook
Login for Business, Embedded Signup, App ID, App Secret, Verify Token, configuración de signup,
Graph API v26.0, PIN de registro, administrador inicial y acceso autenticado al Admin. Los
valores secretos están configurados fuera del repositorio y no se documentan aquí.

El endpoint HTTPS público mediante ngrok fue probado con Meta: la verificación GET respondió
HTTP 200 y un POST firmado respondió HTTP 200 con validación de `X-Hub-Signature-256`. El
middleware `ForwardedHeaders` de [Program.cs](../../src/WhatsAppBot.Api/Program.cs) permite el
ingreso detrás del proxy y debe conservarse.

El popup de Embedded Signup abre desde Admin. La prueba real se detuvo cuando Meta indicó que
el número seleccionado ya pertenece a WhatsApp Business App. Por ello se incorpora el flujo
Coexistence, pero la vinculación real del mismo número aún debe probarse. La verificación
empresarial de Meta continúa pendiente/en revisión. Todavía **no** se ha validado la recepción
ni el envío de mensajes del bot por WhatsApp real.

## Local setup and first administrator

1. Open PowerShell and verify the LocalDB utility and instance:

   ```powershell
   Get-Command sqllocaldb
   sqllocaldb info MSSQLLocalDB
   ```

   If `MSSQLLocalDB` is not listed, create the standard development instance; then ensure it
   is running:

   ```powershell
   sqllocaldb create MSSQLLocalDB -s
   sqllocaldb start MSSQLLocalDB
   sqllocaldb info MSSQLLocalDB
   ```

   Confirm `sqlcmd` is available with `Get-Command sqlcmd`. Use the Windows identity running
   the commands; no SQL username/password is needed.
2. The Development connection is configured under `ConnectionStrings:WhatsAppBot` in
   `src/WhatsAppBot.Api/appsettings.Development.json`. JSON requires the instance backslash
   to be escaped:

   ```json
   {
     "ConnectionStrings": {
       "WhatsAppBot": "Server=(localdb)\\MSSQLLocalDB;Database=WhatsAppBot;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

   The conceptual (non-JSON-escaped) value is
   `Server=(localdb)\MSSQLLocalDB;Database=WhatsAppBot;Trusted_Connection=True;TrustServerCertificate=True;`.
   This is Development-only. Do not put a connection string in code. Admin has no database
   setting and connects exclusively to Api.
3. Create/prepare the database from a clean LocalDB instance and apply the versioned database,
   tables, relationships, indexes, Stored Procedures, and required seed scripts in dependency
   order:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\sql\Initialize-DevelopmentDatabase.ps1 -ServerInstance '(localdb)\MSSQLLocalDB' -Database WhatsAppBot
   ```

   The initializer first creates `WhatsAppBot` using Windows authentication, then applies the
   ordered SQL scripts under `src/WhatsAppBot.Api/Persistence/StoredProcedures/`. It must be
   safe to rerun against the development database and must fail explicitly if any script fails.
4. Validate the instance, database, and initialized schema:

   ```powershell
   sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d WhatsAppBot -Q "SELECT DB_NAME() AS DatabaseName; SELECT COUNT(*) AS UserTableCount FROM sys.tables WHERE is_ms_shipped = 0; SELECT COUNT(*) AS StoredProcedureCount FROM sys.procedures WHERE is_ms_shipped = 0;"
   ```

   Confirm `DatabaseName` is `WhatsAppBot`, the user-table count is greater than zero, and the
   stored-procedure count includes the feature's required procedures. If validation fails,
   inspect the initializer's reported script and error, then rerun after correction.
5. Configure a local API signing key and the initial administrator email in the API project's
   User Secrets. Generate the signing key locally; do not copy it into the repository:

   ```powershell
   $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
   $bytes = New-Object byte[] 48
   $rng.GetBytes($bytes)
   $signingKey = [Convert]::ToBase64String($bytes)
   $rng.Dispose()
   dotnet user-secrets set "Authentication:SigningKey" $signingKey --project .\src\WhatsAppBot.Api
   Remove-Variable signingKey, bytes, rng
   dotnet user-secrets set "Bootstrap:AdminEmail" "admin@example.test" --project .\src\WhatsAppBot.Api
   ```

   Provision exactly one administrator with the one-time bootstrap command. It prompts for a
   unique initial password of at least 14 characters without echoing it. Never pass a password as a command-line
   argument or place it in source, screenshots, shell history, or logs.

   ```powershell
   dotnet run --project .\src\WhatsAppBot.Api -- admin bootstrap
   ```

   The command must hash the password with an adaptive password hasher, create the account only
   when none exists, report that an administrator already exists on later invocations, never
   print the password, and refuse to overwrite an existing administrator.
   For a non-interactive deployment, inject `BOOTSTRAP_ADMIN_PASSWORD` only through the
   approved protected environment/secret store for that one-time process, then remove/revoke it.
   Remove the local bootstrap email after success:

   ```powershell
   dotnet user-secrets remove "Bootstrap:AdminEmail" --project .\src\WhatsAppBot.Api
   ```

   Do not seed a default password or commit credentials.
6. Configure `AdminApi:BaseUrl` in `src/WhatsAppBot.Admin/appsettings.json` only if the API is
   hosted at a different URL. The local defaults are HTTPS `https://localhost:7001` for Api
   and `https://localhost:7002` for Admin; Admin stores its API access token in server-side
   session storage and never sends database credentials to the browser.

7. Before connecting Meta, create/configure the Meta app as described in
   [Meta Developers setup](#meta-developers-setup). Then configure only server-side Meta values
   in the API project's User Secrets:

   ```powershell
   dotnet user-secrets set "Meta:AppId" "<app-id>" --project .\src\WhatsAppBot.Api
   dotnet user-secrets set "Meta:AppSecret" "<secret>" --project .\src\WhatsAppBot.Api
   dotnet user-secrets set "Meta:VerifyToken" "<random-secret>" --project .\src\WhatsAppBot.Api
   dotnet user-secrets set "Meta:EmbeddedSignupConfigId" "<config-id>" --project .\src\WhatsAppBot.Api
   dotnet user-secrets set "Meta:CoexistenceEmbeddedSignupConfigId" "<optional-config-id>" --project .\src\WhatsAppBot.Api
   dotnet user-secrets set "Meta:GraphApiVersion" "<supported-version>" --project .\src\WhatsAppBot.Api
   dotnet user-secrets set "Meta:PhoneNumberRegistrationPin" "<six-digit-pin>" --project .\src\WhatsAppBot.Api
   ```

   Replace placeholders only in the local secret store. The business access token is exchanged
   server-to-server from Embedded Signup and written to User Secrets; SQL stores only its
   generated secret reference. Never paste values into source, screenshots, logs, or this guide.
   `Meta:CoexistenceEmbeddedSignupConfigId` is optional; when absent, Coexistence uses the
   configured Standard ID with its explicit onboarding feature option.
   The Meta `AppId`, Signup configuration ID, and Graph version are public SDK configuration;
   App Secret, Verify Token, registration PIN and business token stay server-side.
8. Restore and build:

   ```powershell
   dotnet restore .\WhatsAppBot.sln
   dotnet build .\WhatsAppBot.sln --configuration Release
   ```

9. Run Api and Admin in separate terminals:

   ```powershell
   dotnet run --project .\src\WhatsAppBot.Api
   dotnet run --project .\src\WhatsAppBot.Admin
   ```

   Confirm Swagger is available at `https://localhost:7001/swagger` only in Development, and
   Admin loads over HTTPS. The Dashboard is a basic status summary; bot settings and frequent
   responses are managed from the Configuration navigation.

10. Use Configuración → Bot to activate/deactivate the bot and select `FaqOnly`, `FaqThenAi`,
     or `AiOnly`. The default remains `FaqThenAi`; the OpenAI adapter is provider-neutral and
     uses the configured secret reference only when AI is required. Without model/credential
     configuration, AI-required messages record a safe outcome and hand off to Humano rather
     than inventing content. Automated tests use a stub; real provider validation remains a
     separate live gate.
11. Use Configuración → WhatsApp Business and complete the official Embedded Signup flow. Select
    Standard or Coexistence explicitly. The backend exchanges the one-time code, validates a
    supplied Phone Number ID or discovers it only when the WABA match is unambiguous, stores the
    token by secret reference, subscribes the app to the WABA, and only then reports Connected.
    Standard performs number registration; Coexistence skips Standard `/register`. Coexistence
    uses `Meta:CoexistenceEmbeddedSignupConfigId` when configured and otherwise falls back to
    `Meta:EmbeddedSignupConfigId`. A canceled or incomplete flow is never reported as connected.
12. Use Configuración → Respuestas Frecuentes to create, edit, activate/deactivate, and delete
    FAQ responses. Put one literal word or phrase per line. The matcher applies Unicode Form KC,
    trims and collapses whitespace, compares invariant case-insensitive literal containment,
    selects the highest priority, and breaks ties by ascending stable response ID. This local
    configuration slice requires neither Meta nor OpenAI credentials.
13. Run automated checks. Persistence tests are guarded by an explicit Development LocalDB
    connection string and reject non-LocalDB targets:

    ```powershell
    $env:WHATSAPPBOT_TEST_CONNECTION_STRING = 'Server=(localdb)\MSSQLLocalDB;Database=WhatsAppBot;Trusted_Connection=True;TrustServerCertificate=True;'
    dotnet test .\WhatsAppBot.sln --configuration Release
    Remove-Item Env:WHATSAPPBOT_TEST_CONNECTION_STRING
    ```

## Approved POC target validation (pending implementation)

The following sequence describes the approved target and must not be reported as complete until
its implementation and evidence exist:

1. Select a tenant through an authenticated Admin membership and verify that integrations,
   configuration, conversations, messages and media cannot cross the tenant boundary. Use
   `AssignedAdminId` for simple responsibility; do not add teams or queues.
2. Receive a new text, audio and image message. Persist the same conversation identity, show
   actor/origin, process text/audio/image through the configured FAQ/OpenAI rules, and expose
   private media through authorized access only.
3. Take a conversation from Bot to Humano, send a manual text reply, return it to Bot, close it,
   and send a new inbound message. Verify the same conversation reopens as Abierta + Bot when the
   bot is enabled, with audit records and no concurrent Bot/Humano send.
4. Verify automatic handoff for customer request, bot inability, business rule and blocking error;
   each reason must be visible and auditable.
5. Verify media expires after the initial 30-day policy without deleting the message, allowed
   metadata or available transcript. Keep the retention period configurable for future policy
   changes.
6. Verify Coexistence processes connection and new messages only. `history`,
   `smb_app_state_sync`, `smb_message_echoes`, contact synchronization and historical recovery
   remain ignored/deferred and do not create fabricated messages or replies.

## Meta Developers setup

1. Confirm the organization is an approved Meta **Tech Provider** or **Solution Partner** (or
   partnered with one) and has completed the access verification applicable to its account.
   Meta's current Embedded Signup implementation guide lists this as a prerequisite. Create or
   select a Meta app associated with the business portfolio and add the official **WhatsApp**
   product. Use Cloud API only; do not use WhatsApp Web automation or client simulators.
2. In Facebook Login for Business, create a v4 configuration from Meta's **WhatsApp Embedded
   Signup Configuration With 60 Expiration Token** template and select the Cloud API product
   only. The flow selects its required assets/permissions (`whatsapp_business_management` and
   `whatsapp_business_messaging`). Complete applicable business verification and App
   Review/Advanced Access. Record the public App ID and Standard configuration ID. To offer
   Coexistence, enable WhatsApp Business app user onboarding for the configuration in Meta and
   optionally provide a dedicated configuration ID as
   `Meta:CoexistenceEmbeddedSignupConfigId`; otherwise the existing
   `Meta:EmbeddedSignupConfigId` is used with the Coexistence `featureType`.
   See Meta's [Embedded Signup v4](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/version-4)
   and [WhatsApp Business app user onboarding](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/onboarding-business-app-users)
   guidance for eligibility and feature enablement.
3. In Facebook Login for Business settings, enable Client OAuth, Web OAuth, Enforce HTTPS,
   Embedded Browser OAuth and JavaScript SDK login. Add the HTTPS Admin origin to Allowed
   Domains and Valid OAuth Redirect URIs. Only test from that registered HTTPS origin.
4. For Standard Cloud API onboarding, choose a six-digit WhatsApp phone-number registration PIN
   for the selected business number and set it only as `Meta:PhoneNumberRegistrationPin` in API
   User Secrets. Complete the official number-ownership verification in Embedded Signup; do not
   type a phone number into the app as a substitute for Meta authorization or verification.
   Coexistence does not run the Standard `/register` operation because the number is already
   registered in WhatsApp Business App.
5. Expose the API over a stable **public HTTPS** hostname. Local `https://localhost:7001` is
   not reachable by Meta; use an approved HTTPS tunnel or deploy a development API endpoint.
   Register that exact callback URL in the app's WhatsApp **Webhooks** configuration:
   `https://<public-api-host>/webhooks/meta/whatsapp`
6. Generate a random Verify Token, set it in API User Secrets as `Meta:VerifyToken`, and enter
   the same value in Meta's webhook verification form. Subscribe the app to the `account_update`
   and `messages` webhook fields. Set the callback URL and Verify Token, then run Meta's GET
   verification test.
   The POST endpoint validates `X-Hub-Signature-256` with the server-only App Secret before
   parsing or persisting allowlisted event fields.
7. Start the API and Admin, sign in, and select Configuración → WhatsApp Business. Choose
   **Standard Cloud API** for a new Cloud API number, or **WhatsApp Business App Coexistence**
   for a number already active in WhatsApp Business App. Complete the Meta-hosted authorization
   and number verification. Standard verifies the selected Phone Number ID belongs to the WABA,
   retrieves its number details, registers it, and subscribes the app to the WABA. Coexistence
   accepts Meta's `FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING` session event, verifies a supplied
   Phone Number ID or discovers it server-side, skips Standard registration, and subscribes the
   app to the WABA. If Meta supplies no Phone Number ID, the server proceeds only when the WABA
   has exactly one phone number; zero or multiple numbers fail safely instead of choosing one.
   Confirm the Admin shows WABA ID, Phone Number ID and the verified business number as
   connected.
8. From an opted-in Meta test recipient, start a customer-initiated WhatsApp conversation and
   send a text matching an active FAQ while the 24-hour customer-service window is open. The
   signed webhook is durably added to inbox, the inbound text is persisted, Business checks
   reply mode and policy, and only an allowed FAQ candidate enters the SQL outbox. The outbox
   worker rechecks the window before using the official Cloud API sender.
9. This milestone deliberately does not call OpenAI: `FaqOnly` misses become no-reply,
   `FaqThenAi` misses and `AiOnly` are stored as `AiFallbackNotConfigured` no-reply outcomes.
   Free-form FAQ replies are not Meta templates and cannot bypass a closed window. No
   template-authoring or template-sending UI is implemented.

### Coexistence prerequisites and POC limits

- The business must be eligible for Meta's WhatsApp Business app user onboarding and already
  operate the number in WhatsApp Business App version 2.24.17 or later. Meta partner/Tech
  Provider eligibility and business verification are external account gates.
- Meta's v4 documentation identifies `feature_type` as the way to enable WhatsApp Business
  app onboarding. The Admin sends the SDK `featureType` value
  `whatsapp_business_app_onboarding` when Coexistence is selected, while preserving `setup: {}`
  and the existing Standard flow. Confirm that the corresponding feature is enabled for the
  selected Meta Login for Business configuration before live testing. The public guide does not
  show the exact JavaScript field casing/value in its sample; if Meta rejects this option, verify
  the current SDK/configuration instructions with Meta before retrying.
- The POC does not request or process `history`, `smb_app_state_sync`, or `smb_message_echoes`
  webhook data and does not synchronize WhatsApp Business App contacts/history. These events
  are ignored safely; only new `messages` and status events are in scope. New text, audio and
  image messages follow the approved POC pipeline; historical recovery remains deferred.
- Meta documents a 20 messages-per-second fixed throughput for numbers shared by WhatsApp
  Business App and Cloud API, as well as Cloud API pricing and coexistence-specific limitations.
  Check the current Meta documentation before production use.

## Validation scenarios

### 1. Administrator login and API boundary

- Sign in through Admin with the provisioned administrator.
- Verify an invalid/inactive user gets a generic authentication error.
- Verify Admin makes API requests only; no SQL connection or SQL credentials exist in Admin.
- Confirm API errors use `ResponseE<T>` and include a trace/correlation ID without stack traces,
  secrets, or SQL details.
- Before configuring Meta, open the local bot settings and verify the default is
  `FaqThenAi`; save `FaqOnly` and `AiOnly` and confirm each setting persists after reload.
- Create a local FAQ response, verify edit/activate/deactivate/delete and matching with the
  configured mode. This local slice requires SQL Server but no Meta or OpenAI account.

### 2. Meta Embedded Signup and number state

- Start the Embedded Signup v4 flow from Admin using a Meta test business.
- Authorize, select/create WABA, register/verify the business phone number and complete the
  official Cloud API setup.
- Verify the one-time code is exchanged only by Api and WABA ID, Phone Number ID, number,
  display name and connection state appear in Admin.
- Confirm that neither the browser nor Admin DTOs contain App Secret, Verify Token, or a
  reusable access token.
- Exercise cancel, missing permission, failed registration, and webhook subscription failure;
  verify the UI never reports these as Connected.

### 3. Webhook integrity, idempotency, and status

- Configure Meta's webhook URL and verify token; a matching GET returns the challenge and a
  non-matching value is rejected.
- Send a valid signed test POST and verify the event is durably recorded before a 200 response.
- Alter the body/signature and verify rejection; replay the same inbound `wamid` and confirm
  only one message and one reply are created.
- Deliver sent/delivered/read/failed status events, including out-of-order events, and verify
  the conversation reflects only known provider statuses.

### 4. Frequent response modes and Admin management

- In Configuración → Respuestas Frecuentes, create, edit, activate, deactivate and delete a
  response with multiple expressions. Verify timestamps and category/priority.
- Use consented test text with different casing/spacing and confirm normalized expression
  containment. Create multiple applicable rules and confirm highest priority wins, then stable
  response-ID tie-break.
- In `FaqOnly`, confirm a hit uses the configured answer and a miss is recorded with `NoReply`
  without calling AI; in default `FaqThenAi`, confirm a hit uses the configured answer without
  calling AI and a miss calls AI; in `AiOnly`, confirm FAQ lookup is skipped and AI processes
  the message.
- Inspect the conversation timeline and verify configured answers are labeled
  `FrequentResponse`, separate from Meta template messages.

### 5. Text, audio, image, and outbound delivery

- From a customer-initiated test conversation still inside Meta's permitted customer-service
  window, send a text message and verify persisted inbound content, mode-correct FAQ/AI answer,
  provider message ID, and delivery status lifecycle.
- Expire/age a test conversation beyond the permitted free-form messaging window or simulate a
  policy response requiring an official template. Verify FAQ and AI free-form replies are both
  blocked, the inbound result has `NoReply` with `MessagingWindowClosed` or `TemplateRequired`,
  and the Admin never labels an unsent answer as sent. Do not configure a template to bypass
  this POC limit.
- Send a consented OGG/Opus voice note; verify safe download, bounded conversion, transcription,
  all three response modes against the transcript, fallback only in `FaqThenAi`, and transcript
  display.
- Send a consented photo; verify private storage, AI analysis, generated response, authorized
  image viewing and no public media URL.
- Cause provider timeout, unsupported/oversized media, transcription failure, image-analysis
  failure, and outbound send failure. Verify visible failure states and that a failed/generated
  response is not presented as sent.
- Confirm disabled bot stores inbound messages but sends no automatic response.

### 6. Human attention, tenant isolation, and retention

- Verify an administrator can take a conversation, switch it to Humano, send a manual text
  message, return it to Bot, close it, and reopen it. Confirm the same conversation identity,
  audit actor/timestamps, and no automatic Bot send while Humano is active.
- Trigger automatic handoff for customer request, bot inability, business rule and blocking
  error. Confirm the reason is allowlisted and visible in the audit timeline.
- Use two tenants and multiple administrators. Confirm memberships authorize access only to the
  selected tenant, `AssignedAdminId` is visible, and conversations, configuration, messages and
  media cannot cross tenant boundaries.
- Verify raw audio/photo objects expire after the initial 30-day policy while the message,
  permitted metadata and available transcript remain in the history. Do not add teams or queues.
- Search application logs and browser responses for App Secret, Verify Token, Meta business
  token, OpenAI API key, full webhook bodies and raw media; none may appear.
- Run end-to-end validation only against Meta test assets and consented recipients until Meta
  review, opt-in policy, AI data terms, and current .NET 10 LTS servicing are verified.

### 7. Retention and release readiness

- Verify raw audio/photo objects expire according to the initial 30-day retention policy or a
  later configured value, with no orphaned public links and without deleting message history.

### 8. Reproducible SC-006 timed check

- Seed or select one connected integration with a known bot state and at least one recent
  conversation. Start a stopwatch when the authenticated administrator opens the Admin home
  page. Stop it when the administrator correctly identifies the WhatsApp connection state,
  bot enabled/disabled state, and latest conversation activity time.
- Repeat the same check five times with the same seeded state and test account. Record each
  elapsed time and correctness result in the demo validation evidence. SC-006 passes only when
  all five attempts are correct and each takes less than 60 seconds.

## Demo dependency matrix

| Demo step | External real service required |
|---|---|
| Admin UI, FAQs, modes, matching, API/database contract | No Meta/AI account; local SQL Server required |
| Meta Embedded Signup and real number connection | Meta app/configuration, eligible WABA/number and required permissions |
| Inbound and outbound WhatsApp | Public HTTPS webhook, Meta Cloud API and test/customer WhatsApp account |
| Audio transcription and image analysis | Meta media retrieval, private storage, FFmpeg, OpenAI |
| Human attention and manual Admin reply | Approved POC controls, tenant memberships, Meta outbound policy |
| Tenant isolation and assignment | Tenant-scoped test accounts and multiple Admin memberships |
| Delivery/read status display | Meta status webhooks; exact status depends on provider events |
