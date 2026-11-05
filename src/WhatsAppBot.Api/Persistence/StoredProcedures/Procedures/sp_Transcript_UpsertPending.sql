SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Transcript_UpsertPending
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @MessageId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.Message
        WHERE MessageId = @MessageId
          AND TenantId = @TenantId
          AND ConversationId = @ConversationId
          AND MessageType = N'Audio'
    )
        THROW 51044, 'The transcript message does not belong to the requested Tenant and Conversation.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Transcript WHERE MessageId = @MessageId)
        UPDATE dbo.Transcript
        SET TenantId = @TenantId,
            TranscriptText = NULL,
            State = N'Pending',
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId;
    ELSE
        INSERT dbo.Transcript(MessageId, TenantId, TranscriptText, State)
        VALUES (@MessageId, @TenantId, NULL, N'Pending');

    COMMIT TRANSACTION;
END;
GO
