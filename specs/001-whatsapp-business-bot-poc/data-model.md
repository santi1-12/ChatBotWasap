# Data Model: POC de Bot de WhatsApp Business

The model is SQL Server-oriented but describes logical data, not a migration or implementation.
All mutable business data is accessed through stored procedures in Persistence. Use UTC
timestamps. Every WhatsApp-owned entity carries `TenantId` directly or through its parent and
uses `IntegrationId` as the technical integration boundary. The POC may operate with one active
integration per tenant for the initial demonstration, but the model must not be a global
singleton and must allow future multiple integrations per company.

## Entities

### Administrator

| Field | Type / rule | Purpose |
|---|---|---|
| AdminId | `bigint`, PK | Administrator identity |
| Email | `nvarchar(320)`, unique, normalized | Login name |
| PasswordHash | `nvarchar(512)` | Adaptive password hash; never plaintext |
| IsActive | `bit` | Login enabled state |
| CreatedAtUtc | `datetime2` | Audit |
| LastLoginAtUtc | `datetime2`, nullable | Audit |

Authentication query returns the hash to Business for password verification. A missing,
inactive, or invalid account returns the same public authentication failure. Do not persist
Meta/API secrets here.

### Tenant / Company

| Field | Type / rule | Purpose |
|---|---|---|
| TenantId | `uniqueidentifier`, PK | Company ownership and authorization boundary |
| LegalName | `nvarchar(256)` | Company name |
| IsActive | `bit` | Tenant availability |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |

Every integration, bot configuration, contact, conversation, message, media reference and
retention decision belongs to one tenant. Tenant identity is resolved from the authenticated
Admin context; a request MUST NOT choose a tenant by trusting an arbitrary body field.

### TenantMembership

| Field | Type / rule | Purpose |
|---|---|---|
| TenantMembershipId | `bigint`, PK | Membership identity |
| TenantId | `uniqueidentifier`, FK | Company scope |
| AdminId | `bigint`, FK | Administrator with access |
| IsActive | `bit` | Membership access state |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |

The POC supports multiple administrators per tenant and simple assignment through
`AssignedAdminId`. Teams, queues, complex roles, plans and feature-billing rules are future
extensions rather than current entities.

### WhatsAppIntegration

| Field | Type / rule | Purpose |
|---|---|---|
| IntegrationId | `uniqueidentifier`, PK | Connection boundary for all WhatsApp data |
| TenantId | `uniqueidentifier`, FK | Owning company |
| WabaId | `nvarchar(64)`, nullable until connected | Meta WhatsApp Business Account ID |
| PhoneNumberId | `nvarchar(64)`, nullable and unique while active | Meta Cloud API phone-number ID |
| BusinessPhoneNumber | `nvarchar(32)`, nullable until connected | Displayable E.164-format number |
| DisplayName | `nvarchar(256)`, nullable | Meta-provided display name |
| OnboardingType | enum/string | Standard or Coexistence |
| ConnectionState | enum/string | NotConnected, Configuring, Connected, Error |
| ConnectedAtUtc | `datetime2`, nullable | Successful connection timestamp |
| LastConnectionErrorCode | `nvarchar(100)`, nullable | Safe diagnostic identifier only |
| AccessTokenSecretRef | `nvarchar(512)`, nullable until connected | Opaque secret-store reference |
| MetaAppSecretRef | `nvarchar(512)` | Secret reference; app-wide value may be held in deployment configuration instead |
| WebhookVerifyTokenRef | `nvarchar(512)` | Secret reference; never sent to Admin |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |
| IsActive | `bit` | Operationally active for its tenant |

The initial demonstration may enforce one active integration per tenant, but this is not a global
singleton rule and does not exclude multiple tenants. Future integrations remain separated by both
`TenantId` and `IntegrationId`. `OnboardingType` is persisted as the selected Standard or
Coexistence flow. Coexistence uses the dedicated config ID when present and falls back to the
Standard config ID; `PhoneNumberId` uses the received value or an unambiguous WABA discovery.
Provision one unconnected `NotConnected` integration and its `FaqThenAi`/bot-disabled
configuration as part of first-time setup when needed; this is a POC connection slot, not a
global connected-number singleton.
Store the Meta App ID, Embedded Signup configuration ID, Graph API version and public webhook
URL as non-secret deployment configuration unless the application later needs per-integration
values.

