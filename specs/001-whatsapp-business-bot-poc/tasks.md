# Tasks: POC de Bot de WhatsApp Business

**Input**: `spec.md`, `plan.md`, `research.md`, `data-model.md`,
`contracts/openapi.yaml`, and `quickstart.md` in this feature directory.

**Order**: `T001-T098` is preserved as the historical implementation baseline. The approved
extension begins at `T099` with the Tenant/Company model, safe migration, conversation states,
Bot/Humano concurrency, human actions, OpenAI, audio, image, private storage, complete
conversations, and final validation. Standard and Coexistence remain supported for new messages;
Coexistence history and `smb_*` synchronization are explicitly deferred.

**Approved POC scope**: Tenant/Company and memberships; one active WhatsApp number per Tenant as
an operational limit only; future multiple integrations per company; `AttentionMode` Bot/Humano;
`LifecycleStatus` Abierta/Cerrada; human priority, handoff, manual replies, reopen, audit,
OpenAI, text, audio, image, private media and initial 30-day retention.

**Explicitly deferred**: `history`, `smb_app_state_sync`, `smb_message_echoes`, historical
contact/conversation synchronization, video, documents, location, teams, queues, campaigns,
operational templates, plans and operational feature flags. No task from `T099-T138` implements
those deferred capabilities.

**Task format**: Every task has an ID, explicit `Dependencies`, expected file path(s), and
an observable completion condition. `[P]` means the task can proceed concurrently with other
ready `[P]` tasks because it does not share their output files.

Tasks `T001-T098` retain their original meaning and completion markers. New tasks are extensions
or corrections and must not be used to reinterpret historical tasks as if they originally
covered the current Tenant, Bot/Humano, AI or multimedia scope.

## Phase 1: Setup — Solution and Host

- [X] T001 Create `WhatsAppBot.sln` at repository root targeting .NET 10 LTS; Dependencies: None; done when the solution file exists and `dotnet sln WhatsAppBot.sln list` succeeds.
- [X] T002 Create the `src/WhatsAppBot.Admin`, `src/WhatsAppBot.Api`, and `src/WhatsAppBot.Shared` projects targeting `net10.0`; Dependencies: T001; done when each project builds independently.
- [X] T003 Configure references so Admin and Api consume Shared, Admin has no SQL dependency/configuration, and Api does not reference Admin in the three `.csproj` files; Dependencies: T002; done when the project-reference graph complies with `plan.md`.
- [X] T004 [P] Define validated configuration Options types for API, Meta, SQL, AI, media storage, and Admin API URL in `src/WhatsAppBot.Api/Infrastructure/Options/` and `src/WhatsAppBot.Admin/Configuration/`; include the SQL connection name `ConnectionStrings:WhatsAppBot` and support Development LocalDB `(localdb)\MSSQLLocalDB` / `WhatsAppBot` / Windows Trusted Connection; Dependencies: T002; done when types define configuration shape/validation only, do not embed connection strings or provider secret values, and contain no configuration-provider setup.
- [X] T005 [P] Add User Secrets identifiers to `src/WhatsAppBot.Api/WhatsAppBot.Api.csproj` and `src/WhatsAppBot.Admin/WhatsAppBot.Admin.csproj` and document the local secret key names in `specs/001-whatsapp-business-bot-poc/quickstart.md`; Dependencies: T002; done when Api's secret keys are stored only in User Secrets and Admin holds only its non-secret Api URL.
- [X] T006 Add required ASP.NET Core, RestSharp, SqlClient, and Swagger/OpenAPI packages to `src/WhatsAppBot.Api/WhatsAppBot.Api.csproj` and `src/WhatsAppBot.Admin/WhatsAppBot.Admin.csproj`; Dependencies: T002, T005; done when restore succeeds and each dependency is only in the project that consumes it.
- [X] T007 Configure development-only Swagger/OpenAPI and contract discovery in `src/WhatsAppBot.Api/Program.cs`; Dependencies: T006; done when the API document is reachable only in the intended Development environment.
- [X] T008 Bind and validate Options and configure sources in `src/WhatsAppBot.Api/Program.cs`, `src/WhatsAppBot.Api/Infrastructure/Extensions/`, `src/WhatsAppBot.Api/appsettings.Development.json`, and `src/WhatsAppBot.Admin/Program.cs`; Dependencies: T004, T005, T006, T007; done when Infrastructure binds `ConnectionStrings:WhatsAppBot` and supplies SQL connection resources, Development uses `Server=(localdb)\\MSSQLLocalDB;Database=WhatsAppBot;Trusted_Connection=True;TrustServerCertificate=True;`, deployed environments can provide a SQL Server connection independently, User Secrets load in Development, environment variables load when deployed, HTTPS is configured, and invalid required settings fail explicitly.
- [X] T009 Configure structured logging, trace IDs, and secret/payload redaction in `src/WhatsAppBot.Api/Program.cs`; Dependencies: T008; done when logs are traceable and omit credentials and complete webhook/media bodies.

## Phase 2: Foundational — Architecture, Persistence, and Secure Admin Access

**Purpose**: Provide the blocking foundation for local Admin/configuration/FAQ work. Media
provider/retention approval in T020 is a media-only gate and does not block local FAQ work,
Meta onboarding, or text processing.

