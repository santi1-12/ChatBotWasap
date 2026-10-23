using System.Data;
using Microsoft.Data.SqlClient;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class WhatsAppIntegrationPersistenceTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task ActivePhoneNumberUniqueness_IsScopedToTenant()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            SELECT columns.name, indexes.filter_definition
            FROM sys.indexes AS indexes
            INNER JOIN sys.index_columns AS index_columns
                ON index_columns.object_id = indexes.object_id
               AND index_columns.index_id = indexes.index_id
            INNER JOIN sys.columns AS columns
                ON columns.object_id = index_columns.object_id
               AND columns.column_id = index_columns.column_id
            WHERE indexes.object_id = OBJECT_ID(N'dbo.WhatsAppIntegration')
              AND indexes.name = N'UX_WhatsAppIntegration_Tenant_PhoneNumberId_Active'
              AND index_columns.is_included_column = 0
            ORDER BY index_columns.key_ordinal;
            """,
            connection);
        var keyColumns = new List<string>();
        string? filter = null;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                keyColumns.Add(reader.GetString(0));
                filter ??= reader.IsDBNull(1) ? null : reader.GetString(1);
            }
        }

        Assert.NotNull(filter);
        Assert.Contains("IsActive", filter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PhoneNumberId", filter, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["TenantId", "PhoneNumberId"], keyColumns);
    }

    [Fact]
    public async Task SignupResultPersistsTenantModalityAndEffectiveConfiguration()
    {
        var tenantId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await ExecuteAsync(
                connection,
                "INSERT dbo.Tenant (TenantId, LegalName, IsActive) VALUES (@TenantId, N'Integration persistence test', 1); " +
                "INSERT dbo.WhatsAppIntegration (IntegrationId, TenantId, ConnectionState, IsActive) " +
                "VALUES (@IntegrationId, @TenantId, N'NotConnected', 0);",
                ("@TenantId", SqlDbType.UniqueIdentifier, tenantId),
                ("@IntegrationId", SqlDbType.UniqueIdentifier, integrationId));

            await ExecuteStoredProcedureAsync(
                connection,
                "dbo.sp_WhatsAppIntegration_SaveSignupResult",
                ("@IntegrationId", SqlDbType.UniqueIdentifier, integrationId),
                ("@TenantId", SqlDbType.UniqueIdentifier, tenantId),
                ("@WabaId", SqlDbType.NVarChar, "waba-persisted"),
                ("@PhoneNumberId", SqlDbType.NVarChar, "phone-persisted"),
                ("@BusinessPhoneNumber", SqlDbType.NVarChar, "+15550000000"),
                ("@DisplayName", SqlDbType.NVarChar, "Persistence test"),
                ("@OnboardingType", SqlDbType.NVarChar, "Coexistence"),
                ("@EffectiveEmbeddedSignupConfigId", SqlDbType.NVarChar, "coexistence-config"),
                ("@AccessTokenSecretRef", SqlDbType.NVarChar, "Meta:BusinessAccessTokens:test"),
                ("@MetaAppSecretRef", SqlDbType.NVarChar, "Meta:AppSecret"),
                ("@WebhookVerifyTokenRef", SqlDbType.NVarChar, "Meta:VerifyToken"));

            await using var command = new SqlCommand(
                """
                SELECT TenantId, OnboardingType, EffectiveEmbeddedSignupConfigId,
                       WabaId, PhoneNumberId, ConnectionState, IsActive
                FROM dbo.WhatsAppIntegration
                WHERE IntegrationId = @IntegrationId;
                """,
                connection);
            command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
                { Value = integrationId });
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(tenantId, reader.GetGuid(reader.GetOrdinal("TenantId")));
            Assert.Equal("Coexistence", reader.GetString(reader.GetOrdinal("OnboardingType")));
            Assert.Equal("coexistence-config", reader.GetString(reader.GetOrdinal("EffectiveEmbeddedSignupConfigId")));
            Assert.Equal("waba-persisted", reader.GetString(reader.GetOrdinal("WabaId")));
            Assert.Equal("phone-persisted", reader.GetString(reader.GetOrdinal("PhoneNumberId")));
            Assert.Equal("Connected", reader.GetString(reader.GetOrdinal("ConnectionState")));
            Assert.True(reader.GetBoolean(reader.GetOrdinal("IsActive")));
        }
        finally
        {
            await ExecuteAsync(
                connection,
                "DELETE dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId; DELETE dbo.Tenant WHERE TenantId = @TenantId;",
                ("@TenantId", SqlDbType.UniqueIdentifier, tenantId),
                ("@IntegrationId", SqlDbType.UniqueIdentifier, integrationId));
        }
    }

    private static async Task ExecuteStoredProcedureAsync(
        SqlConnection connection,
        string procedure,
        params (string Name, SqlDbType Type, object Value)[] parameters)
    {
        await using var command = new SqlCommand(procedure, connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddParameters(command, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        params (string Name, SqlDbType Type, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(
        SqlCommand command,
        IEnumerable<(string Name, SqlDbType Type, object Value)> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.Name, parameter.Type) { Value = parameter.Value });
        }
    }
}
