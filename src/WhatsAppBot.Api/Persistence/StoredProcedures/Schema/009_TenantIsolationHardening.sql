:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [WhatsAppBot];
GO

IF EXISTS (SELECT 1 FROM dbo.BotConfiguration WHERE TenantId IS NULL)
    THROW 51060, N'A bot configuration is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.FrequentResponse WHERE TenantId IS NULL)
    THROW 51061, N'A frequent response is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.Contact WHERE TenantId IS NULL)
    THROW 51062, N'A contact is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.Conversation WHERE TenantId IS NULL)
    THROW 51063, N'A conversation is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.Message WHERE TenantId IS NULL)
    THROW 51064, N'A message is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.MediaAttachment WHERE TenantId IS NULL)
    THROW 51065, N'A media attachment is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.Transcript WHERE TenantId IS NULL)
    THROW 51066, N'A transcript is missing TenantId.', 1;
IF EXISTS (SELECT 1 FROM dbo.WebhookInboxEvent WHERE TenantId IS NULL OR IntegrationId IS NULL)
    THROW 51067, N'A webhook inbox event is missing tenant ownership.', 1;
IF EXISTS (SELECT 1 FROM dbo.MessageOutbox WHERE TenantId IS NULL)
    THROW 51068, N'An outbox item is missing TenantId.', 1;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FrequentResponse') AND name = N'IX_FrequentResponse_Tenant_ActivePriority')
    DROP INDEX IX_FrequentResponse_Tenant_ActivePriority ON dbo.FrequentResponse;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Contact') AND name = N'IX_Contact_Tenant_User')
    DROP INDEX IX_Contact_Tenant_User ON dbo.Contact;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Conversation') AND name = N'IX_Conversation_Tenant_LastMessage')
    DROP INDEX IX_Conversation_Tenant_LastMessage ON dbo.Conversation;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Conversation') AND name = N'IX_Conversation_Tenant_State')
    DROP INDEX IX_Conversation_Tenant_State ON dbo.Conversation;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Message') AND name = N'IX_Message_Tenant_ConversationChronology')
    DROP INDEX IX_Message_Tenant_ConversationChronology ON dbo.Message;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment') AND name = N'IX_MediaAttachment_Tenant_Availability')
    DROP INDEX IX_MediaAttachment_Tenant_Availability ON dbo.MediaAttachment;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment') AND name = N'IX_MediaAttachment_Expired')
    DROP INDEX IX_MediaAttachment_Expired ON dbo.MediaAttachment;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Transcript') AND name = N'IX_Transcript_Tenant_State')
    DROP INDEX IX_Transcript_Tenant_State ON dbo.Transcript;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WebhookInboxEvent') AND name = N'IX_WebhookInbox_Tenant_State')
    DROP INDEX IX_WebhookInbox_Tenant_State ON dbo.WebhookInboxEvent;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MessageOutbox') AND name = N'IX_MessageOutbox_Tenant_State')
    DROP INDEX IX_MessageOutbox_Tenant_State ON dbo.MessageOutbox;
GO

