SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_UpdateOutboundProviderResult
    @MessageId bigint,
    @Succeeded bit,
    @ProviderMessageId nvarchar(256) = NULL,
    @SafeFailureCode nvarchar(100) = NULL,
    @IsTerminal bit
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @CurrentDeliveryState nvarchar(24);
    DECLARE @CurrentFailureCode nvarchar(100);
    SELECT @CurrentDeliveryState = DeliveryState
        ,@CurrentFailureCode = FailureCode
    FROM dbo.Message WITH (UPDLOCK, HOLDLOCK)
    WHERE MessageId = @MessageId AND Direction = N'Outbound';

    IF @Succeeded = 1
    BEGIN
        UPDATE dbo.Message
        SET ProviderMessageId = @ProviderMessageId,
            DeliveryState = CASE
                WHEN @CurrentDeliveryState IN (N'Delivered', N'Read', N'Failed')
                    THEN @CurrentDeliveryState
                ELSE N'Sent'
            END,
            ProcessingState = CASE
                WHEN @CurrentDeliveryState = N'Failed' THEN N'Failed'
                ELSE N'Completed'
            END,
            FailureCode = CASE
                WHEN @CurrentFailureCode IS NOT NULL THEN @CurrentFailureCode
                ELSE NULL
            END,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId AND Direction = N'Outbound';
        UPDATE dbo.MessageOutbox
        SET State = CASE WHEN @CurrentDeliveryState = N'Failed' THEN N'Failed' ELSE N'Sent' END,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId;
    END
    ELSE IF @IsTerminal = 1
    BEGIN
        UPDATE dbo.Message
        SET DeliveryState = N'Failed',
            ProcessingState = N'Failed',
            FailureCode = @SafeFailureCode,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId AND Direction = N'Outbound'
          AND @CurrentDeliveryState NOT IN (N'Delivered', N'Read');
        UPDATE dbo.MessageOutbox
        SET State = CASE
                WHEN @CurrentDeliveryState IN (N'Delivered', N'Read') THEN N'Sent'
                ELSE N'Failed'
            END,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId;
    END
    ELSE
    BEGIN
        UPDATE dbo.Message
        SET DeliveryState = N'Pending',
            ProcessingState = N'Processing',
            FailureCode = NULL,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId AND Direction = N'Outbound';
    END;
    COMMIT TRANSACTION;
END;
GO