- [X] T010 Create API layer structure and namespace boundaries for Middleware, Business, Persistence, Infrastructure, Integrations, Models, and Helpers under `src/WhatsAppBot.Api/`; Dependencies: T002; done when all seven required layers exist as planned.
- [X] T011 Define provider-neutral Business ports for repositories, secret references, Meta, AI text/image/transcription, media storage, and time in `src/WhatsAppBot.Api/Business/Abstractions/`; Dependencies: T010; done when no Business interface references RestSharp, SqlClient, or provider-specific types.
- [X] T012 Add `AddBusiness()`, `AddPersistence()`, `AddInfrastructure()`, and `AddIntegrations()` service extensions in `src/WhatsAppBot.Api/Extensions/` and the layer-specific extension files; Dependencies: T010, T011; done when each extension registers only its own layer.
- [X] T013 Configure `src/WhatsAppBot.Api/Program.cs` as the composition root using the four extensions and host middleware; Dependencies: T007, T008, T009, T012; done when Program has no business use-case or SQL implementation.
- [X] T014 [P] Add `ResponseE<T>`, API error, pagination, and trace DTO contracts in `src/WhatsAppBot.Shared/Responses/`; Dependencies: T002; done when response envelope fields match `contracts/openapi.yaml`.
- [X] T015 [P] Add shared connection, bot mode, direction, message/media, processing/delivery, conversation, and reply-source enums in `src/WhatsAppBot.Shared/Enums/`; Dependencies: T002; done when enum values match the data model and OpenAPI.
- [X] T016 Add internal entity and request/response/contract models in `src/WhatsAppBot.Api/Models/`; Dependencies: T014, T015; done when persistence/domain models are distinct from externally shared DTOs and cover the planned entities.
- [X] T017 Add public request/response DTOs with `DTO` suffix in `src/WhatsAppBot.Shared/DTOs/`, `Requests/`, and `Responses/`; Dependencies: T014, T015; done when API-facing models cover login, bot, FAQ, integration, messages (including allowlisted no-reply `outcomeCode`), and conversations without credential fields.
- [X] T018 Implement global exception handling and safe `ResponseE<T>` mapping in `src/WhatsAppBot.Api/Middleware/GlobalExceptionMiddleware.cs`; Dependencies: T013, T014; done when an unhandled exception produces a traceable safe failure, not stack or provider details.
- [ ] T019 Add API request validation and webhook/body size limits in `src/WhatsAppBot.Api/Middleware/` and `src/WhatsAppBot.Api/Extensions/`; Dependencies: T013, T016, T017; done when malformed/oversized requests are rejected before Business processing.
- [ ] T020 Obtain owner approval for private media storage provider and retention duration and record approved values in `specs/001-whatsapp-business-bot-poc/quickstart.md`; Dependencies: T005; done when both release inputs are explicit before real customer media is processed.
- [X] T021 Implement the Infrastructure secret-store adapter in `src/WhatsAppBot.Api/Infrastructure/Security/`; Dependencies: T004, T010, T011; done when Business/Integrations resolve credentials only through a secret-store interface and persistence stores references rather than secret values.
- [X] T022 Create a clean-install LocalDB database bootstrap and initial SQL Server schema in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Schema/000_CreateDatabase.sql`, `001_InitialSchema.sql`, and `scripts/sql/Initialize-DevelopmentDatabase.ps1`; Dependencies: T016; done when the scripts create `WhatsAppBot` on `(localdb)\MSSQLLocalDB` using Windows authentication, then create Administrator, WhatsAppIntegration, BotConfiguration, FAQ, Contact, Conversation, Message, MediaAttachment, Transcript, WebhookInboxEvent, and MessageOutbox with keys, nullable-before-signup identifiers, UTC audit columns, one-active-number invariant, and default NotConnected/bot-disabled/FaqThenAi configuration, and the initializer applies numbered schema/procedure/required seed scripts in deterministic order while failing on any SQL error.
- [X] T023 Add required unique/query indexes and constraints in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Schema/002_IndexesAndConstraints.sql`; Dependencies: T022; done when FAQ lookup, contact, phone, provider-message idempotency, inbox/outbox, conversation listing, and chronology queries have the modeled indexes.
- [X] T024 Add the FAQ expression TVP type in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Schema/003_TableTypes.sql`; Dependencies: T022; done when FAQ create/update can atomically replace expression sets using the declared SQL type.
- [X] T025 Implement stored-procedure-only SqlClient execution and typed parameter mapping in `src/WhatsAppBot.Api/Persistence/Repositories/StoredProcedureExecutor.cs`; Dependencies: T008, T022; done when Persistence obtains connections from the Infrastructure-provided SQL connection factory/DataSource using `ConnectionStrings:WhatsAppBot`, executes named procedures only, and no SQL connection/configuration or execution exists in Admin, Controllers, Webhooks, or Business.
- [X] T026 Create the Business, Persistence, Api.ContractTests, and Integrations xUnit projects and SQL Server fixture in `tests/WhatsAppBot.Business.Tests/`, `tests/WhatsAppBot.Persistence.Tests/`, `tests/WhatsAppBot.Api.ContractTests/`, and `tests/WhatsAppBot.Integrations.Tests/`; Dependencies: T002, T022, T025; done when each project builds, Persistence tests can use the configured LocalDB development instance or an explicitly configured disposable/test SQL Server database, and no test can silently target production.
- [X] T027 Add administrator lookup/last-login stored procedures in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Admin_GetByEmailForAuthentication.sql` and `sp_Admin_UpdateLastLogin.sql`; Dependencies: T022, T025; done when normalized-email lookup returns only hash/active state and successful login can be audited.
- [X] T028 Implement administrator repository and password-verification use case in `src/WhatsAppBot.Api/Persistence/Repositories/AdminRepository.cs` and `src/WhatsAppBot.Api/Business/UseCases/AuthenticateAdministrator.cs`; Dependencies: T011, T027; done when adaptive password hashes are verified and missing/inactive/invalid accounts share a generic public error.
- [X] T029 Add Admin authentication/authorization and login API endpoint in `src/WhatsAppBot.Api/Infrastructure/Security/` and `src/WhatsAppBot.Api/Controllers/AdminAuthController.cs`; Dependencies: T013, T017, T028; done when administrative APIs reject anonymous requests and the controller delegates authentication to Business.
- [X] T030 Implement the typed Admin-to-Api client with `ResponseE<T>` handling in `src/WhatsAppBot.Admin/Services/WhatsAppBotApiClient.cs`; Dependencies: T003, T017; done when Admin calls Api over HTTPS and has no SQL connection or data access.
- [X] T031 Implement Admin login/logout and secure server-side session in `src/WhatsAppBot.Admin/Pages/Account/` and `src/WhatsAppBot.Admin/Authentication/`; Dependencies: T008, T029, T030; done when authenticated pages require a secure HttpOnly session and logout clears it.
- [X] T032 Implement one-time first-administrator bootstrap via `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Admin_CreateFirstAdministrator.sql`, `src/WhatsAppBot.Api/Persistence/Repositories/AdminRepository.cs`, `src/WhatsAppBot.Api/Infrastructure/Bootstrap/`, `src/WhatsAppBot.Api/Business/UseCases/BootstrapAdministrator.cs`, and command dispatch in `src/WhatsAppBot.Api/Program.cs`; Dependencies: T013, T022, T025, T028; done when it reads email from User Secrets, securely prompts for password without echo (or accepts a protected process environment value for non-interactive setup), hashes it, refuses overwrite, prints no credentials, leaves Program as composition/dispatch only, and is documented in `quickstart.md`.

## Phase 3: Admin Bot Configuration and Frequent Responses (US3/US4)

**Goal**: Reach a useful local Admin/API/SQL increment before requiring Meta or OpenAI accounts.

**Independent test**: Log in, configure bot state and each reply mode, create/edit/toggle/delete
FAQ entries, and verify deterministic FAQ matching against a local SQL Server.

