:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_FrequentResponse_GetById
    @TenantId uniqueidentifier,
    @IntegrationId uniqueidentifier,
    @FrequentResponseId bigint
AS
BEGIN
    SET NOCOUNT ON;

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
      AND response.FrequentResponseId = @FrequentResponseId;
END;
GO
