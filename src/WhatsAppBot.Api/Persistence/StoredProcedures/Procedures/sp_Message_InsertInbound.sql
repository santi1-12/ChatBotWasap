SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_InsertInbound
    @IntegrationId uniqueidentifier,
    @ConversationId bigint,
    @ProviderMessageId nvarchar(256),
    @MessageType nvarchar(24),
    @ContentText nvarchar(max) = NULL,
    @ProviderTimestampUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @MessageId bigint;
    SELECT @MessageId = MessageId
    FROM dbo.Message WITH (UPDLOCK, SERIALIZABLE)
    WHERE IntegrationId = @IntegrationId AND ProviderMessageId = @ProviderMessageId;
    IF @MessageId IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted, @MessageId AS MessageId;
        RETURN;
    END;

    INSERT dbo.Message
        (IntegrationId, ConversationId, ProviderMessageId, Direction, MessageType, ContentText,
         ProcessingState, ProviderTimestampUtc, TenantId, ActorType, Origin)
    SELECT
        @IntegrationId, @ConversationId, @ProviderMessageId, N'Inbound', @MessageType, @ContentText,
        N'Received', @ProviderTimestampUtc, conversation.TenantId, N'Customer', N'MetaCloudApi'
    FROM dbo.Conversation AS conversation
    WHERE conversation.ConversationId = @ConversationId
      AND conversation.IntegrationId = @IntegrationId;
    SET @MessageId = CONVERT(bigint, SCOPE_IDENTITY());
    UPDATE dbo.Conversation
    SET LastMessageAtUtc = CASE WHEN LastMessageAtUtc < @ProviderTimestampUtc
                                THEN @ProviderTimestampUtc ELSE LastMessageAtUtc END
    WHERE ConversationId = @ConversationId;
    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Inserted, @MessageId AS MessageId;
END;
GO
