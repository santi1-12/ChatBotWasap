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

IF OBJECT_ID(N'dbo.Tenant', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenant
    (
        TenantId uniqueidentifier NOT NULL CONSTRAINT PK_Tenant PRIMARY KEY,
        LegalName nvarchar(256) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Tenant_IsActive DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Tenant_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_Tenant_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );
END;
GO

IF OBJECT_ID(N'dbo.TenantMembership', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantMembership
    (
        TenantMembershipId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantMembership PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        AdminId bigint NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_TenantMembership_IsActive DEFAULT (1),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_TenantMembership_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_TenantMembership_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_TenantMembership_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId),
        CONSTRAINT FK_TenantMembership_Admin FOREIGN KEY (AdminId) REFERENCES dbo.Administrator(AdminId)
    );
END;
GO

IF OBJECT_ID(N'dbo.ConversationAuditEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConversationAuditEvent
    (
        ConversationAuditEventId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConversationAuditEvent PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        ConversationId bigint NOT NULL,
        EventType nvarchar(32) NOT NULL,
        PreviousAttentionMode nvarchar(16) NULL,
        NewAttentionMode nvarchar(16) NULL,
        PreviousLifecycleStatus nvarchar(16) NULL,
        NewLifecycleStatus nvarchar(16) NULL,
        ActorType nvarchar(16) NOT NULL,
        Origin nvarchar(32) NOT NULL,
        ActorAdminId bigint NULL,
        ReasonCode nvarchar(100) NULL,
        OccurredAtUtc datetime2(3) NOT NULL CONSTRAINT DF_ConversationAuditEvent_OccurredAt DEFAULT (SYSUTCDATETIME()),
        ExpectedVersion binary(8) NULL,
        ResultingVersion binary(8) NULL,
        CONSTRAINT FK_ConversationAuditEvent_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId),
        CONSTRAINT FK_ConversationAuditEvent_Conversation FOREIGN KEY (ConversationId)
            REFERENCES dbo.Conversation(ConversationId),
        CONSTRAINT FK_ConversationAuditEvent_Admin FOREIGN KEY (ActorAdminId)
            REFERENCES dbo.Administrator(AdminId),
        CONSTRAINT CK_ConversationAuditEvent_EventType CHECK
            (EventType IN (N'Take', N'AutomaticHandoff', N'ReturnToBot', N'Assign', N'Close', N'Reopen', N'ManualReply')),
        CONSTRAINT CK_ConversationAuditEvent_PreviousAttention CHECK
            (PreviousAttentionMode IS NULL OR PreviousAttentionMode IN (N'Bot', N'Humano')),
        CONSTRAINT CK_ConversationAuditEvent_NewAttention CHECK
            (NewAttentionMode IS NULL OR NewAttentionMode IN (N'Bot', N'Humano')),
        CONSTRAINT CK_ConversationAuditEvent_PreviousLifecycle CHECK
            (PreviousLifecycleStatus IS NULL OR PreviousLifecycleStatus IN (N'Abierta', N'Cerrada')),
        CONSTRAINT CK_ConversationAuditEvent_NewLifecycle CHECK
            (NewLifecycleStatus IS NULL OR NewLifecycleStatus IN (N'Abierta', N'Cerrada')),
        CONSTRAINT CK_ConversationAuditEvent_ActorType CHECK (ActorType IN (N'Admin', N'Bot', N'System'))
        ,CONSTRAINT CK_ConversationAuditEvent_Origin CHECK
            (Origin IN (N'MetaCloudApi', N'AdminPanel', N'System', N'FutureWhatsAppBusinessApp'))
    );
END;
GO

IF COL_LENGTH(N'dbo.ConversationAuditEvent', N'Origin') IS NULL
    ALTER TABLE dbo.ConversationAuditEvent ADD Origin nvarchar(32) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.ConversationAuditEvent')
      AND name = N'CK_ConversationAuditEvent_Origin'
)
    ALTER TABLE dbo.ConversationAuditEvent ADD CONSTRAINT CK_ConversationAuditEvent_Origin
        CHECK (Origin IS NULL OR Origin IN (N'MetaCloudApi', N'AdminPanel', N'System', N'FutureWhatsAppBusinessApp'));
GO

IF COL_LENGTH(N'dbo.WhatsAppIntegration', N'TenantId') IS NULL
    ALTER TABLE dbo.WhatsAppIntegration ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.WhatsAppIntegration', N'OnboardingType') IS NULL
    ALTER TABLE dbo.WhatsAppIntegration ADD OnboardingType nvarchar(16) NULL;
