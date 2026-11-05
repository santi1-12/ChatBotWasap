SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_MediaAttachment_MarkDeleted
    @TenantId uniqueidentifier,
    @MessageId bigint
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE dbo.MediaAttachment
    SET AvailabilityState = N'Deleted',
        StorageReference = NULL
    WHERE TenantId = @TenantId
      AND MessageId = @MessageId
       AND AvailabilityState IN (N'Pending', N'Available', N'Failed');
END;
GO
