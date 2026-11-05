SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_Begin
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @ApprovalReference nvarchar(256) = NULL,
    @OperationalNotes nvarchar(2048) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @ConnectionState nvarchar(32);
    DECLARE @OnboardingType nvarchar(16);
    DECLARE @TransitionState nvarchar(40);
    DECLARE @WabaId nvarchar(64);
    DECLARE @PhoneNumberId nvarchar(64);
    DECLARE @AccessTokenSecretRef nvarchar(512);
    DECLARE @EffectiveConfigId nvarchar(128);
    DECLARE @IsActive bit;
    DECLARE @PreviousBotEnabled bit;
    DECLARE @PreviousReplyMode nvarchar(32);
    DECLARE @MigrationId uniqueidentifier = NEWID();
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();

    SELECT
        @ConnectionState = ConnectionState,
        @OnboardingType = OnboardingType,
        @TransitionState = TransitionState,
        @WabaId = WabaId,
        @PhoneNumberId = PhoneNumberId,
        @AccessTokenSecretRef = AccessTokenSecretRef,
        @EffectiveConfigId = EffectiveEmbeddedSignupConfigId,
        @IsActive = IsActive
    FROM dbo.WhatsAppIntegration WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    IF @ConnectionState IS NULL
        THROW 51100, 'WhatsApp integration slot was not found for the Tenant.', 1;
    IF @ConnectionState <> N'Connected' OR @IsActive = 0
        THROW 51101, 'The WhatsApp integration is not connected.', 1;
    IF @OnboardingType <> N'Standard'
        THROW 51102, 'Only a Standard integration can begin this migration.', 1;
    IF @TransitionState <> N'None'
        THROW 51103, 'The WhatsApp integration is already in a transition.', 1;
    IF NULLIF(@WabaId, N'') IS NULL OR NULLIF(@PhoneNumberId, N'') IS NULL
        THROW 51104, 'The current Meta identity is incomplete.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.WhatsAppIntegrationMigration WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId
          AND CompletionState IN (N'Pending', N'Unknown')
    )
        THROW 51105, 'An active migration journal already exists.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.MessageOutbox WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND State = N'Sending'
    )
        THROW 51106, 'Outbound messages are still being sent.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.WebhookInboxEvent WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND State = N'Processing'
    )
        THROW 51107, 'Webhook events are still being processed.', 1;

    SELECT
        @PreviousBotEnabled = IsBotEnabled,
        @PreviousReplyMode = ReplyMode
    FROM dbo.BotConfiguration WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;
    IF @PreviousReplyMode IS NULL
        THROW 51108, 'The bot configuration is not provisioned for the integration.', 1;

    UPDATE dbo.MessageOutbox
    SET State = N'HeldForMigration', UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND State = N'Pending';

    INSERT dbo.WhatsAppIntegrationMigration
    (
        MigrationId, TenantId, IntegrationId, TransitionState, CompletionState,
        PreviousWabaId, PreviousPhoneNumberId, PreviousAccessTokenSecretRef,
        PreviousOnboardingType, PreviousEffectiveEmbeddedSignupConfigId,
        ExpectedExternalOperation, StartedAtUtc, MigrationToCoexistenceAtUtc,
        ApprovalReference, OperationalNotes, PreviousBotEnabled, PreviousReplyMode,
        CreatedAtUtc, UpdatedAtUtc
    )
    VALUES
    (
        @MigrationId, @TenantId, @IntegrationId, N'MigrationToCoexistence', N'Pending',
        @WabaId, @PhoneNumberId, @AccessTokenSecretRef,
        @OnboardingType, @EffectiveConfigId,
        N'Cloud API phone-number deregistration, Business App registration, and Coexistence onboarding',
        @Now, @Now, @ApprovalReference, @OperationalNotes,
        @PreviousBotEnabled, @PreviousReplyMode, @Now, @Now
    );

    UPDATE dbo.WhatsAppIntegration
    SET TransitionState = N'MigrationToCoexistence',
        TransitionStartedAtUtc = @Now,
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    UPDATE dbo.BotConfiguration
    SET IsBotEnabled = 0, UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    COMMIT TRANSACTION;
END;
GO
