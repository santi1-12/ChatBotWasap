:ON ERROR EXIT
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
USE [WhatsAppBot];
GO
CREATE OR ALTER PROCEDURE dbo.sp_Tenant_GetActiveForAdministrator
    @AdminId bigint,
    @TenantId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        membership.TenantMembershipId,
        membership.TenantId,
        membership.AdminId,
        membership.IsActive AS MembershipIsActive,
        membership.CreatedAtUtc AS MembershipCreatedAtUtc,
        membership.UpdatedAtUtc AS MembershipUpdatedAtUtc,
        tenant.LegalName,
        tenant.IsActive AS TenantIsActive,
        tenant.CreatedAtUtc AS TenantCreatedAtUtc,
        tenant.UpdatedAtUtc AS TenantUpdatedAtUtc
    FROM dbo.TenantMembership AS membership
    INNER JOIN dbo.Tenant AS tenant ON tenant.TenantId = membership.TenantId
    WHERE membership.AdminId = @AdminId
      AND membership.IsActive = 1
      AND tenant.IsActive = 1
      AND (@TenantId IS NULL OR membership.TenantId = @TenantId)
    ORDER BY membership.TenantMembershipId;
END;
GO
