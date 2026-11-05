SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_MarkExternalMutation
    @TenantId uniqueidentifier,
    @MigrationId uniqueidentifier,
    @NextState nvarchar(40),
    @ExternalOperationReferenceId nvarchar(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF @NextState NOT IN (N'AwaitingBusinessApp', N'AwaitingCoexistenceSignup', N'Reconciling')
        THROW 51112, 'The requested external migration phase is invalid.', 1;

    DECLARE @TenantIntegrationId uniqueidentifier;
    DECLARE @CompletionState nvarchar(16);
    DECLARE @CurrentState nvarchar(40);
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    SELECT
        @TenantIntegrationId = IntegrationId,
        @CompletionState = CompletionState,
        @CurrentState = TransitionState
    FROM dbo.WhatsAppIntegrationMigration WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = @MigrationId AND TenantId = @TenantId;

    IF @TenantIntegrationId IS NULL
        THROW 51113, 'The WhatsApp migration journal was not found.', 1;
    IF @CompletionState <> N'Pending' OR @CurrentState IN (N'None', N'Completed', N'Unknown')
        THROW 51114, 'The WhatsApp migration cannot advance from its current state.', 1;

    UPDATE dbo.WhatsAppIntegrationMigration
    SET TransitionState = @NextState,
        ExternalMutationOccurred = 1,
        ExternalOperationReferenceId = COALESCE(@ExternalOperationReferenceId, ExternalOperationReferenceId),
        AwaitingBusinessAppAtUtc = CASE WHEN @NextState = N'AwaitingBusinessApp' THEN @Now ELSE AwaitingBusinessAppAtUtc END,
        AwaitingCoexistenceSignupAtUtc = CASE WHEN @NextState = N'AwaitingCoexistenceSignup' THEN @Now ELSE AwaitingCoexistenceSignupAtUtc END,
        ReconcilingAtUtc = CASE WHEN @NextState = N'Reconciling' THEN @Now ELSE ReconcilingAtUtc END,
        UpdatedAtUtc = @Now
    WHERE MigrationId = @MigrationId AND TenantId = @TenantId;

    UPDATE dbo.WhatsAppIntegration
    SET TransitionState = @NextState,
        ConnectionState = N'Unknown',
        IsActive = 0,
        LastConnectionErrorCode = N'MigrationExternalStateUnknown',
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @TenantIntegrationId;

    COMMIT TRANSACTION;
END;
GO
