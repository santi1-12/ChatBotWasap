SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WhatsAppIntegration_SetConnectionState
    @IntegrationId uniqueidentifier,
    @TenantId uniqueidentifier,
    @ConnectionState nvarchar(32),
    @SafeErrorCode nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.WhatsAppIntegration
    SET ConnectionState = @ConnectionState,
        LastConnectionErrorCode = @SafeErrorCode,
        IsActive = CASE WHEN @ConnectionState = N'Connected' THEN IsActive ELSE 0 END,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE IntegrationId = @IntegrationId AND TenantId = @TenantId;
    IF @@ROWCOUNT = 0 THROW 51001, 'WhatsApp integration slot was not found.', 1;
END;
GO
