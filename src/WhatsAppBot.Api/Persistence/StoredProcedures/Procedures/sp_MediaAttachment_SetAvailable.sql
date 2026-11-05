SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MediaAttachment_SetAvailable
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @MessageId bigint,
    @StorageReference nvarchar(512),
    @MimeType nvarchar(128),
    @SizeBytes bigint,
    @ExpiresAtUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE media
    SET StorageReference = @StorageReference,
        MimeType = @MimeType,
        SizeBytes = @SizeBytes,
        AvailabilityState = N'Available',
        ExpiresAtUtc = @ExpiresAtUtc
    FROM dbo.MediaAttachment AS media
    INNER JOIN dbo.Message AS message ON message.MessageId = media.MessageId
    WHERE media.TenantId = @TenantId
      AND media.MessageId = @MessageId
      AND message.ConversationId = @ConversationId;

    IF @@ROWCOUNT = 0
        THROW 51041, 'The media attachment was not found for the requested Tenant and Conversation.', 1;
END;
GO
