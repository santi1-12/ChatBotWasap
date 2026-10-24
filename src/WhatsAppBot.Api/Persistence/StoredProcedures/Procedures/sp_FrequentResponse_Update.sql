:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_FrequentResponse_Update
    @IntegrationId uniqueidentifier,
    @FrequentResponseId bigint,
    @QuestionOrIntent nvarchar(500),
    @AnswerText nvarchar(max),
    @Priority int,
    @Category nvarchar(100) = NULL,
    @IsActive bit,
    @Expressions dbo.FrequentResponseExpressionTableType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM @Expressions)
        THROW 51021, 'At least one expression is required.', 1;

    DECLARE @TenantId uniqueidentifier =
        (SELECT TenantId FROM dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId);
    IF @TenantId IS NULL
        THROW 51022, 'The integration is not assigned to a Tenant.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;
        UPDATE dbo.FrequentResponse
        SET TenantId = @TenantId,
            QuestionOrIntent = @QuestionOrIntent,
            AnswerText = @AnswerText,
            Priority = @Priority,
            Category = @Category,
            IsActive = @IsActive,
            ModifiedAtUtc = SYSUTCDATETIME()
        WHERE IntegrationId = @IntegrationId
          AND FrequentResponseId = @FrequentResponseId;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT CAST(0 AS bit) AS Updated;
            RETURN;
        END;

        DELETE dbo.FrequentResponseExpression
        WHERE FrequentResponseId = @FrequentResponseId;
        INSERT dbo.FrequentResponseExpression
            (FrequentResponseId, ExpressionText, NormalizedExpression)
        SELECT @FrequentResponseId, ExpressionText, NormalizedExpression
        FROM @Expressions;
        COMMIT TRANSACTION;

        SELECT CAST(1 AS bit) AS Updated;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
