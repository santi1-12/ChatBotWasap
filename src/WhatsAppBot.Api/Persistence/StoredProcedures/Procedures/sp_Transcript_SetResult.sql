SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Transcript_SetResult
    @TenantId uniqueidentifier,
    @ConversationId bigint,
    @MessageId bigint,
    @TranscriptText nvarchar(max) = NULL,
    @State nvarchar(16)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE transcript
    SET TranscriptText = @TranscriptText,
        State = @State,
        UpdatedAtUtc = SYSUTCDATETIME()
    FROM dbo.Transcript AS transcript
    INNER JOIN dbo.Message AS message ON message.MessageId = transcript.MessageId
    WHERE transcript.TenantId = @TenantId
      AND transcript.MessageId = @MessageId
      AND message.ConversationId = @ConversationId;

    IF @@ROWCOUNT = 0
        THROW 51045, 'The transcript was not found for the requested Tenant and Conversation.', 1;
END;
GO
