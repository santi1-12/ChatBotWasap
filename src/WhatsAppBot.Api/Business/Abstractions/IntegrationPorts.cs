using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface ISecretReferenceResolver
{
    ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken);
}

public interface IMetaWhatsAppClient
{
    Task<MetaMessageSendResult> SendTextAsync(
        string phoneNumberId, string recipient, string text, CancellationToken cancellationToken);
}

public interface ISecretStore : ISecretReferenceResolver
{
    Task<string> StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken);
    Task DeleteAsync(string secretReference, CancellationToken cancellationToken);
}

public interface IMetaEmbeddedSignupAdapter
{
    Task<MetaSignupResult> CompleteAsync(
        string authorizationCode,
        string wabaId,
        string? phoneNumberId,
        WhatsAppOnboardingType onboardingType,
        CancellationToken cancellationToken);
}

public interface IMetaWebhookSignatureValidator
{
    bool IsValid(ReadOnlySpan<byte> payload, string? signature, string appSecret);
}

public interface IMetaWebhookChallengeValidator
{
    bool IsValid(string mode, string suppliedToken, string expectedToken);
}

public interface IMetaWebhookEventParser
{
    IReadOnlyList<NormalizedMetaEvent> Parse(ReadOnlyMemory<byte> payload);
}

public interface IWhatsAppIntegrationRepository
{
    Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken);
    Task SaveSignupResultAsync(
        MetaSignupResult result, CancellationToken cancellationToken);
    Task SetConnectionStateAsync(
        Guid integrationId, ConnectionState state, string? safeErrorCode, CancellationToken cancellationToken);
    Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken);
}

public interface IWhatsAppIntegrationService
{
    Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken);
    Task<WhatsAppIntegration> CompleteSignupAsync(
        string authorizationCode,
        string wabaId,
        string? phoneNumberId,
        WhatsAppOnboardingType onboardingType,
        CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}

public interface IWebhookInboxRepository
{
    Task<bool> InsertAsync(NormalizedMetaEvent webhookEvent, CancellationToken cancellationToken);
    Task<IReadOnlyList<NormalizedMetaEvent>> ClaimBatchAsync(
        int batchSize, CancellationToken cancellationToken);
    Task SetStateAsync(string eventKey, WebhookInboxState state, string? safeFailureCode,
        CancellationToken cancellationToken);
}

public interface IContactRepository
{
    Task<long> UpsertWhatsAppAsync(
        Guid integrationId, string whatsappUserId, string? displayName, CancellationToken cancellationToken);
}

public interface IConversationRepository
{
    Task<long> GetOrCreateAsync(
        Guid integrationId,
        Guid tenantId,
        long contactId,
        DateTimeOffset lastMessageAtUtc,
        bool isBotEnabled,
        CancellationToken cancellationToken);
    Task<ConversationPage> ListAsync(
        Guid integrationId, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IMessageRepository
{
    Task<ConversationDetailRecord?> GetHistoryAsync(
        Guid integrationId,
        long conversationId,
        long? beforeMessageId,
        int pageSize,
        CancellationToken cancellationToken);
    Task<InboundMessageInsertResult> InsertInboundAsync(
        Guid integrationId, long conversationId, NormalizedMetaEvent inboundEvent,
        CancellationToken cancellationToken);
    Task SetProcessingOutcomeAsync(
        long messageId, ProcessingState state, string? safeOutcomeCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboundMessageWorkItem>> ClaimOutboxBatchAsync(
        int batchSize, CancellationToken cancellationToken);
    Task<OutboxRevalidationResult> RevalidateOutboxAsync(
        OutboundMessageWorkItem item, CancellationToken cancellationToken);
    Task SetOutboxResultAsync(
        long messageId, MetaMessageSendResult result, bool isTerminal, CancellationToken cancellationToken);
    Task SetOutboxStateAsync(
        long messageId, MessageOutboxState state, int retryDelaySeconds, CancellationToken cancellationToken);
}

public interface IWhatsAppMessagingPolicy
{
    MessagingPolicyDecision Evaluate(DateTimeOffset lastCustomerMessageAtUtc, DateTimeOffset nowUtc);
}

public interface IMessageOutboxRepository
{
    Task<OutboundMessageInsertResult> CreatePendingAsync(
        long inboundMessageId, Guid integrationId, long conversationId, string text,
        ReplySource source, long? frequentResponseId,
        CancellationToken cancellationToken);
}

public interface IProcessInboundText
{
    Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken);
}

public interface IProcessInboundMedia
{
    Task ProcessAsync(NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken);
}

public interface IProcessMetaDeliveryStatus
{
    Task ProcessAsync(NormalizedMetaEvent statusEvent, CancellationToken cancellationToken);
}

public interface IAcceptMetaWebhookEvent
{
    Task AcceptAsync(IReadOnlyList<NormalizedMetaEvent> events, CancellationToken cancellationToken);
}

public interface IMetaCloudApiClient
{
    Task<string> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken);
    Task<MetaPhoneNumberDetails> GetPhoneNumberAsync(
        string accessToken, string phoneNumberId, CancellationToken cancellationToken);
    Task<bool> PhoneNumberBelongsToWabaAsync(
        string accessToken, string wabaId, string phoneNumberId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MetaPhoneNumberSummary>> GetPhoneNumbersAsync(
        string accessToken, string wabaId, CancellationToken cancellationToken);
    Task RegisterPhoneNumberAsync(
        string accessToken, string phoneNumberId, CancellationToken cancellationToken);
    Task SubscribeWabaAsync(string accessToken, string wabaId, CancellationToken cancellationToken);
    Task<MetaMessageSendResult> SendTextAsync(
        string accessToken, string phoneNumberId, string recipient, string text,
        CancellationToken cancellationToken);
}

public interface IAiReplyGenerator
{
    Task<string> GenerateReplyAsync(string prompt, CancellationToken cancellationToken);
}

public interface IImageAnalysisService
{
    Task<string> AnalyzeAsync(Stream image, string contentType, CancellationToken cancellationToken);
}

public interface IAudioTranscriptionService
{
    Task<string> TranscribeAsync(Stream audio, string contentType, CancellationToken cancellationToken);
}

public interface IMediaStorage
{
    Task<string> StorePrivateAsync(Stream content, string contentType, CancellationToken cancellationToken);
}
