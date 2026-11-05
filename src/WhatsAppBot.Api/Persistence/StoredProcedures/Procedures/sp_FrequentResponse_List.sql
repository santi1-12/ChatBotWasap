:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_FrequentResponse_List
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @Page int,
    @PageSize int
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 OR @PageSize < 1 OR @PageSize > 100
        THROW 51020, 'Invalid frequent response paging parameters.', 1;

    SELECT
        page.FrequentResponseId,
        page.TenantId,
        page.IntegrationId,
        page.QuestionOrIntent,
        page.AnswerText,
        page.Priority,
        page.Category,
        page.IsActive,
        page.CreatedAtUtc,
        page.ModifiedAtUtc,
        totals.TotalCount,
        page.ExpressionsJson
    FROM
    (
        SELECT
            response.FrequentResponseId,
            response.TenantId,
            response.IntegrationId,
            response.QuestionOrIntent,
            response.AnswerText,
            response.Priority,
            response.Category,
            response.IsActive,
            response.CreatedAtUtc,
            response.ModifiedAtUtc,
            (
                SELECT expression.ExpressionText
                FROM dbo.FrequentResponseExpression AS expression
                WHERE expression.FrequentResponseId = response.FrequentResponseId
                ORDER BY expression.ExpressionId
                FOR JSON PATH
            ) AS ExpressionsJson
        FROM dbo.FrequentResponse AS response
        WHERE response.TenantId = @TenantId
          AND response.IntegrationId = @IntegrationId
        ORDER BY response.Priority DESC, response.FrequentResponseId
        OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    ) AS page
    RIGHT JOIN
    (
        SELECT COUNT_BIG(*) AS TotalCount
        FROM dbo.FrequentResponse
        WHERE TenantId = @TenantId
          AND IntegrationId = @IntegrationId
    ) AS totals ON 1 = 1;
END;
GO
