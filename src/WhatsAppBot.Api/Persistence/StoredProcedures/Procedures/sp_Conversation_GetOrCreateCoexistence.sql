SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_GetOrCreateCoexistence
    @IntegrationId uniqueidentifier,
    @TenantId uniqueidentifier,
    @ContactId bigint,
    @LastMessageAtUtc datetime2(3),
    @IsBotEnabled bit,
    @HumanActivity bit
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WhatsAppIntegration
        WHERE IntegrationId = @IntegrationId AND TenantId = @TenantId
    )
        THROW 51031, 'The integration is not assigned to the Tenant.', 1;
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Contact
        WHERE ContactId = @ContactId
          AND IntegrationId = @IntegrationId
          AND TenantId = @TenantId
    )
        THROW 51032, 'The contact is not assigned to the Tenant integration.', 1;

    BEGIN TRANSACTION;
    DECLARE @ConversationId bigint;
    SELECT @ConversationId = ConversationId
    FROM dbo.Conversation WITH (UPDLOCK, SERIALIZABLE)
    WHERE TenantId = @TenantId
      AND IntegrationId = @IntegrationId
      AND ContactId = @ContactId;

    IF @ConversationId IS NULL
    BEGIN
        INSERT dbo.Conversation
            (TenantId, IntegrationId, ContactId, BasicStatus, LifecycleStatus, AttentionMode,
             LastMessageAtUtc, LastAttentionChangedAtUtc)
        VALUES
            (@TenantId, @IntegrationId, @ContactId, N'Open', N'Abierta',
             CASE WHEN @HumanActivity = 1 THEN N'Humano'
                  WHEN @IsBotEnabled = 1 THEN N'Bot'
                  ELSE N'Humano' END,
             @LastMessageAtUtc, CASE WHEN @HumanActivity = 1 THEN SYSUTCDATETIME() ELSE NULL END);
        SET @ConversationId = CONVERT(bigint, SCOPE_IDENTITY());
    END
    ELSE
    BEGIN
        UPDATE dbo.Conversation
        SET BasicStatus = CASE WHEN @HumanActivity = 1 THEN N'Open' ELSE BasicStatus END,
            LifecycleStatus = CASE WHEN @HumanActivity = 1 THEN N'Abierta' ELSE LifecycleStatus END,
            ClosedAtUtc = CASE WHEN @HumanActivity = 1 THEN NULL ELSE ClosedAtUtc END,
            AttentionMode = CASE WHEN @HumanActivity = 1 THEN N'Humano' ELSE AttentionMode END,
            LastAttentionChangedAtUtc = CASE
                WHEN @HumanActivity = 1 AND AttentionMode <> N'Humano' THEN SYSUTCDATETIME()
                ELSE LastAttentionChangedAtUtc
            END,
            LastMessageAtUtc = CASE WHEN LastMessageAtUtc < @LastMessageAtUtc
                                    THEN @LastMessageAtUtc ELSE LastMessageAtUtc END
        WHERE ConversationId = @ConversationId
          AND TenantId = @TenantId
          AND IntegrationId = @IntegrationId
          AND ContactId = @ContactId;
    END;
    COMMIT TRANSACTION;
    SELECT @ConversationId AS ConversationId;
END;
GO
