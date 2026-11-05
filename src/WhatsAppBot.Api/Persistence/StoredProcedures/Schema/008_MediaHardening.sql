SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

UPDATE media
SET TenantId = message.TenantId
FROM dbo.MediaAttachment AS media
INNER JOIN dbo.Message AS message ON message.MessageId = media.MessageId
WHERE media.TenantId IS NULL
  AND message.TenantId IS NOT NULL;

UPDATE transcript
SET TenantId = message.TenantId
FROM dbo.Transcript AS transcript
INNER JOIN dbo.Message AS message ON message.MessageId = transcript.MessageId
WHERE transcript.TenantId IS NULL
  AND message.TenantId IS NOT NULL;

UPDATE media
SET ExpiresAtUtc = DATEADD(DAY, 30, media.CreatedAtUtc)
FROM dbo.MediaAttachment AS media
WHERE media.ExpiresAtUtc IS NULL;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'IX_MediaAttachment_Tenant_Availability'
)
    DROP INDEX IX_MediaAttachment_Tenant_Availability ON dbo.MediaAttachment;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'IX_MediaAttachment_Expired'
)
    DROP INDEX IX_MediaAttachment_Expired ON dbo.MediaAttachment;

ALTER TABLE dbo.MediaAttachment
    ALTER COLUMN ExpiresAtUtc datetime2(3) NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'IX_MediaAttachment_Tenant_Availability'
)
    CREATE INDEX IX_MediaAttachment_Tenant_Availability
        ON dbo.MediaAttachment(TenantId, AvailabilityState, ExpiresAtUtc, MessageId);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'UX_MediaAttachment_Message'
)
    CREATE UNIQUE INDEX UX_MediaAttachment_Message
        ON dbo.MediaAttachment(MessageId);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MediaAttachment')
      AND name = N'IX_MediaAttachment_Expired'
)
    CREATE INDEX IX_MediaAttachment_Expired
        ON dbo.MediaAttachment(AvailabilityState, ExpiresAtUtc, MediaAttachmentId)
        INCLUDE (TenantId, MessageId, StorageReference, MimeType, SizeBytes, MediaKind);
GO
