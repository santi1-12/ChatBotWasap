:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Admin_CreateFirstAdministrator
    @Email nvarchar(320),
    @PasswordHash nvarchar(512),
    @TenantLegalName nvarchar(256) = N'Initial Tenant'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'WhatsAppBot.FirstAdministrator',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 10000;

    IF @LockResult < 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51000, 'Could not acquire the administrator bootstrap lock.', 1;
    END;

    IF EXISTS (SELECT 1 FROM dbo.Administrator WITH (UPDLOCK, HOLDLOCK))
    BEGIN
        COMMIT TRANSACTION;
        SELECT CAST(0 AS bit);
        RETURN;
    END;

    INSERT dbo.Administrator (Email, PasswordHash, IsActive)
    VALUES (UPPER(LTRIM(RTRIM(@Email))), @PasswordHash, 1);

    DECLARE @AdminId bigint = CONVERT(bigint, SCOPE_IDENTITY());
    DECLARE @TenantId uniqueidentifier = NEWID();
    INSERT dbo.Tenant (TenantId, LegalName, IsActive)
    VALUES (@TenantId, COALESCE(NULLIF(LTRIM(RTRIM(@TenantLegalName)), N''), N'Initial Tenant'), 1);
    INSERT dbo.TenantMembership (TenantId, AdminId, IsActive)
    VALUES (@TenantId, @AdminId, 1);

    COMMIT TRANSACTION;
    SELECT CAST(1 AS bit);
END;
GO
