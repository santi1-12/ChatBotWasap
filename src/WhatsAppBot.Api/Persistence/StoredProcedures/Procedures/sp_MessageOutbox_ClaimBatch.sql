SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MessageOutbox_ClaimBatch
    @BatchSize int
AS
BEGIN
    SET NOCOUNT ON;
    IF @BatchSize NOT BETWEEN 1 AND 100 THROW 51004, 'Invalid outbox batch size.', 1;
    DECLARE @Claimed TABLE (MessageId bigint PRIMARY KEY);
    ;WITH Claimable AS
    (
        SELECT TOP (@BatchSize) o.MessageId, o.IntegrationId
        FROM dbo.MessageOutbox o WITH (UPDLOCK, READPAST, ROWLOCK)
        INNER JOIN dbo.WhatsAppIntegration i ON i.IntegrationId = o.IntegrationId
        INNER JOIN dbo.Message m ON m.MessageId = o.MessageId
        INNER JOIN dbo.Conversation conv ON conv.ConversationId = m.ConversationId
        WHERE i.IsActive = 1 AND i.ConnectionState = N'Connected'
          AND o.TenantId = conv.TenantId
          AND o.ConversationVersion = conv.Version
          AND conv.AttentionMode = N'Bot'
          AND conv.LifecycleStatus = N'Abierta'
          AND (o.State = N'Pending' AND o.NextAttemptAtUtc <= SYSUTCDATETIME()
               OR o.State = N'Sending' AND o.UpdatedAtUtc < DATEADD(minute, -5, SYSUTCDATETIME()))
        ORDER BY o.NextAttemptAtUtc, o.MessageId
    )
    UPDATE o
    SET State = N'Sending',
        AttemptCount = AttemptCount + 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT inserted.MessageId INTO @Claimed
    FROM dbo.MessageOutbox o
    INNER JOIN Claimable claimed ON claimed.MessageId = o.MessageId
    SELECT o.MessageId, o.IntegrationId, o.TenantId, m.ConversationId,
           i.PhoneNumberId, c.WhatsAppUserId AS Recipient, m.ContentText,
           CONVERT(binary(8), o.ConversationVersion) AS ConversationVersion,
           latestCustomerMessage.LastCustomerMessageAtUtc, o.AttemptCount
    FROM @Claimed claimed
    INNER JOIN dbo.MessageOutbox o ON o.MessageId = claimed.MessageId
    INNER JOIN dbo.WhatsAppIntegration i ON i.IntegrationId = o.IntegrationId
    INNER JOIN dbo.Message m ON m.MessageId = o.MessageId
    CROSS APPLY
    (
        SELECT MAX(inbound.ProviderTimestampUtc) AS LastCustomerMessageAtUtc
        FROM dbo.Message inbound
        WHERE inbound.IntegrationId = o.IntegrationId
          AND inbound.ConversationId = m.ConversationId
          AND inbound.Direction = N'Inbound'
    ) latestCustomerMessage
    INNER JOIN dbo.Conversation conv ON conv.ConversationId = m.ConversationId
    INNER JOIN dbo.Contact c ON c.ContactId = conv.ContactId;
END;
GO