- [X] T033 [US3] Add bot configuration get/upsert stored procedures in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_BotConfiguration_Get.sql` and `sp_BotConfiguration_Upsert.sql`; Dependencies: T022, T025; done when enabled state and `FaqOnly`, `FaqThenAi`, `AiOnly` persist by IntegrationId with `FaqThenAi` as default.
- [X] T034 [US3] Implement bot configuration repository and Business use cases in `src/WhatsAppBot.Api/Persistence/Repositories/BotConfigurationRepository.cs` and `src/WhatsAppBot.Api/Business/UseCases/`; Dependencies: T011, T033; done when reads/updates validate supported modes and default consistently.
- [X] T035 [US3] Implement bot configuration endpoints in `src/WhatsAppBot.Api/Controllers/BotConfigurationController.cs`; Dependencies: T017, T029, T034; done when GET/PUT match OpenAPI and the controller only delegates.
- [X] T036 [US3] Implement authenticated Admin layout/navigation shell in `src/WhatsAppBot.Admin/Pages/Shared/_Layout.cshtml`; Dependencies: T031; done when navigation exposes Configuración, Respuestas Frecuentes, WhatsApp, and Conversaciones without adding business behavior.
- [X] T037 [US3] Implement Admin bot enabled/mode controls in `src/WhatsAppBot.Admin/Pages/Settings/Bot/Index.cshtml` and `Index.cshtml.cs`; Dependencies: T030, T035, T036; done when modes/default and disabled state display and save through Api only.
- [X] T038 [P] [US4] Add Business mode tests in `tests/WhatsAppBot.Business.Tests/BotReplyModeTests.cs`; Dependencies: T011, T015, T026; done when `FaqOnly` replies only on FAQ hit, `FaqThenAi` falls back only on miss, `AiOnly` skips FAQ, and transcripts follow identical rules.
- [X] T039 [P] [US4] Add deterministic FAQ matcher tests in `tests/WhatsAppBot.Business.Tests/FrequentResponseMatcherTests.cs`; Dependencies: T011, T015, T026; done when Unicode Form KC, trimmed/collapsed whitespace, invariant case-folding, literal containment, active rules, priority, and ascending stable-ID tie-break are asserted.
- [X] T040 [P] [US4] Add FAQ persistence integration tests in `tests/WhatsAppBot.Persistence.Tests/FrequentResponseRepositoryTests.cs`; Dependencies: T026; done when expression updates are atomic and create/edit/activation timestamps behave per `data-model.md`.
- [X] T041 [US4] Add FAQ list/get stored procedures and repository methods in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_FrequentResponse_List.sql`, `sp_FrequentResponse_GetById.sql`, and `src/WhatsAppBot.Api/Persistence/Repositories/FrequentResponseRepository.cs`; Dependencies: T022, T025; done when list paging and detail-with-expressions match the contract.
- [X] T042 [US4] Add FAQ create/update stored procedures and repository methods in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_FrequentResponse_Create.sql` and `sp_FrequentResponse_Update.sql`; Dependencies: T024, T041; done when parent and all expressions commit or roll back atomically and successful edits update ModifiedAtUtc.
- [X] T043 [US4] Add FAQ activate/deactivate/delete stored procedures and repository methods in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_FrequentResponse_SetActive.sql` and `sp_FrequentResponse_Delete.sql`; Dependencies: T041; done when state changes update ModifiedAtUtc and deleted/inactive rules cannot be returned as active.
- [X] T044 [US4] Add active FAQ candidate stored procedure and repository query in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_FrequentResponse_ListActiveCandidates.sql`; Dependencies: T022, T025; done when candidates are scoped to IntegrationId and ordered by descending priority then stable ID.
- [X] T045 [US4] Implement FAQ validation and deterministic matcher in `src/WhatsAppBot.Api/Business/Services/FrequentResponseMatcher.cs`; Dependencies: T011, T039; done when data constraints and matching behavior in `research.md` are enforced without NLP/fuzzy matching.
- [X] T046 [US4] Implement FAQ CRUD Business use cases in `src/WhatsAppBot.Api/Business/UseCases/FrequentResponses/`; Dependencies: T011, T041, T042, T043, T045; done when invalid commands never reach Persistence and persistence errors remain explicit.
- [X] T047 [US4] Implement FAQ API endpoint delegation and DTO mapping in `src/WhatsAppBot.Api/Controllers/FrequentResponsesController.cs`; Dependencies: T017, T029, T046; done when all FAQ routes match OpenAPI and the controller contains no SQL/matching logic.
- [X] T048 [US4] Implement FAQ Admin list/create/edit pages in `src/WhatsAppBot.Admin/Pages/Settings/FrequentResponses/`; Dependencies: T030, T036, T047; done when prompt/intent, expressions, response, priority, optional category, state, and audit dates can be managed only via Api.
- [X] T049 [US4] Implement FAQ activate/deactivate/delete controls and visible error handling in `src/WhatsAppBot.Admin/Pages/Settings/FrequentResponses/`; Dependencies: T048; done when API failures are not shown as successful mutations.
- [X] T050 [US4] Add FAQ and bot-configuration API contract tests in `tests/WhatsAppBot.Api.ContractTests/AdminConfigurationContractTests.cs`; Dependencies: T017, T026, T035, T047; done when validation, status, pagination, mode, DTO, and `ResponseE<T>` responses match OpenAPI.

## Phase 4: Official Meta Connection (US1)

**Goal**: Complete Embedded Signup and show the authenticated one-number connection state.

**Independent test**: Complete Meta's test-business flow and verify number/WABA/state in Admin;
cancelled or failed setup never appears connected.

- [X] T051 [P] [US1] Add Meta Embedded Signup adapter tests in `tests/WhatsAppBot.Integrations.Tests/MetaEmbeddedSignupTests.cs`; Dependencies: T011, T017, T026; done when stubbed code exchange/registration/subscription tests assert server-side credential handling and redaction.
- [X] T052 [US1] Implement the typed RestSharp Meta Cloud API client in `src/WhatsAppBot.Api/Integrations/Meta/MetaCloudApiClient.cs`; Dependencies: T004, T008, T011, T012, T021; done when requests use secret-store references, cancellation/timeouts, and safe provider error mapping.
- [X] T053 [US1] Implement Embedded Signup code exchange, phone registration, and token storage adapter in `src/WhatsAppBot.Api/Integrations/Meta/MetaEmbeddedSignupAdapter.cs`; Dependencies: T021, T052; done when it exchanges only the single-use code server-to-server and stores returned credentials by secret reference.
- [X] T054 [US1] Add WhatsApp integration state procedures and repository in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_WhatsAppIntegration_GetCurrent.sql`, `sp_WhatsAppIntegration_SaveSignupResult.sql`, `sp_WhatsAppIntegration_SetConnectionState.sql`, and `src/WhatsAppBot.Api/Persistence/Repositories/WhatsAppIntegrationRepository.cs`; Dependencies: T022, T025; done when SQL stores identifiers/status/secret references only and enforces one active POC number.
- [X] T055 [US1] Implement Business complete-signup/status/disconnect use cases in `src/WhatsAppBot.Api/Business/UseCases/CompleteWhatsAppSignup.cs`, `GetWhatsAppIntegration.cs`, and `DisconnectWhatsAppIntegration.cs`; Dependencies: T011, T053, T054; done when Connected requires successful authorization, registration, and WABA webhook subscription and disconnect disables the bot.
- [X] T056 [US1] Implement WhatsApp integration API endpoints in `src/WhatsAppBot.Api/Controllers/WhatsAppIntegrationController.cs`; Dependencies: T017, T029, T055; done when endpoints delegate and never accept reusable access tokens or App Secret from Admin.
- [X] T057 [US1] Implement Admin Embedded Signup v4 launch/callback in `src/WhatsAppBot.Admin/Pages/Settings/WhatsApp/Index.cshtml` and `Index.cshtml.cs`; Dependencies: T030, T036, T056; done when browser returns the one-time code/asset IDs to Api and never calls Graph API.
- [ ] T058 [US1] Implement Admin connection and bot-state display in `src/WhatsAppBot.Admin/Pages/Settings/WhatsApp/Index.cshtml`; Dependencies: T037, T057; done when all four connection states, number, display name, WABA ID, date, and bot status are shown without secrets.
- [ ] T059 [US1] Add integration API contract tests in `tests/WhatsAppBot.Api.ContractTests/WhatsAppIntegrationContractTests.cs`; Dependencies: T017, T026, T056; done when authorization, invalid/cancelled signup, provider failure, disconnect, and safe DTO responses are verified.

## Phase 5: Webhooks, Text, FAQ Reply, and AI Fallback (US2/US4)

**Goal**: Deliver the first real vertical slice from customer-initiated text to mode-correct,
policy-compliant response and persisted conversation.

