using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class AdminRepository(StoredProcedureExecutor executor) : IAdministratorRepository
{
    public Task<Administrator?> GetForAuthenticationAsync(
        string normalizedEmail, CancellationToken cancellationToken) =>
        executor.QuerySingleAsync(
            "dbo.sp_Admin_GetByEmailForAuthentication",
            [new SqlParameter("@Email", SqlDbType.NVarChar, 320) { Value = normalizedEmail }],
            reader => new Administrator(
                reader.GetInt64(reader.GetOrdinal("AdminId")),
                reader.GetString(reader.GetOrdinal("Email")),
                reader.GetString(reader.GetOrdinal("PasswordHash")),
                reader.GetBoolean(reader.GetOrdinal("IsActive"))),
            cancellationToken);

    public Task UpdateLastLoginAsync(long adminId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_Admin_UpdateLastLogin",
            [new SqlParameter("@AdminId", SqlDbType.BigInt) { Value = adminId }],
            cancellationToken);

    public Task<bool> CreateFirstAdministratorAsync(
        string normalizedEmail,
        string passwordHash,
        string tenantLegalName,
        CancellationToken cancellationToken) =>
        executor.ExecuteBooleanAsync(
            "dbo.sp_Admin_CreateFirstAdministrator",
            [
                new SqlParameter("@Email", SqlDbType.NVarChar, 320) { Value = normalizedEmail },
                new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 512) { Value = passwordHash },
                new SqlParameter("@TenantLegalName", SqlDbType.NVarChar, 256) { Value = tenantLegalName }
            ],
            cancellationToken);
}