IF COL_LENGTH(N'dbo.BotConfiguration', N'TenantId') IS NULL
    ALTER TABLE dbo.BotConfiguration ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.FrequentResponse', N'TenantId') IS NULL
    ALTER TABLE dbo.FrequentResponse ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Contact', N'TenantId') IS NULL
    ALTER TABLE dbo.Contact ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Conversation', N'TenantId') IS NULL
    ALTER TABLE dbo.Conversation ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Conversation', N'AttentionMode') IS NULL
    ALTER TABLE dbo.Conversation ADD AttentionMode nvarchar(16) NOT NULL
        CONSTRAINT DF_Conversation_AttentionMode DEFAULT (N'Bot');
IF COL_LENGTH(N'dbo.Conversation', N'LifecycleStatus') IS NULL
    ALTER TABLE dbo.Conversation ADD LifecycleStatus nvarchar(16) NOT NULL
        CONSTRAINT DF_Conversation_LifecycleStatus DEFAULT (N'Abierta');
IF COL_LENGTH(N'dbo.Conversation', N'AssignedAdminId') IS NULL
    ALTER TABLE dbo.Conversation ADD AssignedAdminId bigint NULL;
IF COL_LENGTH(N'dbo.Conversation', N'Version') IS NULL
    ALTER TABLE dbo.Conversation ADD Version rowversion;
IF COL_LENGTH(N'dbo.Conversation', N'ClosedAtUtc') IS NULL
    ALTER TABLE dbo.Conversation ADD ClosedAtUtc datetime2(3) NULL;
IF COL_LENGTH(N'dbo.Conversation', N'LastAttentionChangedAtUtc') IS NULL
    ALTER TABLE dbo.Conversation ADD LastAttentionChangedAtUtc datetime2(3) NULL;
IF COL_LENGTH(N'dbo.Message', N'TenantId') IS NULL
    ALTER TABLE dbo.Message ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Message', N'ActorType') IS NULL
    ALTER TABLE dbo.Message ADD ActorType nvarchar(16) NULL;
IF COL_LENGTH(N'dbo.Message', N'Origin') IS NULL
    ALTER TABLE dbo.Message ADD Origin nvarchar(32) NULL;
IF COL_LENGTH(N'dbo.Message', N'CreatedByAdminId') IS NULL
    ALTER TABLE dbo.Message ADD CreatedByAdminId bigint NULL;
IF COL_LENGTH(N'dbo.MediaAttachment', N'TenantId') IS NULL
    ALTER TABLE dbo.MediaAttachment ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Transcript', N'TenantId') IS NULL
    ALTER TABLE dbo.Transcript ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.WebhookInboxEvent', N'TenantId') IS NULL
    ALTER TABLE dbo.WebhookInboxEvent ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.MessageOutbox', N'TenantId') IS NULL
    ALTER TABLE dbo.MessageOutbox ADD TenantId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.MessageOutbox', N'ConversationVersion') IS NULL
    ALTER TABLE dbo.MessageOutbox ADD ConversationVersion binary(8) NULL;
GO

