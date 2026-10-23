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
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'WhatsAppBot.TenantMigration',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 10000;
IF @LockResult < 0
    THROW 51010, N'Could not acquire the Tenant migration lock.', 1;

DECLARE @TenantId uniqueidentifier;
SELECT TOP (1) @TenantId = TenantId
FROM dbo.Tenant WITH (UPDLOCK, HOLDLOCK)
WHERE IsActive = 1
ORDER BY CreatedAtUtc, TenantId;

IF @TenantId IS NULL
BEGIN
    SELECT TOP (1) @TenantId = TenantId
    FROM dbo.Tenant WITH (UPDLOCK, HOLDLOCK)
    ORDER BY CreatedAtUtc, TenantId;
END;

IF @TenantId IS NULL
BEGIN
    SET @TenantId = NEWID();
    INSERT dbo.Tenant (TenantId, LegalName, IsActive)
    VALUES (@TenantId, N'Initial Tenant', 1);
END
ELSE
BEGIN
    UPDATE dbo.Tenant
    SET IsActive = 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND IsActive = 0;
END;

UPDATE membership
SET IsActive = 1,
    UpdatedAtUtc = SYSUTCDATETIME()
FROM dbo.TenantMembership AS membership
WHERE membership.TenantId = @TenantId
  AND membership.IsActive = 0;

INSERT dbo.TenantMembership (TenantId, AdminId, IsActive)
SELECT @TenantId, administrator.AdminId, 1
FROM dbo.Administrator AS administrator
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.TenantMembership AS membership
    INNER JOIN dbo.Tenant AS tenant ON tenant.TenantId = membership.TenantId
    WHERE membership.AdminId = administrator.AdminId
      AND membership.IsActive = 1
      AND tenant.IsActive = 1
);

-- Preserve the legacy lifecycle and message provenance before assigning ownership.
UPDATE conversation
SET LifecycleStatus = CASE
        WHEN conversation.BasicStatus = N'Closed' THEN N'Cerrada'
        ELSE N'Abierta'
    END
FROM dbo.Conversation AS conversation
WHERE (conversation.BasicStatus = N'Closed' AND conversation.LifecycleStatus <> N'Cerrada')
   OR (conversation.BasicStatus <> N'Closed' AND conversation.LifecycleStatus <> N'Abierta');

UPDATE message
SET ActorType = CASE
        WHEN message.Direction = N'Inbound' THEN N'Customer'
        WHEN message.ReplySource = N'Admin' THEN N'Admin'
        ELSE N'Bot'
    END,
    Origin = CASE
        WHEN message.Direction = N'Inbound' THEN N'MetaCloudApi'
        WHEN message.ReplySource = N'Admin' THEN N'AdminPanel'
        ELSE N'MetaCloudApi'
    END
FROM dbo.Message AS message
WHERE message.TenantId IS NULL;

UPDATE integration
SET TenantId = @TenantId
FROM dbo.WhatsAppIntegration AS integration
WHERE integration.TenantId IS NULL;

UPDATE configuration
SET TenantId = COALESCE(integration.TenantId, @TenantId)
FROM dbo.BotConfiguration AS configuration
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = configuration.IntegrationId
WHERE configuration.TenantId IS NULL;

UPDATE response
SET TenantId = COALESCE(integration.TenantId, @TenantId)
FROM dbo.FrequentResponse AS response
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = response.IntegrationId
WHERE response.TenantId IS NULL;

UPDATE contact
SET TenantId = COALESCE(integration.TenantId, @TenantId)
FROM dbo.Contact AS contact
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = contact.IntegrationId
WHERE contact.TenantId IS NULL;

UPDATE conversation
SET TenantId = COALESCE(integration.TenantId, contact.TenantId, @TenantId)
FROM dbo.Conversation AS conversation
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = conversation.IntegrationId
LEFT JOIN dbo.Contact AS contact
    ON contact.ContactId = conversation.ContactId
WHERE conversation.TenantId IS NULL;

UPDATE message
SET TenantId = COALESCE(conversation.TenantId, integration.TenantId, @TenantId)
FROM dbo.Message AS message
LEFT JOIN dbo.Conversation AS conversation
    ON conversation.ConversationId = message.ConversationId
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = message.IntegrationId
WHERE message.TenantId IS NULL;

UPDATE media
SET TenantId = COALESCE(message.TenantId, @TenantId)
FROM dbo.MediaAttachment AS media
LEFT JOIN dbo.Message AS message
    ON message.MessageId = media.MessageId
WHERE media.TenantId IS NULL;

UPDATE transcript
SET TenantId = COALESCE(message.TenantId, @TenantId)
FROM dbo.Transcript AS transcript
LEFT JOIN dbo.Message AS message
    ON message.MessageId = transcript.MessageId
WHERE transcript.TenantId IS NULL;

UPDATE inbox
SET TenantId = COALESCE(integration.TenantId, @TenantId)
FROM dbo.WebhookInboxEvent AS inbox
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = inbox.IntegrationId
WHERE inbox.TenantId IS NULL;

UPDATE outbox
SET TenantId = COALESCE(message.TenantId, integration.TenantId, @TenantId)
FROM dbo.MessageOutbox AS outbox
LEFT JOIN dbo.Message AS message
    ON message.MessageId = outbox.MessageId
LEFT JOIN dbo.WhatsAppIntegration AS integration
    ON integration.IntegrationId = outbox.IntegrationId
WHERE outbox.TenantId IS NULL;

IF EXISTS (SELECT 1 FROM dbo.Administrator AS administrator
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.TenantMembership AS membership
        INNER JOIN dbo.Tenant AS tenant ON tenant.TenantId = membership.TenantId
        WHERE membership.AdminId = administrator.AdminId
          AND membership.IsActive = 1
          AND tenant.IsActive = 1
    ))
    THROW 51011, N'Every administrator must have an active Tenant membership after migration.', 1;

IF EXISTS (SELECT 1 FROM dbo.WhatsAppIntegration WHERE TenantId IS NULL)
    THROW 51012, N'An integration was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.BotConfiguration WHERE TenantId IS NULL)
    THROW 51013, N'A bot configuration was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.FrequentResponse WHERE TenantId IS NULL)
    THROW 51014, N'A frequent response was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.Contact WHERE TenantId IS NULL)
    THROW 51015, N'A contact was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.Conversation WHERE TenantId IS NULL)
    THROW 51016, N'A conversation was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.Message WHERE TenantId IS NULL)
    THROW 51017, N'A message was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.MediaAttachment WHERE TenantId IS NULL)
    THROW 51018, N'A media attachment was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.Transcript WHERE TenantId IS NULL)
    THROW 51019, N'A transcript was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.WebhookInboxEvent WHERE TenantId IS NULL)
    THROW 51020, N'A webhook inbox event was left without a Tenant during migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.MessageOutbox WHERE TenantId IS NULL)
    THROW 51021, N'An outbox item was left without a Tenant during migration.', 1;

COMMIT TRANSACTION;
GO
