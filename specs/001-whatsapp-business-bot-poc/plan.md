# Implementation Plan: POC de Bot de WhatsApp Business

**Branch**: `001-whatsapp-business-bot-poc` | **Date**: 2026-09-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification for a client-presentable WhatsApp Business bot POC.

## Summary

Build a .NET 10 LTS solution with three projects: an ASP.NET Core administrative web
application (`WhatsAppBot.Admin`), an ASP.NET Core Web API (`WhatsAppBot.Api`), and shared
contracts (`WhatsAppBot.Shared`). The Admin is a thin server-rendered client of the API; the
API owns authentication, use-case orchestration, persistence, and integrations.

The API uses the mandated Middleware, Business, Persistence, Infrastructure, Integrations,
Models, and Helpers layers. SQL Server stored procedures are the only persistence interface.
Meta WhatsApp Cloud API and AI/media providers are accessed behind Integration contracts.
Persist inbound webhook events before acknowledging Meta, process them idempotently in a
background worker, and track outbound work/status independently from AI generation. Tenant
authorization, conversation attention state and optimistic concurrency are Business policies,
not concerns of Controllers or Admin pages.

For the POC, use Meta Embedded Signup v4, OpenAI API for response generation, image analysis
and transcription, and FFmpeg to convert WhatsApp OGG/Opus voice media to an accepted
transcription format. The FAQ matcher is deterministic normalized phrase containment, ordered
by configured priority, not an NLP engine.

## Technical Context

**Language/Version**: C# / .NET 10 LTS, latest supported 10.0.x servicing patch at build and
deployment time.

**Primary Dependencies**: ASP.NET Core Web API; ASP.NET Core Razor Pages for Admin;
RestSharp for Meta and OpenAI HTTP integrations; Microsoft.Data.SqlClient for stored
procedures; built-in dependency injection and logging; Swagger/OpenAPI; FFmpeg for audio
normalization; xUnit for tests.

**Storage**: SQL Server for administrators, integration/bot configuration, FAQ rules,
contacts, conversations, message metadata, webhook inbox/outbox, and transcripts. Local
Development uses SQL Server LocalDB instance `(localdb)\MSSQLLocalDB`, database `WhatsAppBot`,
and Windows/Trusted Connection. Its configured connection string is
`Server=(localdb)\\MSSQLLocalDB;Database=WhatsAppBot;Trusted_Connection=True;TrustServerCertificate=True;`
in `appsettings.Development.json` (with the backslash escaped for JSON). LocalDB is not a
production-engine decision. Infrastructure owns connection/options binding and SqlClient/
DataSource configuration; Persistence owns repositories, data operations, and Stored
Procedures. Admin consumes only Api and never connects to LocalDB or SQL Server. Deployed
environments remain compatible with SQL Server and provide their connection through
environment-specific configuration/secret storage. Store original media in a private object
store behind a storage interface; persist only its reference and processing metadata in SQL
Server. User Secrets are for local secrets; production credentials belong in an approved
managed secret store.

**Testing**: xUnit unit tests for Business and matching rules; SQL integration tests for stored
procedures; API contract tests; Meta webhook signature/idempotency tests; external-integration
tests with HTTP stubs; opt-in end-to-end tests against a Meta test number and configured AI
provider.

**Target Platform**: ASP.NET Core 10 hosting environment with public HTTPS ingress for Meta
webhooks and a reachable SQL Server. Local Development uses `(localdb)\MSSQLLocalDB`; deployed
environments use a SQL Server connection supplied through configuration. Hosting/cloud vendor
is not fixed by this plan.

**Project Type**: Three-project web solution: administrative web app, backend API, and shared
contracts.

**Performance Goals**: Persist and acknowledge valid Meta webhook deliveries promptly (target
under 2 seconds at p95, without waiting for media downloads or AI); process replies
asynchronously. Admin list/detail screens should be usable within 2 seconds at p95 for the
tenant-sized POC and a demo-sized conversation history.