UPDATE outbox
SET ConversationVersion = conversation.Version
FROM dbo.MessageOutbox AS outbox
INNER JOIN dbo.Message AS message ON message.MessageId = outbox.MessageId
INNER JOIN dbo.Conversation AS conversation ON conversation.ConversationId = message.ConversationId
WHERE outbox.ConversationVersion IS NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'CK_WhatsAppIntegration_OnboardingType'
)
    ALTER TABLE dbo.WhatsAppIntegration ADD CONSTRAINT CK_WhatsAppIntegration_OnboardingType
        CHECK (OnboardingType IS NULL OR OnboardingType IN (N'Standard', N'Coexistence'));
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'CK_WhatsAppIntegration_ConnectionState'
      AND definition NOT LIKE N'%Unknown%'
)
BEGIN
    ALTER TABLE dbo.WhatsAppIntegration DROP CONSTRAINT CK_WhatsAppIntegration_ConnectionState;
    ALTER TABLE dbo.WhatsAppIntegration ADD CONSTRAINT CK_WhatsAppIntegration_ConnectionState
        CHECK (ConnectionState IN (N'NotConnected', N'Configuring', N'Connected', N'Error', N'Unknown'));
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'CK_Conversation_AttentionMode'
)
    ALTER TABLE dbo.Conversation ADD CONSTRAINT CK_Conversation_AttentionMode
        CHECK (AttentionMode IN (N'Bot', N'Humano'));
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'CK_Conversation_LifecycleStatus'
)
    ALTER TABLE dbo.Conversation ADD CONSTRAINT CK_Conversation_LifecycleStatus
        CHECK (LifecycleStatus IN (N'Abierta', N'Cerrada'));
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'CK_Message_ActorType'
)
    ALTER TABLE dbo.Message ADD CONSTRAINT CK_Message_ActorType
        CHECK (ActorType IS NULL OR ActorType IN (N'Customer', N'Bot', N'Admin', N'System'));
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'CK_Message_Origin'
)
    ALTER TABLE dbo.Message ADD CONSTRAINT CK_Message_Origin
        CHECK (Origin IS NULL OR Origin IN (N'MetaCloudApi', N'AdminPanel', N'System', N'FutureWhatsAppBusinessApp'));
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.TenantMembership')
      AND name = N'FK_TenantMembership_Tenant'
)
BEGIN
    ALTER TABLE dbo.TenantMembership WITH CHECK ADD CONSTRAINT FK_TenantMembership_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.TenantMembership CHECK CONSTRAINT FK_TenantMembership_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.TenantMembership')
      AND name = N'FK_TenantMembership_Admin'
)
BEGIN
    ALTER TABLE dbo.TenantMembership WITH CHECK ADD CONSTRAINT FK_TenantMembership_Admin
        FOREIGN KEY (AdminId) REFERENCES dbo.Administrator(AdminId);
    ALTER TABLE dbo.TenantMembership CHECK CONSTRAINT FK_TenantMembership_Admin;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'FK_WhatsAppIntegration_Tenant'
)
BEGIN
    ALTER TABLE dbo.WhatsAppIntegration WITH CHECK ADD CONSTRAINT FK_WhatsAppIntegration_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.WhatsAppIntegration CHECK CONSTRAINT FK_WhatsAppIntegration_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.BotConfiguration')
      AND name = N'FK_BotConfiguration_Tenant'
)
BEGIN
    ALTER TABLE dbo.BotConfiguration WITH CHECK ADD CONSTRAINT FK_BotConfiguration_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.BotConfiguration CHECK CONSTRAINT FK_BotConfiguration_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.FrequentResponse')
      AND name = N'FK_FrequentResponse_Tenant'
)
BEGIN
    ALTER TABLE dbo.FrequentResponse WITH CHECK ADD CONSTRAINT FK_FrequentResponse_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.FrequentResponse CHECK CONSTRAINT FK_FrequentResponse_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Contact')
      AND name = N'FK_Contact_Tenant'
)
BEGIN
    ALTER TABLE dbo.Contact WITH CHECK ADD CONSTRAINT FK_Contact_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.Contact CHECK CONSTRAINT FK_Contact_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'FK_Conversation_Tenant'
)
BEGIN
    ALTER TABLE dbo.Conversation WITH CHECK ADD CONSTRAINT FK_Conversation_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.Conversation CHECK CONSTRAINT FK_Conversation_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Conversation')
      AND name = N'FK_Conversation_AssignedAdmin'
)
BEGIN
    ALTER TABLE dbo.Conversation WITH CHECK ADD CONSTRAINT FK_Conversation_AssignedAdmin
        FOREIGN KEY (AssignedAdminId) REFERENCES dbo.Administrator(AdminId);
    ALTER TABLE dbo.Conversation CHECK CONSTRAINT FK_Conversation_AssignedAdmin;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'FK_Message_Tenant'
)
BEGIN
    ALTER TABLE dbo.Message WITH CHECK ADD CONSTRAINT FK_Message_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.Message CHECK CONSTRAINT FK_Message_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'FK_Message_CreatedByAdmin'
)
BEGIN
    ALTER TABLE dbo.Message WITH CHECK ADD CONSTRAINT FK_Message_CreatedByAdmin
        FOREIGN KEY (CreatedByAdminId) REFERENCES dbo.Administrator(AdminId);
    ALTER TABLE dbo.Message CHECK CONSTRAINT FK_Message_CreatedByAdmin;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'FK_MediaAttachment_Tenant'
)
BEGIN
    ALTER TABLE dbo.MediaAttachment WITH CHECK ADD CONSTRAINT FK_MediaAttachment_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.MediaAttachment CHECK CONSTRAINT FK_MediaAttachment_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Transcript')
      AND name = N'FK_Transcript_Tenant'
)
BEGIN
    ALTER TABLE dbo.Transcript WITH CHECK ADD CONSTRAINT FK_Transcript_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.Transcript CHECK CONSTRAINT FK_Transcript_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.WebhookInboxEvent')
      AND name = N'FK_WebhookInbox_Tenant'
)
BEGIN
    ALTER TABLE dbo.WebhookInboxEvent WITH CHECK ADD CONSTRAINT FK_WebhookInbox_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.WebhookInboxEvent CHECK CONSTRAINT FK_WebhookInbox_Tenant;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.MessageOutbox')
      AND name = N'FK_MessageOutbox_Tenant'
)
BEGIN
    ALTER TABLE dbo.MessageOutbox WITH CHECK ADD CONSTRAINT FK_MessageOutbox_Tenant
        FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId);
    ALTER TABLE dbo.MessageOutbox CHECK CONSTRAINT FK_MessageOutbox_Tenant;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
      AND name = N'UQ_WhatsAppIntegration_Tenant_Integration'
)
    ALTER TABLE dbo.WhatsAppIntegration ADD CONSTRAINT UQ_WhatsAppIntegration_Tenant_Integration
        UNIQUE (TenantId, IntegrationId);
GO
