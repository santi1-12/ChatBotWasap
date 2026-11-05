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

public static class AiProviderNames
{
    public const string OpenAI = "OpenAI";
    public const string GitHubCopilot = "GitHubCopilot";

    public static bool IsSupported(string? provider) =>
        string.Equals(provider, OpenAI, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, GitHubCopilot, StringComparison.OrdinalIgnoreCase);
}

public sealed class AiOptions
{
    public const string SectionName = "AI";
    public string Provider { get; init; } = AiProviderNames.OpenAI;
    public int RequestTimeoutSeconds { get; init; } = 60;
    public int MaxInputCharacters { get; init; } = 12000;
    public int MaxOutputCharacters { get; init; } = 8000;
}

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public string ApiKeyReference { get; init; } = "OpenAI:ApiKey";
    public string Endpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
    public string TranscriptionEndpoint { get; init; } = "https://api.openai.com/v1/audio/transcriptions";
    public string? TextModel { get; init; }
    public string? ImageModel { get; init; }
    public string? TranscriptionModel { get; init; }
    public int MaxOutputTokens { get; init; } = 1000;
    public long MaxAudioBytes { get; init; } = 25_000_000;
    public long MaxImageBytes { get; init; } = 10_000_000;
}

public sealed class GitHubCopilotOptions
{
    public const string SectionName = "AI:GitHubCopilot";
    public string Model { get; init; } = "auto";
    public bool UseLoggedInUser { get; init; } = true;
}

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";
    public const string LocalPrivateProvider = "LocalPrivate";
    public string Provider { get; init; } = LocalPrivateProvider;
    public string? PrivateContainer { get; init; }
    public int RequestTimeoutSeconds { get; init; } = 30;
    public long MaxFileBytes { get; init; } = 25_000_000;
    public int RetentionDays { get; init; } = 30;
    public bool EnableImageAnalysis { get; init; }
    public int CleanupBatchSize { get; init; } = 50;
    public int CleanupIntervalMinutes { get; init; } = 60;
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
