SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WebhookInbox_Insert
    @EventKey nvarchar(256),
    @EventType nvarchar(80),
    @PhoneNumberId nvarchar(64),
    @WabaId nvarchar(64) = NULL,
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
    INSERT dbo.WebhookInboxEvent (IntegrationId, TenantId, EventKey, EventType, NormalizedEventJson)
    VALUES (@IntegrationId, @TenantId, @EventKey, @EventType, @NormalizedEventJson);
    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Inserted;
END;
GO
