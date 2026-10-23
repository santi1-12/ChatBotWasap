using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Api.Infrastructure.Options;
using Microsoft.Extensions.Options;
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
        Assert.Equal(MessageOutcomeCodes.AiConfigurationMissing, fixture.Messages.OutcomeCode);
        Assert.Equal(mode == BotReplyMode.AiOnly ? 0 : 1, fixture.FaqRepository.CandidateQueries);
    }

    [Fact]
    public async Task AiOnly_SkipsFaqEvenWhenCandidateExists()
    {
        var fixture = new Fixture(BotReplyMode.AiOnly, hasMatch: true, hoursAgo: 1);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(0, fixture.FaqRepository.CandidateQueries);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.AiConfigurationMissing, fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task FaqThenAiMiss_UsesProviderReplyAndPersistsAiCandidate()
    {
        var fixture = new Fixture(BotReplyMode.FaqThenAi, hasMatch: false, hoursAgo: 1, aiReply: "AI answer");

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Ai.Calls);
        Assert.Equal([ReplySource.AI], fixture.Outbox.Sources);
        Assert.Equal(["AI answer"], fixture.Outbox.Texts);
        Assert.Null(fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task HumanConversation_DoesNotEvaluateFaqOrAi()
    {
        var fixture = new Fixture(
            BotReplyMode.FaqThenAi,
            hasMatch: true,
            hoursAgo: 1,
            attentionMode: AttentionMode.Humano,
            aiReply: "must not be used");

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(0, fixture.FaqRepository.CandidateQueries);
        Assert.Equal(0, fixture.Ai.Calls);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.ConversationInHumanMode, fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task HumanTakeoverWhileAiIsProcessing_DoesNotCreateOutbox()
    {
        var fixture = new Fixture(
            BotReplyMode.AiOnly,
            hasMatch: false,
            hoursAgo: 1,
            aiReply: "must not be sent");
        fixture.Ai.OnGenerate = () => fixture.Conversations.AttentionMode = AttentionMode.Humano;

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(1, fixture.Ai.Calls);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.ConversationInHumanMode, fixture.Messages.OutcomeCode);
    }

    [Fact]
    public async Task AiTimeout_HandoffsWithoutFabricatingAnOutboundReply()
    {
        var fixture = new Fixture(
            BotReplyMode.AiOnly,
            hasMatch: false,
            hoursAgo: 1,
            aiFailure: new AiGenerationException(
                MessageOutcomeCodes.AiProviderTimeout,
                true,
                ConversationHandoffReasonCodes.BlockingError));

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Equal(MessageOutcomeCodes.AiProviderTimeout, fixture.Messages.OutcomeCode);
        Assert.Equal([ConversationHandoffReasonCodes.BlockingError], fixture.Handoff.Reasons);
    }

    [Fact]
    public async Task DuplicateInboundEvent_DoesNotRepeatAiOrOutboxWork()
    {
        var fixture = new Fixture(
            BotReplyMode.AiOnly,
            hasMatch: false,
            hoursAgo: 1,
            aiReply: "must not be used",
            inboundAlreadyPersisted: true);

        await fixture.Processor.ProcessAsync(fixture.Inbound, CancellationToken.None);

        Assert.Equal(0, fixture.Ai.Calls);
        Assert.Equal(0, fixture.Outbox.CreateCalls);
        Assert.Null(fixture.Messages.OutcomeCode);
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
        public Fixture(
            BotReplyMode mode,
            bool hasMatch,
            int hoursAgo,
            bool botEnabled = true,
            string? aiReply = null,
            AttentionMode attentionMode = AttentionMode.Bot,
            AiGenerationException? aiFailure = null,
            bool inboundAlreadyPersisted = false)
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
            Conversations.AttentionMode = attentionMode;
            Messages = new FakeMessageRepository { Inserted = !inboundAlreadyPersisted };
            Outbox = new FakeOutboxRepository();
            Ai = new FakeAiReplyGenerator(aiReply, aiFailure);
            Handoff = new FakeHandoffService();
            FaqRepository = new FakeFrequentResponseRepository(hasMatch
                ? [new FrequentResponseRecord(
                    new FrequentResponse(7, integrationId, "hours", "We are open 9 to 5", 10, null, true),
                    ["business hours"])]
                : []);
            var resolver = new BotReplyResolver(FaqRepository, new FrequentResponseMatcher());
            Processor = new ProcessInboundText(
                Integration, Contacts, Conversations, Messages, Conversations, Configuration, resolver,
                Ai, new AiContextBuilder(), Options.Create(new AiOptions()), Outbox,
                new WhatsAppMessagingPolicy(), new FakeClock(Now), Handoff);
            Inbound = InboundEvent(Now.AddHours(-hoursAgo));
        }

        public FakeIntegrationRepository Integration { get; }
        public FakeContactRepository Contacts { get; }
        public FakeConversationRepository Conversations { get; }
        public FakeMessageRepository Messages { get; }
        public FakeOutboxRepository Outbox { get; }
        public FakeAiReplyGenerator Ai { get; }
        public FakeHandoffService Handoff { get; }
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
        public Task<WhatsAppIntegration> GetByPhoneNumberIdAsync(
            string phoneNumberId, CancellationToken cancellationToken) => GetCurrentAsync(cancellationToken);
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
            Guid integrationId, Guid tenantId, string whatsappUserId, string? displayName,
            CancellationToken cancellationToken) =>
            Task.FromResult(5L);
    }

    private sealed class FakeConversationRepository : IConversationRepository, IConversationStateRepository
    {
        public AttentionMode AttentionMode { get; set; } = AttentionMode.Bot;

        public Task<long> GetOrCreateAsync(
            Guid integrationId, Guid tenantId, long contactId, DateTimeOffset lastMessageAtUtc,
            bool isBotEnabled,
            CancellationToken cancellationToken) => Task.FromResult(6L);

        public Task<ConversationPage> ListAsync(
            Guid integrationId, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ConversationStateRecord?> GetStateAsync(
            Guid tenantId, long conversationId, CancellationToken cancellationToken) =>
            Task.FromResult<ConversationStateRecord?>(new ConversationStateRecord(
                tenantId, conversationId, AttentionMode, LifecycleStatus.Abierta, null, "AQ=="));
    }

    private sealed class FakeAiReplyGenerator(string? reply, AiGenerationException? failure) : IAiReplyGenerator
    {
        public int Calls { get; private set; }
        public Action? OnGenerate { get; set; }

        public Task<string> GenerateReplyAsync(string prompt, CancellationToken cancellationToken)
        {
            Calls++;
            OnGenerate?.Invoke();
            if (failure is not null)
            {
                return Task.FromException<string>(failure);
            }

            return reply is not null
                ? Task.FromResult(reply)
                : Task.FromException<string>(new AiGenerationException(
                    MessageOutcomeCodes.AiConfigurationMissing,
                    false,
                    ConversationHandoffReasonCodes.BlockingError));
        }
    }

    private sealed class FakeHandoffService : IConversationHandoffService
    {
        public List<string> Reasons { get; } = [];

        public Task<bool> TryHandoffAsync(
            Guid tenantId, long conversationId, string reasonCode,
            CancellationToken cancellationToken)
        {
            Reasons.Add(reasonCode);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public int InboundPersisted { get; private set; }
        public string? OutcomeCode { get; private set; }
        public bool Inserted { get; init; } = true;

        public Task<ConversationDetailRecord?> GetHistoryAsync(
            Guid integrationId,
            long conversationId,
            long? beforeMessageId,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<ConversationDetailRecord?>(null);

        public Task<InboundMessageInsertResult> InsertInboundAsync(
            Guid integrationId, long conversationId, NormalizedMetaEvent inboundEvent,
            CancellationToken cancellationToken)
        {
            InboundPersisted++;
            return Task.FromResult(new InboundMessageInsertResult(Inserted, 9));
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
        public List<ReplySource> Sources { get; } = [];
        public List<string> Texts { get; } = [];

        public Task<OutboundMessageInsertResult> CreatePendingAsync(
            long inboundMessageId, Guid integrationId, long conversationId, string text,
            ReplySource source, long? frequentResponseId, CancellationToken cancellationToken)
        {
            CreateCalls++;
            Sources.Add(source);
            Texts.Add(text);
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
        public Task<BotConfiguration> GetForIntegrationAsync(
            Guid integrationId, CancellationToken cancellationToken) => GetAsync(cancellationToken);
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