**Independent test**: Send an allowed in-window customer text to the connected test number;
verify one inbound record, FAQ answer on hit, AI only on the permitted fallback/mode, and visible
conversation history. Outside the permitted window, no free-form FAQ/AI response is sent.

- [X] T060 [P] [US2] Add Meta webhook signature/challenge tests in `tests/WhatsAppBot.Api.ContractTests/MetaWebhookContractTests.cs`; Dependencies: T017, T026, T029; done when valid/invalid HMAC signatures and GET challenges are covered.
- [X] T061 [US2] Implement normalized Meta webhook event parsing in `src/WhatsAppBot.Api/Integrations/Meta/MetaWebhookEventParser.cs`; Dependencies: T011, T015; done when allowlisted text/audio/image/status fields map to internal events without retaining raw bodies.
- [X] T062 [US2] Implement Meta webhook GET verification and POST raw-byte signature validation endpoint in `src/WhatsAppBot.Api/Webhooks/MetaWhatsAppWebhookEndpoint.cs`; Dependencies: T013, T019, T052, T061; done when GET verifies Verify Token, POST verifies App Secret signature before parsing, and both delegate to Business.
- [X] T063 [US2] Implement durable idempotent webhook inbox procedures/repository in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_WebhookInbox_Insert.sql`, `sp_WebhookInbox_ClaimBatch.sql`, `sp_WebhookInbox_SetState.sql`, and `src/WhatsAppBot.Api/Persistence/Repositories/WebhookInboxRepository.cs`; Dependencies: T022, T025; done when unique event keys and bounded worker leases prevent duplicate processing.
- [X] T064 [US2] Implement webhook acceptance use case and inbox worker in `src/WhatsAppBot.Api/Business/UseCases/AcceptMetaWebhookEvent.cs` and `src/WhatsAppBot.Api/Business/Workers/WebhookInboxWorker.cs`; Dependencies: T011, T061, T063; done when webhook acknowledgement follows durable persistence and slow message processing runs asynchronously.
- [X] T065 [US2] Add idempotency integration tests in `tests/WhatsAppBot.Persistence.Tests/WebhookInboxIdempotencyTests.cs`; Dependencies: T026, T063; done when replayed provider IDs create one durable event and no duplicate processing.
- [X] T066 [US2] Implement contact upsert persistence in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Contact_UpsertWhatsApp.sql` and `src/WhatsAppBot.Api/Persistence/Repositories/ContactRepository.cs`; Dependencies: T022, T025; done when `(IntegrationId, WhatsAppUserId)` identifies one contact.
- [X] T067 [US2] Implement conversation get-or-create persistence in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Conversation_GetOrCreate.sql` and `src/WhatsAppBot.Api/Persistence/Repositories/ConversationRepository.cs`; Dependencies: T022, T025, T066; done when each inbound interaction is associated with the modeled conversation without inventing status transitions.
- [X] T068 [US2] Implement idempotent inbound-message persistence in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Message_InsertInbound.sql` and `src/WhatsAppBot.Api/Persistence/Repositories/MessageRepository.cs`; Dependencies: T022, T025, T067; done when `(IntegrationId, ProviderMessageId)` prevents duplicate inbound messages.
- [ ] T069 [P] [US2] Add Business text-processing mode tests in `tests/WhatsAppBot.Business.Tests/InboundTextProcessingTests.cs`; Dependencies: T011, T015, T026, T038, T039; done when bot-disabled, FAQ hit/miss, transcript-equivalent mode decisions, and no-unconditional-AI behavior are verified.
- [X] T070 [US2] Implement inbound text Business orchestration in `src/WhatsAppBot.Api/Business/UseCases/ProcessInboundText.cs` and `src/WhatsAppBot.Api/Business/Services/MessageProcessingService.cs`; Dependencies: T034, T044, T045, T055, T064, T066, T067, T068, T072; done when it persists inbound before any outbound policy evaluation, checks bot state, selects/generates a response per the configured mode, then invokes T072 for the outbound candidate; a blocked candidate records the controlled no-reply outcome without blocking inbound persistence.
- [ ] T071 [US2] Implement OpenAI text-generation adapter and provider configuration binding in `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiReplyGenerator.cs` and `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiServiceCollectionExtensions.cs`; Dependencies: T004, T008, T011, T012; done when API keys are server-side, provider calls are bounded, and Business sees only provider-neutral results.
- [X] T072 [US2] Implement the provider-neutral Business WhatsApp messaging-window/template eligibility gate for outbound response candidates in `src/WhatsAppBot.Api/Business/Services/WhatsAppMessagingPolicy.cs` and safe outcome mapping in `src/WhatsAppBot.Api/Models/Enums/MessageOutcomeCode.cs`; Dependencies: T011, T015, T061; done when each bot-selected/generated outbound candidate is checked against the customer-initiated message timestamp/current permitted window before it can be persisted as sendable or sent, while inbound persistence remains unaffected; expired/template-required candidates are blocked from outbound message/outbox persistence and record `NoReply` with `MessagingWindowClosed` or `TemplateRequired`, never substituting FAQ or AI content for an official template.
- [X] T073 [US2] Implement outbound message/outbox atomic persistence in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Message_CreateOutboundPending.sql` and `sp_MessageOutbox_ClaimBatch.sql`; Dependencies: T022, T025, T070, T072; done when only a candidate approved by T072 is persisted as a sendable outbound message together with its outbox work, and a blocked candidate cannot enter the sendable outbox.
- [X] T074 [US2] Implement official Meta outbound message sender in `src/WhatsAppBot.Api/Integrations/Meta/MetaMessageSender.cs`; Dependencies: T052, T072, T073; done when it sends only T072-approved messages retrieved from the persisted outbox through Cloud API and maps provider window/template rejections to safe non-success results.
- [X] T075 [US2] Implement outbound worker, bounded retry/state procedure, and provider-result persistence in `src/WhatsAppBot.Api/Business/Workers/MessageOutboxWorker.cs`, `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_MessageOutbox_SetState.sql`, and `sp_Message_UpdateOutboundProviderResult.sql`; Dependencies: T073, T074; done when generated, sent, failed, and delivered states remain distinct and retryable failures are bounded.
- [ ] T076 [US2] Implement delivery-status reconciliation in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Message_ApplyDeliveryStatus.sql` and `src/WhatsAppBot.Api/Business/UseCases/ApplyMessageStatus.cs`; Dependencies: T063, T075; done when sent/delivered/read/failed webhook events update known states without regressing to false success.
- [ ] T077 [US2] Map persisted safe policy outcomes to the allowlisted `MessageDTO.outcomeCode` and Admin display in `src/WhatsAppBot.Api/Models/Contracts/` and `src/WhatsAppBot.Admin/Pages/Conversations/`; Dependencies: T017, T072; done when policy-blocked replies are visible as not sent, expose no raw provider errors, and match OpenAPI.
- [ ] T078 [US2] Add real-service-independent text vertical-slice integration tests in `tests/WhatsAppBot.Api.ContractTests/TextReplyFlowTests.cs`; Dependencies: T026, T065, T070, T071, T072, T075; done when a stubbed webhook-to-persistence-to-policy-approved-outbox test verifies FAQ hit, FAQ miss with fallback, and no-send policy outcomes.

## Phase 6: Conversation Visibility (US3)

**Goal**: Let Admin inspect the working text slice before adding more media types.

