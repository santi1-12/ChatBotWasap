:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [WhatsAppBot];
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_ActiveSlot'
)
    DROP INDEX UX_WhatsAppIntegration_ActiveSlot ON dbo.WhatsAppIntegration;
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_PhoneNumberId_Active'
)
    DROP INDEX UX_WhatsAppIntegration_PhoneNumberId_Active ON dbo.WhatsAppIntegration;
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_Tenant_ActiveSlot'
      AND filter_definition LIKE N'%TenantId%'
)
    DROP INDEX UX_WhatsAppIntegration_Tenant_ActiveSlot ON dbo.WhatsAppIntegration;
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_Tenant_PhoneNumberId_Active'
      AND filter_definition LIKE N'%TenantId%'
)
    DROP INDEX UX_WhatsAppIntegration_Tenant_PhoneNumberId_Active ON dbo.WhatsAppIntegration;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TenantMembership')
      AND name = N'UX_TenantMembership_Tenant_Admin'
)
    CREATE UNIQUE INDEX UX_TenantMembership_Tenant_Admin
        ON dbo.TenantMembership(TenantId, AdminId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TenantMembership')
      AND name = N'IX_TenantMembership_Admin'
)
    CREATE INDEX IX_TenantMembership_Admin
        ON dbo.TenantMembership(AdminId, IsActive, TenantId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'IX_WhatsAppIntegration_Tenant'
)
    CREATE INDEX IX_WhatsAppIntegration_Tenant
        ON dbo.WhatsAppIntegration(TenantId, IsActive, IntegrationId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_Tenant_ActiveSlot'
)
    CREATE UNIQUE INDEX UX_WhatsAppIntegration_Tenant_ActiveSlot
        ON dbo.WhatsAppIntegration(TenantId, ActiveSlot)
        WHERE IsActive = 1;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UX_WhatsAppIntegration_Tenant_PhoneNumberId_Active'
)
    CREATE UNIQUE INDEX UX_WhatsAppIntegration_Tenant_PhoneNumberId_Active
        ON dbo.WhatsAppIntegration(TenantId, PhoneNumberId)
        WHERE IsActive = 1 AND PhoneNumberId IS NOT NULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FrequentResponse')
      AND name = N'IX_FrequentResponse_Tenant_ActivePriority'
)
    CREATE INDEX IX_FrequentResponse_Tenant_ActivePriority
        ON dbo.FrequentResponse(TenantId, IntegrationId, IsActive, Priority DESC, FrequentResponseId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Contact')
      AND name = N'IX_Contact_Tenant_User'
)
    CREATE INDEX IX_Contact_Tenant_User
        ON dbo.Contact(TenantId, IntegrationId, WhatsAppUserId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'IX_Conversation_Tenant_LastMessage'
)
    CREATE INDEX IX_Conversation_Tenant_LastMessage
        ON dbo.Conversation(TenantId, IntegrationId, LastMessageAtUtc DESC, ConversationId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'IX_Conversation_Tenant_State'
)
    CREATE INDEX IX_Conversation_Tenant_State
        ON dbo.Conversation(TenantId, LifecycleStatus, AttentionMode, AssignedAdminId, ConversationId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'IX_Message_Tenant_ConversationChronology'
)
    CREATE INDEX IX_Message_Tenant_ConversationChronology
        ON dbo.Message(TenantId, ConversationId, CreatedAtUtc, MessageId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'IX_MediaAttachment_Tenant_Availability'
)
    CREATE INDEX IX_MediaAttachment_Tenant_Availability
        ON dbo.MediaAttachment(TenantId, AvailabilityState, ExpiresAtUtc, MessageId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Transcript')
      AND name = N'IX_Transcript_Tenant_State'
)
    CREATE INDEX IX_Transcript_Tenant_State
        ON dbo.Transcript(TenantId, State, MessageId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WebhookInboxEvent')
      AND name = N'IX_WebhookInbox_Tenant_State'
)
    CREATE INDEX IX_WebhookInbox_Tenant_State
        ON dbo.WebhookInboxEvent(TenantId, State, ReceivedAtUtc, WebhookInboxEventId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MessageOutbox')
      AND name = N'IX_MessageOutbox_Tenant_State'
)
    CREATE INDEX IX_MessageOutbox_Tenant_State
        ON dbo.MessageOutbox(TenantId, State, NextAttemptAtUtc, MessageId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ConversationAuditEvent')
      AND name = N'IX_ConversationAuditEvent_Tenant_Conversation'
)
    CREATE INDEX IX_ConversationAuditEvent_Tenant_Conversation
        ON dbo.ConversationAuditEvent(TenantId, ConversationId, OccurredAtUtc, ConversationAuditEventId);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ConversationAuditEvent')
      AND name = N'IX_ConversationAuditEvent_Tenant_Admin'
)
    CREATE INDEX IX_ConversationAuditEvent_Tenant_Admin
        ON dbo.ConversationAuditEvent(TenantId, ActorAdminId, OccurredAtUtc, ConversationAuditEventId);
GO
