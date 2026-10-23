namespace WhatsAppBot.Api.Infrastructure.Options;

public sealed class MetaOptions
{
    public const string SectionName = "Meta";
    public string? AppId { get; init; }
    public string? AppSecret { get; init; }
    public string? VerifyToken { get; init; }
    public string? EmbeddedSignupConfigId { get; init; }
    public string? CoexistenceEmbeddedSignupConfigId { get; init; }
    public string? GraphApiVersion { get; init; }
    public string? PhoneNumberRegistrationPin { get; init; }
    public int WebhookMaxBodyBytes { get; init; } = 262144;
    public int WorkerBatchSize { get; init; } = 20;

    public string? EffectiveCoexistenceEmbeddedSignupConfigId =>
        string.IsNullOrWhiteSpace(CoexistenceEmbeddedSignupConfigId)
            ? EmbeddedSignupConfigId
            : CoexistenceEmbeddedSignupConfigId;
}

public sealed class AiOptions
{
    public const string SectionName = "OpenAI";
    public string? ApiKey { get; init; }
    public string? TextModel { get; init; }
    public string? ImageModel { get; init; }
    public string? TranscriptionModel { get; init; }
}

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";
    public string? Provider { get; init; }
    public string? PrivateContainer { get; init; }
}

public sealed class ApiOptions
{
    public const string SectionName = "Api";
    public int RequestTimeoutSeconds { get; init; } = 30;
}
