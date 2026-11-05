using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;
using ConnectionState = WhatsAppBot.Shared.Enums.ConnectionState;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class WhatsAppMigrationRepository(
    StoredProcedureExecutor executor) : IWhatsAppMigrationRepository
{
    public Task<WhatsAppMigrationStatus?> GetStatusAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        executor.QuerySingleAsync(
            "dbo.sp_WhatsAppMigration_GetStatus",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId }
            ],
            MapStatus,
            cancellationToken);

    public async Task<WhatsAppMigrationPreflight> GetPreflightAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_WhatsAppMigration_Preflight",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId }
            ],
            MapPreflight,
            cancellationToken)
        ?? throw new InvalidOperationException("The WhatsApp integration slot has not been provisioned.");

    public async Task<WhatsAppMigrationStatus> BeginAsync(
        Guid tenantId,
        Guid integrationId,
        string? approvalReference,
        string? operationalNotes,
        CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppMigration_Begin",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@ApprovalReference", SqlDbType.NVarChar, 256)
                    { Value = (object?)approvalReference ?? DBNull.Value },
                new SqlParameter("@OperationalNotes", SqlDbType.NVarChar, 2048)
                    { Value = (object?)operationalNotes ?? DBNull.Value }
            ],
            cancellationToken);

        return await GetRequiredStatusAsync(tenantId, integrationId, cancellationToken);
    }

    public async Task<WhatsAppMigrationStatus> CancelAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppMigration_Cancel",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId }
            ],
            cancellationToken);

        return await GetRequiredStatusAsync(tenantId, integrationId, cancellationToken);
    }

    public async Task<WhatsAppMigrationStatus> MarkExternalMutationAsync(
        Guid tenantId,
        Guid integrationId,
        MigrationExternalMutationCommand command,
        CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppMigration_MarkExternalMutation",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@MigrationId", SqlDbType.UniqueIdentifier) { Value = command.MigrationId },
                new SqlParameter("@NextState", SqlDbType.NVarChar, 40) { Value = command.NextState.ToString() },
                new SqlParameter("@ExternalOperationReferenceId", SqlDbType.NVarChar, 256)
                    { Value = (object?)command.ExternalOperationReferenceId ?? DBNull.Value }
            ],
            cancellationToken);

        return await GetRequiredStatusAsync(tenantId, integrationId, cancellationToken);
    }

    public async Task<WhatsAppMigrationStatus> MarkUnknownAsync(
        Guid tenantId,
        Guid integrationId,
        MigrationUnknownCommand command,
        CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppMigration_MarkUnknown",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@MigrationId", SqlDbType.UniqueIdentifier) { Value = command.MigrationId },
                new SqlParameter("@SafeErrorCode", SqlDbType.NVarChar, 100) { Value = command.SafeErrorCode },
                new SqlParameter("@ExternalOperationReferenceId", SqlDbType.NVarChar, 256)
                    { Value = (object?)command.ExternalOperationReferenceId ?? DBNull.Value }
            ],
            cancellationToken);

        return await GetRequiredStatusAsync(tenantId, integrationId, cancellationToken);
    }

    public async Task<WhatsAppIntegration> ReconcileSignupAsync(
        MetaSignupResult result, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "dbo.sp_WhatsAppMigration_ReconcileSignup",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = result.TenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = result.IntegrationId },
                new SqlParameter("@WabaId", SqlDbType.NVarChar, 64) { Value = result.WabaId },
                new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64) { Value = result.PhoneNumberId },
                new SqlParameter("@BusinessPhoneNumber", SqlDbType.NVarChar, 32)
                    { Value = result.BusinessPhoneNumber },
                new SqlParameter("@DisplayName", SqlDbType.NVarChar, 256)
                    { Value = (object?)result.DisplayName ?? DBNull.Value },
                new SqlParameter("@EffectiveEmbeddedSignupConfigId", SqlDbType.NVarChar, 128)
                    { Value = result.EffectiveEmbeddedSignupConfigId },
                new SqlParameter("@AccessTokenSecretRef", SqlDbType.NVarChar, 512)
                    { Value = result.AccessTokenSecretRef },
                new SqlParameter("@MetaAppSecretRef", SqlDbType.NVarChar, 512)
                    { Value = result.MetaAppSecretRef },
                new SqlParameter("@WebhookVerifyTokenRef", SqlDbType.NVarChar, 512)
                    { Value = result.WebhookVerifyTokenRef },
                new SqlParameter("@HistoryImportRequested", SqlDbType.Bit) { Value = result.ImportHistory }
            ],
            cancellationToken);

        return await QueryIntegrationAsync(result.TenantId, result.IntegrationId, cancellationToken)
            ?? throw new InvalidOperationException("The reconciled WhatsApp integration was not found.");
    }

    private async Task<WhatsAppMigrationStatus> GetRequiredStatusAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        await GetStatusAsync(tenantId, integrationId, cancellationToken)
        ?? throw new InvalidOperationException("The WhatsApp migration journal was not found after persistence.");

    private async Task<WhatsAppIntegration?> QueryIntegrationAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_WhatsAppIntegration_GetCurrent",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64) { Value = DBNull.Value },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId }
            ],
            MapIntegration,
            cancellationToken);

    private static WhatsAppMigrationStatus MapStatus(SqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("MigrationId")),
            reader.GetGuid(reader.GetOrdinal("TenantId")),
            reader.GetGuid(reader.GetOrdinal("IntegrationId")),
            Enum.Parse<TransitionState>(reader.GetString(reader.GetOrdinal("TransitionState")), false),
            Enum.Parse<MigrationCompletionState>(
                reader.GetString(reader.GetOrdinal("CompletionState")), false),
            reader.GetBoolean(reader.GetOrdinal("ExternalMutationOccurred")),
            GetNullableString(reader, "PreviousWabaId"),
            GetNullableString(reader, "PreviousPhoneNumberId"),
            GetNullableString(reader, "PreviousAccessTokenSecretRef"),
            GetNullableString(reader, "NewWabaId"),
            GetNullableString(reader, "NewPhoneNumberId"),
            GetNullableString(reader, "NewAccessTokenSecretRef"),
            reader.GetString(reader.GetOrdinal("ExpectedExternalOperation")),
            GetNullableString(reader, "ExternalOperationReferenceId"),
            GetUtcDateTime(reader, "StartedAtUtc"),
            GetUtcDateTime(reader, "UpdatedAtUtc"),
            GetNullableDateTime(reader, "PreparingAtUtc"),
            GetNullableDateTime(reader, "MigrationToCoexistenceAtUtc"),
            GetNullableDateTime(reader, "AwaitingBusinessAppAtUtc"),
            GetNullableDateTime(reader, "AwaitingCoexistenceSignupAtUtc"),
            GetNullableDateTime(reader, "ReconcilingAtUtc"),
            GetNullableDateTime(reader, "CompletedAtUtc"),
            GetNullableDateTime(reader, "FailedAtUtc"),
            GetNullableDateTime(reader, "UnknownAtUtc"),
            GetNullableString(reader, "ErrorCode"),
            GetNullableString(reader, "ApprovalReference"),
            GetNullableString(reader, "OperationalNotes"),
            reader.GetBoolean(reader.GetOrdinal("PreviousBotEnabled")),
            Enum.Parse<BotReplyMode>(reader.GetString(reader.GetOrdinal("PreviousReplyMode")), false),
            reader.GetInt32(reader.GetOrdinal("HeldOutboxCount")),
            reader.GetInt32(reader.GetOrdinal("SendingOutboxCount")));

    private static WhatsAppMigrationPreflight MapPreflight(SqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("TenantId")),
            reader.GetGuid(reader.GetOrdinal("IntegrationId")),
            reader.GetBoolean(reader.GetOrdinal("CanBegin")),
            GetNullableString(reader, "BlockingCode"),
            Enum.Parse<ConnectionState>(reader.GetString(reader.GetOrdinal("ConnectionState")), false),
            GetNullableString(reader, "OnboardingType") is { } onboardingType
                ? Enum.Parse<WhatsAppOnboardingType>(onboardingType, false)
                : null,
            Enum.Parse<TransitionState>(reader.GetString(reader.GetOrdinal("TransitionState")), false),
            reader.GetBoolean(reader.GetOrdinal("HasMetaIdentity")),
            false,
            reader.GetInt32(reader.GetOrdinal("SendingOutboxCount")),
            reader.GetInt32(reader.GetOrdinal("PendingOutboxCount")),
            reader.GetInt32(reader.GetOrdinal("HeldOutboxCount")),
            reader.GetInt32(reader.GetOrdinal("ProcessingWebhookCount")));

    private static WhatsAppIntegration MapIntegration(SqlDataReader reader) =>
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
            TenantId = reader.GetGuid(reader.GetOrdinal("TenantId")),
            OnboardingType = GetNullableString(reader, "OnboardingType") is { } onboardingType
                ? Enum.Parse<WhatsAppOnboardingType>(onboardingType, false)
                : null,
            TransitionState = Enum.Parse<TransitionState>(
                reader.GetString(reader.GetOrdinal("TransitionState")), false),
            TransitionStartedAtUtc = GetNullableDateTime(reader, "TransitionStartedAtUtc"),
            EffectiveEmbeddedSignupConfigId = GetNullableString(reader, "EffectiveEmbeddedSignupConfigId"),
            HistoryImportRequested = reader.GetBoolean(reader.GetOrdinal("HistoryImportRequested")),
            LastConnectionErrorCode = GetNullableString(reader, "LastConnectionErrorCode"),
            AccessTokenSecretRef = GetNullableString(reader, "AccessTokenSecretRef"),
            MetaAppSecretRef = GetNullableString(reader, "MetaAppSecretRef"),
            WebhookVerifyTokenRef = GetNullableString(reader, "WebhookVerifyTokenRef"),
            CreatedAtUtc = GetUtcDateTime(reader, "CreatedAtUtc"),
            UpdatedAtUtc = GetUtcDateTime(reader, "UpdatedAtUtc")
        };

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
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