### BotConfiguration

| Field | Type / rule | Purpose |
|---|---|---|
| IntegrationId | `uniqueidentifier`, PK/FK | Per-number configuration |
| IsBotEnabled | `bit` | Automatic processing switch; defaults false until connected/explicitly activated |
| ReplyMode | enum | `FaqOnly`, `FaqThenAi`, `AiOnly`; default `FaqThenAi` |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |

AI provider keys and model details are deployment configuration/secret references, not fields
returned in public DTOs.

### FrequentResponse

| Field | Type / rule | Purpose |
|---|---|---|
| FrequentResponseId | `bigint`, PK | Stable priority tie-break identity |
| IntegrationId | `uniqueidentifier`, FK | Configuration scope |
| QuestionOrIntent | `nvarchar(500)` | Admin-entered question, intent, or description |
| AnswerText | `nvarchar(max)` | Configured response content |
| Priority | `int` | Higher number wins |
| Category | `nvarchar(100)`, nullable | Optional user-defined category; no category table needed in POC |
| IsActive | `bit` | Search eligibility |
| CreatedAtUtc / ModifiedAtUtc | `datetime2` | Audit |

Require non-empty `QuestionOrIntent`, answer, at least one non-empty expression, and a defined
priority range (recommend 0-1000). Update `ModifiedAtUtc` on every successful edit or state
change; creation time is immutable.

### FrequentResponseExpression

| Field | Type / rule | Purpose |
|---|---|---|
| ExpressionId | `bigint`, PK | Expression identity |
| FrequentResponseId | `bigint`, FK | Parent response |
| ExpressionText | `nvarchar(500)` | Phrase/word to match |
| NormalizedExpression | computed by Business or stored normalized value | Case/spacing-normalized match value |

One frequent response has one or more expressions. Enforce unique normalized expressions per
response. Replace the set atomically during edit using a SQL table-valued parameter or a
transactional stored procedure.

### Contact

| Field | Type / rule | Purpose |
|---|---|---|
| ContactId | `bigint`, PK | Contact identity |
| TenantId | `uniqueidentifier`, FK | Owning company |
| IntegrationId | `uniqueidentifier`, FK | WhatsApp number scope |
| WhatsAppUserId | `nvarchar(64)` | WhatsApp `wa_id`; unique with IntegrationId |
| DisplayName | `nvarchar(256)`, nullable | Profile name when supplied |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |

### Conversation

| Field | Type / rule | Purpose |
|---|---|---|
| ConversationId | `bigint`, PK | Conversation identity |
| TenantId | `uniqueidentifier`, FK | Owning company and authorization scope |
| IntegrationId | `uniqueidentifier`, FK | WhatsApp number scope |
| ContactId | `bigint`, FK | Customer contact |
| AttentionMode | enum/string | Bot, Humano |
| LifecycleStatus | enum/string | Abierta, Cerrada |
| AssignedAdminId | `bigint`, nullable FK | Simple responsible administrator |
| Version | `rowversion`/opaque version | Optimistic concurrency guard |
| ClosedAtUtc | `datetime2`, nullable | Last close timestamp |
| LastAttentionChangedAtUtc | `datetime2`, nullable | Attention transition audit |
| CreatedAtUtc | `datetime2` | First interaction |
| LastMessageAtUtc | `datetime2` | Admin list sort |

There is one conversation identity per active `(IntegrationId, ContactId)` in the POC. Closing and
reopening does not create a new conversation or discard history. A new inbound message reopens a
closed conversation as `Abierta + Bot` when the bot is enabled; when the bot is disabled it reopens
without automatic processing and remains available for human attention. `LifecycleStatus` and
`AttentionMode` are independent dimensions and MUST NOT be collapsed into one enum.

The effective processing rule is `Humano > Bot`: while `AttentionMode = Humano`, no bot candidate
may be created for that conversation. Before an automatic or manual outbound item enters the
outbox, the current mode, lifecycle, assignment and `Version` must be revalidated. A conditional
transition on the expected version prevents two Admin actions or a Bot/Humano handoff from
overwriting each other.

### ConversationAuditEvent

