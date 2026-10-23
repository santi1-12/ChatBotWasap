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
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int WebhookMaxBodyBytes { get; init; } = 262144;
    public int WorkerBatchSize { get; init; } = 20;
    public int MaxPhoneNumberPages { get; init; } = 10;

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
    public int RequestTimeoutSeconds { get; init; } = 60;
    public int MaxInputCharacters { get; init; } = 12000;
    public int MaxOutputTokens { get; init; } = 1000;
    public long MaxAudioBytes { get; init; } = 25_000_000;
    public long MaxImageBytes { get; init; } = 10_000_000;
}

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";
    public string? Provider { get; init; }
    public string? PrivateContainer { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 30;
    public long MaxFileBytes { get; init; } = 25_000_000;
    public int RetentionDays { get; init; } = 30;
}

public sealed class FfmpegOptions
{
    public const string SectionName = "FFmpeg";
    public string? ExecutablePath { get; init; }
    public int TimeoutSeconds { get; init; } = 60;
    public long MaxInputBytes { get; init; } = 25_000_000;
    public long MaxOutputBytes { get; init; } = 50_000_000;
    public int MaxDurationSeconds { get; init; } = 300;
}

public sealed class WorkerOptions
{
    public const string SectionName = "Workers";
    public int PollIntervalSeconds { get; init; } = 1;
    public int ItemTimeoutSeconds { get; init; } = 120;
    public int MaximumAttempts { get; init; } = 5;
    public int RetryBaseDelaySeconds { get; init; } = 5;
}

public sealed class ApiOptions
{
    public const string SectionName = "Api";
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int MaxRequestBodyBytes { get; init; } = 1_048_576;
    public int CompletionFailureStateTimeoutSeconds { get; init; } = 5;
}
