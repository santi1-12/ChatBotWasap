SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppIntegration_GetCurrent
    @TenantId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        IntegrationId, TenantId, WabaId, PhoneNumberId, BusinessPhoneNumber, DisplayName,
        ConnectionState, ConnectedAtUtc, LastConnectionErrorCode,
        AccessTokenSecretRef, MetaAppSecretRef, WebhookVerifyTokenRef,
        CreatedAtUtc, UpdatedAtUtc, IsActive
    FROM dbo.WhatsAppIntegration
    WHERE @TenantId IS NULL OR TenantId = @TenantId
    ORDER BY CASE WHEN IsActive = 1 THEN 0 ELSE 1 END, CreatedAtUtc, IntegrationId;
END;
GO