| Field | Type / rule | Purpose |
|---|---|---|
| ConversationAuditEventId | `bigint`, PK | Audit event identity |
| TenantId | `uniqueidentifier`, FK | Owning company |
| ConversationId | `bigint`, FK | Conversation |
| EventType | enum/string | Take, AutomaticHandoff, ReturnToBot, Assign, Close, Reopen, ManualReply |
| PreviousAttentionMode | enum/string, nullable | Prior attention state |
| NewAttentionMode | enum/string, nullable | Resulting attention state |
| PreviousLifecycleStatus | enum/string, nullable | Prior lifecycle state |
| NewLifecycleStatus | enum/string, nullable | Resulting lifecycle state |
| ActorType | enum/string | Admin, Bot, System |
| ActorAdminId | `bigint`, nullable FK | Administrator who acted |
| ReasonCode | `nvarchar(100)`, nullable | Handoff or transition reason |
| OccurredAtUtc | `datetime2` | Event timestamp |
| ExpectedVersion / ResultingVersion | opaque version, nullable | Concurrency evidence |

Automatic handoff reasons are allowlisted as customer request, bot inability, business rule or
blocking error. The event is retained independently of message content so the Admin can explain
who acted, what changed and why.

### Message

| Field | Type / rule | Purpose |
|---|---|---|
| MessageId | `bigint`, PK | Internal identity |
| TenantId | `uniqueidentifier`, FK | Owning company and authorization scope |
| IntegrationId | `uniqueidentifier`, FK | Scope and idempotency key boundary |
| ConversationId | `bigint`, FK | Conversation |
| ProviderMessageId | `nvarchar(256)`, nullable, unique with IntegrationId | Meta inbound/outbound `wamid` |
| Direction | enum | Inbound or Outbound |
| MessageType | enum | Text, Audio, Image, Unsupported; extensible for future types |
| ActorType | enum/string | Customer, Bot, Admin, System |
| Origin | enum/string | MetaCloudApi, AdminPanel, System, FutureWhatsAppBusinessApp |
| CreatedByAdminId | `bigint`, nullable FK | Admin responsible for a manual message |
| ContentText | `nvarchar(max)`, nullable | Text/caption or sent response |
| ReplySource | enum, nullable | FrequentResponse, AI, Admin |
| FrequentResponseId | `bigint`, nullable FK | Rule used for response |
| SourceInboundMessageId | `bigint`, nullable FK | Inbound message that caused an automatic reply |
| ProcessingState | enum | Received, Processing, Completed, Failed, NoReply |
| DeliveryState | enum, nullable | Pending, Sent, Delivered, Read, Failed |
| ProviderTimestampUtc | `datetime2`, nullable | Meta event timestamp |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Local audit |
| FailureCode | `nvarchar(100)`, nullable | Safe diagnostic only; includes policy outcomes such as `MessagingWindowClosed` or `TemplateRequired` when no outbound message is sent |

Inbound Meta message ID is unique per integration to prevent replay duplicates. Keep
`ProcessingState` separate from `DeliveryState`: generated does not imply sent, and sent does
not imply delivered/read. A policy-blocked reply remains a no-reply processing result with a
safe `FailureCode`; do not create a successful outbound delivery record for content that Meta
requires to be sent as an approved template. The Admin `MessageDTO.outcomeCode` is a safe
allowlisted projection of this field, not the raw provider error. Manual Admin messages use
`ActorType = Admin`, `Origin = AdminPanel` and `ReplySource = Admin`; automatic Bot messages
identify their FAQ or AI source. `SourceInboundMessageId` is unique for an automatic reply where
the workflow requires one reply per inbound message.

### MediaAttachment

| Field | Type / rule | Purpose |
|---|---|---|
| MediaAttachmentId | `bigint`, PK | Attachment |
| TenantId | `uniqueidentifier`, FK | Owning company |
| MessageId | `bigint`, FK, unique when one attachment per message | Message association |
| MetaMediaId | `nvarchar(128)`, nullable | Meta media identifier |
| MediaKind | enum | Audio or Image in POC; extensible for future media |
| MimeType | `nvarchar(128)`, nullable | Validated content type |
| SizeBytes | `bigint`, nullable | Validated limit |
| StorageReference | `nvarchar(512)`, nullable | Private object-store reference; never a public URL |
| AvailabilityState | enum | Pending, Available, Expired, Failed, Deleted |
| CreatedAtUtc / ExpiresAtUtc | `datetime2` | Initial 30-day retention and future configurable expiry |

