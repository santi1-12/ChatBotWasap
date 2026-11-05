SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppIntegration_SaveSignupResult
    @IntegrationId uniqueidentifier,
    @TenantId uniqueidentifier,
    @WabaId nvarchar(64),
    @PhoneNumberId nvarchar(64),
    @BusinessPhoneNumber nvarchar(32),
    @DisplayName nvarchar(256) = NULL,
    @OnboardingType nvarchar(16),
    @EffectiveEmbeddedSignupConfigId nvarchar(128),
    @AccessTokenSecretRef nvarchar(512),
    @MetaAppSecretRef nvarchar(512),
    @WebhookVerifyTokenRef nvarchar(512),
    @HistoryImportRequested bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF @OnboardingType NOT IN (N'Standard', N'Coexistence')
        THROW 51005, 'The WhatsApp onboarding type is invalid.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration WITH (UPDLOCK, HOLDLOCK)
        WHERE IntegrationId = @IntegrationId
          AND TenantId = @TenantId
          AND (
              TransitionState NOT IN (N'None', N'Completed')
              OR (@OnboardingType = N'Standard' AND TransitionState = N'Completed')
          )
    )
        THROW 51006, 'The signup result must be reconciled through the active migration.', 1;

    UPDATE dbo.WhatsAppIntegration WITH (UPDLOCK, SERIALIZABLE)
    SET WabaId = @WabaId,
        PhoneNumberId = @PhoneNumberId,
        BusinessPhoneNumber = @BusinessPhoneNumber,
        DisplayName = @DisplayName,
         OnboardingType = @OnboardingType,
         EffectiveEmbeddedSignupConfigId = @EffectiveEmbeddedSignupConfigId,
         HistoryImportRequested = @HistoryImportRequested,
        ConnectionState = N'Connected',
        ConnectedAtUtc = SYSUTCDATETIME(),
        LastConnectionErrorCode = NULL,
        AccessTokenSecretRef = @AccessTokenSecretRef,
        MetaAppSecretRef = @MetaAppSecretRef,
        WebhookVerifyTokenRef = @WebhookVerifyTokenRef,
        IsActive = 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE IntegrationId = @IntegrationId AND TenantId = @TenantId;
    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51002, 'WhatsApp integration slot was not found.', 1;
    END;
    COMMIT TRANSACTION;
END;
GO