- [ ] T079 [US3] Add conversation list/detail stored procedures in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Conversation_List.sql` and `sp_Conversation_GetDetail.sql`; Dependencies: T022, T025, T067, T068; done when results include contact, last activity, basic status, paginated detail, and chronological messages.
- [ ] T080 [US3] Implement Business list/detail query use cases in `src/WhatsAppBot.Api/Business/UseCases/ListConversations.cs` and `GetConversationDetail.cs`; Dependencies: T011, T079; done when queries are scoped to the active integration and use no SQL directly.
- [ ] T081 [US3] Implement conversation API endpoints in `src/WhatsAppBot.Api/Controllers/ConversationsController.cs`; Dependencies: T017, T029, T080; done when route/query behavior matches OpenAPI and controller only delegates.
- [ ] T082 [US3] Implement Admin conversation list and text timeline in `src/WhatsAppBot.Admin/Pages/Conversations/Index.cshtml` and `Details.cshtml`; Dependencies: T030, T036, T077, T081; done when contact, latest activity, status, direction, message type, FAQ/AI source, and delivery/processing outcomes are visible.

## Phase 7: Audio and Transcription (US2)

- [ ] T083 [P] [US2] Add audio download/transcription tests in `tests/WhatsAppBot.Integrations.Tests/AudioProcessingTests.cs`; Dependencies: T011, T015, T020, T026; done when size/type rejection, OGG/Opus conversion, provider failure, and temporary-file cleanup are tested.
- [ ] T084 [US2] Implement Meta media download, validation, private storage adapter, and metadata persistence in `src/WhatsAppBot.Api/Integrations/Meta/MetaMediaClient.cs`, `src/WhatsAppBot.Api/Infrastructure/MediaStorage/`, and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_MediaAttachment_Upsert.sql`; Dependencies: T020, T052, T063; done when media is size/type checked and SQL stores only its private reference and approved retention metadata.
- [ ] T085 [US2] Implement OGG/Opus normalization and OpenAI transcription adapter in `src/WhatsAppBot.Api/Integrations/Audio/AudioNormalizationService.cs`, `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiAudioTranscriptionService.cs`, and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Transcript_Upsert.sql`; Dependencies: T071, T084; done when transcript state/text persists, limits are enforced, and temporary files are removed.
- [ ] T086 [US2] Implement Business inbound-audio orchestration in `src/WhatsAppBot.Api/Business/UseCases/ProcessInboundAudio.cs`; Dependencies: T070, T072, T084, T085; done when persisted transcript goes through the same FAQ/mode/policy rules as text.
- [ ] T087 [US2] Implement transcript display in `src/WhatsAppBot.Admin/Pages/Conversations/Details.cshtml`; Dependencies: T082, T086; done when successful transcript and failed/unavailable states are distinguishable.

## Phase 8: Photography and Analysis (US2)

- [ ] T088 [P] [US2] Add image download/analysis integration tests in `tests/WhatsAppBot.Integrations.Tests/ImageAnalysisTests.cs`; Dependencies: T011, T020, T026; done when private media handling, size/type, provider success/failure, and no-public-URL behavior are verified.
- [ ] T089 [US2] Implement OpenAI image analysis adapter in `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiImageAnalysisService.cs`; Dependencies: T071, T084; done when provider-specific calls remain behind the image-analysis interface with bounded timeouts and safe failures.
- [ ] T090 [US2] Implement Business image-message analysis/reply orchestration in `src/WhatsAppBot.Api/Business/UseCases/ProcessInboundImage.cs`; Dependencies: T070, T072, T074, T084, T089; done when image processing follows the selected AI-enabled mode and policy gate and persists a truthful result.
- [ ] T091 [US2] Implement authorized media streaming and Admin image/audio rendering in `src/WhatsAppBot.Api/Business/UseCases/GetMessageMedia.cs`, `src/WhatsAppBot.Api/Controllers/MessageMediaController.cs`, and `src/WhatsAppBot.Admin/Pages/Conversations/Details.cshtml`; Dependencies: T081, T084, T087; done when conversation authorization is enforced and private storage URLs are never exposed.

## Phase 9: Final Dashboard, Resilience, and Demo

- [ ] T092 [US3] Implement the final basic dashboard summary in `src/WhatsAppBot.Admin/Pages/Dashboard/Index.cshtml` and its Api-backed page model; Dependencies: T058, T079, T082; done when connection, bot state, recent conversation activity, and only basic POC counts are displayed.
- [ ] T093 Add the reproducible SC-006 timed validation record template and procedure in `specs/001-whatsapp-business-bot-poc/demo-validation.md`; Dependencies: T092; done when five repeated runs with seeded connection/bot/recent-conversation data each record correctness and elapsed time under 60 seconds.
- [ ] T094 [P] Add cross-cutting security and failure tests in `tests/WhatsAppBot.Api.ContractTests/SecurityAndFailureTests.cs` and `tests/WhatsAppBot.Integrations.Tests/ProviderFailureTests.cs`; Dependencies: T018, T019, T026, T052, T071, T072; done when secret redaction, invalid input, provider timeout, webhook signature failure, and no-success-shaped failures are covered.
- [ ] T095 Implement approved-retention cleanup for SQL metadata and private media in `src/WhatsAppBot.Api/Business/Workers/RetentionCleanupWorker.cs` and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/`; Dependencies: T020, T021, T084; done when expired data is removed/marked consistently without orphan public links.
- [ ] T096 Document live Meta/OpenAI prerequisites and customer-initiated, in-window demonstration constraints in `tests/WhatsAppBot.Integrations.Tests/LiveDemoValidation.md`; Dependencies: T052, T071, T072; done when only approved test assets/consented recipients and allowed messaging windows are used.
- [ ] T097 Run the ordered full demo from `specs/001-whatsapp-business-bot-poc/quickstart.md` and record evidence in `specs/001-whatsapp-business-bot-poc/demo-validation.md`; Dependencies: T032, T037, T049, T058, T062, T064, T078, T082, T087, T090, T091, T092, T093, T094, T095, T096; done when Admin → bot config → FAQ → Meta → webhook → text/FAQ → AI fallback → visible conversation → audio → photo completes with consented test data.
- [ ] T098 Verify clean-install SQL Server LocalDB preparation, solution build/tests, supported .NET 10 servicing, approved retention/AI processing, Meta access, and one-active-number policy in `specs/001-whatsapp-business-bot-poc/release-readiness.md`; Dependencies: T097; done when `sqllocaldb`/`sqlcmd` checks pass, `Initialize-DevelopmentDatabase.ps1` prepares a fresh `(localdb)\MSSQLLocalDB` database named `WhatsAppBot`, catalog validation finds expected tables and Stored Procedures, no unsupported runtime or unapproved external-service/data-processing gate remains, and deployed environments remain compatible with configured SQL Server.

## Phase 10: Tenant, Contracts, Migration, and Runtime Safety

