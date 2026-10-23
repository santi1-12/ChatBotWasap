using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Shared.DTOs;

public sealed class WhatsAppIntegrationDTO
{
    public string? WabaId { get; init; }
    public string? PhoneNumberId { get; init; }
    public string? BusinessPhoneNumber { get; init; }
    public string? DisplayName { get; init; }
    public required ConnectionState ConnectionState { get; init; }
    public required bool BotEnabled { get; init; }
    public DateTimeOffset? ConnectedAtUtc { get; init; }
    public string? ConnectionErrorCode { get; init; }
    public bool HasCredentialReference { get; init; }
    public string? MetaAppId { get; init; }
    public string? EmbeddedSignupConfigId { get; init; }
    public string? CoexistenceEmbeddedSignupConfigId { get; init; }
    public string? MetaGraphApiVersion { get; init; }
}

public sealed class EmbeddedSignupCompletionRequestDTO
{
    [Required, MaxLength(2048)]
    public string AuthorizationCode { get; init; } = string.Empty;

    [Required, MaxLength(64)]
    public string WabaId { get; init; } = string.Empty;

    [MaxLength(64)]
    public string? PhoneNumberId { get; init; }

    [Required, EnumDataType(typeof(WhatsAppOnboardingType))]
    public WhatsAppOnboardingType? OnboardingType { get; init; }
}

public sealed class BotConfigurationDTO
{
    public required bool IsBotEnabled { get; init; }
    public required BotReplyMode ReplyMode { get; init; }
}

public sealed class UpdateBotConfigurationRequestDTO
{
    [Required]
    public bool? IsBotEnabled { get; init; }

    [Required, EnumDataType(typeof(BotReplyMode))]
    public BotReplyMode? ReplyMode { get; init; }
}

public class FrequentResponseRequestDTO : IValidatableObject
{
    [Required, MaxLength(500)]
    public string QuestionOrIntent { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public IReadOnlyList<string>? Expressions { get; init; } = [];

    [Required, MinLength(1)]
    public string AnswerText { get; init; } = string.Empty;

    [Range(0, 1000)]
    public int Priority { get; init; }

    [MaxLength(100)]
    public string? Category { get; init; }

    [Required]
    public bool? IsActive { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Expressions is null || Expressions.Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "Expressions must contain non-empty values.",
                [nameof(Expressions)]);
        }
    }
}

public sealed class FrequentResponseDTO
{
    public long FrequentResponseId { get; init; }
    public string QuestionOrIntent { get; init; } = string.Empty;
    public IReadOnlyList<string> Expressions { get; init; } = [];
    public string AnswerText { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string? Category { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset ModifiedAtUtc { get; init; }
}

public sealed class MessageDTO
{
    public long MessageId { get; init; }
    public required MessageDirection Direction { get; init; }
    public required MessageType MessageType { get; init; }
    public string? Text { get; init; }
    public string? Transcription { get; init; }
    public bool MediaAvailable { get; init; }
    public string? MediaUrl { get; init; }
    public ReplySource? ReplySource { get; init; }
    public required ProcessingState ProcessingState { get; init; }
    public DeliveryState? DeliveryState { get; init; }
    public string? OutcomeCode { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class ConversationSummaryDTO
{
    public long ConversationId { get; init; }
    public required string Contact { get; init; }
    public DateTimeOffset LastMessageAtUtc { get; init; }
    public required ConversationStatus Status { get; init; }
    public string? LastMessagePreview { get; init; }
}

public sealed class ConversationDetailDTO
{
    public required ConversationSummaryDTO Conversation { get; init; }
    public IReadOnlyList<MessageDTO> Messages { get; init; } = [];
}

public sealed class PagedResultDTO<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}
