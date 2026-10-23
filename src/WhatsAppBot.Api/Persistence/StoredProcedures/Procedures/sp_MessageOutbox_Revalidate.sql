:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_MessageOutbox_Revalidate
    @MessageId bigint,
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @ConversationId bigint,
    @ConversationVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @OutcomeCode nvarchar(100) = NULL;
    DECLARE @Allowed bit = 0;
    DECLARE @State nvarchar(16);
    DECLARE @CurrentVersion binary(8);
    DECLARE @AttentionMode nvarchar(16);
    DECLARE @LifecycleStatus nvarchar(16);
    DECLARE @ReplySource nvarchar(32);
    DECLARE @LastCustomerMessageAtUtc datetime2(3);

    SELECT
        @State = outbox.State,
        @CurrentVersion = conversation.Version,
        @AttentionMode = conversation.AttentionMode,
        @LifecycleStatus = conversation.LifecycleStatus,
        @ReplySource = message.ReplySource
    FROM dbo.MessageOutbox AS outbox WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.Message AS message ON message.MessageId = outbox.MessageId
    INNER JOIN dbo.Conversation AS conversation WITH (UPDLOCK, HOLDLOCK)
        ON conversation.ConversationId = message.ConversationId
    INNER JOIN dbo.WhatsAppIntegration AS integration
        ON integration.IntegrationId = outbox.IntegrationId
    WHERE outbox.MessageId = @MessageId
      AND outbox.TenantId = @TenantId
      AND outbox.IntegrationId = @IntegrationId
      AND message.ConversationId = @ConversationId;

    SELECT @LastCustomerMessageAtUtc = MAX(message.ProviderTimestampUtc)
    FROM dbo.Message AS message
    WHERE message.TenantId = @TenantId
      AND message.IntegrationId = @IntegrationId
      AND message.ConversationId = @ConversationId
      AND message.Direction = N'Inbound';

    IF @State IS NULL
        SET @OutcomeCode = N'ConversationVersionConflict';
    ELSE IF @State <> N'Sending'
        SET @OutcomeCode = N'ConversationVersionConflict';
    ELSE IF @CurrentVersion <> @ConversationVersion
        SET @OutcomeCode = N'ConversationVersionConflict';
    ELSE IF @ReplySource = N'Admin' AND @AttentionMode <> N'Humano'
        SET @OutcomeCode = N'ManualReplyRequiresHuman';
    ELSE IF @ReplySource <> N'Admin' AND @AttentionMode <> N'Bot'
        SET @OutcomeCode = N'ConversationInHumanMode';
    ELSE IF @LifecycleStatus <> N'Abierta'
        SET @OutcomeCode = N'ConversationClosed';
    ELSE IF @LastCustomerMessageAtUtc IS NULL OR
            DATEDIFF_BIG(second, @LastCustomerMessageAtUtc, SYSUTCDATETIME()) NOT BETWEEN 0 AND 86399
        SET @OutcomeCode = N'MessagingWindowClosed';
    ELSE IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration
        WHERE IntegrationId = @IntegrationId
          AND TenantId = @TenantId
          AND IsActive = 1
          AND ConnectionState = N'Connected'
    )
        SET @OutcomeCode = N'IntegrationUnavailable';
    ELSE
        SET @Allowed = 1;

    IF @Allowed = 0
    BEGIN
        UPDATE dbo.Message
        SET ProcessingState = N'NoReply',
            DeliveryState = NULL,
            FailureCode = @OutcomeCode,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId AND Direction = N'Outbound';

        UPDATE dbo.MessageOutbox
        SET State = N'Failed',
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId AND State = N'Sending';
    END;

    COMMIT TRANSACTION;
    SELECT @Allowed AS IsAllowed, @OutcomeCode AS OutcomeCode;
END;
GO
