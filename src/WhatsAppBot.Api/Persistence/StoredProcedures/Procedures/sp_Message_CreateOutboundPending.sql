SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_CreateOutboundPending
    @TenantId uniqueidentifier,
    @InboundMessageId bigint,
    @IntegrationId uniqueidentifier,
    @ConversationId bigint,
    @ContentText nvarchar(max),
    @ReplySource nvarchar(32),
    @FrequentResponseId bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @ExistingOutboundMessageId bigint;
    SELECT @ExistingOutboundMessageId = MessageId
    FROM dbo.MessageOutbox WITH (UPDLOCK, SERIALIZABLE)
    WHERE TenantId = @TenantId
      AND SourceInboundMessageId = @InboundMessageId;
    IF @ExistingOutboundMessageId IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted, @ExistingOutboundMessageId AS MessageId,
               CONVERT(nvarchar(100), NULL) AS OutcomeCode;
        RETURN;
    END;

    DECLARE @LastCustomerMessageAtUtc datetime2(3);
    DECLARE @ConversationVersion binary(8);
    DECLARE @AttentionMode nvarchar(16);
    DECLARE @LifecycleStatus nvarchar(16);
    DECLARE @OutcomeCode nvarchar(100) = NULL;
    DECLARE @TransitionState nvarchar(40);
    SELECT @TransitionState = TransitionState
    FROM dbo.WhatsAppIntegration WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;
    SELECT
        @ConversationVersion = conversation.Version,
        @AttentionMode = conversation.AttentionMode,
        @LifecycleStatus = conversation.LifecycleStatus
    FROM dbo.Conversation AS conversation WITH (UPDLOCK, HOLDLOCK)
    WHERE conversation.TenantId = @TenantId
      AND conversation.IntegrationId = @IntegrationId
      AND conversation.ConversationId = @ConversationId;

    SELECT @LastCustomerMessageAtUtc = MAX(ProviderTimestampUtc)
    FROM dbo.Message WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId
       AND IntegrationId = @IntegrationId
       AND ConversationId = @ConversationId
       AND Direction = N'Inbound'
       AND IsHistorical = 0;

    IF @TransitionState NOT IN (N'None', N'Completed')
    BEGIN
        SET @OutcomeCode = N'IntegrationMigrationInProgress';
    END
    ELSE IF NOT EXISTS
       (SELECT 1 FROM dbo.Message
         WHERE MessageId = @InboundMessageId
           AND TenantId = @TenantId
           AND IntegrationId = @IntegrationId
          AND ConversationId = @ConversationId
          AND Direction = N'Inbound')
           OR @ConversationVersion IS NULL
    BEGIN
        SET @OutcomeCode = N'ConversationVersionConflict';
    END
    ELSE IF @AttentionMode <> N'Bot'
    BEGIN
        SET @OutcomeCode = N'ConversationInHumanMode';
    END
    ELSE IF @LifecycleStatus <> N'Abierta'
    BEGIN
        SET @OutcomeCode = N'ConversationClosed';
    END
    ELSE IF @FrequentResponseId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.FrequentResponse
        WHERE FrequentResponseId = @FrequentResponseId
          AND TenantId = @TenantId
          AND IntegrationId = @IntegrationId
    )
    BEGIN
        SET @OutcomeCode = N'FrequentResponseNotFound';
    END
    ELSE IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WhatsAppIntegration
        WHERE IntegrationId = @IntegrationId AND TenantId = @TenantId
          AND IsActive = 1 AND ConnectionState = N'Connected'
    )
    BEGIN
        SET @OutcomeCode = N'IntegrationUnavailable';
    END
    ELSE IF @LastCustomerMessageAtUtc IS NULL
        OR DATEDIFF_BIG(second, @LastCustomerMessageAtUtc, SYSUTCDATETIME()) NOT BETWEEN 0 AND 86399
    BEGIN
        SET @OutcomeCode = N'MessagingWindowClosed';
    END;

    IF @OutcomeCode IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted, CONVERT(bigint, NULL) AS MessageId,
               @OutcomeCode AS OutcomeCode;
        RETURN;
    END;

    INSERT dbo.Message
        (IntegrationId, ConversationId, Direction, MessageType, ContentText, ReplySource,
         FrequentResponseId, ProcessingState, DeliveryState, ProviderTimestampUtc,
         TenantId, ActorType, Origin)
    VALUES
        (@IntegrationId, @ConversationId, N'Outbound', N'Text', @ContentText, @ReplySource,
         @FrequentResponseId, N'Processing', N'Pending', SYSUTCDATETIME(),
         @TenantId, N'Bot', N'MetaCloudApi');
    DECLARE @OutboundMessageId bigint = CONVERT(bigint, SCOPE_IDENTITY());
    INSERT dbo.MessageOutbox
        (MessageId, IntegrationId, SourceInboundMessageId, TenantId, ConversationVersion)
    VALUES
        (@OutboundMessageId, @IntegrationId, @InboundMessageId, @TenantId, @ConversationVersion);
    UPDATE dbo.Message
    SET ProcessingState = N'Completed', UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND MessageId = @InboundMessageId;

    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Inserted, @OutboundMessageId AS MessageId, CONVERT(nvarchar(100), NULL) AS OutcomeCode;
END;
GO
