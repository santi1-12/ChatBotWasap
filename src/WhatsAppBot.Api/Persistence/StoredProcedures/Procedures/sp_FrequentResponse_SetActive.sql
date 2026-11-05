:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_FrequentResponse_SetActive
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @FrequentResponseId bigint,
    @IsActive bit
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.FrequentResponse
    SET IsActive = @IsActive,
        ModifiedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND FrequentResponseId = @FrequentResponseId;

    SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS bit);
END;
GO
