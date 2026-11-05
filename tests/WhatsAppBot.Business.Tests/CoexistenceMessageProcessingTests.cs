using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Business.Tests;

public sealed class CoexistenceMessageProcessingTests
{
    [Fact]
    public async Task HistoricalMessage_IsPersistedWithoutBotWork()
    {
        var fixture = new Fixture(historyRequested: true);

        var result = await fixture.Processor.ProcessAsync(
            fixture.CreateEvent(MetaWebhookEventTypes.HistoricalMessage), CancellationToken.None);

        Assert.Equal(HistoryImportOutcomes.Imported, result.HistoryOutcome);
        Assert.False(fixture.Conversations.HumanActivity);
        Assert.True(fixture.Messages.LastEvent?.IsHistorical == true);
        Assert.Equal(Origin.MetaCloudApi, fixture.Messages.LastEvent?.Origin);
        Assert.Equal(0, fixture.Messages.OutboxCalls);
    }

    [Fact]
    public async Task AppEcho_IsPersistedAsOutboundHumanActivity()
    {
        var fixture = new Fixture(historyRequested: false);

        var result = await fixture.Processor.ProcessAsync(
            fixture.CreateEvent(MetaWebhookEventTypes.OutboundAppMessage), CancellationToken.None);

        Assert.Null(result.HistoryOutcome);
        Assert.True(fixture.Conversations.HumanActivity);
        Assert.Equal(MessageDirection.Outbound, fixture.Messages.LastEvent?.Direction);
        Assert.Equal(ActorType.Admin, fixture.Messages.LastEvent?.ActorType);
    }

    [Fact]
    public async Task DuplicateHistoricalMessage_ReportsDuplicateWithoutSecondInsert()
    {
        var fixture = new Fixture(historyRequested: true)
        {
            InsertResult = new InboundMessageInsertResult(false, 42)
        };

        var result = await fixture.Processor.ProcessAsync(
            fixture.CreateEvent(MetaWebhookEventTypes.HistoricalMessage), CancellationToken.None);

        Assert.Equal(HistoryImportOutcomes.Duplicate, result.HistoryOutcome);
        Assert.Equal(1, fixture.Messages.InsertCalls);
    }

    [Fact]
    public async Task HistoricalMessageWithoutConsent_IsSkippedBeforePersistence()
    {
        var fixture = new Fixture(historyRequested: false);

        var result = await fixture.Processor.ProcessAsync(
            fixture.CreateEvent(MetaWebhookEventTypes.HistoricalMessage), CancellationToken.None);

        Assert.Equal(HistoryImportOutcomes.Skipped, result.HistoryOutcome);
        Assert.Equal(0, fixture.Contacts.Calls);
        Assert.Equal(0, fixture.Messages.InsertCalls);
    }

    private sealed class Fixture
    {
        private readonly Guid tenantId = Guid.NewGuid();
        private readonly Guid integrationId = Guid.NewGuid();
        private readonly WhatsAppIntegration integration;

        public Fixture(bool historyRequested)
        {
            integration = new WhatsAppIntegration(
                integrationId,
                "waba-1",
                "phone-1",
                "+15550000000",
                "Test",
                ConnectionState.Connected,
                DateTimeOffset.UtcNow,
                true)
            {
                TenantId = tenantId,
                OnboardingType = WhatsAppOnboardingType.Coexistence,
                HistoryImportRequested = historyRequested
            };
            Contacts = new FakeContactRepository();
            Conversations = new FakeConversationRepository();
            Messages = new FakeMessageRepository();
            Processor = new ProcessCoexistenceMessage(
                new FakeIntegrationRepository(integration),
                Contacts,
                Conversations,
                Messages,
                new FakeBotConfigurationService());
        }

        public ProcessCoexistenceMessage Processor { get; }
        public FakeContactRepository Contacts { get; }
        public FakeConversationRepository Conversations { get; }
        public FakeMessageRepository Messages { get; }
        public InboundMessageInsertResult InsertResult
        {
            get => Messages.Result;
            init => Messages.Result = value;
        }

