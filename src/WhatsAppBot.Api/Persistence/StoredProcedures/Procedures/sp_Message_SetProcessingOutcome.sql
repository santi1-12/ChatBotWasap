SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_SetProcessingOutcome
    @MessageId bigint,
    @ProcessingState nvarchar(24),
    @SafeOutcomeCode nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Message
    SET ProcessingState = @ProcessingState,
        FailureCode = @SafeOutcomeCode,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE MessageId = @MessageId AND Direction = N'Inbound';
END;
GO
