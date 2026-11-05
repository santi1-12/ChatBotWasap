SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WebhookInbox_Insert
    @EventKey nvarchar(256),
    @EventType nvarchar(80),
    @PhoneNumberId nvarchar(64),
    @WabaId nvarchar(64) = NULL,
    @HistoryImportKey nvarchar(256) = NULL,
    @HistoryTotalCount int = 0,
    @NormalizedEventJson nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @ExistingId bigint;
    SELECT @ExistingId = WebhookInboxEventId
    FROM dbo.WebhookInboxEvent WITH (UPDLOCK, SERIALIZABLE)
    WHERE EventKey = @EventKey;
    IF @ExistingId IS NOT NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted;
        RETURN;
    END;

    -- The schema uniqueness rule is Tenant-scoped. This guard only prevents
    -- ambiguous tenant-less webhook routing for the same provider identity.
    IF (SELECT COUNT(*)
        FROM dbo.WhatsAppIntegration
        WHERE PhoneNumberId = @PhoneNumberId
          AND (@WabaId IS NULL OR WabaId = @WabaId)
          AND IsActive = 1 AND ConnectionState = N'Connected') > 1
        THROW 51003, 'The webhook phone number is connected to multiple active integrations.', 1;

    DECLARE @IntegrationId uniqueidentifier =
        (SELECT TOP (1) IntegrationId
         FROM dbo.WhatsAppIntegration
         WHERE PhoneNumberId = @PhoneNumberId
           AND (@WabaId IS NULL OR WabaId = @WabaId)
           AND IsActive = 1 AND ConnectionState = N'Connected');
    DECLARE @TenantId uniqueidentifier =
        (SELECT TenantId FROM dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId);
    IF @IntegrationId IS NULL OR @TenantId IS NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Inserted;
        RETURN;
    END;
    INSERT dbo.WebhookInboxEvent
        (IntegrationId, TenantId, EventKey, EventType, HistoryImportKey, HistoryTotalCount, NormalizedEventJson)
    VALUES
        (@IntegrationId, @TenantId, @EventKey, @EventType, @HistoryImportKey,
         CASE WHEN @HistoryTotalCount < 0 THEN 0 ELSE @HistoryTotalCount END, @NormalizedEventJson);
    IF @HistoryImportKey IS NOT NULL AND @EventType = N'HistoricalMessage'
    BEGIN
        IF NOT EXISTS
        (
            SELECT 1 FROM dbo.HistoryImportStatus WITH (UPDLOCK, SERIALIZABLE)
            WHERE TenantId = @TenantId
              AND IntegrationId = @IntegrationId
              AND HistoryImportKey = @HistoryImportKey
        )
        BEGIN
            INSERT dbo.HistoryImportStatus
                (TenantId, IntegrationId, HistoryImportKey, TotalCount, StartedAtUtc)
            VALUES
                (@TenantId, @IntegrationId, @HistoryImportKey,
                 CASE WHEN @HistoryTotalCount < 0 THEN 0 ELSE @HistoryTotalCount END,
                 SYSUTCDATETIME());
        END
        ELSE
        BEGIN
            UPDATE dbo.HistoryImportStatus
            SET TotalCount = CASE WHEN @HistoryTotalCount > TotalCount THEN @HistoryTotalCount ELSE TotalCount END,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId
              AND IntegrationId = @IntegrationId
              AND HistoryImportKey = @HistoryImportKey;
        END;
    END;
    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Inserted;
END;
GO
