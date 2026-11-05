SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MediaAttachment_GetForAccess
    @TenantId uniqueidentifier,
    @MessageId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        media.TenantId,
        message.ConversationId,
        media.MessageId,
        media.MediaKind,
        media.MimeType,
        media.SizeBytes,
        media.StorageReference,
        media.AvailabilityState,
        media.ExpiresAtUtc
    FROM dbo.MediaAttachment AS media
    INNER JOIN dbo.Message AS message ON message.MessageId = media.MessageId
    INNER JOIN dbo.Conversation AS conversation
        ON conversation.ConversationId = message.ConversationId
       AND conversation.TenantId = media.TenantId
    WHERE media.TenantId = @TenantId
      AND media.MessageId = @MessageId;
END;
GO
