SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Contact_UpsertWhatsApp
    @IntegrationId uniqueidentifier,
    @TenantId uniqueidentifier,
    @WhatsAppUserId nvarchar(64),
    @DisplayName nvarchar(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration
        WHERE IntegrationId = @IntegrationId AND TenantId = @TenantId
    )
        THROW 51031, 'The integration is not assigned to the Tenant.', 1;

    BEGIN TRANSACTION;
    DECLARE @ContactId bigint;
    SELECT @ContactId = ContactId
    FROM dbo.Contact WITH (UPDLOCK, SERIALIZABLE)
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND WhatsAppUserId = @WhatsAppUserId;
    IF @ContactId IS NULL
    BEGIN
        INSERT dbo.Contact (TenantId, IntegrationId, WhatsAppUserId, DisplayName)
        SELECT integration.TenantId, @IntegrationId, @WhatsAppUserId, @DisplayName
        FROM dbo.WhatsAppIntegration AS integration
        WHERE integration.IntegrationId = @IntegrationId
          AND integration.TenantId = @TenantId;
        SET @ContactId = CONVERT(bigint, SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.Contact
        SET DisplayName = COALESCE(@DisplayName, DisplayName),
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE ContactId = @ContactId;
    END;
    COMMIT TRANSACTION;
    SELECT @ContactId AS ContactId;
END;
GO
