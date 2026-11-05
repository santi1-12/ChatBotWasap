SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MediaAttachment_ListExpired
    @NowUtc datetime2(3),
    @BatchSize int
AS
BEGIN
    SET NOCOUNT ON;
    IF @BatchSize < 1 OR @BatchSize > 500
        THROW 51043, 'Invalid media cleanup batch size.', 1;

    SELECT TOP (@BatchSize)
        media.MediaAttachmentId,
        media.TenantId,
        message.ConversationId,
        media.MessageId,
        MetaMediaId,
        MediaKind,
        MimeType,
        SizeBytes,
        StorageReference,
        AvailabilityState,
        media.CreatedAtUtc,
        ExpiresAtUtc
    FROM dbo.MediaAttachment AS media
    INNER JOIN dbo.Message AS message ON message.MessageId = media.MessageId
    WHERE media.AvailabilityState IN (N'Pending', N'Available', N'Failed')
      AND media.ExpiresAtUtc IS NOT NULL
      AND media.ExpiresAtUtc <= @NowUtc
      AND (media.StorageReference IS NOT NULL OR media.AvailabilityState = N'Pending')
    ORDER BY media.ExpiresAtUtc, media.MediaAttachmentId;
END;
GO
