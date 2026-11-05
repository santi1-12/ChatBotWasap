SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WebhookInbox_SetState
    @TenantId uniqueidentifier,
    @EventKey nvarchar(256),
    @State nvarchar(16),
    @SafeFailureCode nvarchar(100) = NULL,
    @HistoryOutcome nvarchar(24) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @PreviousOutcome nvarchar(24);
    DECLARE @HistoryImportKey nvarchar(256);
    DECLARE @HistoryTotalCount int;
    DECLARE @IntegrationId uniqueidentifier;
    CREATE TABLE #HistoryStateUpdate
    (
        IntegrationId uniqueidentifier NULL,
        HistoryImportKey nvarchar(256) NULL,
        HistoryTotalCount int NULL,
        PreviousOutcome nvarchar(24) NULL
    );
    UPDATE dbo.WebhookInboxEvent
    SET State = @State,
        LastFailureCode = @SafeFailureCode,
        HistoryOutcome = COALESCE(@HistoryOutcome, HistoryOutcome),
        ProcessedAtUtc = CASE WHEN @State IN (N'Completed', N'Failed') THEN SYSUTCDATETIME() ELSE NULL END
    OUTPUT inserted.IntegrationId, inserted.HistoryImportKey, inserted.HistoryTotalCount,
           deleted.HistoryOutcome
    INTO #HistoryStateUpdate (IntegrationId, HistoryImportKey, HistoryTotalCount, PreviousOutcome)
    WHERE TenantId = @TenantId
      AND EventKey = @EventKey;

    SELECT TOP (1)
        @IntegrationId = IntegrationId,
        @HistoryImportKey = HistoryImportKey,
        @HistoryTotalCount = HistoryTotalCount,
        @PreviousOutcome = PreviousOutcome
    FROM #HistoryStateUpdate;

    IF @HistoryImportKey IS NOT NULL AND @HistoryOutcome IS NOT NULL AND
       (@PreviousOutcome IS NULL OR @PreviousOutcome <> @HistoryOutcome)
    BEGIN
        IF @PreviousOutcome = N'Imported'
            UPDATE dbo.HistoryImportStatus SET ImportedCount = CASE WHEN ImportedCount > 0 THEN ImportedCount - 1 ELSE 0 END WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND HistoryImportKey = @HistoryImportKey;
        IF @PreviousOutcome = N'Duplicate'
            UPDATE dbo.HistoryImportStatus SET DuplicateCount = CASE WHEN DuplicateCount > 0 THEN DuplicateCount - 1 ELSE 0 END WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND HistoryImportKey = @HistoryImportKey;
        IF @PreviousOutcome = N'Skipped'
            UPDATE dbo.HistoryImportStatus SET SkippedCount = CASE WHEN SkippedCount > 0 THEN SkippedCount - 1 ELSE 0 END WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND HistoryImportKey = @HistoryImportKey;
        IF @PreviousOutcome = N'Failed'
            UPDATE dbo.HistoryImportStatus SET FailedCount = CASE WHEN FailedCount > 0 THEN FailedCount - 1 ELSE 0 END WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND HistoryImportKey = @HistoryImportKey;

        UPDATE dbo.HistoryImportStatus
        SET ProcessedCount = CASE WHEN @PreviousOutcome IS NULL THEN ProcessedCount + 1 ELSE ProcessedCount END,
            ImportedCount = ImportedCount + CASE WHEN @HistoryOutcome = N'Imported' THEN 1 ELSE 0 END,
            DuplicateCount = DuplicateCount + CASE WHEN @HistoryOutcome = N'Duplicate' THEN 1 ELSE 0 END,
            SkippedCount = SkippedCount + CASE WHEN @HistoryOutcome = N'Skipped' THEN 1 ELSE 0 END,
            FailedCount = FailedCount + CASE WHEN @HistoryOutcome = N'Failed' THEN 1 ELSE 0 END,
            State = CASE
                WHEN @HistoryOutcome = N'Failed' AND ProcessedCount + CASE WHEN @PreviousOutcome IS NULL THEN 1 ELSE 0 END >= TotalCount AND TotalCount > 0 THEN N'Failed'
                WHEN ProcessedCount + CASE WHEN @PreviousOutcome IS NULL THEN 1 ELSE 0 END >= TotalCount AND TotalCount > 0 THEN N'Completed'
                ELSE N'InProgress'
            END,
            CompletedAtUtc = CASE
                WHEN ProcessedCount + CASE WHEN @PreviousOutcome IS NULL THEN 1 ELSE 0 END >= TotalCount AND TotalCount > 0
                    THEN SYSUTCDATETIME()
                ELSE NULL
            END,
            ErrorCode = CASE WHEN @HistoryOutcome = N'Failed' THEN @SafeFailureCode ELSE NULL END,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE TenantId = @TenantId
          AND IntegrationId = @IntegrationId
          AND HistoryImportKey = @HistoryImportKey;
    END;
    COMMIT TRANSACTION;
END;
GO