- [X] T099 Extend shared and domain models for `TenantId`, `AttentionMode`, `LifecycleStatus`, actor, origin, `OnboardingType`, outcomes, assignment and conversation version in `src/WhatsAppBot.Shared/Enums/`, `src/WhatsAppBot.Shared/DTOs/`, `src/WhatsAppBot.Api/Models/Entities/`, and `src/WhatsAppBot.Api/Models/Contracts/`; Dependencies: T011, T015, T016, T017; done when the new fields are represented without using one combined Bot/Humano/Cerrada enum.
- [X] T100 [OAS] Synchronize OpenAPI contracts and contract tests for Tenant-scoped conversations, actions, manual replies, safe outcomes, connection modality, `409` conflicts, `410` expired media and authorized media in `specs/001-whatsapp-business-bot-poc/contracts/openapi.yaml` and `tests/WhatsAppBot.Api.ContractTests/`; Dependencies: T099; done when the OpenAPI document parses and contract tests detect DTO/route/status drift.
- [X] T101 [SP] Create the Tenant-aware logical SQL model for Company, TenantMembership, TenantId relationships, separate attention/lifecycle states, `AssignedAdminId`, audit events, `rowversion` and the operational one-active-WhatsApp-number-per-Tenant invariant in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Schema/005_TenantModel.sql` and `006_TenantIndexes.sql`; Dependencies: T099; done when the schema supports multiple Tenants and future multiple integrations without a global singleton constraint.
- [X] T102 [SP] Implement server-side Tenant bootstrap, membership lookup and active-Tenant session context in `src/WhatsAppBot.Api/Business/Abstractions/TenantPorts.cs`, `src/WhatsAppBot.Api/Infrastructure/Security/TenantContext.cs`, `src/WhatsAppBot.Api/Business/UseCases/`, and `src/WhatsAppBot.Api/Persistence/Repositories/`; Dependencies: T099, T101; done when bootstrap creates the first Tenant and associates the bootstrap administrator, and requests cannot select Tenant solely from browser input.
- [X] T103 [SP] Migrate the current singleton data model to the initial Tenant without data loss in `src/WhatsAppBot.Api/Persistence/StoredProcedures/Schema/007_TenantMigration.sql`, `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/`, and `scripts/sql/Initialize-DevelopmentDatabase.ps1`; Dependencies: T101, T102; done when the existing integration, administrator, conversations and messages keep their identities and are associated with the initial Tenant.
- [X] T104 [P] Bind independent configurable budgets for Admin, Meta, SQL, OpenAI, media storage and FFmpeg in `src/WhatsAppBot.Api/Infrastructure/Options/` and `src/WhatsAppBot.Admin/Configuration/`; Dependencies: T004, T008, T099; done when invalid required configuration fails explicitly and no model, timeout or budget value is hardcoded in an integration.
- [ ] T105 Propagate deadlines and contain provider/cancellation failures in `src/WhatsAppBot.Api/Integrations/`, `src/WhatsAppBot.Api/Business/Workers/`, and `tests/WhatsAppBot.Integrations.Tests/ProviderFailureTests.cs`; Dependencies: T104; done when Meta, SQL, OpenAI, storage and FFmpeg failures do not terminate workers or produce success-shaped results.
- [ ] T106 [SP] Persist and reconcile `CompletionStateUnknown` in `src/WhatsAppBot.Api/Helpers/CompletionExecutionContext.cs`, `CompletionPhaseLogger.cs`, `src/WhatsAppBot.Api/Business/UseCases/`, `src/WhatsAppBot.Api/Persistence/Repositories/`, and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/`; Dependencies: T102, T103, T104; done when an indeterminate Embedded Signup result is persisted safely, its state can be queried/reconciled, and a single-use authorization code is never retried blindly.
- [ ] T107 [SP] Normalize and route new Meta message/status events in `src/WhatsAppBot.Api/Integrations/Meta/MetaWebhookEventParser.cs`, `src/WhatsAppBot.Api/Business/Workers/WebhookInboxWorker.cs`, and `src/WhatsAppBot.Api/Models/Contracts/`; Dependencies: T099, T102, T063, T064; done when text/audio/image/status events reach the correct worker, while `history`, `smb_app_state_sync` and `smb_message_echoes` are ignored safely without implementation tasks or fabricated messages.

## Phase 11: Conversation Lifecycle and Human Attention

- [ ] T108 [SP] Implement Tenant-scoped conversation persistence and reopening in `src/WhatsAppBot.Api/Persistence/Repositories/ConversationRepository.cs`, `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Conversation_GetOrCreate.sql`, and `src/WhatsAppBot.Api/Models/Entities/Entities.cs`; Dependencies: T101, T102, T107, T066, T067, T068; done when a closed conversation keeps its `ConversationId` and reopens as `Abierta + Bot` when enabled or `Abierta + Humano` when disabled.
- [ ] T109 [SP] Implement conversation actions, assignment and audit in `src/WhatsAppBot.Api/Business/UseCases/`, `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Conversation_ApplyAction.sql`, `sp_Conversation_RecordAuditEvent.sql`, and `sp_Conversation_SetAssignment.sql`; Dependencies: T108; done when take, manual handoff, return-to-bot, close, reopen and assignment persist actor, origin, timestamp and reason.
- [ ] T110 [SP] Implement optimistic conversation concurrency with SQL Server `rowversion` in `src/WhatsAppBot.Api/Business/Services/`, `src/WhatsAppBot.Api/Persistence/Repositories/ConversationRepository.cs`, and `tests/WhatsAppBot.Persistence.Tests/`; Dependencies: T108, T109; done when stale transitions return a controlled conflict and cannot overwrite a newer state.
- [ ] T111 [SP] Revalidate Tenant, Integration, `AttentionMode`, `LifecycleStatus`, `rowversion` and messaging window before outbox send in `src/WhatsAppBot.Api/Business/Services/MessageProcessingService.cs`, `src/WhatsAppBot.Api/Business/Workers/MessageOutboxWorker.cs`, and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/`; Dependencies: T105, T107, T110, T072, T073, T074, T075; done when a human takeover invalidates pending Bot work before effective provider delivery.
- [ ] T112 Implement automatic handoff rules for customer request, bot inability, business rule and blocking error in `src/WhatsAppBot.Api/Business/Services/ConversationHandoffService.cs`, `src/WhatsAppBot.Api/Business/UseCases/`, and `tests/WhatsAppBot.Business.Tests/`; Dependencies: T110, T111; done when each automatic handoff changes attention to Humano, records an allowlisted reason and prevents a concurrent Bot response.
- [ ] T113 [SP] [OAS] [Meta] Implement manual Admin text replies in `src/WhatsAppBot.Api/Business/UseCases/SendManualConversationMessage.cs`, `src/WhatsAppBot.Api/Controllers/ConversationsController.cs`, `src/WhatsAppBot.Admin/Services/WhatsAppBotApiClient.cs`, and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Message_CreateAdminOutboundPending.sql`; Dependencies: T109, T111, T072, T073, T074, T075; done when free-form text is allowed only in Humano mode and the permitted window, is stored as Admin/AdminPanel, and is blocked safely outside the window without implementing template campaigns.
- [ ] T114 [SP] [Meta] Implement idempotent and out-of-order delivery reconciliation in `src/WhatsAppBot.Api/Business/UseCases/ApplyMessageStatus.cs`, `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/sp_Message_ApplyDeliveryStatus.sql`, and message repositories; Dependencies: T107, T111, T075; done when `Sent → Delivered → Read` never regresses, `Failed` remains auditable, and stale provider events cannot overwrite later confirmed states.
- [ ] T115 [OAS] [US2] Project safe message outcomes independently from AI and conversation actions in `src/WhatsAppBot.Api/Models/Enums/MessageOutcomeCode.cs`, `src/WhatsAppBot.Api/Models/Contracts/`, `src/WhatsAppBot.Api/Controllers/ConversationsController.cs`, and `src/WhatsAppBot.Admin/Pages/Conversations/`; Dependencies: T100, T111, T114; done when all allowlisted outcomes are visible without raw provider errors and unsent messages are never shown as Sent.
- [ ] T116 [OAS] [US3] Complete the Tenant-scoped Conversations API and Admin experience in `src/WhatsAppBot.Api/Controllers/ConversationsController.cs`, `src/WhatsAppBot.Api/Business/UseCases/`, `src/WhatsAppBot.Admin/Pages/Conversations/`, and `src/WhatsAppBot.Admin/Services/WhatsAppBotApiClient.cs`; Dependencies: T100, T108, T109, T110, T111, T113, T114, T115; done when list/detail and actions show lifecycle, attention, responsible admin, actor/origin, audit, delivery, outcomes and authorized history.

