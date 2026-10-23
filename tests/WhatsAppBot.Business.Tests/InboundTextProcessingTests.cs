using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Business.Tests;

public sealed class InboundTextProcessingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-29T12:00:00Z");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task FaqOnlyMatch_PersistsInboundThenCreatesOutboxCandidate()
    {
        var fixture = new Fixture(BotReplyMode.FaqOnly, hasMatch: true, hoursAgo: 1);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Messages.InboundPersisted);
        Assert.Equal(1, fixture.Outbox.CreateCalls);
        Assert.Null(fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task FaqOnlyMiss_PersistsInboundWithoutAiOrOutbox()
    {
        var fixture = new Fixture(BotReplyMode.FaqOnly, hasMatch: false, hoursAgo: 1);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Messages.InboundPersisted);
        Assert.Equal(1, fixture.FaqRepository.CandidateQueries);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.NoFrequentResponse, fixture.Messages.OutcomeCode);
    }

    [Theory]
    [InlineData(BotReplyMode.FaqThenAi)]
    [InlineData(BotReplyMode.AiOnly)]
    public async Task AiModesWithoutProvider_DoNotInventReplyOrPersistOutbox(BotReplyMode mode)
    {
        var fixture = new Fixture(mode, hasMatch: false, hoursAgo: 1);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Messages.InboundPersisted);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.AiFallbackNotConfigured, fixture.Messages.OutcomeCode);
        Assert.Equal(mode == BotReplyMode.AiOnly ? 0 : 1, fixture.FaqRepository.CandidateQueries);
    }

    [Fact]
    public async Task AiOnly_SkipsFaqEvenWhenCandidateExists()
    {
        var fixture = new Fixture(BotReplyMode.AiOnly, hasMatch: true, hoursAgo: 1);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(0, fixture.FaqRepository.CandidateQueries);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.AiFallbackNotConfigured, fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task ExpiredCustomerWindow_PersistsInboundButNeverCreatesOutbox()
    {
        var fixture = new Fixture(BotReplyMode.FaqOnly, hasMatch: true, hoursAgo: 25);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Messages.InboundPersisted);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.MessagingWindowClosed, fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task DisabledBot_PersistsInboundWithoutEvaluatingFaq()
    {
        var fixture = new Fixture(BotReplyMode.FaqOnly, hasMatch: true, hoursAgo: 1, botEnabled: false);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Messages.InboundPersisted);
        Assert.Equal(0, fixture.FaqRepository.CandidateQueries);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.BotDisabled, fixture.Messages.OutcomeCode);
    }

    [Fact]
    public void MessagingPolicy_BlocksAtAndAfterTheTwentyFourHourBoundary()
    {
        var policy = new WhatsAppMessagingPolicy();
        Assert.True(policy.Evaluate(Now.AddHours(-23), Now).IsAllowed);
        Assert.False(policy.Evaluate(Now.AddHours(-24), Now).IsAllowed);
        Assert.False(policy.Evaluate(Now.AddHours(1), Now).IsAllowed);
    }

    private static NormalizedMetaEvent InboundEvent(DateTimeOffset customerMessageTime) => new(
        "message:phone-1:wamid.inbound-1",
        MetaWebhookEventTypes.InboundText,
        "phone-1",
        "waba-1",
        "15551234567",
        "Customer",
        "wamid.inbound-1",
        "business hours",
        customerMessageTime,
        null);

    private sealed class Fixture
    {
        public Fixture(BotReplyMode mode, bool hasMatch, int hoursAgo, bool botEnabled = true)
        {
            var integrationId = Guid.NewGuid();
            Configuration = new FakeBotConfigurationService(new BotConfiguration(integrationId, botEnabled, mode));
            Integration = new FakeIntegrationRepository(new WhatsAppIntegration(
                integrationId, "waba-1", "phone-1", "+15551234567", "Test", ConnectionState.Connected,
                DateTimeOffset.UtcNow, true)
            {
                TenantId = TenantId
            });
            Contacts = new FakeContactRepository();
            Conversations = new FakeConversationRepository();
            Messages = new FakeMessageRepository();
            Outbox = new FakeOutboxRepository();
            FaqRepository = new FakeFrequentResponseRepository(hasMatch
                ? [new FrequentResponseRecord(
                    new FrequentResponse(7, integrationId, "hours", "We are open 9 to 5", 10, null, true),
                    ["business hours"])]
                : []);
            var resolver = new BotReplyResolver(FaqRepository, new FrequentResponseMatcher());
            Processor = new ProcessInboundText(
                Integration, Contacts, Conversations, Messages, Configuration, resolver, Outbox,
                new WhatsAppMessagingPolicy(), new FakeClock(Now), new FakeHandoffService());
            Inbound = InboundEvent(Now.AddHours(-hoursAgo));
        }

        public FakeIntegrationRepository Integration { get; }
        public FakeContactRepository Contacts { get; }
        public FakeConversationRepository Conversations { get; }
        public FakeMessageRepository Messages { get; }
        public FakeOutboxRepository Outbox { get; }
        public FakeFrequentResponseRepository FaqRepository { get; }
        public FakeBotConfigurationService Configuration { get; }
        public ProcessInboundText Processor { get; }
        public NormalizedMetaEvent Inbound { get; }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeIntegrationRepository(WhatsAppIntegration integration)
        : IWhatsAppIntegrationRepository
    {
        public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(integration);
        public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task SetConnectionStateAsync(
            Guid integrationId, ConnectionState state, string? safeErrorCode, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeContactRepository : IContactRepository
    {
        public Task<long> UpsertWhatsAppAsync(
            Guid integrationId, string whatsappUserId, string? displayName, CancellationToken cancellationToken) =>
            Task.FromResult(5L);
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public Task<long> GetOrCreateAsync(
            Guid integrationId, Guid tenantId, long contactId, DateTimeOffset lastMessageAtUtc,
            bool isBotEnabled,
            CancellationToken cancellationToken) => Task.FromResult(6L);

        public Task<ConversationPage> ListAsync(
            Guid integrationId, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeHandoffService : IConversationHandoffService
    {
        public Task<bool> TryHandoffAsync(
            Guid tenantId, long conversationId, string reasonCode,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public int InboundPersisted { get; private set; }
        public string? OutcomeCode { get; private set; }

        public Task<ConversationDetailRecord?> GetHistoryAsync(
            Guid integrationId,
            long conversationId,
            long? beforeMessageId,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<InboundMessageInsertResult> InsertInboundAsync(
            Guid integrationId, long conversationId, NormalizedMetaEvent inboundEvent,
            CancellationToken cancellationToken)
        {
            InboundPersisted++;
            return Task.FromResult(new InboundMessageInsertResult(true, 9));
        }

        public Task SetProcessingOutcomeAsync(
            long messageId, ProcessingState state, string? safeOutcomeCode, CancellationToken cancellationToken)
        {
            OutcomeCode = safeOutcomeCode;
            return Task.CompletedTask;
        }

        public Task<DeliveryStatusApplyResult> ApplyDeliveryStatusAsync(
            NormalizedMetaEvent statusEvent, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<OutboundMessageWorkItem>> ClaimOutboxBatchAsync(
            int batchSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<OutboxRevalidationResult> RevalidateOutboxAsync(
            OutboundMessageWorkItem item, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task SetOutboxResultAsync(
            long messageId, MetaMessageSendResult result, bool isTerminal, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task SetOutboxStateAsync(
            long messageId, MessageOutboxState state, int retryDelaySeconds, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeOutboxRepository : IMessageOutboxRepository
    {
        public int CreateCalls { get; private set; }
        public Task<OutboundMessageInsertResult> CreatePendingAsync(
            long inboundMessageId, Guid integrationId, long conversationId, string text,
            ReplySource source, long? frequentResponseId, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(new OutboundMessageInsertResult(true, 10, null));
        }

        public Task<ManualMessageInsertResult> CreateAdminPendingAsync(
            Guid tenantId, long conversationId, long adminId, string text,
            string expectedVersion, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeBotConfigurationService(BotConfiguration configuration) : IBotConfigurationService
    {
        public Task<BotConfiguration> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(configuration);
        public Task<BotConfiguration> UpdateAsync(
            bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeFrequentResponseRepository(
        IReadOnlyList<FrequentResponseRecord> candidates) : IFrequentResponseRepository
    {
        public int CandidateQueries { get; private set; }

        public Task<IReadOnlyList<FrequentResponseRecord>> ListActiveCandidatesAsync(
            Guid integrationId, CancellationToken cancellationToken)
        {
            CandidateQueries++;
            return Task.FromResult(candidates);
        }
        public Task<FrequentResponsePage> ListAsync(
            Guid integrationId, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<FrequentResponseRecord?> GetAsync(
            Guid integrationId, long frequentResponseId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<FrequentResponseRecord> CreateAsync(
            Guid integrationId, FrequentResponseCommand command, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<FrequentResponseRecord?> UpdateAsync(
            Guid integrationId, long frequentResponseId, FrequentResponseCommand command,
            CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> SetActiveAsync(
            Guid integrationId, long frequentResponseId, bool isActive, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<bool> DeleteAsync(
            Guid integrationId, long frequentResponseId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
