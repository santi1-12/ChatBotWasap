using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Infrastructure.Database;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace WhatsAppBot.Persistence.Tests;

public sealed class LocalDbFixture
{
    public LocalDbFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("WHATSAPPBOT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set WHATSAPPBOT_TEST_CONNECTION_STRING explicitly to the Development LocalDB test database.");
        }

        var parsed = new SqlConnectionStringBuilder(connectionString);
        if (!parsed.DataSource.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(parsed.InitialCatalog, "WhatsAppBot", StringComparison.OrdinalIgnoreCase) ||
            !parsed.IntegratedSecurity)
        {
            throw new InvalidOperationException(
                "Persistence tests are restricted to Windows-authenticated Development LocalDB database WhatsAppBot.");
        }

        ConnectionString = connectionString;
        Executor = new StoredProcedureExecutor(
            new SqlConnectionFactory(connectionString), Options.Create(new SqlOptions()));
        ConfigurationRepository = new BotConfigurationRepository(Executor);
        FrequentResponseRepository = new FrequentResponseRepository(Executor);
        WebhookInboxRepository = new WebhookInboxRepository(Executor);
        OutboxRepository = new MessageOutboxRepository(Executor);
    }

    public string ConnectionString { get; }
    public StoredProcedureExecutor Executor { get; }
    public BotConfigurationRepository ConfigurationRepository { get; }
    public FrequentResponseRepository FrequentResponseRepository { get; }
    public WebhookInboxRepository WebhookInboxRepository { get; }
    public MessageOutboxRepository OutboxRepository { get; }
}

[CollectionDefinition(Name)]
public sealed class LocalDbCollection : ICollectionFixture<LocalDbFixture>
{
    public const string Name = "Development LocalDB";
}
