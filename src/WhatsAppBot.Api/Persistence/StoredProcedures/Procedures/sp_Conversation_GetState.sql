:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_GetState
    @TenantId uniqueidentifier,
    @ConversationId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        conversation.TenantId,
        conversation.ConversationId,
        conversation.AttentionMode,
        conversation.LifecycleStatus,
        conversation.AssignedAdminId,
        CONVERT(binary(8), conversation.Version) AS Version
    FROM dbo.Conversation AS conversation
    WHERE conversation.TenantId = @TenantId
      AND conversation.ConversationId = @ConversationId;
END;
GO
