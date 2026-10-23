using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Models.Contracts;

public sealed record FrequentResponseCommand(
    string QuestionOrIntent,
    IReadOnlyList<string> Expressions,
    string AnswerText,
    int Priority,
    string? Category,
    bool IsActive);

public sealed record FrequentResponseRecord(
    FrequentResponse Response,
    IReadOnlyList<string> Expressions);

public sealed record FrequentResponsePage(
    IReadOnlyList<FrequentResponseRecord> Items,
    int Page,
    int PageSize,
    int TotalCount);

public enum BotReplyResolutionKind
{
    FrequentResponse,
    NoReply,
    AiFallbackRequired
}

public sealed record BotReplyResolution(
    BotReplyResolutionKind Kind,
    FrequentResponseRecord? FrequentResponse = null,
    bool FaqEvaluationSkipped = false);

public sealed class BusinessValidationException(string message) : Exception(message);

public sealed class BusinessNotFoundException(string resource) : Exception($"{resource} was not found.");

public sealed class ConversationConflictException(string message = "The conversation changed before the action could be applied.")
    : Exception(message);

public sealed class ManualReplyRejectedException(string safeCode)
    : Exception("The manual reply could not be accepted.")
{
    public string SafeCode { get; } = safeCode;
}

public sealed class ExternalDependencyException(string safeCode, bool isTransient = false)
    : Exception("An external service request could not be completed.")
{
    public string SafeCode { get; } = safeCode;
    public bool IsTransient { get; } = isTransient;
}

public sealed class AiGenerationException(
    string safeCode,
    bool isTransient,
    string handoffReasonCode) : Exception("The AI provider could not generate a safe reply.")
{
    public string SafeCode { get; } = safeCode;
    public bool IsTransient { get; } = isTransient;
    public string HandoffReasonCode { get; } = handoffReasonCode;
}

public static class ReplyModeRules
{
    public static bool IsSupported(BotReplyMode mode) => Enum.IsDefined(mode);
}
