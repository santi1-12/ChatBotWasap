:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_BotConfiguration_Upsert
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @IsBotEnabled bit,
    @ReplyMode nvarchar(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    SET XACT_ABORT ON;

    IF @ReplyMode NOT IN (N'FaqOnly', N'FaqThenAi', N'AiOnly')
        THROW 51011, 'The selected reply mode is not supported.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration
        WHERE IntegrationId = @IntegrationId
          AND TenantId = @TenantId
    )
        THROW 51012, 'The integration is not assigned to the active Tenant.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration WITH (UPDLOCK, HOLDLOCK)
        WHERE IntegrationId = @IntegrationId
          AND TenantId = @TenantId
          AND TransitionState NOT IN (N'None', N'Completed')
    )
    BEGIN
        THROW 51013, 'Bot configuration cannot change during a WhatsApp migration.', 1;
    END;

    UPDATE dbo.BotConfiguration
    SET TenantId = @TenantId,
        IsBotEnabled = @IsBotEnabled,
        ReplyMode = @ReplyMode,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT dbo.BotConfiguration (IntegrationId, TenantId, IsBotEnabled, ReplyMode)
        VALUES (@IntegrationId, @TenantId, @IsBotEnabled, @ReplyMode);
    END;

    COMMIT TRANSACTION;

    SELECT TenantId, IntegrationId, IsBotEnabled, ReplyMode, CreatedAtUtc, UpdatedAtUtc
    FROM dbo.BotConfiguration
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId;
END;
GO
