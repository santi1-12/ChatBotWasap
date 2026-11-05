:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_GetDetail
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @ConversationId bigint,
    @BeforeMessageId bigint = NULL,
    @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;

    IF @ConversationId < 1 OR @PageSize < 1 OR @PageSize > 100
        THROW 51022, 'Invalid conversation detail parameters.', 1;

    ;WITH message_page AS
    (
        SELECT TOP (@PageSize)
            message.MessageId,
            message.Direction,
            message.MessageType,
            message.ContentText,
            message.ActorType,
            message.Origin,
            message.CreatedByAdminId,
            message.ReplySource,
            message.ProcessingState,
             message.DeliveryState,
             message.CreatedAtUtc,
             message.FailureCode,
             message.IsHistorical
        FROM dbo.Message AS message
        WHERE message.TenantId = @TenantId
          AND message.IntegrationId = @IntegrationId
          AND message.ConversationId = @ConversationId
          AND (@BeforeMessageId IS NULL OR message.MessageId < @BeforeMessageId)
        ORDER BY message.CreatedAtUtc DESC, message.MessageId DESC
    )
    SELECT
        conversation.ConversationId,
        conversation.TenantId,
        COALESCE(NULLIF(LTRIM(RTRIM(contact.DisplayName)), N''), contact.WhatsAppUserId) AS Contact,
        conversation.LastMessageAtUtc,
        conversation.BasicStatus AS Status,
        conversation.AttentionMode,
        conversation.LifecycleStatus,
        conversation.AssignedAdminId,
        CONVERT(binary(8), conversation.Version) AS Version,
        lastMessage.LastMessagePreview,
        message_page.MessageId,
        message_page.Direction,
        message_page.MessageType,
        message_page.ContentText,
        message_page.ActorType,
        message_page.Origin,
        message_page.CreatedByAdminId,
         outbox.SourceInboundMessageId,
        CASE WHEN transcript.State = N'Completed' THEN transcript.TranscriptText END AS Transcription,
         CONVERT(bit, CASE WHEN media.AvailabilityState = N'Available'
                                AND (media.ExpiresAtUtc IS NULL OR media.ExpiresAtUtc > SYSUTCDATETIME())
                            THEN 1 ELSE 0 END) AS MediaAvailable,
         media.AvailabilityState AS MediaAvailabilityState,
         media.ExpiresAtUtc AS MediaExpiresAtUtc,
        message_page.ReplySource,
        message_page.ProcessingState,
             message_page.DeliveryState,
             message_page.FailureCode AS OutcomeCode,
         message_page.IsHistorical,
             message_page.CreatedAtUtc
    FROM dbo.Conversation AS conversation
    INNER JOIN dbo.Contact AS contact
        ON contact.ContactId = conversation.ContactId
       AND contact.TenantId = @TenantId
       AND contact.IntegrationId = @IntegrationId
    OUTER APPLY
    (
        SELECT TOP (1)
            CASE message.MessageType
                WHEN N'Audio' THEN N'[Audio]'
                WHEN N'Image' THEN N'[Imagen]'
                WHEN N'Unsupported' THEN N'[Mensaje no compatible]'
                ELSE message.ContentText
            END AS LastMessagePreview
        FROM dbo.Message AS message
         WHERE message.TenantId = @TenantId
           AND message.IntegrationId = @IntegrationId
          AND message.ConversationId = conversation.ConversationId
        ORDER BY message.CreatedAtUtc DESC, message.MessageId DESC
    ) AS lastMessage
    LEFT JOIN message_page
        ON 1 = 1
    LEFT JOIN dbo.MessageOutbox AS outbox
        ON outbox.MessageId = message_page.MessageId
       AND outbox.TenantId = conversation.TenantId
    LEFT JOIN dbo.MediaAttachment AS media
         ON media.MessageId = message_page.MessageId
        AND media.TenantId = conversation.TenantId
    LEFT JOIN dbo.Transcript AS transcript
        ON transcript.MessageId = message_page.MessageId
       AND transcript.TenantId = conversation.TenantId
    WHERE conversation.TenantId = @TenantId
      AND conversation.IntegrationId = @IntegrationId
      AND conversation.ConversationId = @ConversationId
    ORDER BY
        CASE WHEN message_page.MessageId IS NULL THEN 1 ELSE 0 END,
        message_page.CreatedAtUtc,
        message_page.MessageId;
END;
GO
