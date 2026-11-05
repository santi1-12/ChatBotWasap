SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MediaAttachment_SetFailed
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @MessageId bigint,
    @StorageReference nvarchar(512) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE media
    SET AvailabilityState = N'Failed',
        StorageReference = @StorageReference,
        SizeBytes = NULL
    FROM dbo.MediaAttachment AS media
    INNER JOIN dbo.Message AS message
        ON message.MessageId = media.MessageId
       AND message.TenantId = media.TenantId
    WHERE media.TenantId = @TenantId
      AND media.MessageId = @MessageId
      AND message.TenantId = @TenantId
      AND message.ConversationId = @ConversationId;

    IF @@ROWCOUNT = 0
        THROW 51042, 'The media attachment was not found for the requested Tenant and Conversation.', 1;
END;
GO