Store original files outside SQL Server. Only authorized Admin requests may stream media; use
short-lived access or an API proxy and enforce authorization by conversation. Configured
retention cleanup removes or expires the object and marks metadata consistently; it MUST NOT
delete the conversation message. Expiration preserves allowed metadata and any available
transcript. Video, documents, location, contacts and other media are future extensions.

### Transcript

| Field | Type / rule | Purpose |
|---|---|---|
| MessageId | `bigint`, PK/FK | Audio message being transcribed |
| TranscriptText | `nvarchar(max)`, nullable | Transcript when successful |
| State | enum | Pending, Completed, Failed |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |

### WebhookInboxEvent

| Field | Type / rule | Purpose |
|---|---|---|
| WebhookInboxEventId | `bigint`, PK | Durable delivery record |
| IntegrationId | `uniqueidentifier`, nullable FK | Resolved integration scope |
| EventKey | `nvarchar(256)`, unique | Provider message/status identifier or stable event hash |
| EventType | `nvarchar(80)` | Message, status, account notification, future Coexistence event |
| NormalizedEventJson | `nvarchar(max)` | Allowlisted event fields required for processing; no raw webhook body or secrets |
| State | enum | Received, Processing, Completed, Failed |
| AttemptCount | `int` | Retry control |
| ReceivedAtUtc / ProcessedAtUtc | `datetime2`, nullable | Audit |
| LastFailureCode | `nvarchar(100)`, nullable | Safe diagnostic |

Validate Meta signature against raw bytes before parsing/storing. Do not retain the complete raw
webhook body; store only the normalized fields needed by the worker, with size limits and short
retention before the event is marked complete. For this POC, `history`, `smb_app_state_sync` and
`smb_message_echoes` are outside processing scope and must be safely ignored or recorded as an
unsupported future event without creating messages. If Meta does not provide an event ID for an
event, derive an idempotency key from stable provider IDs and event type, not a random request ID.

### MessageOutbox

| Field | Type / rule | Purpose |
|---|---|---|
| MessageId | `bigint`, PK/FK | Pending outbound message |
| TenantId | `uniqueidentifier`, FK | Owning company |
| IntegrationId | `uniqueidentifier`, FK | Sending number |
| State | enum | Pending, Sending, Sent, Failed |
| AttemptCount | `int` | Bounded retries |
| NextAttemptAtUtc | `datetime2` | Backoff scheduling |
| CreatedAtUtc / UpdatedAtUtc | `datetime2` | Audit |

Use a claim/lease procedure to prevent concurrent workers from sending the same message. Before
claim/send, revalidate tenant authorization, conversation version, lifecycle and attention mode;
an automatic item must be discarded safely if a human has taken the conversation. Retry only
failures known to be transient and preserve the Meta message ID once accepted.

## Relationships and Indexes

- `Tenant 1—* TenantMembership *—1 Administrator`
- `Tenant 1—* WhatsAppIntegration 1—1 BotConfiguration`
- `WhatsAppIntegration 1—* FrequentResponse 1—* FrequentResponseExpression`
- `WhatsAppIntegration 1—* Contact 1—* Conversation 1—* Message`
- `Conversation 1—* ConversationAuditEvent`
- `Message 1—0..1 MediaAttachment`, `Message 1—0..1 Transcript`,
  `Message 1—0..1 MessageOutbox`
- `WhatsAppIntegration 1—* WebhookInboxEvent`
- Unique: `(TenantId, IntegrationId)`, `(TenantId, WhatsAppUserId)`,
  `(TenantId, IntegrationId, ProviderMessageId)`, `(TenantId, IntegrationId, PhoneNumberId)`
  when active, `EventKey` within its tenant boundary, and
  `(FrequentResponseId, NormalizedExpression)`.
