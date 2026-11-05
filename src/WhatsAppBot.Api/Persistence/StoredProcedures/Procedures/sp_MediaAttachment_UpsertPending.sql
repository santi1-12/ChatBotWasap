SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MediaAttachment_UpsertPending
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @MessageId bigint,
    @MetaMediaId nvarchar(128),
    @MediaKind nvarchar(16),
    @MimeType nvarchar(128) = NULL,
    @ExpiresAtUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Message AS message
        WHERE message.MessageId = @MessageId
          AND message.TenantId = @TenantId
          AND message.ConversationId = @ConversationId
          AND message.MessageType = @MediaKind
    )
        THROW 51040, 'The media message does not belong to the requested Tenant and Conversation.', 1;

    DECLARE @MediaAttachmentId bigint;
    SELECT @MediaAttachmentId = MediaAttachmentId
    FROM dbo.MediaAttachment WITH (UPDLOCK, SERIALIZABLE)
    WHERE MessageId = @MessageId
      AND TenantId = @TenantId;

    IF @MediaAttachmentId IS NULL
    BEGIN
        INSERT dbo.MediaAttachment
            (MessageId, TenantId, MetaMediaId, MediaKind, MimeType, AvailabilityState, ExpiresAtUtc)
        VALUES
            (@MessageId, @TenantId, @MetaMediaId, @MediaKind, @MimeType, N'Pending', @ExpiresAtUtc);
        SET @MediaAttachmentId = CONVERT(bigint, SCOPE_IDENTITY());
    END
    ELSE IF EXISTS
    (
        SELECT 1 FROM dbo.MediaAttachment
        WHERE MediaAttachmentId = @MediaAttachmentId
          AND AvailabilityState <> N'Available'
    )
    BEGIN
        UPDATE dbo.MediaAttachment
        SET TenantId = @TenantId,
            MetaMediaId = @MetaMediaId,
            MediaKind = @MediaKind,
            MimeType = @MimeType,
            SizeBytes = NULL,
            StorageReference = NULL,
            AvailabilityState = N'Pending',
            ExpiresAtUtc = @ExpiresAtUtc
        WHERE MediaAttachmentId = @MediaAttachmentId
          AND TenantId = @TenantId;
    END;

    COMMIT TRANSACTION;

    SELECT
        media.MediaAttachmentId,
        media.TenantId,
        message.ConversationId,
        media.MessageId,
        media.MetaMediaId,
        media.MediaKind,
        media.MimeType,
        media.SizeBytes,
        media.StorageReference,
        media.AvailabilityState,
        media.CreatedAtUtc,
        media.ExpiresAtUtc
    FROM dbo.MediaAttachment AS media
    INNER JOIN dbo.Message AS message
        ON message.MessageId = media.MessageId
       AND message.TenantId = media.TenantId
    WHERE media.TenantId = @TenantId
      AND media.MediaAttachmentId = @MediaAttachmentId;
END;
GO
