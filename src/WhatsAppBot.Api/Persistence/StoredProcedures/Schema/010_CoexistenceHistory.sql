:ON ERROR EXIT
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

IF COL_LENGTH(N'dbo.WhatsAppIntegration', N'HistoryImportRequested') IS NULL
BEGIN
    ALTER TABLE dbo.WhatsAppIntegration ADD HistoryImportRequested bit NOT NULL
        CONSTRAINT DF_WhatsAppIntegration_HistoryImportRequested DEFAULT (0);
END;
GO

IF COL_LENGTH(N'dbo.Message', N'IsHistorical') IS NULL
BEGIN
    ALTER TABLE dbo.Message ADD IsHistorical bit NOT NULL
        CONSTRAINT DF_Message_IsHistorical DEFAULT (0);
END;
IF COL_LENGTH(N'dbo.Message', N'SourceEventKey') IS NULL
    ALTER TABLE dbo.Message ADD SourceEventKey nvarchar(256) NULL;
IF COL_LENGTH(N'dbo.Message', N'HistoryImportKey') IS NULL
    ALTER TABLE dbo.Message ADD HistoryImportKey nvarchar(256) NULL;
GO

IF COL_LENGTH(N'dbo.WebhookInboxEvent', N'HistoryImportKey') IS NULL
    ALTER TABLE dbo.WebhookInboxEvent ADD HistoryImportKey nvarchar(256) NULL;
IF COL_LENGTH(N'dbo.WebhookInboxEvent', N'HistoryTotalCount') IS NULL
    ALTER TABLE dbo.WebhookInboxEvent ADD HistoryTotalCount int NOT NULL
        CONSTRAINT DF_WebhookInbox_HistoryTotalCount DEFAULT (0);
IF COL_LENGTH(N'dbo.WebhookInboxEvent', N'HistoryOutcome') IS NULL
    ALTER TABLE dbo.WebhookInboxEvent ADD HistoryOutcome nvarchar(24) NULL;
GO

IF OBJECT_ID(N'dbo.HistoryImportStatus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HistoryImportStatus
    (
        HistoryImportStatusId bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HistoryImportStatus PRIMARY KEY,
        TenantId uniqueidentifier NOT NULL,
        IntegrationId uniqueidentifier NOT NULL,
        HistoryImportKey nvarchar(256) NOT NULL,
        State nvarchar(16) NOT NULL CONSTRAINT DF_HistoryImportStatus_State DEFAULT (N'Pending'),
        TotalCount int NOT NULL CONSTRAINT DF_HistoryImportStatus_TotalCount DEFAULT (0),
        ProcessedCount int NOT NULL CONSTRAINT DF_HistoryImportStatus_ProcessedCount DEFAULT (0),
        ImportedCount int NOT NULL CONSTRAINT DF_HistoryImportStatus_ImportedCount DEFAULT (0),
        DuplicateCount int NOT NULL CONSTRAINT DF_HistoryImportStatus_DuplicateCount DEFAULT (0),
        SkippedCount int NOT NULL CONSTRAINT DF_HistoryImportStatus_SkippedCount DEFAULT (0),
        FailedCount int NOT NULL CONSTRAINT DF_HistoryImportStatus_FailedCount DEFAULT (0),
        StartedAtUtc datetime2(3) NULL,
        CompletedAtUtc datetime2(3) NULL,
        ErrorCode nvarchar(100) NULL,
        CreatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_HistoryImportStatus_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_HistoryImportStatus_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_HistoryImportStatus_Tenant_Integration_Key
            UNIQUE (TenantId, IntegrationId, HistoryImportKey),
        CONSTRAINT FK_HistoryImportStatus_Tenant
            FOREIGN KEY (TenantId) REFERENCES dbo.Tenant(TenantId),
        CONSTRAINT FK_HistoryImportStatus_Integration
            FOREIGN KEY (IntegrationId) REFERENCES dbo.WhatsAppIntegration(IntegrationId),
        CONSTRAINT CK_HistoryImportStatus_State
            CHECK (State IN (N'NotRequested', N'Pending', N'InProgress', N'Completed', N'Failed', N'Unavailable')),
        CONSTRAINT CK_HistoryImportStatus_Counts
            CHECK (TotalCount >= 0 AND ProcessedCount >= 0 AND ImportedCount >= 0 AND
                   DuplicateCount >= 0 AND SkippedCount >= 0 AND FailedCount >= 0)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Message')
      AND name = N'UX_Message_Tenant_Integration_SourceEventKey'
)
    CREATE UNIQUE INDEX UX_Message_Tenant_Integration_SourceEventKey
        ON dbo.Message (TenantId, IntegrationId, SourceEventKey)
        WHERE SourceEventKey IS NOT NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WebhookInboxEvent')
      AND name = N'IX_WebhookInbox_HistoryImport'
)
    CREATE INDEX IX_WebhookInbox_HistoryImport
        ON dbo.WebhookInboxEvent (TenantId, IntegrationId, HistoryImportKey, State);
GO
