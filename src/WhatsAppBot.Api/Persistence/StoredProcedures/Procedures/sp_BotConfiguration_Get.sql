:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_BotConfiguration_Get
    @TenantId uniqueidentifier = NULL,
    @IntegrationId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        IntegrationId, IsBotEnabled, ReplyMode, CreatedAtUtc, UpdatedAtUtc
    FROM dbo.BotConfiguration
    WHERE (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@IntegrationId IS NULL OR IntegrationId = @IntegrationId)
    ORDER BY CreatedAtUtc, IntegrationId;

    IF @@ROWCOUNT = 0
        THROW 51010, 'The initial bot configuration has not been provisioned.', 1;
END;
GO