**Constraints**: Use only official Meta WhatsApp Business Platform / Cloud API; no WhatsApp Web,
scraping, or client simulation. Controllers and webhook handlers only validate transport
concerns and delegate. No SQL text in Business, Controllers, or Admin. External calls have
timeouts and cancellation; webhook delivery and outgoing message processing are idempotent.
Never log or expose access tokens, App Secret, Verify Token, AI keys, raw media, or sensitive
payloads. Respect WhatsApp customer opt-in, messaging-window and approved-template policy.
The FAQ feature is internal bot content, not a Meta message-template substitute.

**Scale/Scope**: The POC validates Standard and Coexistence, new messages, text/audio/image,
OpenAI processing, conversations, Bot/Humano attention, manual Admin replies, lifecycle control,
and a tenant/company boundary with multiple administrators and simple assignment. The initial
demonstration may use one active integration per tenant; this is not a global singleton and does
not exclude future multiple integrations per company. No teams, queues, billing, campaigns, CRM,
advanced permissions, or advanced reporting are implemented. Coexistence history, contacts and
`history`/`smb_app_state_sync`/`smb_message_echoes` remain a later extension.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Constitution requirement | Plan decision | Gate |
|---|---|---|
| Mission-driven, bounded POC | Three projects and only the requested demo journeys; no campaign/CRM features | PASS |
| Secure by default | Server-side secret references, webhook signature validation, input validation, no secrets in logs, private media store, and minimal retention | PASS, subject to retention approval before real customer data |
| Test-first quality | Unit, stored-procedure integration, API contract, webhook security/idempotency and external E2E checks planned | PASS |
| Observable operations | Structured logs, correlation IDs, processing/delivery states, and explicit failures | PASS |
| Simplicity/change control | Stored procedures and deterministic FAQ matching; no custom NLP or broker in POC | PASS |
| Official WhatsApp integration | Embedded Signup v4 and Cloud API only; Graph API isolated in Integrations | PASS |
| Business/Integrations boundary | Business owns use cases and policies; provider adapters remain behind interfaces | PASS |
| Admin/API boundary | Razor Pages communicates only with Api; no Admin SQL access or business rules | PASS |
| Tenant and future multi-number support | Tenant-scoped records use IntegrationId as the technical integration boundary; one active integration per tenant may remain an operational POC limit without a global singleton | PASS |
| Supported runtime | Use the explicitly requested .NET 10 LTS and latest supported 10.0.x servicing patch at build/deployment; verify lifecycle status before release. | PASS |

No architectural constitution rule is waived. Media/message retention must be approved before
use with real customer data; this plan uses private storage with an initial 30-day media expiry,
configurable later. Expiring an attachment never deletes its message or allowed metadata.

## System Design

### Solution Structure

```text
WhatsAppBot.sln
src/
├── WhatsAppBot.Admin/
│   ├── Pages/
│   │   ├── Account/                 # Login
│   │   ├── Dashboard/
│   │   ├── Settings/WhatsApp/
│   │   ├── Settings/FrequentResponses/
│   │   └── Conversations/
│   ├── Services/                    # Typed API client only
│   ├── Authentication/              # Secure server-side session/cookie
│   └── Program.cs
├── WhatsAppBot.Api/
│   ├── Controllers/                 # Admin REST controllers; delegate only
│   ├── Webhooks/                    # Meta challenge/transport validation; delegate only
│   ├── Middleware/                  # Exception envelope, correlation/request context
│   ├── Business/
│   │   ├── Abstractions/            # Use-case ports and integration/persistence contracts
│   │   ├── UseCases/                # Connect, FAQ CRUD, process inbound, query inbox
│   │   ├── Services/                # FAQ matching, bot mode, message orchestration
│   │   └── Workers/                 # Persisted webhook inbox/outbox processing
│   ├── Persistence/
│   │   ├── Repositories/            # Stored-procedure-only implementations
│   │   └── StoredProcedures/        # Bootstrap/schema/procedure scripts, names and typed parameters
│   ├── Infrastructure/
│   │   ├── Extensions/              # AddInfrastructure()
│   │   ├── Options/                 # Validated provider/runtime and SQL connection settings
│   │   ├── Security/                # Secret-store adapter, token protection
│   │   └── MediaStorage/            # Private object storage adapter
│   ├── Integrations/
│   │   ├── Extensions/              # AddIntegrations()
│   │   ├── Meta/                    # Embedded Signup exchange, Cloud API, webhooks
│   │   ├── OpenAI/                  # Reply, vision and transcription adapters
│   │   └── Audio/                   # OGG/Opus normalization adapter
│   ├── Models/
│   │   ├── Entities/
│   │   ├── Requests/
│   │   ├── Responses/
│   │   ├── Contracts/
│   │   └── Enums/
│   ├── Helpers/                    # Small, genuinely cross-cutting utilities
│   ├── Extensions/                 # AddBusiness(), AddPersistence()
│   └── Program.cs                  # Composition root only
└── WhatsAppBot.Shared/
    ├── DTOs/                        # Public DTO names end in DTO
    ├── Requests/
    ├── Responses/                   # ResponseE<T>
    ├── Contracts/
    └── Enums/
tests/
├── WhatsAppBot.Business.Tests/
├── WhatsAppBot.Persistence.Tests/
├── WhatsAppBot.Api.ContractTests/
└── WhatsAppBot.Integrations.Tests/
```

