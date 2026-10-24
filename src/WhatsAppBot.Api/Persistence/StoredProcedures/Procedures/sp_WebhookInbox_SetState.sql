SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WebhookInbox_SetState
    @EventKey nvarchar(256),
    @State nvarchar(16),
    @SafeFailureCode nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.WebhookInboxEvent
    SET State = @State,
        LastFailureCode = @SafeFailureCode,
        ProcessedAtUtc = CASE WHEN @State IN (N'Completed', N'Failed') THEN SYSUTCDATETIME() ELSE NULL END
    WHERE EventKey = @EventKey;
END;
GO
