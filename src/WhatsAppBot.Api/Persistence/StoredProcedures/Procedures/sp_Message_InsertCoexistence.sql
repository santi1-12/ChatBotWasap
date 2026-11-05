SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_InsertCoexistence
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @ConversationId bigint,
    @ProviderMessageId nvarchar(256) = NULL,
    @SourceEventKey nvarchar(256),
    @MessageType nvarchar(24),
    @ContentText nvarchar(max) = NULL,
    @ProviderTimestampUtc datetime2(3),
    @Direction nvarchar(16),
    @ActorType nvarchar(16),
    @Origin nvarchar(32),
    @IsHistorical bit,
    @HistoryImportKey nvarchar(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @MessageId bigint;
    SELECT @MessageId = MessageId
    FROM dbo.Message WITH (UPDLOCK, SERIALIZABLE)
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND (SourceEventKey = @SourceEventKey OR
           (@ProviderMessageId IS NOT NULL AND ProviderMessageId = @ProviderMessageId));
    IF @MessageId IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted, @MessageId AS MessageId;
        RETURN;
    END;

    INSERT dbo.Message
        (IntegrationId, ConversationId, ProviderMessageId, Direction, MessageType, ContentText,
         ProcessingState, ProviderTimestampUtc, CreatedAtUtc, TenantId, ActorType, Origin,
         IsHistorical, SourceEventKey, HistoryImportKey, FailureCode)
    SELECT
        @IntegrationId, @ConversationId, @ProviderMessageId, @Direction, @MessageType, @ContentText,
        CASE WHEN @IsHistorical = 1 OR @Direction = N'Outbound' THEN N'NoReply' ELSE N'Received' END,
        @ProviderTimestampUtc, @ProviderTimestampUtc, conversation.TenantId, @ActorType, @Origin,
        @IsHistorical, @SourceEventKey, @HistoryImportKey,
        CASE WHEN @IsHistorical = 1 THEN N'HistoricalImport'
             WHEN @Direction = N'Outbound' THEN N'OutboundAppEcho'
             ELSE NULL END
    FROM dbo.Conversation AS conversation
    WHERE conversation.ConversationId = @ConversationId
      AND conversation.TenantId = @TenantId
      AND conversation.IntegrationId = @IntegrationId;
    SET @MessageId = CONVERT(bigint, SCOPE_IDENTITY());
    IF @MessageId IS NULL
        THROW 51031, 'The Coexistence message conversation does not belong to the requested Tenant.', 1;
    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Inserted, @MessageId AS MessageId;
END;
GO
