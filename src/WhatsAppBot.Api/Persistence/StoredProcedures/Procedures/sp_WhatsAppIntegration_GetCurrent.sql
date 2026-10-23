SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppIntegration_GetCurrent
    @TenantId uniqueidentifier = NULL,
    @PhoneNumberId nvarchar(64) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- A tenant-less lookup is used by webhook routing. Reject ambiguous provider
    -- identity without turning this routing guard into a global uniqueness rule.
    IF @PhoneNumberId IS NOT NULL AND
       (SELECT COUNT(*) FROM dbo.WhatsAppIntegration
        WHERE PhoneNumberId = @PhoneNumberId AND IsActive = 1 AND ConnectionState = N'Connected') > 1
        THROW 51003, 'The webhook phone number is connected to multiple active integrations.', 1;

    SELECT TOP (1)
        IntegrationId, TenantId, WabaId, PhoneNumberId, BusinessPhoneNumber, DisplayName,
        ConnectionState, ConnectedAtUtc, LastConnectionErrorCode, OnboardingType,
        EffectiveEmbeddedSignupConfigId,
        AccessTokenSecretRef, MetaAppSecretRef, WebhookVerifyTokenRef,
        CreatedAtUtc, UpdatedAtUtc, IsActive
    FROM dbo.WhatsAppIntegration
    WHERE (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@PhoneNumberId IS NULL OR PhoneNumberId = @PhoneNumberId)
    ORDER BY CASE WHEN IsActive = 1 THEN 0 ELSE 1 END, CreatedAtUtc, IntegrationId;
END;
GO