- Query indexes: `(TenantId, IntegrationId, IsActive, Priority DESC, FrequentResponseId)` for
  FAQ candidates; `(TenantId, IntegrationId, LastMessageAtUtc DESC)` for conversation listing;
  `(TenantId, ConversationId, CreatedAtUtc, MessageId)` for message history;
  `(TenantId, ConversationId, LifecycleStatus, AttentionMode)` for action guards;
  `(TenantId, AssignedAdminId, LifecycleStatus)` for simple assignment; and
  `(State, NextAttemptAtUtc)` for inbox/outbox workers.

## Stored Procedure Inventory

Persistence must call only stored procedures, with typed parameters and explicit transactions
where an operation spans parent/child rows.

| Stored procedure | Responsibility |
|---|---|
| `sp_Admin_GetByEmailForAuthentication` | Retrieve normalized email, password hash and active state for Business password verification |
| `sp_Admin_UpdateLastLogin` | Update successful login audit |
| `sp_Admin_CreateFirstAdministrator` | One-time insert for initial administrator bootstrap; accept only normalized email and adaptive password hash |
| `sp_WhatsAppIntegration_GetCurrent` | Load current integration and secret references |
| `sp_WhatsAppIntegration_SaveSignupResult` | Persist WABA/phone IDs and secret reference after server-side exchange |
| `sp_WhatsAppIntegration_SetConnectionState` | Set Configuring/Connected/Error and safe error code |
| `sp_WhatsAppIntegration_Disconnect` | Disable connection/bot and clear active state without exposing provider secrets |
| `sp_BotConfiguration_Get` | Read enable switch and response mode |
| `sp_BotConfiguration_Upsert` | Save enable switch/mode |
| `sp_FrequentResponse_List` | Paginated FAQ list |
| `sp_FrequentResponse_GetById` | Load response plus expressions |
| `sp_FrequentResponse_Create` | Atomically insert response and expression TVP |
| `sp_FrequentResponse_Update` | Atomically edit response and replace expressions |
| `sp_FrequentResponse_SetActive` | Activate/deactivate with modified timestamp |
| `sp_FrequentResponse_Delete` | Delete response and expressions atomically |
| `sp_FrequentResponse_ListActiveCandidates` | Return active responses and expressions ordered by priority for Business matching |
| `sp_Contact_UpsertWhatsApp` | Insert/update contact by integration and WhatsApp user ID |
| `sp_Conversation_GetOrCreate` | Get or create conversation for integration/contact |
| `sp_Conversation_List` | Paginate conversation list sorted by last message |
| `sp_Conversation_GetDetail` | Return authorized conversation metadata and paginated messages |
| `sp_Conversation_ApplyAction` | Atomically take, hand off, return to bot, close or reopen with expected-version guard |
| `sp_Conversation_RecordAuditEvent` | Persist actor, reason, transition and resulting version |
| `sp_Conversation_SetAssignment` | Set or clear the simple `AssignedAdminId` within a tenant |
| `sp_Message_InsertInbound` | Idempotently record inbound provider message |
| `sp_Message_CreateOutboundPending` | Persist outbound response and outbox item atomically |
| `sp_Message_CreateAdminOutboundPending` | Persist a manual Admin reply and outbox item atomically |
| `sp_Message_UpdateOutboundProviderResult` | Store provider message ID / send failure and delivery state |
| `sp_Message_ApplyDeliveryStatus` | Reconcile sent/delivered/read/failed webhook status |
| `sp_MediaAttachment_Upsert` | Store media metadata and private storage reference |
| `sp_Transcript_Upsert` | Save transcript state and transcript text |
| `sp_WebhookInbox_Insert` | Persist webhook event idempotently before 200 acknowledgement |
| `sp_WebhookInbox_ClaimBatch` | Lease pending events for a worker |
| `sp_WebhookInbox_SetState` | Complete/fail/retry a processed event |
| `sp_MessageOutbox_ClaimBatch` | Lease outbound items for a worker |
| `sp_MessageOutbox_SetState` | Complete/fail/retry outbound item with bounded attempts |

Tenant membership and tenant-scoped integration/configuration procedures are part of the future
implementation breakdown. This document intentionally does not create tables, migrations or SQL;
the inventory describes the logical persistence boundary only. The execution breakdown remains
in `tasks.md`, which is not changed by this documentation update.

If refresh tokens are selected instead of short-lived API tokens, add dedicated hashed refresh
token procedures; the POC plan otherwise uses short-lived API access tokens and a protected
Admin server-side session.
