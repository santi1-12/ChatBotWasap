SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_ReconcileSignup
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @WabaId nvarchar(64),
    @PhoneNumberId nvarchar(64),
    @BusinessPhoneNumber nvarchar(32),
    @DisplayName nvarchar(256) = NULL,
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

    DECLARE @MigrationId uniqueidentifier;
    DECLARE @ExternalMutationOccurred bit;
    DECLARE @CompletionState nvarchar(16);
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();

    SELECT TOP (1)
        @MigrationId = MigrationId,
        @ExternalMutationOccurred = ExternalMutationOccurred,
        @CompletionState = CompletionState
    FROM dbo.WhatsAppIntegrationMigration WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId
    ORDER BY CreatedAtUtc DESC, UpdatedAtUtc DESC, MigrationId DESC;

    IF @MigrationId IS NULL
        THROW 51116, 'The WhatsApp migration journal was not found.', 1;
    IF @ExternalMutationOccurred = 0 OR @CompletionState IN (N'Succeeded', N'Canceled')
        THROW 51117, 'The Coexistence result cannot be reconciled in the current state.', 1;
    IF NULLIF(@WabaId, N'') IS NULL OR NULLIF(@PhoneNumberId, N'') IS NULL OR
       NULLIF(@AccessTokenSecretRef, N'') IS NULL OR NULLIF(@EffectiveEmbeddedSignupConfigId, N'') IS NULL
        THROW 51118, 'The Coexistence result is incomplete.', 1;

    UPDATE dbo.WhatsAppIntegration
    SET WabaId = @WabaId,
        PhoneNumberId = @PhoneNumberId,
        BusinessPhoneNumber = @BusinessPhoneNumber,
        DisplayName = @DisplayName,
        OnboardingType = N'Coexistence',
        EffectiveEmbeddedSignupConfigId = @EffectiveEmbeddedSignupConfigId,
        HistoryImportRequested = @HistoryImportRequested,
        ConnectionState = N'Connected',
        ConnectedAtUtc = @Now,
        LastConnectionErrorCode = NULL,
        AccessTokenSecretRef = @AccessTokenSecretRef,
        MetaAppSecretRef = @MetaAppSecretRef,
        WebhookVerifyTokenRef = @WebhookVerifyTokenRef,
        IsActive = 1,
        TransitionState = N'Completed',
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;
    IF @@ROWCOUNT = 0
        THROW 51119, 'The WhatsApp integration slot was not found.', 1;

    UPDATE dbo.WhatsAppIntegrationMigration
    SET TransitionState = N'Completed',
        CompletionState = N'Succeeded',
        NewWabaId = @WabaId,
        NewPhoneNumberId = @PhoneNumberId,
        NewAccessTokenSecretRef = @AccessTokenSecretRef,
        ReconcilingAtUtc = COALESCE(ReconcilingAtUtc, @Now),
        CompletedAtUtc = @Now,
        ErrorCode = NULL,
        UpdatedAtUtc = @Now
    WHERE MigrationId = @MigrationId AND TenantId = @TenantId;

    COMMIT TRANSACTION;
END;
GO
