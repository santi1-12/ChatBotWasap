:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Message_ApplyDeliveryStatus
    @PhoneNumberId nvarchar(64),
    @ProviderMessageId nvarchar(256),
    @DeliveryStatus nvarchar(24),
    @ProviderTimestampUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @DeliveryStatus NOT IN (N'sent', N'delivered', N'read', N'failed')
        THROW 51050, 'The Meta delivery status is not supported.', 1;

    BEGIN TRANSACTION;
    DECLARE @MessageId bigint;
    DECLARE @CurrentState nvarchar(24);
    DECLARE @CurrentStatusAtUtc datetime2(3);
    DECLARE @CurrentFailureCode nvarchar(100);
    DECLARE @NewState nvarchar(24);
    DECLARE @ShouldApply bit = 0;

    SELECT TOP (1)
        @MessageId = message.MessageId,
        @CurrentState = message.DeliveryState,
        @CurrentStatusAtUtc = message.DeliveryStatusAtUtc,
        @CurrentFailureCode = message.FailureCode
    FROM dbo.Message AS message WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.WhatsAppIntegration AS integration
        ON integration.IntegrationId = message.IntegrationId
    WHERE integration.PhoneNumberId = @PhoneNumberId
      AND message.ProviderMessageId = @ProviderMessageId
      AND message.Direction = N'Outbound'
    ORDER BY message.MessageId DESC;

    IF @MessageId IS NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CONVERT(bit, 0) AS Found, CONVERT(bit, 0) AS Applied,
               CONVERT(nvarchar(24), NULL) AS DeliveryState;
        RETURN;
    END;

    DECLARE @CurrentRank int = CASE @CurrentState
        WHEN N'Pending' THEN 0
        WHEN N'Sent' THEN 1
        WHEN N'Delivered' THEN 2
        WHEN N'Read' THEN 3
        -- Failed records a provider failure, but a later confirmed delivery may recover it.
        WHEN N'Failed' THEN 1
        ELSE -1
    END;
    DECLARE @IncomingRank int = CASE @DeliveryStatus
        WHEN N'sent' THEN 1
        WHEN N'delivered' THEN 2
        WHEN N'read' THEN 3
        WHEN N'failed' THEN 4
        ELSE -1
    END;

    IF @CurrentState = N'Read'
        SET @ShouldApply = 0;
    ELSE IF @DeliveryStatus = N'failed'
        SET @ShouldApply = CASE
            WHEN @CurrentState IN (N'Pending', N'Sent') AND
                 (@CurrentStatusAtUtc IS NULL OR @ProviderTimestampUtc >= @CurrentStatusAtUtc)
                THEN 1
            ELSE 0
        END;
    ELSE IF @IncomingRank > @CurrentRank AND
            (@CurrentStatusAtUtc IS NULL OR @ProviderTimestampUtc >= @CurrentStatusAtUtc)
        SET @ShouldApply = 1;

    IF @ShouldApply = 1
    BEGIN
        SET @NewState = CASE @DeliveryStatus
            WHEN N'sent' THEN N'Sent'
            WHEN N'delivered' THEN N'Delivered'
            WHEN N'read' THEN N'Read'
            ELSE N'Failed'
        END;

        UPDATE dbo.Message
        SET DeliveryState = @NewState,
            ProcessingState = CASE WHEN @NewState = N'Failed' THEN N'Failed' ELSE N'Completed' END,
            FailureCode = CASE
                WHEN @NewState = N'Failed' THEN N'MetaDeliveryFailed'
                WHEN @CurrentFailureCode IS NOT NULL THEN @CurrentFailureCode
                ELSE NULL
            END,
            DeliveryStatusAtUtc = @ProviderTimestampUtc,
            UpdatedAtUtc = SYSUTCDATETIME()
        WHERE MessageId = @MessageId;
    END
    ELSE
        SET @NewState = @CurrentState;

    COMMIT TRANSACTION;
    SELECT CONVERT(bit, 1) AS Found, @ShouldApply AS Applied, @NewState AS DeliveryState;
END;
GO
