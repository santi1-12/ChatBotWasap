using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class WebhookInboxIdempotencyTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task ReplayedProviderEvent_IsPersistedOnlyOnce()
    {
        var eventKey = $"test:{Guid.NewGuid():N}";
        var phoneNumberId = $"test-{Guid.NewGuid():N}";
        var wabaId = "test-waba";
        var temporaryIntegrationId = Guid.NewGuid();
        var ownsIntegration = false;
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        try
        {
            var activeIntegration = await FindActiveIntegrationAsync(connection);
            if (activeIntegration is null)
            {
                await CreateConnectedTestIntegrationAsync(connection, temporaryIntegrationId, phoneNumberId);
                ownsIntegration = true;
            }
            else
            {
                phoneNumberId = activeIntegration.Value.PhoneNumberId
                    ?? throw new InvalidOperationException("The active test integration has no phone number ID.");
                wabaId = activeIntegration.Value.WabaId
                    ?? throw new InvalidOperationException("The active test integration has no WABA ID.");
            }

            var webhookEvent = new NormalizedMetaEvent(
                eventKey,
                MetaWebhookEventTypes.DeliveryStatus,
                phoneNumberId,
                wabaId,
                null,
                null,
                $"wamid.{Guid.NewGuid():N}",
                null,
                DateTimeOffset.UtcNow,
                "delivered");

            Assert.True(await fixture.WebhookInboxRepository.InsertAsync(webhookEvent, CancellationToken.None));
            Assert.False(await fixture.WebhookInboxRepository.InsertAsync(webhookEvent, CancellationToken.None));
        }
        finally
        {
            await DeleteTestDataAsync(connection, eventKey, temporaryIntegrationId, ownsIntegration);
        }
    }

    private static async Task<(Guid IntegrationId, string? PhoneNumberId, string? WabaId)?> FindActiveIntegrationAsync(
        SqlConnection connection)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) IntegrationId, PhoneNumberId, WabaId FROM dbo.WhatsAppIntegration WHERE IsActive = 1;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? (reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2))
            : null;
    }

    private static async Task CreateConnectedTestIntegrationAsync(
        SqlConnection connection, Guid integrationId, string phoneNumberId)
    {
        await using var command = new SqlCommand(
            """
            DECLARE @TenantId uniqueidentifier =
                (SELECT TOP (1) TenantId FROM dbo.Tenant WHERE IsActive = 1 ORDER BY CreatedAtUtc, TenantId);
            INSERT dbo.WhatsAppIntegration
                (IntegrationId, TenantId, WabaId, PhoneNumberId, BusinessPhoneNumber, ConnectionState, IsActive)
            VALUES (@IntegrationId, @TenantId, N'test-waba', @PhoneNumberId, N'+15550000000', N'Connected', 1);
            INSERT dbo.BotConfiguration (IntegrationId, TenantId, IsBotEnabled, ReplyMode)
            VALUES (@IntegrationId, @TenantId, 0, N'FaqThenAi');
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
            { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@PhoneNumberId", SqlDbType.NVarChar, 64)
            { Value = phoneNumberId });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteTestDataAsync(
        SqlConnection connection, string eventKey, Guid integrationId, bool ownsIntegration)
    {
        await using var command = new SqlCommand(
            """
            DELETE dbo.WebhookInboxEvent WHERE EventKey = @EventKey;
            IF @OwnsIntegration = 1
            BEGIN
                DELETE dbo.BotConfiguration WHERE IntegrationId = @IntegrationId;
                DELETE dbo.WhatsAppIntegration WHERE IntegrationId = @IntegrationId;
            END;
            """,
            connection);
        command.Parameters.Add(new SqlParameter("@EventKey", SqlDbType.NVarChar, 256) { Value = eventKey });
        command.Parameters.Add(new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier)
            { Value = integrationId });
        command.Parameters.Add(new SqlParameter("@OwnsIntegration", SqlDbType.Bit) { Value = ownsIntegration });
        await command.ExecuteNonQueryAsync();
    }
}
