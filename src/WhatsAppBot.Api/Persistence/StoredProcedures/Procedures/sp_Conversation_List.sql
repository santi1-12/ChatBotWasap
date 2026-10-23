:ON ERROR EXIT
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Conversation_List
    @IntegrationId uniqueidentifier,
    @Page int,
    @PageSize int
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 OR @PageSize < 1 OR @PageSize > 100
        THROW 51021, 'Invalid conversation paging parameters.', 1;

    SELECT
        page.ConversationId,
        page.Contact,
        page.LastMessageAtUtc,
        page.Status,
        page.LastMessagePreview,
        totals.TotalCount
    FROM
    (
        SELECT
            conversation.ConversationId,
            COALESCE(NULLIF(LTRIM(RTRIM(contact.DisplayName)), N''), contact.WhatsAppUserId) AS Contact,
            conversation.LastMessageAtUtc,
            conversation.BasicStatus AS Status,
            lastMessage.LastMessagePreview
        FROM dbo.Conversation AS conversation
        INNER JOIN dbo.Contact AS contact
            ON contact.ContactId = conversation.ContactId
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
            WHERE message.IntegrationId = @IntegrationId
              AND message.ConversationId = conversation.ConversationId
            ORDER BY message.CreatedAtUtc DESC, message.MessageId DESC
        ) AS lastMessage
        WHERE conversation.IntegrationId = @IntegrationId
        ORDER BY conversation.LastMessageAtUtc DESC, conversation.ConversationId DESC
        OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    ) AS page
    RIGHT JOIN
    (
        SELECT COUNT_BIG(*) AS TotalCount
        FROM dbo.Conversation AS conversation
        INNER JOIN dbo.Contact AS contact
            ON contact.ContactId = conversation.ContactId
           AND contact.IntegrationId = @IntegrationId
        WHERE conversation.IntegrationId = @IntegrationId
    ) AS totals ON 1 = 1;
END;
GO
