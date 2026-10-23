:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_RecordAuditEvent
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @EventType nvarchar(32),
    @PreviousAttentionMode nvarchar(16) = NULL,
    @NewAttentionMode nvarchar(16) = NULL,
    @PreviousLifecycleStatus nvarchar(16) = NULL,
    @NewLifecycleStatus nvarchar(16) = NULL,
    @ActorType nvarchar(16),
    @Origin nvarchar(32),
    @ActorAdminId bigint = NULL,
    @ReasonCode nvarchar(100) = NULL,
    @ExpectedVersion binary(8) = NULL,
    @ResultingVersion binary(8) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EventType NOT IN (N'Take', N'AutomaticHandoff', N'ReturnToBot', N'Assign', N'Close', N'Reopen', N'ManualReply')
        THROW 51037, 'The conversation audit event is not supported.', 1;
    IF @ActorType NOT IN (N'Admin', N'System')
        THROW 51038, 'The conversation audit actor is not supported.', 1;
    IF @Origin NOT IN (N'MetaCloudApi', N'AdminPanel', N'System', N'FutureWhatsAppBusinessApp')
        THROW 51039, 'The conversation audit origin is not supported.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Conversation AS conversation
        WHERE conversation.TenantId = @TenantId
          AND conversation.ConversationId = @ConversationId
    )
        THROW 51040, 'The conversation is not available in the Tenant.', 1;

    INSERT dbo.ConversationAuditEvent
        (TenantId, ConversationId, EventType, PreviousAttentionMode, NewAttentionMode,
         PreviousLifecycleStatus, NewLifecycleStatus, ActorType, Origin, ActorAdminId,
         ReasonCode, ExpectedVersion, ResultingVersion)
    VALUES
        (@TenantId, @ConversationId, @EventType, @PreviousAttentionMode, @NewAttentionMode,
         @PreviousLifecycleStatus, @NewLifecycleStatus, @ActorType, @Origin, @ActorAdminId,
         @ReasonCode, @ExpectedVersion, @ResultingVersion);
END;
GO
