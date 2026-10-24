:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_BotConfiguration_Upsert
    @IntegrationId uniqueidentifier,
    @IsBotEnabled bit,
    @ReplyMode nvarchar(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ReplyMode NOT IN (N'FaqOnly', N'FaqThenAi', N'AiOnly')
        THROW 51011, 'The selected reply mode is not supported.', 1;

    DECLARE @TenantId uniqueidentifier =
        (SELECT TenantId FROM dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId);
    IF @TenantId IS NULL
        THROW 51012, 'The integration is not assigned to a Tenant.', 1;

    UPDATE dbo.BotConfiguration
    SET TenantId = @TenantId,
        IsBotEnabled = @IsBotEnabled,
        ReplyMode = @ReplyMode,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE IntegrationId = @IntegrationId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT dbo.BotConfiguration (IntegrationId, TenantId, IsBotEnabled, ReplyMode)
        VALUES (@IntegrationId, @TenantId, @IsBotEnabled, @ReplyMode);
    END;

    SELECT IntegrationId, IsBotEnabled, ReplyMode, CreatedAtUtc, UpdatedAtUtc
    FROM dbo.BotConfiguration
    WHERE IntegrationId = @IntegrationId;
END;
GO
