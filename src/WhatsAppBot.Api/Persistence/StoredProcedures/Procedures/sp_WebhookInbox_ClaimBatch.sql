SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_WebhookInbox_ClaimBatch
    @BatchSize int
AS
BEGIN
    SET NOCOUNT ON;
    IF @BatchSize NOT BETWEEN 1 AND 100 THROW 51003, 'Invalid inbox batch size.', 1;
    DECLARE @Claimed TABLE
    (
        EventKey nvarchar(256),
        IntegrationId uniqueidentifier,
        TenantId uniqueidentifier,
        NormalizedEventJson nvarchar(max),
        AttemptCount int
    );
    ;WITH Claimable AS
    (
        SELECT TOP (@BatchSize) *
        FROM dbo.WebhookInboxEvent WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE State = N'Received'
           OR (State = N'Processing' AND ReceivedAtUtc < DATEADD(minute, -5, SYSUTCDATETIME()))
        ORDER BY ReceivedAtUtc, WebhookInboxEventId
    )
    UPDATE Claimable
    SET State = N'Processing',
        AttemptCount = AttemptCount + 1,
        ReceivedAtUtc = SYSUTCDATETIME()
    OUTPUT inserted.EventKey, inserted.IntegrationId, inserted.TenantId,
           inserted.NormalizedEventJson, inserted.AttemptCount INTO @Claimed;
    SELECT EventKey, IntegrationId, TenantId, NormalizedEventJson, AttemptCount FROM @Claimed;
END;
GO