`WhatsAppBot.Api.Models` contains internal domain/persistence models and transport-boundary
types; `WhatsAppBot.Shared` contains types exchanged between Admin and Api. Avoid duplicating
the same type in both projects. Each layer references inward: Controllers/Webhooks → Business
→ abstractions; Persistence, Infrastructure, and Integrations implement abstractions; Program
registers extensions. `AddBusiness()`, `AddPersistence()`, `AddInfrastructure()`, and
`AddIntegrations()` register their own services. Program only configures host, middleware,
authentication, authorization, endpoints, Swagger, and the four extensions.

### Request and Message Flows

**Admin**: Razor Pages sends requests to Api through a typed server-side HTTP client. It
contains presentation validation only, never business rules or direct SQL. Api authenticates
administrators and returns `ResponseE<T>` DTO contracts.

**Meta onboarding**: The Admin launches Meta's Embedded Signup v4 JS flow and explicitly selects
Standard or Coexistence. It sends the one-time authorization code and returned WABA ID / optional
Phone Number ID to Api. Api uses `Meta:EmbeddedSignupConfigId` for Standard and
`Meta:CoexistenceEmbeddedSignupConfigId` for Coexistence when present, falling back to the
Standard config otherwise. A supplied Phone Number ID is validated; when absent, Api discovers
it from the WABA and continues only for one unambiguous match. Standard performs the normal
registration; Coexistence does not run Standard `/register`. Both flows subscribe the WABA to
webhooks. The resulting customer business token is stored in the configured secret store; SQL
stores only an opaque secret reference and non-secret identifiers/status.

**Webhook receipt**: GET verifies `hub.verify_token` and returns the challenge only on a
constant-time match. POST validates `X-Hub-Signature-256` against the raw request body using
the App Secret, validates the event envelope, and asks Business to persist an idempotent
inbox event. Return 200 only after durable persistence. A hosted worker processes the persisted
event so slow media and AI work do not hold the webhook request open.

**Inbound processing**: Resolve the authenticated tenant and integration, persist contact/message
and establish or reopen the same conversation identity. A closed conversation reopens as
`Abierta + Bot` when the bot is enabled; with the bot disabled it reopens without automatic
processing. Before any Bot work, check `AttentionMode`; `Humano > Bot` and a human-taken
conversation suppresses Bot candidates. Persist an audit event for automatic handoff when the
client requests a person, the bot cannot resolve, a business rule applies or a blocking error
occurs. For configured FAQ modes, normalize text (or the audio transcript) and compare against
active expressions; choose highest priority, tie-break by stable FAQ ID. On no match, invoke AI
only for fallback or AI-only mode. `FaqOnly` never invokes AI and records a no-match without
reply; `FaqThenAi` is the default and invokes AI only on a FAQ miss; `AiOnly` skips FAQ lookup.
These rules apply equally to text and audio transcripts. Image messages are passed to vision in
AI-enabled modes. Before any automatic or manual outbound item enters the outbox, Business
revalidates tenant authorization, lifecycle, attention mode, assignment/version and the current
WhatsApp messaging-window/template policy. A human transition invalidates pending Bot work. The
POC demo is limited to customer-initiated conversations within the permitted customer-service
window. If an official template is required, FAQ/AI/manual content is not sent as a substitute;
the result records a visible no-reply/policy outcome. Approved work is sent via the Meta adapter,
then reconciles `wamid` and sent/delivered/read/failed status webhooks. Duplicates do not create
duplicate messages or replies.

