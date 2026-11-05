namespace WhatsAppBot.Admin.Configuration;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string BaseUrl { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 30;
}
