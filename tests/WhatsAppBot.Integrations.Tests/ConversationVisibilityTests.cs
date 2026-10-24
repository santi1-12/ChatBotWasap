using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WhatsAppBot.Admin.Authentication;
using WhatsAppBot.Admin.Configuration;
using WhatsAppBot.Admin.Pages.Conversations;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;
using WhatsAppBot.Shared.Requests;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Integrations.Tests;

public sealed class ConversationVisibilityTests
{
    [Fact]
    public async Task ListConversations_ReturnsEmptyPageForActiveIntegration()
    {
        var integrationId = Guid.NewGuid();
        var repository = new FakeConversationRepository
        {
            Page = new ConversationPage([], 1, 25, 0)
        };
        var useCase = new ListConversations(
            new FakeIntegrationRepository(integrationId), repository);

        var result = await useCase.ExecuteAsync(1, 25, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(integrationId, repository.RequestedIntegrationId);
    }

    [Fact]
    public async Task ListConversations_PreservesPagingAndConversationSummaries()
    {
        var integrationId = Guid.NewGuid();
        var repository = new FakeConversationRepository
        {
            Page = new ConversationPage(
                [CreateSummary(42, "Customer", "Latest question")], 2, 25, 26)
        };
        var useCase = new ListConversations(
            new FakeIntegrationRepository(integrationId), repository);

        var result = await useCase.ExecuteAsync(2, 25, CancellationToken.None);

        Assert.Equal(2, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(26, result.TotalCount);
        Assert.Equal(42, Assert.Single(result.Items).ConversationId);
        Assert.Equal(integrationId, repository.RequestedIntegrationId);
    }

    [Fact]
    public async Task GetConversationDetail_ReturnsChronologicalHistoryWithoutBotConfigurationGate()
    {
        var integrationId = Guid.NewGuid();
        var detail = new ConversationDetailRecord(
            CreateSummary(42, "Customer", "Latest question"),
            [
                CreateMessage(1, MessageType.Text, "First"),
                CreateMessage(2, MessageType.Audio, null),
                CreateMessage(3, MessageType.Image, null, MessageDirection.Outbound, true)
            ]);
        var repository = new FakeMessageRepository(integrationId, detail);
        var useCase = new GetConversationDetail(
            new FakeIntegrationRepository(integrationId), repository);

        var result = await useCase.ExecuteAsync(42, null, CancellationToken.None);

        Assert.Equal(42, result.Conversation.ConversationId);
        Assert.Equal([1L, 2L, 3L], result.Messages.Select(message => message.MessageId));
        Assert.Contains(result.Messages, message => message.Direction == MessageDirection.Outbound);
        Assert.Contains(result.Messages, message => message.MessageType == MessageType.Image && message.MediaAvailable);
        Assert.Equal(integrationId, repository.RequestedIntegrationId);
        Assert.Equal(50, repository.RequestedPageSize);
    }

    [Fact]
    public async Task GetConversationDetail_ReturnsNotFoundForMissingConversation()
    {
        var integrationId = Guid.NewGuid();
        var useCase = new GetConversationDetail(
            new FakeIntegrationRepository(integrationId),
            new FakeMessageRepository(integrationId, null));

        await Assert.ThrowsAsync<BusinessNotFoundException>(() =>
            useCase.ExecuteAsync(404, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetConversationDetail_DoesNotReturnConversationFromAnotherIntegration()
    {
        var currentIntegrationId = Guid.NewGuid();
        var otherIntegrationId = Guid.NewGuid();
        var repository = new FakeMessageRepository(
            otherIntegrationId,
            new ConversationDetailRecord(CreateSummary(99, "Other tenant", null), []));
        var useCase = new GetConversationDetail(
            new FakeIntegrationRepository(currentIntegrationId), repository);

        await Assert.ThrowsAsync<BusinessNotFoundException>(() =>
            useCase.ExecuteAsync(99, null, CancellationToken.None));

        Assert.Equal(currentIntegrationId, repository.RequestedIntegrationId);
    }

    [Fact]
    public async Task AdminApiClient_ConsumesConversationListAndDetailEndpoints()
    {
        var handler = new ConversationApiHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var client = new WhatsAppBotApiClient(
            httpClient,
            new FakeAdminSession(),
            Options.Create(new AdminApiOptions { RequestTimeoutSeconds = 30 }),
            NullLogger<WhatsAppBotApiClient>.Instance);

        var list = await client.ListConversationsAsync(2, 25, CancellationToken.None);
        var detail = await client.GetConversationAsync(42, 17, CancellationToken.None);

        Assert.Equal(42, Assert.Single(list!.Data!.Items).ConversationId);
        Assert.Equal(42, detail!.Data!.Conversation.ConversationId);
        Assert.Contains("page=2", handler.Requests[0].Query);
        Assert.Contains("beforeMessageId=17", handler.Requests[1].Query);
    }

    [Fact]
    public async Task ConversationPages_HandleSuccessEmptyHistoryAndControlledError()
    {
        var client = new FakeAdminApiClient
        {
            ListResponse = ResponseE<PagedResultDTO<ConversationSummaryDTO>>.Ok(
                new PagedResultDTO<ConversationSummaryDTO>
                {
                    Items = [CreateSummaryDTO(42, "Customer", "Hello")],
                    Page = 1,
                    PageSize = 25,
                    TotalCount = 1
                }, "test"),
            DetailResponse = ResponseE<ConversationDetailDTO>.Ok(
                new ConversationDetailDTO
                {
                    Conversation = CreateSummaryDTO(42, "Customer", null),
                    Messages = []
                }, "test")
        };

        var listPage = new IndexModel(client);
        await listPage.OnGetAsync(1, 25, CancellationToken.None);
        var detailPage = new DetailsModel(client);
        await detailPage.OnGetAsync(42, null, CancellationToken.None);

        Assert.Equal(42, Assert.Single(listPage.Conversations).ConversationId);
        Assert.Empty(detailPage.Conversation!.Messages);
        Assert.False(detailPage.HasOlderMessages);

        var errorPage = new DetailsModel(new FakeAdminApiClient
        {
            DetailException = new HttpRequestException("network")
        });
        await errorPage.OnGetAsync(42, null, CancellationToken.None);

        Assert.Null(errorPage.Conversation);
        Assert.Contains("contactar", errorPage.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversationPageLabelsSupportTextAudioImageAndSafeOutcome()
    {
        var page = new DetailsModel(new FakeAdminApiClient());

        Assert.Equal("Texto", page.MessageTypeLabel(MessageType.Text));
        Assert.Equal("Audio", page.MessageTypeLabel(MessageType.Audio));
        Assert.Equal("Imagen", page.MessageTypeLabel(MessageType.Image));
        Assert.Equal("El bot estaba desactivado", page.OutcomeLabel("BotDisabled"));
        Assert.Null(page.OutcomeLabel("RawProviderError"));
    }

    private static ConversationSummaryRecord CreateSummary(
        long id, string contact, string? preview) =>
        new(id, contact, DateTimeOffset.Parse("2026-10-22T12:00:00Z"), ConversationStatus.Open, preview);

    private static ConversationSummaryDTO CreateSummaryDTO(
        long id, string contact, string? preview) => new()
    {
        ConversationId = id,
        Contact = contact,
        LastMessageAtUtc = DateTimeOffset.Parse("2026-10-22T12:00:00Z"),
        Status = ConversationStatus.Open,
        LastMessagePreview = preview
    };

    private static ConversationMessageRecord CreateMessage(
        long id,
        MessageType type,
        string? text,
        MessageDirection direction = MessageDirection.Inbound,
        bool mediaAvailable = false) => new(
        id,
        direction,
        type,
        text,
        type == MessageType.Audio ? "Audio transcript" : null,
        mediaAvailable,
        null,
        ProcessingState.Completed,
        null,
        null,
        DateTimeOffset.Parse($"2026-10-22T12:0{id}:00Z"));

    private sealed class FakeIntegrationRepository(Guid integrationId) : IWhatsAppIntegrationRepository
    {
        public Task<WhatsAppIntegration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new WhatsAppIntegration(
                integrationId, "waba", "phone", "+15550000000", "Test", ConnectionState.Connected,
                DateTimeOffset.UtcNow, true));

        public Task<WhatsAppIntegration> GetByPhoneNumberIdAsync(
            string phoneNumberId, CancellationToken cancellationToken) => GetCurrentAsync(cancellationToken);

        public Task SaveSignupResultAsync(MetaSignupResult result, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetConnectionStateAsync(
            Guid integrationId, ConnectionState state, string? safeErrorCode,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DisconnectAsync(Guid integrationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public ConversationPage Page { get; init; } = new([], 1, 25, 0);
        public Guid? RequestedIntegrationId { get; private set; }

        public Task<ConversationPage> ListAsync(
            Guid integrationId, int page, int pageSize, CancellationToken cancellationToken)
        {
            RequestedIntegrationId = integrationId;
            return Task.FromResult(Page);
        }

        public Task<long> GetOrCreateAsync(
            Guid integrationId, Guid tenantId, long contactId, DateTimeOffset lastMessageAtUtc,
            bool isBotEnabled,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeMessageRepository(
        Guid allowedIntegrationId, ConversationDetailRecord? detail) : IMessageRepository
    {
        public Guid? RequestedIntegrationId { get; private set; }
        public int RequestedPageSize { get; private set; }

        public Task<ConversationDetailRecord?> GetHistoryAsync(
            Guid integrationId,
            long conversationId,
            long? beforeMessageId,
            int pageSize,
            CancellationToken cancellationToken)
        {
            RequestedIntegrationId = integrationId;
            RequestedPageSize = pageSize;
            return Task.FromResult(integrationId == allowedIntegrationId ? detail : null);
        }

        public Task<InboundMessageInsertResult> InsertInboundAsync(
            Guid integrationId, long conversationId, NormalizedMetaEvent inboundEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetProcessingOutcomeAsync(
            long messageId, ProcessingState state, string? safeOutcomeCode,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DeliveryStatusApplyResult> ApplyDeliveryStatusAsync(
            NormalizedMetaEvent statusEvent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<OutboundMessageWorkItem>> ClaimOutboxBatchAsync(
            int batchSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OutboxRevalidationResult> RevalidateOutboxAsync(
            OutboundMessageWorkItem item, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetOutboxResultAsync(
            long messageId, MetaMessageSendResult result, bool isTerminal,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetOutboxStateAsync(
            long messageId, MessageOutboxState state, int retryDelaySeconds,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ConversationApiHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            object response = request.RequestUri!.AbsolutePath.EndsWith("/42", StringComparison.Ordinal)
                ? ResponseE<ConversationDetailDTO>.Ok(
                    new ConversationDetailDTO
                    {
                        Conversation = CreateSummaryDTO(42, "Customer", "Hello"),
                        Messages = []
                    }, "detail")
                : ResponseE<PagedResultDTO<ConversationSummaryDTO>>.Ok(
                    new PagedResultDTO<ConversationSummaryDTO>
                    {
                        Items = [CreateSummaryDTO(42, "Customer", "Hello")],
                        Page = 2,
                        PageSize = 25,
                        TotalCount = 1
                    }, "list");
            var content = new StringContent(
                JsonSerializer.Serialize(response, response.GetType(), JsonOptions),
                Encoding.UTF8,
                "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }

    private sealed class FakeAdminSession : IAdminSession
    {
        public void SetApiAccessToken(string accessToken) { }
        public string? GetApiAccessToken() => "test-token";
        public void Clear() { }
    }

    private sealed class FakeAdminApiClient : IWhatsAppBotApiClient
    {
        public ResponseE<PagedResultDTO<ConversationSummaryDTO>>? ListResponse { get; init; }
        public ResponseE<ConversationDetailDTO>? DetailResponse { get; init; }
        public Exception? ListException { get; init; }
        public Exception? DetailException { get; init; }

        public Task<ResponseE<PagedResultDTO<ConversationSummaryDTO>>?> ListConversationsAsync(
            int page, int pageSize, CancellationToken cancellationToken) =>
            ListException is null
                ? Task.FromResult(ListResponse)
                : Task.FromException<ResponseE<PagedResultDTO<ConversationSummaryDTO>>?>(ListException);

        public Task<ResponseE<ConversationDetailDTO>?> GetConversationAsync(
            long conversationId, long? beforeMessageId, CancellationToken cancellationToken) =>
            DetailException is null
                ? Task.FromResult(DetailResponse)
                : Task.FromException<ResponseE<ConversationDetailDTO>?>(DetailException);

        public Task<ResponseE<ConversationDetailDTO>?> ApplyConversationActionAsync(
            long conversationId, ConversationActionRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResponseE<MessageDTO>?> SendManualConversationMessageAsync(
            long conversationId, ManualMessageRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResponseE<AuthResponseDTO>?> LoginAsync(
            LoginRequestDTO request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResponseE<WhatsAppIntegrationDTO>?> GetWhatsAppIntegrationAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResponseE<WhatsAppIntegrationDTO>?> CompleteWhatsAppSignupAsync(
            EmbeddedSignupCompletionRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DisconnectWhatsAppAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResponseE<BotConfigurationDTO>?> GetBotConfigurationAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResponseE<BotConfigurationDTO>?> UpdateBotConfigurationAsync(
            UpdateBotConfigurationRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<PagedResultDTO<FrequentResponseDTO>>?> ListFrequentResponsesAsync(
            int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResponseE<FrequentResponseDTO>?> GetFrequentResponseAsync(
            long id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ResponseE<FrequentResponseDTO>?> CreateFrequentResponseAsync(
            FrequentResponseRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<FrequentResponseDTO>?> UpdateFrequentResponseAsync(
            long id, FrequentResponseRequestDTO request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ResponseE<bool>?> SetFrequentResponseActiveAsync(
            long id, bool isActive, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteFrequentResponseAsync(long id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
