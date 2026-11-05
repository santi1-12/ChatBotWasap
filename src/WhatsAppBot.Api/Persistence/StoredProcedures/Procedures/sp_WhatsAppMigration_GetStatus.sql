SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_GetStatus
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        migration.MigrationId,
        migration.TenantId,
        migration.IntegrationId,
        migration.TransitionState,
        migration.CompletionState,
        migration.ExternalMutationOccurred,
        migration.PreviousWabaId,
        migration.PreviousPhoneNumberId,
        migration.PreviousAccessTokenSecretRef,
        migration.NewWabaId,
        migration.NewPhoneNumberId,
        migration.NewAccessTokenSecretRef,
        migration.ExpectedExternalOperation,
        migration.ExternalOperationReferenceId,
        migration.StartedAtUtc,
        migration.UpdatedAtUtc,
        migration.PreparingAtUtc,
        migration.MigrationToCoexistenceAtUtc,
        migration.AwaitingBusinessAppAtUtc,
        migration.AwaitingCoexistenceSignupAtUtc,
        migration.ReconcilingAtUtc,
        migration.CompletedAtUtc,
        migration.FailedAtUtc,
        migration.UnknownAtUtc,
        migration.ErrorCode,
        migration.ApprovalReference,
        migration.OperationalNotes,
        migration.PreviousBotEnabled,
        migration.PreviousReplyMode,
        CONVERT(int, (
            SELECT COUNT(*)
            FROM dbo.MessageOutbox AS held
            WHERE held.TenantId = @TenantId
              AND held.IntegrationId = @IntegrationId
              AND held.State = N'HeldForMigration')) AS HeldOutboxCount,
        CONVERT(int, (
            SELECT COUNT(*)
            FROM dbo.MessageOutbox AS sending
            WHERE sending.TenantId = @TenantId
              AND sending.IntegrationId = @IntegrationId
              AND sending.State = N'Sending')) AS SendingOutboxCount
    FROM dbo.WhatsAppIntegrationMigration AS migration
    WHERE migration.TenantId = @TenantId
      AND migration.IntegrationId = @IntegrationId
    ORDER BY migration.CreatedAtUtc DESC, migration.UpdatedAtUtc DESC, migration.MigrationId DESC;
END;
GO
