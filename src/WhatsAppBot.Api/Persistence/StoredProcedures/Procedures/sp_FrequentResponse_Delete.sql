:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_FrequentResponse_Delete
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @FrequentResponseId bigint
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.FrequentResponse
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND FrequentResponseId = @FrequentResponseId;

    SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS bit);
END;
GO