## Phase 12: Integration State and OpenAI Text

- [ ] T117 [SP] [US1] Persist Standard/Coexistence modality, Tenant, WABA, Phone Number ID and effective configuration in `src/WhatsAppBot.Api/Models/Entities/`, `src/WhatsAppBot.Api/Integrations/Meta/MetaEmbeddedSignupAdapter.cs`, `src/WhatsAppBot.Api/Persistence/Repositories/`, and integration Stored Procedures; Dependencies: T101, T102, T053, T054; done when Standard and Coexistence are distinguishable, Phone Number discovery remains unambiguous, and one active integration is enforced per Tenant only.
- [ ] T118 [OAS] [US1] Complete connection UI and contract coverage in `src/WhatsAppBot.Admin/Pages/Settings/WhatsApp/`, `src/WhatsAppBot.Admin/wwwroot/js/meta-embedded-signup-events.js`, and `tests/WhatsAppBot.Api.ContractTests/WhatsAppIntegrationContractTests.cs`; Dependencies: T100, T117, T057; done when modality, date, number, WABA, bot state and all connection states are displayed without declaring unknown completion successful or failed.
- [ ] T119 [P] [AI] Implement the provider-neutral OpenAI adapter and secure configuration in `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiReplyGenerator.cs`, `OpenAiServiceCollectionExtensions.cs`, and `src/WhatsAppBot.Api/Infrastructure/Options/`; Dependencies: T011, T099, T104; done when API keys remain server-side, models/timeouts/limits are configurable, and Business sees only provider-neutral results.
- [ ] T120 [AI] Implement AI context construction and response processing in `src/WhatsAppBot.Api/Business/Services/`, `src/WhatsAppBot.Api/Business/UseCases/ProcessInboundText.cs`, and new AI context components; Dependencies: T108, T110, T119; done when FAQ hit/miss, transcript context and configured `FaqOnly/FaqThenAi/AiOnly` behavior are applied without processing a Humano conversation as Bot.
- [ ] T121 [AI] Implement AI timeout/error/fallback and handoff integration in `src/WhatsAppBot.Api/Business/Services/ConversationHandoffService.cs`, `ProcessInboundText.cs`, and `tests/WhatsAppBot.Business.Tests/`; Dependencies: T105, T112, T120; done when provider failure, inability or business rule can trigger an audited Humano handoff without implementing outcome projection in this task.
- [ ] T122 [P] [US2] Add stubbed text vertical-slice tests in `tests/WhatsAppBot.Api.ContractTests/TextReplyFlowTests.cs`, `tests/WhatsAppBot.Business.Tests/`, and `tests/WhatsAppBot.Persistence.Tests/`; Dependencies: T111, T115, T120, T121; done when webhook-to-persistence-to-FAQ/AI-to-policy-to-outbox covers replay, Bot/Humano priority, conflicts, window blocks and safe outcomes.

## Phase 13: Private Storage, Audio, Image, and Retention

- [ ] T123 [P] [Storage] Implement provider-neutral private media storage and approval gate in `src/WhatsAppBot.Api/Business/Abstractions/`, `src/WhatsAppBot.Api/Infrastructure/MediaStorage/`, `src/WhatsAppBot.Api/Infrastructure/Options/`, and the approved retention documentation; Dependencies: T011, T020, T104; done when storage is private, access is authorized, no permanent public URL exists, and the initial retention is 30 days without fixing an unapproved cloud provider.
- [ ] T124 [SP] [Meta] [Storage] Implement Meta media download, validation and metadata persistence in `src/WhatsAppBot.Api/Integrations/Meta/MetaMediaClient.cs`, `src/WhatsAppBot.Api/Infrastructure/MediaStorage/`, `src/WhatsAppBot.Api/Persistence/Repositories/`, and `sp_MediaAttachment_Upsert.sql`; Dependencies: T106, T107, T117, T123; done when MIME/size limits are enforced and SQL stores only private references, state and expiry metadata.
- [ ] T125 [SP] [AI] [Storage] Implement the complete audio pipeline in `src/WhatsAppBot.Api/Integrations/Audio/AudioNormalizationService.cs`, `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiAudioTranscriptionService.cs`, `src/WhatsAppBot.Api/Business/UseCases/ProcessInboundAudio.cs`, and `sp_Transcript_Upsert.sql`; Dependencies: T119, T123, T124; done when download, storage, OGG/Opus normalization, transcription, persistence, FAQ/AI processing and temporary-file cleanup are complete.
- [ ] T126 [AI] [Storage] Implement the complete image pipeline in `src/WhatsAppBot.Api/Integrations/OpenAI/OpenAiImageAnalysisService.cs`, `src/WhatsAppBot.Api/Business/UseCases/ProcessInboundImage.cs`, and image persistence mappings; Dependencies: T111, T119, T123, T124; done when images are stored privately, analyzed only for AI-enabled modes, persisted truthfully and routed through the same policy/outbox guards.
- [ ] T127 [OAS] [Storage] Implement authorized media access and Admin rendering in `src/WhatsAppBot.Api/Business/UseCases/GetMessageMedia.cs`, `src/WhatsAppBot.Api/Controllers/MessageMediaController.cs`, and `src/WhatsAppBot.Admin/Pages/Conversations/Details.cshtml`; Dependencies: T116, T123, T124, T125, T126; done when access is Tenant/conversation-authorized, expired media returns a safe expired result, and no storage URL is exposed publicly.
- [ ] T128 [SP] [Storage] Implement configurable media expiration and cleanup in `src/WhatsAppBot.Api/Business/Workers/RetentionCleanupWorker.cs` and `src/WhatsAppBot.Api/Persistence/StoredProcedures/Procedures/`; Dependencies: T123, T127; done when files expire after the configured initial 30-day period while messages, allowed metadata and transcripts remain.
- [ ] T129 [P] [AI] [Storage] Add media processing tests in `tests/WhatsAppBot.Integrations.Tests/AudioProcessingTests.cs`, `ImageAnalysisTests.cs`, and storage/failure test files; Dependencies: T123, T124, T125, T126, T127, T128; done when size/MIME rejection, provider failure, FFmpeg cleanup, private access, expiration and no-public-URL behavior are covered with stubs.

## Phase 14: Isolation, External Validation, Demo, and Release

