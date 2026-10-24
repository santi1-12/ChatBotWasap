:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_GetAudit
    @TenantId uniqueidentifier,
    @ConversationId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        audit.EventType,
        audit.ActorType,
        audit.Origin,
        audit.ActorAdminId,
        audit.ReasonCode,
        audit.OccurredAtUtc,
        CONVERT(binary(8), audit.ResultingVersion) AS ResultingVersion
    FROM dbo.ConversationAuditEvent AS audit
    WHERE audit.TenantId = @TenantId
      AND audit.ConversationId = @ConversationId
    ORDER BY audit.OccurredAtUtc, audit.ConversationAuditEventId;
END;
GO
