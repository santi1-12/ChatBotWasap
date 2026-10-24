:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_SetAssignment
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @AssignedAdminId bigint = NULL,
    @ExpectedVersion binary(8),
    @ActorAdminId bigint,
    @Origin nvarchar(32) = N'AdminPanel'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DATALENGTH(@ExpectedVersion) <> 8 OR @ActorAdminId IS NULL
        THROW 51041, 'A valid assignment actor and expected version are required.', 1;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.TenantMembership
        WHERE TenantId = @TenantId AND AdminId = @ActorAdminId AND IsActive = 1
    )
        THROW 51042, 'The assignment actor is not an active Tenant member.', 1;
    IF @AssignedAdminId IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM dbo.TenantMembership
        WHERE TenantId = @TenantId AND AdminId = @AssignedAdminId AND IsActive = 1
    )
        THROW 51043, 'The assigned administrator is not an active Tenant member.', 1;

    BEGIN TRANSACTION;
    DECLARE @PreviousAttentionMode nvarchar(16);
    DECLARE @PreviousLifecycleStatus nvarchar(16);
    DECLARE @CurrentVersion binary(8);
    SELECT
        @PreviousAttentionMode = AttentionMode,
        @PreviousLifecycleStatus = LifecycleStatus,
        @CurrentVersion = Version
    FROM dbo.Conversation WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND ConversationId = @ConversationId;

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

    UPDATE dbo.Conversation
    SET AssignedAdminId = @AssignedAdminId,
        LastAttentionChangedAtUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND ConversationId = @ConversationId
      AND Version = @ExpectedVersion;

    DECLARE @ResultingVersion binary(8);
    SELECT @ResultingVersion = Version
    FROM dbo.Conversation
    WHERE TenantId = @TenantId AND ConversationId = @ConversationId;

    INSERT dbo.ConversationAuditEvent
        (TenantId, ConversationId, EventType, PreviousAttentionMode, NewAttentionMode,
         PreviousLifecycleStatus, NewLifecycleStatus, ActorType, Origin, ActorAdminId,
         ReasonCode, ExpectedVersion, ResultingVersion)
    VALUES
        (@TenantId, @ConversationId, N'Assign', @PreviousAttentionMode, @PreviousAttentionMode,
         @PreviousLifecycleStatus, @PreviousLifecycleStatus, N'Admin', @Origin, @ActorAdminId,
         CASE WHEN @AssignedAdminId IS NULL THEN N'Unassigned' ELSE N'Assigned' END,
         @ExpectedVersion, @ResultingVersion);

    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Applied, CONVERT(bit, 1) AS Found,
           CONVERT(bit, 0) AS Conflict, @ResultingVersion AS ResultingVersion;
END;
GO
