SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MessageOutbox_SetState
    @MessageId bigint,
    @State nvarchar(16),
    @RetryDelaySeconds int = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MessageOutbox
    SET State = @State,
        NextAttemptAtUtc = DATEADD(second, CASE WHEN @RetryDelaySeconds BETWEEN 0 AND 3600
                                                THEN @RetryDelaySeconds ELSE 3600 END, SYSUTCDATETIME()),
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE MessageId = @MessageId;
END;
GO
