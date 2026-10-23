using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class BotConfigurationRepository(
    StoredProcedureExecutor executor,
    ITenantContext? tenantContext = null) : IBotConfigurationRepository
{
    public async Task<BotConfiguration> GetCurrentAsync(CancellationToken cancellationToken) =>
        await QueryAsync(await TryGetTenantIdAsync(cancellationToken), null, cancellationToken)
        ?? throw new InvalidOperationException("The initial bot configuration has not been provisioned.");

    public async Task<BotConfiguration> GetForIntegrationAsync(
        Guid integrationId, CancellationToken cancellationToken) =>
        await QueryAsync(null, integrationId, cancellationToken)
        ?? throw new InvalidOperationException("The bot configuration has not been provisioned for the integration.");

    public async Task<BotConfiguration> UpdateAsync(
        Guid integrationId, bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_BotConfiguration_Upsert",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@IsBotEnabled", SqlDbType.Bit) { Value = isBotEnabled },
                new SqlParameter("@ReplyMode", SqlDbType.NVarChar, 32) { Value = replyMode.ToString() }
            ],
            Map,
            cancellationToken)
        ?? throw new InvalidOperationException("The bot configuration could not be updated.");

    private static BotConfiguration Map(SqlDataReader reader) => new(
        reader.GetGuid(reader.GetOrdinal("IntegrationId")),
        reader.GetBoolean(reader.GetOrdinal("IsBotEnabled")),
        Enum.Parse<BotReplyMode>(reader.GetString(reader.GetOrdinal("ReplyMode")), ignoreCase: false))
    {
        CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(
            reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")), DateTimeKind.Utc)),
        UpdatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(
            reader.GetDateTime(reader.GetOrdinal("UpdatedAtUtc")), DateTimeKind.Utc))
    };

    private Task<BotConfiguration?> QueryAsync(
        Guid? tenantId, Guid? integrationId, CancellationToken cancellationToken) =>
        executor.QuerySingleAsync(
            "dbo.sp_BotConfiguration_Get",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier)
                {
                    Value = (object?)tenantId ?? DBNull.Value
                },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
                {
                    Value = (object?)integrationId ?? DBNull.Value
                }
            ],
            Map,
            cancellationToken);

    private async Task<Guid?> TryGetTenantIdAsync(CancellationToken cancellationToken) =>
        tenantContext is null
            ? null
            : (await tenantContext.GetRequiredAsync(cancellationToken)).TenantId;
}
