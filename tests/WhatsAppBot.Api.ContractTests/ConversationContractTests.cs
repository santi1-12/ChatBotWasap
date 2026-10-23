using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Api.ContractTests;

public sealed class ConversationContractTests : IClassFixture<ConversationApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient client;

    public ConversationContractTests(ConversationApiFactory factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Conversations_RejectsAnonymousRequests()
    {
        using var list = await client.GetAsync("/api/v1/admin/conversations");
        using var detail = await client.GetAsync("/api/v1/admin/conversations/42");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CanListAndReadConversationDetail()
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("ContractTest");

        using var listResponse = await client.GetAsync("/api/v1/admin/conversations?page=2&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await listResponse.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<ResponseE<PagedResultDTO<ConversationSummaryDTO>>>(
            listJson, JsonOptions);

        Assert.True(list?.Success);
        Assert.Equal(42, Assert.Single(list!.Data!.Items).ConversationId);
        Assert.Equal(2, list.Data.Page);

        using var detailResponse = await client.GetAsync(
            "/api/v1/admin/conversations/42?beforeMessageId=17");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detailJson = await detailResponse.Content.ReadAsStringAsync();
        var detail = JsonSerializer.Deserialize<ResponseE<ConversationDetailDTO>>(
            detailJson, JsonOptions);

        Assert.True(detail?.Success);
        Assert.Equal(42, detail!.Data!.Conversation.ConversationId);
        var message = Assert.Single(detail.Data.Messages);
        Assert.Equal(MessageType.Audio, message.MessageType);
        Assert.True(message.MediaAvailable);
        Assert.Null(message.MediaUrl);
        Assert.Equal("BotDisabled", message.OutcomeCode);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class ConversationApiFactory : WebApplicationFactory<Program>
{
    public ConversationApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__WhatsAppBot",
            "Server=localhost;Database=WhatsAppBotConversationContractTests;Trusted_Connection=True;TrustServerCertificate=True;");
        Environment.SetEnvironmentVariable(
            "Authentication__SigningKey",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        Environment.SetEnvironmentVariable("Authentication__Issuer", "WhatsAppBot.ConversationContractTests");
        Environment.SetEnvironmentVariable("Authentication__Audience", "WhatsAppBot.ConversationContractTests");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IListConversations>();
            services.RemoveAll<IGetConversationDetail>();
            services.AddSingleton<IListConversations, FakeListConversations>();
            services.AddSingleton<IGetConversationDetail, FakeConversationDetail>();
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "ContractTest";
                    options.DefaultChallengeScheme = "ContractTest";
                    options.DefaultScheme = "ContractTest";
                })
                .AddScheme<AuthenticationSchemeOptions, ContractTestAuthenticationHandler>(
                    "ContractTest", _ => { });
        });
    }
}

internal sealed class FakeListConversations : IListConversations
{
    public Task<ConversationPage> ExecuteAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new ConversationPage(
            [new ConversationSummaryRecord(
                42,
                "Customer",
                DateTimeOffset.Parse("2026-10-22T12:00:00Z"),
                ConversationStatus.Open,
                "Hello")],
            page,
            pageSize,
            1));
}

internal sealed class FakeConversationDetail : IGetConversationDetail
{
    public Task<ConversationDetailRecord> ExecuteAsync(
        long conversationId, long? beforeMessageId, CancellationToken cancellationToken) =>
        Task.FromResult(new ConversationDetailRecord(
            new ConversationSummaryRecord(
                conversationId,
                "Customer",
                DateTimeOffset.Parse("2026-10-22T12:00:00Z"),
                ConversationStatus.Open,
                "Hello"),
            [new ConversationMessageRecord(
                7,
                MessageDirection.Inbound,
                MessageType.Audio,
                null,
                "Transcript",
                true,
                null,
                ProcessingState.NoReply,
                null,
                "BotDisabled",
                DateTimeOffset.Parse("2026-10-22T11:59:00Z"))]));
}
