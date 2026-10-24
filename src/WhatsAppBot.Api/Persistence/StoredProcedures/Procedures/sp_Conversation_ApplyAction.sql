:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_ApplyAction
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @Action nvarchar(32),
    @ExpectedVersion binary(8),
    @ActorType nvarchar(16),
    @Origin nvarchar(32),
    @ActorAdminId bigint = NULL,
    @ReasonCode nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @TenantId IS NULL OR @ConversationId < 1 OR DATALENGTH(@ExpectedVersion) <> 8
        THROW 51030, 'A valid Tenant, conversation and expected version are required.', 1;
    IF @Action NOT IN (N'Take', N'Handoff', N'ReturnToBot', N'Close', N'Reopen')
        THROW 51031, 'The conversation action is not supported.', 1;
    IF @ActorType NOT IN (N'Admin', N'System')
        THROW 51032, 'The conversation actor is not supported.', 1;
    IF @Origin NOT IN (N'MetaCloudApi', N'AdminPanel', N'System', N'FutureWhatsAppBusinessApp')
        THROW 51033, 'The conversation origin is not supported.', 1;
    IF @ActorType = N'Admin' AND @ActorAdminId IS NULL
        THROW 51034, 'An administrator is required for an Admin conversation action.', 1;
    IF @ActorType = N'System' AND @ActorAdminId IS NOT NULL
        THROW 51035, 'A system conversation action cannot carry an administrator.', 1;
    IF @ActorAdminId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.TenantMembership AS membership
        WHERE membership.TenantId = @TenantId
          AND membership.AdminId = @ActorAdminId
          AND membership.IsActive = 1
    )
        THROW 51036, 'The administrator is not an active member of the Tenant.', 1;

    BEGIN TRANSACTION;

    DECLARE @PreviousAttentionMode nvarchar(16);
    DECLARE @PreviousLifecycleStatus nvarchar(16);
    DECLARE @PreviousBasicStatus nvarchar(16);
    DECLARE @PreviousAssignedAdminId bigint;
    DECLARE @CurrentVersion binary(8);

    SELECT
        @PreviousAttentionMode = conversation.AttentionMode,
        @PreviousLifecycleStatus = conversation.LifecycleStatus,
        @PreviousBasicStatus = conversation.BasicStatus,
        @PreviousAssignedAdminId = conversation.AssignedAdminId,
        @CurrentVersion = conversation.Version
    FROM dbo.Conversation AS conversation WITH (UPDLOCK, HOLDLOCK)
    WHERE conversation.TenantId = @TenantId
      AND conversation.ConversationId = @ConversationId;

    IF @CurrentVersion IS NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Applied, CONVERT(bit, 0) AS Found,
               CONVERT(bit, 0) AS Conflict, CONVERT(binary(8), NULL) AS ResultingVersion;
        RETURN;
    END;

    IF @CurrentVersion <> @ExpectedVersion
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Applied, CONVERT(bit, 1) AS Found,
               CONVERT(bit, 1) AS Conflict, @CurrentVersion AS ResultingVersion;
        RETURN;
    END;

    DECLARE @NewAttentionMode nvarchar(16) = @PreviousAttentionMode;
    DECLARE @NewLifecycleStatus nvarchar(16) = @PreviousLifecycleStatus;
    DECLARE @NewBasicStatus nvarchar(16) = @PreviousBasicStatus;
    DECLARE @NewAssignedAdminId bigint = @PreviousAssignedAdminId;
    DECLARE @ClosedAtUtc datetime2(3) = NULL;
    DECLARE @EventType nvarchar(32);

    IF @Action IN (N'Take', N'Handoff')
    BEGIN
        SET @NewAttentionMode = N'Humano';
        SET @NewLifecycleStatus = N'Abierta';
        SET @NewBasicStatus = N'Open';
        SET @NewAssignedAdminId = @ActorAdminId;
        SET @EventType = CASE
            WHEN @Action = N'Handoff' AND @ActorType = N'System'
                THEN N'AutomaticHandoff'
            ELSE N'Take'
        END;
    END
    ELSE IF @Action = N'ReturnToBot'
    BEGIN
        SET @NewAttentionMode = N'Bot';
        SET @NewLifecycleStatus = N'Abierta';
        SET @NewBasicStatus = N'Open';
        SET @NewAssignedAdminId = NULL;
        SET @EventType = N'ReturnToBot';
    END
    ELSE IF @Action = N'Close'
    BEGIN
        SET @NewLifecycleStatus = N'Cerrada';
        SET @NewBasicStatus = N'Closed';
        SET @NewAssignedAdminId = NULL;
        SET @ClosedAtUtc = SYSUTCDATETIME();
        SET @EventType = N'Close';
    END
    ELSE
    BEGIN
        SET @NewLifecycleStatus = N'Abierta';
        SET @NewBasicStatus = N'Open';
        SET @NewAttentionMode = CASE
            WHEN @PreviousAttentionMode = N'Humano' AND @PreviousAssignedAdminId IS NOT NULL
                THEN N'Humano'
            ELSE N'Bot'
        END;
        SET @EventType = N'Reopen';
    END;

    UPDATE dbo.Conversation
    SET AttentionMode = @NewAttentionMode,
        LifecycleStatus = @NewLifecycleStatus,
        BasicStatus = @NewBasicStatus,
        AssignedAdminId = @NewAssignedAdminId,
        ClosedAtUtc = @ClosedAtUtc,
        LastAttentionChangedAtUtc = CASE
            WHEN AttentionMode <> @NewAttentionMode OR LifecycleStatus <> @NewLifecycleStatus
                THEN SYSUTCDATETIME()
            ELSE LastAttentionChangedAtUtc
        END
    WHERE TenantId = @TenantId
      AND ConversationId = @ConversationId
      AND Version = @ExpectedVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT CONVERT(bit, 0) AS Applied, CONVERT(bit, 1) AS Found,
               CONVERT(bit, 1) AS Conflict, @CurrentVersion AS ResultingVersion;
        RETURN;
    END;

    DECLARE @ResultingVersion binary(8);
    SELECT @ResultingVersion = Version
    FROM dbo.Conversation
    WHERE TenantId = @TenantId AND ConversationId = @ConversationId;

    INSERT dbo.ConversationAuditEvent
        (TenantId, ConversationId, EventType, PreviousAttentionMode, NewAttentionMode,
         PreviousLifecycleStatus, NewLifecycleStatus, ActorType, Origin, ActorAdminId,
         ReasonCode, ExpectedVersion, ResultingVersion)
    VALUES
        (@TenantId, @ConversationId, @EventType, @PreviousAttentionMode, @NewAttentionMode,
         @PreviousLifecycleStatus, @NewLifecycleStatus, @ActorType, @Origin, @ActorAdminId,
         @ReasonCode, @ExpectedVersion, @ResultingVersion);

    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Applied, CONVERT(bit, 1) AS Found,
           CONVERT(bit, 0) AS Conflict, @ResultingVersion AS ResultingVersion;
END;
GO
