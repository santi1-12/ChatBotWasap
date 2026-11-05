SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_Preflight
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ConnectionState nvarchar(32);
    DECLARE @OnboardingType nvarchar(16);
    DECLARE @TransitionState nvarchar(40);
    DECLARE @WabaId nvarchar(64);
    DECLARE @PhoneNumberId nvarchar(64);
    DECLARE @IsActive bit;
    DECLARE @SendingOutboxCount int;
    DECLARE @PendingOutboxCount int;
    DECLARE @HeldOutboxCount int;
    DECLARE @ProcessingWebhookCount int;
    DECLARE @BlockingCode nvarchar(100) = NULL;

    SELECT
        @ConnectionState = ConnectionState,
        @OnboardingType = OnboardingType,
        @TransitionState = TransitionState,
        @WabaId = WabaId,
        @PhoneNumberId = PhoneNumberId,
        @IsActive = IsActive
    FROM dbo.WhatsAppIntegration
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    IF @ConnectionState IS NULL
        THROW 51100, 'WhatsApp integration slot was not found for the Tenant.', 1;

    SELECT @SendingOutboxCount = COUNT(*)
    FROM dbo.MessageOutbox
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND State = N'Sending';
    SELECT @PendingOutboxCount = COUNT(*)
    FROM dbo.MessageOutbox
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND State = N'Pending';
    SELECT @HeldOutboxCount = COUNT(*)
    FROM dbo.MessageOutbox
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND State = N'HeldForMigration';
    SELECT @ProcessingWebhookCount = COUNT(*)
    FROM dbo.WebhookInboxEvent
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId AND State = N'Processing';

    IF @ConnectionState <> N'Connected' OR @IsActive = 0
        SET @BlockingCode = N'IntegrationNotConnected';
    ELSE IF @OnboardingType <> N'Standard'
        SET @BlockingCode = N'IntegrationNotStandard';
    ELSE IF @TransitionState <> N'None'
        SET @BlockingCode = N'MigrationAlreadyStarted';
    ELSE IF EXISTS
    (
        SELECT 1 FROM dbo.WhatsAppIntegrationMigration
        WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId
          AND CompletionState IN (N'Pending', N'Unknown')
    )
        SET @BlockingCode = N'MigrationJournalActive';
    ELSE IF @SendingOutboxCount > 0
        SET @BlockingCode = N'OutboxSending';
    ELSE IF @ProcessingWebhookCount > 0
        SET @BlockingCode = N'WebhookProcessing';
    ELSE IF NULLIF(@WabaId, N'') IS NULL OR NULLIF(@PhoneNumberId, N'') IS NULL
        SET @BlockingCode = N'MetaIdentityMissing';

    SELECT
        @TenantId AS TenantId,
        @IntegrationId AS IntegrationId,
        CONVERT(bit, CASE WHEN @BlockingCode IS NULL THEN 1 ELSE 0 END) AS CanBegin,
        @BlockingCode AS BlockingCode,
        @ConnectionState AS ConnectionState,
        @OnboardingType AS OnboardingType,
        @TransitionState AS TransitionState,
        CONVERT(bit, CASE WHEN NULLIF(@WabaId, N'') IS NOT NULL
                            AND NULLIF(@PhoneNumberId, N'') IS NOT NULL THEN 1 ELSE 0 END) AS HasMetaIdentity,
        @SendingOutboxCount AS SendingOutboxCount,
        @PendingOutboxCount AS PendingOutboxCount,
        @HeldOutboxCount AS HeldOutboxCount,
        @ProcessingWebhookCount AS ProcessingWebhookCount;
END;
GO
