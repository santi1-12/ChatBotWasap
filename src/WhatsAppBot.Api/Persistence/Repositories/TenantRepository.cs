using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class TenantRepository(StoredProcedureExecutor executor) : ITenantRepository
{
    public Task<TenantScope?> GetActiveForAdministratorAsync(
        long administratorId,
        Guid? tenantId,
        CancellationToken cancellationToken) =>
        executor.QuerySingleAsync(
            "dbo.sp_Tenant_GetActiveForAdministrator",
            [
                new SqlParameter("@AdminId", SqlDbType.BigInt) { Value = administratorId },
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier)
                {
                    Value = (object?)tenantId ?? DBNull.Value
                }
            ],
            Map,
            cancellationToken);

    private static TenantScope Map(SqlDataReader reader)
    {
        var tenant = new TenantRecord(
            reader.GetGuid(reader.GetOrdinal("TenantId")),
            reader.GetString(reader.GetOrdinal("LegalName")),
            reader.GetBoolean(reader.GetOrdinal("TenantIsActive")),
            ToUtc(reader, "TenantCreatedAtUtc"),
            ToUtc(reader, "TenantUpdatedAtUtc"));
        var membership = new TenantMembershipRecord(
            reader.GetInt64(reader.GetOrdinal("TenantMembershipId")),
            tenant.TenantId,
            reader.GetInt64(reader.GetOrdinal("AdminId")),
            reader.GetBoolean(reader.GetOrdinal("MembershipIsActive")),
            ToUtc(reader, "MembershipCreatedAtUtc"),
            ToUtc(reader, "MembershipUpdatedAtUtc"));
        return new TenantScope(tenant, membership);
    }

    private static DateTimeOffset ToUtc(SqlDataReader reader, string columnName) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(columnName)), DateTimeKind.Utc));
}
