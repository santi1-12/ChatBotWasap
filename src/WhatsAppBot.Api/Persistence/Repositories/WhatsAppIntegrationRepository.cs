using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;
using ConnectionState = WhatsAppBot.Shared.Enums.ConnectionState;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class WhatsAppIntegrationRepository(
    StoredProcedureExecutor executor,
    ITenantContext tenantContext)
    : IWhatsAppIntegrationRepository
{
    public async Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
        await QueryCurrentAsync(
            null, (await tenantContext.GetRequiredAsync(cancellationToken)).TenantId, cancellationToken)
        ?? throw new InvalidOperationException("The initial WhatsApp integration slot has not been provisioned.");

    public async Task<WhatsAppIntegration> GetByPhoneNumberIdAsync(
        string phoneNumberId, CancellationToken cancellationToken) =>
        await QueryCurrentAsync(phoneNumberId, null, cancellationToken)
        ?? throw new InvalidOperationException("The webhook phone number is not connected to an integration.");

    public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "dbo.sp_WhatsAppIntegration_SaveSignupResult",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = result.IntegrationId },
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = result.TenantId },
                new SqlParameter("@WabaId", SqlDbType.NVarChar, 64) { Value = result.WabaId },
                new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64) { Value = result.PhoneNumberId },
                new SqlParameter("@BusinessPhoneNumber", SqlDbType.NVarChar, 32) { Value = result.BusinessPhoneNumber },
                new SqlParameter("@DisplayName", SqlDbType.NVarChar, 256)
                    { Value = (object?)result.DisplayName ?? DBNull.Value },
                new SqlParameter("@OnboardingType", SqlDbType.NVarChar, 16) { Value = result.OnboardingType.ToString() },
                new SqlParameter("@EffectiveEmbeddedSignupConfigId", SqlDbType.NVarChar, 128)
                    { Value = result.EffectiveEmbeddedSignupConfigId },
                new SqlParameter("@AccessTokenSecretRef", SqlDbType.NVarChar, 512)
                    { Value = result.AccessTokenSecretRef },
                new SqlParameter("@MetaAppSecretRef", SqlDbType.NVarChar, 512)
                    { Value = result.MetaAppSecretRef },
                new SqlParameter("@WebhookVerifyTokenRef", SqlDbType.NVarChar, 512)
                    { Value = result.WebhookVerifyTokenRef }
            ],
            cancellationToken);

    public async Task SetConnectionStateAsync(
        Guid integrationId, ConnectionState state, string? safeErrorCode, CancellationToken cancellationToken)
    {
        var tenantId = (await tenantContext.GetRequiredAsync(cancellationToken)).TenantId;
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppIntegration_SetConnectionState",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@ConnectionState", SqlDbType.NVarChar, 32) { Value = state.ToString() },
                new SqlParameter("@SafeErrorCode", SqlDbType.NVarChar, 100)
                    { Value = (object?)safeErrorCode ?? DBNull.Value }
            ],
            cancellationToken);
    }

    public async Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var tenantId = (await tenantContext.GetRequiredAsync(cancellationToken)).TenantId;
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppIntegration_Disconnect",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId }
            ],
            cancellationToken);
    }

    private static WhatsAppIntegration Map(SqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("IntegrationId")),
            GetNullableString(reader, "WabaId"),
            GetNullableString(reader, "PhoneNumberId"),
            GetNullableString(reader, "BusinessPhoneNumber"),
            GetNullableString(reader, "DisplayName"),
            Enum.Parse<ConnectionState>(reader.GetString(reader.GetOrdinal("ConnectionState")), false),
            GetNullableDateTime(reader, "ConnectedAtUtc"),
            reader.GetBoolean(reader.GetOrdinal("IsActive")))
        {
            TenantId = GetNullableGuid(reader, "TenantId"),
            OnboardingType = GetNullableString(reader, "OnboardingType") is { } onboardingType
                ? Enum.Parse<WhatsAppOnboardingType>(onboardingType, false)
                : null,
            EffectiveEmbeddedSignupConfigId = GetNullableString(reader, "EffectiveEmbeddedSignupConfigId"),
            LastConnectionErrorCode = GetNullableString(reader, "LastConnectionErrorCode"),
            AccessTokenSecretRef = GetNullableString(reader, "AccessTokenSecretRef"),
            MetaAppSecretRef = GetNullableString(reader, "MetaAppSecretRef"),
            WebhookVerifyTokenRef = GetNullableString(reader, "WebhookVerifyTokenRef"),
            CreatedAtUtc = GetUtcDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = GetUtcDateTime(reader, "UpdatedAtUtc")
        };

    private Task<WhatsAppIntegration?> QueryCurrentAsync(
        string? phoneNumberId, Guid? tenantId, CancellationToken cancellationToken) =>
        executor.QuerySingleAsync(
            "dbo.sp_WhatsAppIntegration_GetCurrent",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier)
                {
                    Value = (object?)tenantId ?? DBNull.Value
                },
                new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64)
                {
                    Value = (object?)phoneNumberId ?? DBNull.Value
                }
            ],
            Map,
            cancellationToken);

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static Guid? GetNullableGuid(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static DateTimeOffset? GetNullableDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc));
    }

    private static DateTimeOffset GetUtcDateTime(SqlDataReader reader, string name) =>
        new(DateTime.SpecifyKind(reader.GetDateTime(reader.GetOrdinal(name)), DateTimeKind.Utc));
}