**Admin experiences**: Tenant-scoped login/session; dashboard; WhatsApp connection/status and bot
state/mode; frequent response CRUD; conversation list and detail timeline with direction/type,
lifecycle, attention mode, assignment, actor/origin, audit events, transcription, secure image
access, and whether the response source is FAQ, AI or Admin. Actions include take/handoff,
manual text reply, return-to-bot, close and reopen. Teams and queues are not part of this POC.

**Future Coexistence events**: The webhook envelope and normalized event dispatch must leave an
extension point for `history`, `smb_app_state_sync` and `smb_message_echoes`, but the POC ignores
those events safely and does not import historical conversations or synchronize contacts.

### Tenant and Conversation Control

Tenant scope is resolved from the authenticated Admin membership and carried through every
Business use case and repository port. `TenantId` is the ownership boundary; `IntegrationId`
identifies the WhatsApp number/connection within that tenant. Controllers do not accept an
untrusted tenant selector, and Persistence applies both boundaries to reads and writes.

Conversation control uses two independent dimensions: `AttentionMode` (`Bot` or `Humano`) and
`LifecycleStatus` (`Abierta` or `Cerrada`). A simple `AssignedAdminId` identifies the responsible
administrator. Taking, returning, closing, reopening, assigning, automatic handoff and manual
reply each produce an audit event. Automatic work and manual work use an expected conversation
version; the outbox rechecks the version and mode immediately before sending. This enforces
`Humano > Bot` without requiring a broker or a second workflow store.

The first inbound message after closure reuses the same conversation identity. If the bot is
enabled it transitions to `Abierta + Bot`; otherwise it reopens without automatic processing.
Manual Admin replies are initially text, are persisted as `Admin` origin, and pass the same
messaging-window/template guard as Bot replies.

## Constitution Check (Post-Design)

The design preserves the official Meta-only integration requirement, keeps all Graph API,
OpenAI, transcription, and image-analysis calls behind Integrations adapters, assigns
orchestration, tenant isolation and Bot/Humano guards to Business, and isolates SQL
Server/stored procedures in Persistence. Admin is a thin API consumer. Credentials are stored as
secure references and never returned in DTOs. Webhook events, messages, transitions and outbound
sends are idempotent and observable. Optimistic conversation versioning prevents concurrent Bot
and Humano sends. No custom NLP, unofficial WhatsApp library, or unnecessary broker is
introduced.

**Post-design result**: PASS for architecture and runtime target. Release remains conditional on
Meta app access/review, and approved retention/consent policy.

## Dependencies and External Configuration

### Meta Developers and WhatsApp Business prerequisites

- Meta Developer account and an application enabled for WhatsApp Business Platform / Cloud API.
- Create a Facebook Login for Business configuration with **Embedded Signup v4**, configured
  for Cloud API only; do not integrate v2, which Meta says is deprecated on 2026-10-15.
- Meta business portfolio, WABA, business phone number eligible for Cloud API, and an Admin
  who can authorize/select the assets. For an existing WhatsApp Business App number, validate
  the applicable Embedded Signup coexistence flow before onboarding.
- Configure HTTPS callback/webhook URL, webhook verify token, subscribe to `messages` and
  status updates, and subscribe the application to the connected WABA.
- Request the Cloud API permissions `whatsapp_business_management` and
  `whatsapp_business_messaging`. Complete applicable App Review / Advanced Access, business
  verification, and provider onboarding requirements before onboarding a customer WABA.
- Confirm whether the organization operates as a direct developer, Tech Provider, or Solution
  Partner; token type, App Review, customer onboarding, and payment/credit-line requirements
  depend on this status. The POC does not assume partner credit-line privileges.
- Confirm customer opt-in and WhatsApp conversation-window/template rules. FAQ answers and AI
  output are not approved Meta templates and cannot bypass those rules.

