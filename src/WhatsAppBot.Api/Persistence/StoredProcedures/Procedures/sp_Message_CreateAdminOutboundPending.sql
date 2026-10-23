:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_CreateAdminOutboundPending
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @AdminId bigint,
    @ContentText nvarchar(max),
    @ExpectedVersion binary(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Found bit = 0;
    DECLARE @Conflict bit = 0;
    DECLARE @OutcomeCode nvarchar(100) = NULL;
    DECLARE @IntegrationId uniqueidentifier;
    DECLARE @PreviousAttentionMode nvarchar(16);
    DECLARE @PreviousLifecycleStatus nvarchar(16);
    DECLARE @CurrentVersion binary(8);
    DECLARE @LastCustomerMessageAtUtc datetime2(3);

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.TenantMembership
        WHERE TenantId = @TenantId AND AdminId = @AdminId AND IsActive = 1
    )
        SET @OutcomeCode = N'ManualReplyNotAuthorized';

    SELECT
        @Found = 1,
        @IntegrationId = conversation.IntegrationId,
        @PreviousAttentionMode = conversation.AttentionMode,
        @PreviousLifecycleStatus = conversation.LifecycleStatus,
        @CurrentVersion = conversation.Version
    FROM dbo.Conversation AS conversation WITH (UPDLOCK, HOLDLOCK)
    WHERE conversation.TenantId = @TenantId
      AND conversation.ConversationId = @ConversationId;

    IF @Found = 0
    BEGIN
        SET @OutcomeCode = N'ConversationVersionConflict';
    END
    ELSE IF @CurrentVersion <> @ExpectedVersion
    BEGIN
        SET @Conflict = 1;
        SET @OutcomeCode = N'ConversationVersionConflict';
    END
    ELSE IF @PreviousAttentionMode <> N'Humano'
    BEGIN
        SET @Conflict = 1;
        SET @OutcomeCode = N'ManualReplyRequiresHuman';
    END
    ELSE IF @PreviousLifecycleStatus <> N'Abierta'
    BEGIN
        SET @Conflict = 1;
        SET @OutcomeCode = N'ConversationClosed';
    END
    ELSE IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration
        WHERE IntegrationId = @IntegrationId
          AND TenantId = @TenantId
          AND IsActive = 1
          AND ConnectionState = N'Connected'
    )
    BEGIN
        SET @OutcomeCode = N'IntegrationUnavailable';
    END
    ELSE
    BEGIN
        SELECT @LastCustomerMessageAtUtc = MAX(message.ProviderTimestampUtc)
        FROM dbo.Message AS message
        WHERE message.TenantId = @TenantId
          AND message.ConversationId = @ConversationId
          AND message.Direction = N'Inbound';

        IF @LastCustomerMessageAtUtc IS NULL OR
           DATEDIFF_BIG(second, @LastCustomerMessageAtUtc, SYSUTCDATETIME()) NOT BETWEEN 0 AND 86399
            SET @OutcomeCode = N'MessagingWindowClosed';
    END;

    IF @OutcomeCode IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted, @Found AS Found, @Conflict AS Conflict,
               CONVERT(bigint, NULL) AS MessageId, @OutcomeCode AS OutcomeCode,
               CONVERT(datetime2(3), NULL) AS CreatedAtUtc;
        RETURN;
    END;

    INSERT dbo.Message
        (TenantId, IntegrationId, ConversationId, Direction, MessageType, ContentText,
         ReplySource, ProcessingState, DeliveryState, ActorType, Origin, CreatedByAdminId)
    VALUES
        (@TenantId, @IntegrationId, @ConversationId, N'Outbound', N'Text', @ContentText,
         N'Admin', N'Processing', N'Pending', N'Admin', N'AdminPanel', @AdminId);
    DECLARE @MessageId bigint = CONVERT(bigint, SCOPE_IDENTITY());
    DECLARE @CreatedAtUtc datetime2(3);
    SELECT @CreatedAtUtc = CreatedAtUtc
    FROM dbo.Message
    WHERE MessageId = @MessageId;

    UPDATE dbo.Conversation
    SET LastMessageAtUtc = CASE
        WHEN LastMessageAtUtc < @CreatedAtUtc THEN @CreatedAtUtc ELSE LastMessageAtUtc END
    WHERE TenantId = @TenantId
      AND ConversationId = @ConversationId
      AND Version = @ExpectedVersion;

    DECLARE @ResultingVersion binary(8);
    SELECT @ResultingVersion = Version
    FROM dbo.Conversation
    WHERE TenantId = @TenantId AND ConversationId = @ConversationId;

    INSERT dbo.MessageOutbox
        (MessageId, IntegrationId, SourceInboundMessageId, TenantId, ConversationVersion)
    VALUES (@MessageId, @IntegrationId, NULL, @TenantId, @ResultingVersion);

    INSERT dbo.ConversationAuditEvent
        (TenantId, ConversationId, EventType, PreviousAttentionMode, NewAttentionMode,
         PreviousLifecycleStatus, NewLifecycleStatus, ActorType, Origin, ActorAdminId,
         ReasonCode, ExpectedVersion, ResultingVersion)
    VALUES
        (@TenantId, @ConversationId, N'ManualReply', @PreviousAttentionMode, @PreviousAttentionMode,
         @PreviousLifecycleStatus, @PreviousLifecycleStatus, N'Admin', N'AdminPanel', @AdminId,
         NULL, @ExpectedVersion, @ResultingVersion);

    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Inserted, CONVERT(bit, 1) AS Found,
           CONVERT(bit, 0) AS Conflict, @MessageId AS MessageId,
           CONVERT(nvarchar(100), NULL) AS OutcomeCode, @CreatedAtUtc AS CreatedAtUtc;
END;
GO
