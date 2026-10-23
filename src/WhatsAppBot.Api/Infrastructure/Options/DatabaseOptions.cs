namespace WhatsAppBot.Api.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string ConnectionStringName = "WhatsAppBot";
    public string WhatsAppBot { get; init; } = string.Empty;
}

public sealed class SqlOptions
{
    public const string SectionName = "Sql";
    public int CommandTimeoutSeconds { get; init; } = 15;
}