        public NormalizedMetaEvent CreateEvent(string eventType) => new(
            $"event:{eventType}",
            eventType,
            "phone-1",
            "waba-1",
            "15551234567",
            "Customer",
            "wamid-1",
            "Hello",
            DateTimeOffset.Parse("2026-10-22T12:00:00Z"),
            null)
        {
            TenantId = tenantId,
            IntegrationId = integrationId,
            SourceField = eventType == MetaWebhookEventTypes.HistoricalMessage
                ? "history"
                : "smb_message_echoes",
            SourceEventKey = $"source:{eventType}",
            IsHistorical = eventType == MetaWebhookEventTypes.HistoricalMessage,
            HistoryImportKey = eventType == MetaWebhookEventTypes.HistoricalMessage ? "history-1" : null,
            Direction = eventType == MetaWebhookEventTypes.OutboundAppMessage
                ? MessageDirection.Outbound
                : MessageDirection.Inbound,
            ActorType = eventType == MetaWebhookEventTypes.OutboundAppMessage
                ? ActorType.Admin
                : ActorType.Customer,
            Origin = eventType == MetaWebhookEventTypes.OutboundAppMessage
                ? Origin.FutureWhatsAppBusinessApp
                : Origin.MetaCloudApi,
            MessageType = MessageType.Text
        };
    }

    private sealed class FakeIntegrationRepository(WhatsAppIntegration integration)
        : IWhatsAppIntegrationRepository
    {
        public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(integration);

        public Task<WhatsAppIntegration> GetByPhoneNumberIdAsync(
            Guid tenantId, Guid integrationId, string phoneNumberId,
            CancellationToken cancellationToken) => Task.FromResult(integration);

        public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<HistoryImportStatusRecord?> GetLatestHistoryImportAsync(
            Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
            Task.FromResult<HistoryImportStatusRecord?>(null);

        public Task SetConnectionStateAsync(
            Guid integrationId, ConnectionState state, string? safeErrorCode,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeContactRepository : IContactRepository
    {
        public int Calls { get; private set; }

        public Task<long> UpsertWhatsAppAsync(
            Guid integrationId, Guid tenantId, string whatsappUserId, string? displayName,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(10L);
        }
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public bool HumanActivity { get; private set; }

        public Task<long> GetOrCreateAsync(
            Guid integrationId, Guid tenantId, long contactId, DateTimeOffset lastMessageAtUtc,
            bool isBotEnabled, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> GetOrCreateCoexistenceAsync(
            Guid integrationId, Guid tenantId, long contactId, DateTimeOffset lastMessageAtUtc,
            bool isBotEnabled, bool humanActivity, CancellationToken cancellationToken)
        {
            HumanActivity = humanActivity;
            return Task.FromResult(20L);
        }

        public Task<ConversationPage> ListAsync(
            Guid tenantId, Guid integrationId, int page, int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public InboundMessageInsertResult Result { get; set; } = new(true, 30);
        public int InsertCalls { get; private set; }
        public NormalizedMetaEvent? LastEvent { get; private set; }
        public int OutboxCalls { get; private set; }

        public Task<ConversationDetailRecord?> GetHistoryAsync(
            Guid tenantId, Guid integrationId, long conversationId, long? beforeMessageId,
            int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<InboundMessageInsertResult> InsertInboundAsync(
            Guid tenantId, Guid integrationId, long conversationId, NormalizedMetaEvent inboundEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<InboundMessageInsertResult> InsertCoexistenceAsync(
            Guid tenantId, Guid integrationId, long conversationId, NormalizedMetaEvent coexistenceEvent,
            CancellationToken cancellationToken)
        {
            InsertCalls++;
            LastEvent = coexistenceEvent;
            return Task.FromResult(Result);
        }

        public Task SetProcessingOutcomeAsync(
            Guid tenantId, long messageId, ProcessingState state, string? safeOutcomeCode,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DeliveryStatusApplyResult> ApplyDeliveryStatusAsync(
            Guid tenantId, Guid integrationId, NormalizedMetaEvent statusEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<OutboundMessageWorkItem>> ClaimOutboxBatchAsync(
            int batchSize, CancellationToken cancellationToken)
        {
            OutboxCalls++;
            return Task.FromResult<IReadOnlyList<OutboundMessageWorkItem>>([]);
        }

        public Task<OutboxRevalidationResult> RevalidateOutboxAsync(
            OutboundMessageWorkItem item, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetOutboxResultAsync(
            Guid tenantId, long messageId, MetaMessageSendResult result, bool isTerminal,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetOutboxStateAsync(
            Guid tenantId, long messageId, MessageOutboxState state, int retryDelaySeconds,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeBotConfigurationService : IBotConfigurationService
    {
        public Task<BotConfiguration> GetAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<BotConfiguration> GetForIntegrationAsync(
            Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
            Task.FromResult(new BotConfiguration(integrationId, true, BotReplyMode.FaqOnly));

        public Task<BotConfiguration> UpdateAsync(
            bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
