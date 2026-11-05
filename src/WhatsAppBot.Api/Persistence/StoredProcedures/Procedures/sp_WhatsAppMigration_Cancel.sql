SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_Cancel
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @MigrationId uniqueidentifier;
    DECLARE @ExternalMutationOccurred bit;
    DECLARE @CompletionState nvarchar(16);
    DECLARE @TransitionState nvarchar(40);
    DECLARE @PreviousBotEnabled bit;
    DECLARE @PreviousReplyMode nvarchar(32);
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();

    SELECT TOP (1)
        @MigrationId = MigrationId,
        @ExternalMutationOccurred = ExternalMutationOccurred,
        @CompletionState = CompletionState,
        @TransitionState = TransitionState,
        @PreviousBotEnabled = PreviousBotEnabled,
        @PreviousReplyMode = PreviousReplyMode
    FROM dbo.WhatsAppIntegrationMigration WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId
    ORDER BY CreatedAtUtc DESC, UpdatedAtUtc DESC, MigrationId DESC;

    IF @MigrationId IS NULL
        THROW 51109, 'No WhatsApp migration journal exists.', 1;
    IF @ExternalMutationOccurred = 1 OR @CompletionState <> N'Pending'
        THROW 51110, 'The migration cannot be canceled after an external operation may have occurred.', 1;
    IF @TransitionState IN (N'None', N'Completed')
        THROW 51111, 'The WhatsApp migration is not active.', 1;

    UPDATE dbo.MessageOutbox
    SET State = N'Pending', UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND State = N'HeldForMigration';

    UPDATE dbo.BotConfiguration
    SET IsBotEnabled = @PreviousBotEnabled,
        ReplyMode = @PreviousReplyMode,
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    UPDATE dbo.WhatsAppIntegration
    SET TransitionState = N'None',
        TransitionStartedAtUtc = NULL,
        LastConnectionErrorCode = NULL,
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    UPDATE dbo.WhatsAppIntegrationMigration
    SET TransitionState = N'None',
        CompletionState = N'Canceled',
        CompletedAtUtc = @Now,
        UpdatedAtUtc = @Now
    WHERE MigrationId = @MigrationId AND TenantId = @TenantId;

    COMMIT TRANSACTION;
END;
GO
