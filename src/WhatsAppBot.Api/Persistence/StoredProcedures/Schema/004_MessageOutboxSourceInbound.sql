SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [WhatsAppBot];
GO

IF COL_LENGTH(N'dbo.MessageOutbox', N'SourceInboundMessageId') IS NULL
    ALTER TABLE dbo.MessageOutbox ADD SourceInboundMessageId bigint NULL;
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.MessageOutbox')
      AND name = N'FK_MessageOutbox_SourceInbound'
)
    ALTER TABLE dbo.MessageOutbox
        ADD CONSTRAINT FK_MessageOutbox_SourceInbound
        FOREIGN KEY (SourceInboundMessageId) REFERENCES dbo.Message(MessageId);
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MessageOutbox')
      AND name = N'UX_MessageOutbox_SourceInbound'
)
    CREATE UNIQUE INDEX UX_MessageOutbox_SourceInbound
        ON dbo.MessageOutbox(SourceInboundMessageId)
        WHERE SourceInboundMessageId IS NOT NULL;
GO
