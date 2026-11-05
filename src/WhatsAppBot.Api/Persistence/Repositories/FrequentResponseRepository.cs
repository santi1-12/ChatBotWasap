using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class FrequentResponseRepository(StoredProcedureExecutor executor) : IFrequentResponseRepository
{
    private sealed record ExpressionRow(string ExpressionText);

    public async Task<FrequentResponsePage> ListAsync(
        Guid tenantId, Guid integrationId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync(
            "dbo.sp_FrequentResponse_List",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@Page", SqlDbType.Int) { Value = page },
                new SqlParameter("@PageSize", SqlDbType.Int) { Value = pageSize }
            ],
            Map,
            cancellationToken);
        return new FrequentResponsePage(
            rows.Where(row => row.Record is not null).Select(row => row.Record!).ToArray(), page, pageSize,
            rows.Count == 0 ? 0 : checked((int)rows[0].TotalCount));
    }

    public async Task<FrequentResponseRecord?> GetAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, CancellationToken cancellationToken) =>
        await executor.QuerySingleAsync(
            "dbo.sp_FrequentResponse_GetById",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@FrequentResponseId", SqlDbType.BigInt) { Value = frequentResponseId }
            ],
            MapRecord,
            cancellationToken);

    public async Task<FrequentResponseRecord> CreateAsync(
        Guid tenantId, Guid integrationId, FrequentResponseCommand command, CancellationToken cancellationToken)
    {
        var id = await executor.QuerySingleAsync(
            "dbo.sp_FrequentResponse_Create",
            CreateParameters(tenantId, integrationId, command),
            reader => reader.GetInt64(reader.GetOrdinal("FrequentResponseId")),
            cancellationToken);
        if (id < 1)
        {
            throw new InvalidOperationException("The stored procedure did not return the created response id.");
        }

        return await GetAsync(tenantId, integrationId, id, cancellationToken)
            ?? throw new InvalidOperationException("The created frequent response could not be read.");
    }

    public async Task<FrequentResponseRecord?> UpdateAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, FrequentResponseCommand command,
        CancellationToken cancellationToken)
    {
        var updated = await executor.QuerySingleAsync(
            "dbo.sp_FrequentResponse_Update",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@FrequentResponseId", SqlDbType.BigInt) { Value = frequentResponseId },
                .. CreateCommandParameters(command)
            ],
            reader => reader.GetBoolean(reader.GetOrdinal("Updated")),
            cancellationToken);
        return updated is true
            ? await GetAsync(tenantId, integrationId, frequentResponseId, cancellationToken)
            : null;
    }

    public Task<bool> SetActiveAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, bool isActive,
        CancellationToken cancellationToken) =>
        executor.ExecuteBooleanAsync(
            "dbo.sp_FrequentResponse_SetActive",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@FrequentResponseId", SqlDbType.BigInt) { Value = frequentResponseId },
                new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive }
            ],
            cancellationToken);

    public Task<bool> DeleteAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, CancellationToken cancellationToken) =>
        executor.ExecuteBooleanAsync(
            "dbo.sp_FrequentResponse_Delete",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@FrequentResponseId", SqlDbType.BigInt) { Value = frequentResponseId }
            ],
            cancellationToken);

    public async Task<IReadOnlyList<FrequentResponseRecord>> ListActiveCandidatesAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        (await executor.QueryAsync(
            "dbo.sp_FrequentResponse_ListActiveCandidates",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId }
            ],
            MapRecord,
            cancellationToken)).ToArray();

    private static SqlParameter[] CreateParameters(
        Guid tenantId, Guid integrationId, FrequentResponseCommand command) =>
    [
        new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
        new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
        .. CreateCommandParameters(command)
    ];

    private static SqlParameter[] CreateCommandParameters(FrequentResponseCommand command)
    {
        var expressions = new DataTable();
        expressions.Columns.Add("ExpressionText", typeof(string));
        expressions.Columns.Add("NormalizedExpression", typeof(string));
        foreach (var expression in command.Expressions)
        {
            expressions.Rows.Add(expression, Business.Services.FrequentResponseMatcher.Normalize(expression));
        }

        return
        [
            new SqlParameter("@QuestionOrIntent", SqlDbType.NVarChar, 500) { Value = command.QuestionOrIntent },
            new SqlParameter("@AnswerText", SqlDbType.NVarChar, -1) { Value = command.AnswerText },
            new SqlParameter("@Priority", SqlDbType.Int) { Value = command.Priority },
            new SqlParameter("@Category", SqlDbType.NVarChar, 100) { Value = (object?)command.Category ?? DBNull.Value },
            new SqlParameter("@IsActive", SqlDbType.Bit) { Value = command.IsActive },
            new SqlParameter("@Expressions", SqlDbType.Structured)
            {
                TypeName = "dbo.FrequentResponseExpressionTableType",
                Value = expressions
            }
        ];
    }

    private static (FrequentResponseRecord? Record, long TotalCount) Map(SqlDataReader reader) =>
        (reader.IsDBNull(reader.GetOrdinal("FrequentResponseId")) ? null : MapRecord(reader),
            reader.GetInt64(reader.GetOrdinal("TotalCount")));

    private static FrequentResponseRecord MapRecord(SqlDataReader reader)
    {
        var expressions = JsonSerializer.Deserialize<List<ExpressionRow>>(
            reader.GetString(reader.GetOrdinal("ExpressionsJson"))) ?? [];
        var response = new FrequentResponse(
            reader.GetInt64(reader.GetOrdinal("FrequentResponseId")),
            reader.GetGuid(reader.GetOrdinal("IntegrationId")),
            reader.GetString(reader.GetOrdinal("QuestionOrIntent")),
            reader.GetString(reader.GetOrdinal("AnswerText")),
            reader.GetInt32(reader.GetOrdinal("Priority")),
            reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
            reader.GetBoolean(reader.GetOrdinal("IsActive")))
        {
            TenantId = reader.GetGuid(reader.GetOrdinal("TenantId")),
            CreatedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")), DateTimeKind.Utc)),
            ModifiedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("ModifiedAtUtc")), DateTimeKind.Utc))
        };
        return new FrequentResponseRecord(response, expressions.Select(item => item.ExpressionText).ToArray());
    }
}
