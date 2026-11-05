SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppIntegration_Disconnect
    @IntegrationId uniqueidentifier,
    @TenantId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration WITH (UPDLOCK, HOLDLOCK)
        WHERE IntegrationId = @IntegrationId
          AND TenantId = @TenantId
          AND TransitionState NOT IN (N'None', N'Completed')
    )
        THROW 51005, 'The WhatsApp integration cannot be disconnected during a migration.', 1;

    UPDATE dbo.WhatsAppIntegration
    SET WabaId = NULL,
        PhoneNumberId = NULL,
        BusinessPhoneNumber = NULL,
        DisplayName = NULL,
        OnboardingType = NULL,
        EffectiveEmbeddedSignupConfigId = NULL,
        ConnectionState = N'NotConnected',
        ConnectedAtUtc = NULL,
        LastConnectionErrorCode = NULL,
        AccessTokenSecretRef = NULL,
        MetaAppSecretRef = NULL,
        WebhookVerifyTokenRef = NULL,
        IsActive = 0,
        TransitionState = N'None',
        TransitionStartedAtUtc = NULL,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE IntegrationId = @IntegrationId AND TenantId = @TenantId;
    IF @@ROWCOUNT = 0 THROW 51004, 'WhatsApp integration slot was not found for the Tenant.', 1;
    UPDATE dbo.BotConfiguration
    SET IsBotEnabled = 0, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE IntegrationId = @IntegrationId
      AND TenantId = @TenantId;
    COMMIT TRANSACTION;
END;
GO