ALTER TABLE dbo.BotConfiguration ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.FrequentResponse ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.Contact ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.Conversation ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.Message ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.MediaAttachment ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.Transcript ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.WebhookInboxEvent ALTER COLUMN TenantId uniqueidentifier NOT NULL;
ALTER TABLE dbo.WebhookInboxEvent ALTER COLUMN IntegrationId uniqueidentifier NOT NULL;
ALTER TABLE dbo.MessageOutbox ALTER COLUMN TenantId uniqueidentifier NOT NULL;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WebhookInboxEvent')
      AND name = N'UX_WebhookInbox_EventKey'
)
    DROP INDEX UX_WebhookInbox_EventKey ON dbo.WebhookInboxEvent;
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WebhookInboxEvent')
      AND name = N'UX_WebhookInbox_Tenant_EventKey'
)
    CREATE UNIQUE INDEX UX_WebhookInbox_Tenant_EventKey
        ON dbo.WebhookInboxEvent(TenantId, EventKey);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_Tenant_Integration'
)
    CREATE UNIQUE INDEX UX_WhatsAppIntegration_Tenant_Integration
        ON dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BotConfiguration')
      AND name = N'UX_BotConfiguration_Tenant_Integration'
)
    CREATE UNIQUE INDEX UX_BotConfiguration_Tenant_Integration
        ON dbo.BotConfiguration(TenantId, IntegrationId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FrequentResponse')
      AND name = N'UX_FrequentResponse_Tenant_Id'
)
    CREATE UNIQUE INDEX UX_FrequentResponse_Tenant_Id
        ON dbo.FrequentResponse(TenantId, FrequentResponseId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Contact')
      AND name = N'UX_Contact_Tenant_Id'
)
    CREATE UNIQUE INDEX UX_Contact_Tenant_Id
        ON dbo.Contact(TenantId, ContactId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'UX_Conversation_Tenant_Id'
)
    CREATE UNIQUE INDEX UX_Conversation_Tenant_Id
        ON dbo.Conversation(TenantId, ConversationId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'UX_Message_Tenant_Id'
)
    CREATE UNIQUE INDEX UX_Message_Tenant_Id
        ON dbo.Message(TenantId, MessageId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'UX_MediaAttachment_Tenant_Message'
)
    CREATE UNIQUE INDEX UX_MediaAttachment_Tenant_Message
        ON dbo.MediaAttachment(TenantId, MessageId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Transcript')
      AND name = N'UX_Transcript_Tenant_Message'
)
    CREATE UNIQUE INDEX UX_Transcript_Tenant_Message
        ON dbo.Transcript(TenantId, MessageId);
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MessageOutbox')
      AND name = N'UX_MessageOutbox_Tenant_Message'
)
    CREATE UNIQUE INDEX UX_MessageOutbox_Tenant_Message
        ON dbo.MessageOutbox(TenantId, MessageId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FrequentResponse') AND name = N'IX_FrequentResponse_Tenant_ActivePriority')
    CREATE INDEX IX_FrequentResponse_Tenant_ActivePriority
        ON dbo.FrequentResponse(TenantId, IntegrationId, IsActive, Priority DESC, FrequentResponseId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Contact') AND name = N'IX_Contact_Tenant_User')
    CREATE INDEX IX_Contact_Tenant_User
        ON dbo.Contact(TenantId, IntegrationId, WhatsAppUserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Conversation') AND name = N'IX_Conversation_Tenant_LastMessage')
    CREATE INDEX IX_Conversation_Tenant_LastMessage
        ON dbo.Conversation(TenantId, IntegrationId, LastMessageAtUtc DESC, ConversationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Conversation') AND name = N'IX_Conversation_Tenant_State')
    CREATE INDEX IX_Conversation_Tenant_State
        ON dbo.Conversation(TenantId, LifecycleStatus, AttentionMode, AssignedAdminId, ConversationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Message') AND name = N'IX_Message_Tenant_ConversationChronology')
    CREATE INDEX IX_Message_Tenant_ConversationChronology
        ON dbo.Message(TenantId, ConversationId, CreatedAtUtc, MessageId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment') AND name = N'IX_MediaAttachment_Tenant_Availability')
    CREATE INDEX IX_MediaAttachment_Tenant_Availability
        ON dbo.MediaAttachment(TenantId, AvailabilityState, ExpiresAtUtc, MessageId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Transcript') AND name = N'IX_Transcript_Tenant_State')
    CREATE INDEX IX_Transcript_Tenant_State
        ON dbo.Transcript(TenantId, State, MessageId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WebhookInboxEvent') AND name = N'IX_WebhookInbox_Tenant_State')
    CREATE INDEX IX_WebhookInbox_Tenant_State
        ON dbo.WebhookInboxEvent(TenantId, State, ReceivedAtUtc, WebhookInboxEventId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MessageOutbox') AND name = N'IX_MessageOutbox_Tenant_State')
    CREATE INDEX IX_MessageOutbox_Tenant_State
        ON dbo.MessageOutbox(TenantId, State, NextAttemptAtUtc, MessageId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BotConfiguration_TenantIntegration')
    ALTER TABLE dbo.BotConfiguration ADD CONSTRAINT FK_BotConfiguration_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FrequentResponse_TenantIntegration')
    ALTER TABLE dbo.FrequentResponse ADD CONSTRAINT FK_FrequentResponse_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Contact_TenantIntegration')
    ALTER TABLE dbo.Contact ADD CONSTRAINT FK_Contact_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Conversation_TenantIntegration')
    ALTER TABLE dbo.Conversation ADD CONSTRAINT FK_Conversation_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Conversation_TenantContact')
    ALTER TABLE dbo.Conversation ADD CONSTRAINT FK_Conversation_TenantContact
        FOREIGN KEY (TenantId, ContactId)
        REFERENCES dbo.Contact(TenantId, ContactId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Message_TenantIntegration')
    ALTER TABLE dbo.Message ADD CONSTRAINT FK_Message_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Message_TenantConversation')
    ALTER TABLE dbo.Message ADD CONSTRAINT FK_Message_TenantConversation
        FOREIGN KEY (TenantId, ConversationId)
        REFERENCES dbo.Conversation(TenantId, ConversationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Message_TenantFrequentResponse')
    ALTER TABLE dbo.Message ADD CONSTRAINT FK_Message_TenantFrequentResponse
        FOREIGN KEY (TenantId, FrequentResponseId)
        REFERENCES dbo.FrequentResponse(TenantId, FrequentResponseId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MediaAttachment_TenantMessage')
    ALTER TABLE dbo.MediaAttachment ADD CONSTRAINT FK_MediaAttachment_TenantMessage
        FOREIGN KEY (TenantId, MessageId)
        REFERENCES dbo.Message(TenantId, MessageId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Transcript_TenantMessage')
    ALTER TABLE dbo.Transcript ADD CONSTRAINT FK_Transcript_TenantMessage
        FOREIGN KEY (TenantId, MessageId)
        REFERENCES dbo.Message(TenantId, MessageId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WebhookInbox_TenantIntegration')
    ALTER TABLE dbo.WebhookInboxEvent ADD CONSTRAINT FK_WebhookInbox_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessageOutbox_TenantIntegration')
    ALTER TABLE dbo.MessageOutbox ADD CONSTRAINT FK_MessageOutbox_TenantIntegration
        FOREIGN KEY (TenantId, IntegrationId)
        REFERENCES dbo.WhatsAppIntegration(TenantId, IntegrationId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessageOutbox_TenantMessage')
    ALTER TABLE dbo.MessageOutbox ADD CONSTRAINT FK_MessageOutbox_TenantMessage
        FOREIGN KEY (TenantId, MessageId)
        REFERENCES dbo.Message(TenantId, MessageId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessageOutbox_TenantSourceInbound')
    ALTER TABLE dbo.MessageOutbox ADD CONSTRAINT FK_MessageOutbox_TenantSourceInbound
        FOREIGN KEY (TenantId, SourceInboundMessageId)
        REFERENCES dbo.Message(TenantId, MessageId);
GO
