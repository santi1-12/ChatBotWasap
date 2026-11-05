SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppMigration_MarkUnknown
    @TenantId uniqueidentifier,
    @MigrationId uniqueidentifier,
    @SafeErrorCode nvarchar(100),
    @ExternalOperationReferenceId nvarchar(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @IntegrationId uniqueidentifier;
    DECLARE @Now datetime2(3) = SYSUTCDATETIME();
    SELECT @IntegrationId = IntegrationId
    FROM dbo.WhatsAppIntegrationMigration WITH (UPDLOCK, HOLDLOCK)
    WHERE MigrationId = @MigrationId AND TenantId = @TenantId;
    IF @IntegrationId IS NULL
        THROW 51115, 'The WhatsApp migration journal was not found.', 1;

    UPDATE dbo.WhatsAppIntegrationMigration
    SET TransitionState = N'Unknown',
        CompletionState = N'Unknown',
        ExternalMutationOccurred = 1,
        ExternalOperationReferenceId = COALESCE(@ExternalOperationReferenceId, ExternalOperationReferenceId),
        ErrorCode = @SafeErrorCode,
        UnknownAtUtc = @Now,
        UpdatedAtUtc = @Now
    WHERE MigrationId = @MigrationId AND TenantId = @TenantId;

    UPDATE dbo.WhatsAppIntegration
    SET TransitionState = N'Unknown',
        ConnectionState = N'Unknown',
        IsActive = 0,
        LastConnectionErrorCode = N'MigrationStateUnknown',
        UpdatedAtUtc = @Now
    WHERE TenantId = @TenantId AND IntegrationId = @IntegrationId;

    COMMIT TRANSACTION;
END;
GO