### Credentials and provider configuration

- **Meta**: App ID, App Secret, Facebook Login for Business configuration ID, webhook Verify
  Token, Graph API version, and the business access token exchanged server-to-server from the
  one-time Embedded Signup code. Never send the App Secret or access token to Admin/browser.
- **Secret storage**: `.NET User Secrets` locally; production uses an approved managed secret
  store. Store references in SQL, not secret values. If hosted in Azure, use Azure Key Vault;
  otherwise supply a conforming secret-store adapter. Verify Token is a secret too.
- **OpenAI** (selected POC provider): API key, configured text/vision model, transcription
  model, request timeout, and usage budget/limits. Keep model identifiers configurable.
- **SQL Server**: connection string from environment-specific configuration (Development
  LocalDB in `appsettings.Development.json`; deployed SQL Server settings from that
  environment's configuration/secret store), least-privilege service identity, Stored
  Procedures and TVP type for FAQ expression updates.
- **Development SQL Server**: LocalDB `(localdb)\MSSQLLocalDB`, database `WhatsAppBot`,
  Windows/Trusted Connection. Configure `ConnectionStrings:WhatsAppBot` in
  `appsettings.Development.json` as
  `Server=(localdb)\\MSSQLLocalDB;Database=WhatsAppBot;Trusted_Connection=True;TrustServerCertificate=True;`.
  Never embed connection strings in Business, Controllers, or repository classes.
- **SQL initialization**: clean-install scripts create the database, tables, relationships,
  indexes, Stored Procedures, and only required initial data on LocalDB. Infrastructure
  supplies configured SQL Server connection resources; Persistence alone executes data
  operations and Stored Procedures. LocalDB is Development-only and is not assumed in
  production.
- **Media storage**: private bucket/container, server-side credentials, maximum media size,
  retention/expiry and deletion policy. Do not use a public URL or persist Meta's temporary
  media URL as the durable record.
- **Admin**: initial administrator provisioning path, HTTPS origin, cookie protection keys,
  JWT signing key held in a secret store, and allowed origins/CSRF configuration.
- **Tenant authorization**: authenticated Admin memberships, an active tenant context, and a
  simple `AssignedAdminId` policy. The POC does not require teams, queues or complex roles.
- **Audio normalization**: deploy a pinned, patched FFmpeg binary or image package. WhatsApp
  voice notes are commonly OGG/Opus; OpenAI transcription currently documents mp3, mp4, mpeg,
  mpga, m4a, wav and webm (25 MB max), not OGG. Convert only when required; delete temporary
  media after the provider call.

### External-service dependencies and risks

| Dependency | Real external service needed for | Main risk / mitigation |
|---|---|---|
| Meta Embedded Signup + Graph Cloud API | Real onboarding, number registration, inbound/outbound messages, media retrieval and status webhooks | App review/permissions, account eligibility, messaging policy, rate/quality/throughput limits and version changes; isolate adapter, pin/configure Graph version and use a Meta test number first |
| OpenAI API | Live generated text, audio transcription and image understanding | Cost, latency, data-processing approval, rate limits, unsupported codec/size, model changes; configurable models, timeouts, usage caps, minimal media transmission, visible failure states |
| SQL Server | Persisting settings, FAQ rules, conversations, events and statuses | Connectivity/permissions/procedure drift; integration-test every procedure and use explicit transactions |
| Private media/object store | Retaining images/audio for processing and conversation display | Privacy, storage cost and public leakage; private access, short-lived authorized reads, retention cleanup and no public links |
| FFmpeg | Converting OGG/Opus voice note to supported transcription format | Native dependency, deployment/patching and untrusted media; size/time limits, isolated process, no shell interpolation, bounded resources, ephemeral files |

## Recommended Implementation Order

1. Confirm the current .NET 10 LTS patch at project setup. Obtain Meta access, opt-in/messaging,
   AI data-processing, and retention approvals before their respective live-service or real-data
   validation gates; these approvals do not block local Admin, SQL, FAQ, and mode development.
2. Create solution/projects, dependency directions, DI extension methods, validated Options,
   secure configuration providers, global exception middleware, structured logs and trace IDs.
3. Define the logical tenant, membership, integration, conversation-state, audit and
   concurrency model before schema work. Securely bootstrap the first administrator through a
   one-time command that delegates to Business, then implement Admin login/session and the typed
   Admin-to-Api client with an explicit tenant context.
4. Define SQL schema, stored procedures, least-privilege database identity and persistence
   tests; every read/write must enforce tenant plus integration scope and every transition must
   use an expected conversation version.
5. Implement bot configuration (enabled state and three modes) and frequent-response CRUD,
   matching and Admin screens. Validate these locally against SQL before any Meta or AI setup.
6. Configure Meta Embedded Signup v4 with explicit Standard/Coexistence branching, server-side
   code exchange, Phone Number ID validation/discovery, config-ID fallback, conditional number
   registration, token references, WABA subscription and truthful connection status.
7. Implement webhook verification/signature validation, durable idempotent inbox and status
   ingestion. Parse only new `messages`/status events for this POC; retain extension points for
   future `history`, `smb_app_state_sync` and `smb_message_echoes` without importing them now.
8. Implement text ingestion and conversation/message persistence first. Apply closed-conversation
   reopen rules, `Humano > Bot`, automatic handoff reasons and audit events. Verify `FaqOnly`
   matching/no-reply and `FaqThenAi` matching locally using a fake sender/provider; then enable
   the real Meta sender and OpenAI fallback behind their interfaces.
9. Add Admin conversation controls for take/handoff, manual text reply, return-to-bot, close,
   reopen, assignment and audit display. Revalidate state, tenant, version and messaging policy
   before every automatic or manual outbox item.
10. Add audio download/transcription/mode evaluation, then image download/analysis/reply. Use
    private media storage, authorized reads, 30-day initial expiry, metadata/transcript retention
    and cleanup without deleting messages.
11. Complete the final dashboard summary, security/resilience checks, tenant-isolation checks,
    timed SC-006 scenario, and end-to-end demo/release validation with consented test data.

## Project Structure

### Documentation (this feature)

```text
specs/001-whatsapp-business-bot-poc/
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── openapi.yaml
└── quickstart.md
```

`tasks.md` is maintained as the execution breakdown for this design and is generated or
updated in a separate task-planning phase.

### Source Code (repository root)

```text
WhatsAppBot.sln
src/
├── WhatsAppBot.Admin/
│   ├── Pages/{Account,Dashboard,Settings/WhatsApp,Settings/FrequentResponses,Conversations}/
│   ├── Services/
│   ├── Authentication/
│   └── Program.cs
├── WhatsAppBot.Api/
│   ├── Controllers/
│   ├── Webhooks/
│   ├── Middleware/
│   ├── Business/{Abstractions,UseCases,Services,Workers}/
│   ├── Persistence/{Repositories,StoredProcedures}/
│   ├── Infrastructure/{Extensions,Options,Security,MediaStorage}/
│   ├── Integrations/{Extensions,Meta,OpenAI,Audio}/
│   ├── Models/{Entities,Requests,Responses,Contracts,Enums}/
│   ├── Helpers/
│   ├── Extensions/
│   └── Program.cs
└── WhatsAppBot.Shared/{DTOs,Requests,Responses,Contracts,Enums}/
tests/
├── WhatsAppBot.Business.Tests/
├── WhatsAppBot.Persistence.Tests/
├── WhatsAppBot.Api.ContractTests/
└── WhatsAppBot.Integrations.Tests/
```

**Structure Decision**: Use exactly three deployable solution projects named as requested.
Admin is a Razor Pages API client; Api is the composition root and owns the seven mandated
layers; Shared carries public DTOs/contracts/enums only. Tests are separate test projects,
not additional production solution layers.

## Complexity Tracking

No constitution violations or unnecessary architectural projects are proposed. A hosted worker
and durable inbox/outbox are required to decouple webhook acknowledgement from slow AI/media
processing and prevent duplicate replies; SQL Server is reused as the durable work store, so
the POC does not add a broker. Tenant scope, a simple administrator assignment, and optimistic
conversation versioning are deliberate additions required to guarantee isolation and the
Bot/Humano priority without introducing teams, queues or a second coordination service.