- [ ] T130 [SP] [US3] Add Tenant isolation and authorization tests in `tests/WhatsAppBot.Api.ContractTests/TenantIsolationTests.cs`, `SecurityAndFailureTests.cs`, and Persistence tests; Dependencies: T102, T116, T127; done when multiple Tenants and memberships cannot cross configuration, integrations, conversations, messages or media.
- [ ] T131 [AI] [Storage] Add cross-provider failure and timeout tests in `tests/WhatsAppBot.Api.ContractTests/SecurityAndFailureTests.cs`, `tests/WhatsAppBot.Integrations.Tests/ProviderFailureTests.cs`, and completion tests; Dependencies: T105, T119, T123, T127; done when Meta, OpenAI, SQL, storage, FFmpeg and cancellation failures are bounded, redacted and non-success-shaped.
- [ ] T132 [US3] Extend the existing dashboard task `T092` with API-backed connection, bot, Tenant and recent activity data in `src/WhatsAppBot.Admin/Pages/Dashboard/`, API contracts and client services; Dependencies: T116, T117; done when the dashboard contains no hardcoded connection/activity values and respects the effective Tenant.
- [ ] T133 [Meta] [US1] Validate Standard Embedded Signup with Meta real in `tests/WhatsAppBot.Integrations.Tests/LiveStandardValidation.md`; Dependencies: T105, T117, T118; done when authorization, Phone Number ID validation, registration, WABA subscription and truthful Connected state are evidenced.
- [ ] T134 [Meta] [US1] Validate Coexistence Embedded Signup with Meta real in `tests/WhatsAppBot.Integrations.Tests/LiveCoexistenceValidation.md`; Dependencies: T105, T117, T118; done when an eligible WhatsApp Business App number completes onboarding without Standard `/register` and only new messages are in scope.
- [ ] T135 [Meta] [AI] [US2] Validate live text and OpenAI processing in `tests/WhatsAppBot.Integrations.Tests/LiveTextAiValidation.md`; Dependencies: T122, T133, T134; done when an in-window customer message produces the correct FAQ/AI result, delivery state, safe outcome and audited handoff behavior.
- [ ] T136 [Meta] [AI] [Storage] [US2] Validate live audio and image processing in `tests/WhatsAppBot.Integrations.Tests/LiveMediaValidation.md`; Dependencies: T129, T133, T134; done when real media is downloaded, privately stored, transcribed/analyzed, displayed with authorization and retained according to policy.
- [ ] T137 [Meta] [AI] [Storage] Run the expanded final demonstration extending `T097` in `specs/001-whatsapp-business-bot-poc/demo-validation.md`; Dependencies: T115, T122, T129, T132, T135, T136; done when Tenant isolation, Bot/Humano actions, manual reply, Standard/Coexistence new messages, text, AI, audio, image and retention are evidenced with consented data.
- [ ] T138 [SP] [Meta] [AI] [Storage] Complete release readiness extending `T098` in `specs/001-whatsapp-business-bot-poc/release-readiness.md`; Dependencies: T103, T130, T131, T133, T134, T135, T136, T137; done when clean install, data-preserving migration, SQL catalog, tests, runtime, Meta, OpenAI, storage, retention and Tenant isolation gates pass.

## Dependency Order and Incremental Checkpoints

1. **Admin foundation**: T001-T032. The first administrator is securely bootstrapped; Admin
   authenticates through Api and has no SQL access.
2. **Bot configuration and FAQs**: T033-T050. This increment is testable locally with SQL;
   it proves all three modes and deterministic FAQ CRUD/matching before external services.
3. **Meta connection**: T051-T059. Complete official Embedded Signup and verify a single
   connected test number.
4. **Webhook and text**: T060-T078. First prove FAQ hit/no-match behavior with stubs and real
   customer-initiated in-window test messages; AI is called only in `FaqThenAi` on a miss or
   directly in `AiOnly`.
5. **Conversation visibility**: T079-T082. Inspect the working text interaction in Admin.
6. **Audio, then photography**: T083-T087, then T088-T091. Each extends the already-tested
   message and conversation flow; audio transcripts use exactly the text response-mode rules.
7. **Dashboard and final gates**: T092-T098. Finish UI summary, security/resilience, timed
   SC-006 proof, and complete demo only after the vertical slices are working.
8. **Tenant and runtime foundation**: T099-T107. Add the Tenant boundary, safe migration,
   configurable budgets, bounded failures, unknown completion reconciliation, and normalized
   Meta events before extending conversation behavior.
9. **Human conversation**: T108-T116. Preserve conversation identity on reopen, enforce
   Bot/Humano priority and optimistic concurrency, then add audited handoff, manual replies,
   delivery reconciliation, safe outcomes, and complete Admin visibility.
10. **Integration and AI**: T117-T122. Persist Standard/Coexistence state, complete the
    connection contract, add provider-neutral OpenAI text processing, and verify the text slice.
11. **Multimedia and storage**: T123-T129. Approve private storage, process audio and image
    messages, authorize media access, expire files, and test provider failures and cleanup.
12. **Security and validation**: T130-T138. Verify Tenant isolation, external failures,
    API-backed dashboard behavior, Standard/Coexistence live flows, AI/media validation, demo,
    and release readiness.

The media-provider/retention decision and private media adapter are required before processing
real audio/photo data; they do not block earlier local Admin, FAQ, Meta connection, webhook, or
text-only increments. The Tenant migration and isolation gates are required before treating the
new multi-Tenant scope as release-ready.

The Admin, SQL, FAQ, mode, and matcher increments can be tested without Meta/OpenAI credentials.
The Meta-connected text increment uses Meta test assets; the OpenAI service is required only
for the fallback/AI-only and multimodal scenarios. No task adds multi-company, simultaneous
numbers, billing, CRM, campaigns, advanced reporting, or complex roles.

## Parallel Opportunities

- T004 and T005 use different configuration files after projects exist.
- T014 and T015 create independent Shared response and enum files.
- T038, T039, and T040 target separate test files after the foundation is ready.
- T051 and T060 target different test suites/contracts.
- T069 can be authored with the FAQ matcher/mode tests because the test files are separate.
- T083 and T088 are separate media adapter test suites; they may be prepared in parallel after
  media interfaces are stable, though implementation phases remain audio then photography.
- T094 can proceed independently of the SC-006 documentation task T093 after its prerequisites.
- T117 and T119 can proceed in parallel once their separate integration and configuration
  prerequisites are ready.
- T133 and T134 can proceed in parallel because they record separate live validation paths.
- T135 and T136 can proceed in parallel after the text and media vertical slices are ready.
- LocalDB is the Development-only target for T022/T026/T098; deployment configuration remains
  SQL Server-compatible and is not coupled to the LocalDB instance.

## Validation Summary

- **Total tasks**: 138, sequential IDs T001-T138; T001-T098 remain the historical baseline and
  T099-T138 are the approved Tenant, Bot/Humano, AI and multimedia extension.
- **Dependencies**: Explicit on every task; `None` is stated where there is no prerequisite.
- **Parallel tasks**: Only tasks with separate files and satisfied prerequisites are marked
  `[P]`; the task list and opportunities section identify their boundaries.
- **Scope**: Tenant/Company and memberships; one active WhatsApp number per Tenant as an
  operational POC limit; future multiple integrations per company; Bot/Humano attention,
  lifecycle, handoff, manual replies, OpenAI, text/audio/image, private media and initial
  30-day retention. History synchronization, `smb_*`, billing, CRM, campaigns, operational
  templates, advanced reporting, teams, queues and plans remain out of scope.
