SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_HistoryImport_GetLatest
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        HistoryImportKey,
        State,
        TotalCount,
        ProcessedCount,
        ImportedCount,
        DuplicateCount,
        SkippedCount,
        FailedCount,
        StartedAtUtc,
        CompletedAtUtc,
        ErrorCode
    FROM dbo.HistoryImportStatus
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
    ORDER BY CreatedAtUtc DESC, HistoryImportStatusId DESC;
END;
GO
