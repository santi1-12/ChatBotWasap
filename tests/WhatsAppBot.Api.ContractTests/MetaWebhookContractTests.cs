using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Webhooks;

namespace WhatsAppBot.Api.ContractTests;

public sealed class MetaWebhookContractTests
{
    [Fact]
    public void GetChallenge_AcceptsOnlySubscribeAndExactVerifyToken()
    {
        var validator = new MetaWebhookChallengeValidator();

        Assert.True(validator.IsValid("subscribe", "verify-value", "verify-value"));
        Assert.False(validator.IsValid("subscribe", "wrong", "verify-value"));
        Assert.False(validator.IsValid("unsubscribe", "verify-value", "verify-value"));
        Assert.False(validator.IsValid("subscribe", "", "verify-value"));
    }

    [Fact]
    public async Task GetWebhookEndpoint_ReturnsChallengeOnlyForValidToken()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Meta:WebhookMaxBodyBytes"] = "4096";
        builder.Services.Configure<MetaOptions>(builder.Configuration.GetSection("Meta"));
        builder.Services.AddSingleton<ISecretReferenceResolver, FakeSecretResolver>();
        builder.Services.AddSingleton<IMetaWebhookChallengeValidator, MetaWebhookChallengeValidator>();
        builder.Services.AddSingleton<IMetaWebhookSignatureValidator, MetaWebhookSignatureValidator>();
        builder.Services.AddSingleton<IMetaWebhookEventParser, MetaWebhookEventParser>();
        builder.Services.AddSingleton<FakeWebhookAcceptor>();
        builder.Services.AddSingleton<IAcceptMetaWebhookEvent>(services =>
            services.GetRequiredService<FakeWebhookAcceptor>());
        await using var app = builder.Build();
        app.MapMetaWhatsAppWebhook();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var validResponse = await client.GetAsync(
            "/webhooks/meta/whatsapp?hub.mode=subscribe&hub.verify_token=verify-value&hub.challenge=challenge-value");
        var invalidResponse = await client.GetAsync(
            "/webhooks/meta/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=challenge-value");

        Assert.Equal(System.Net.HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal("challenge-value", await validResponse.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, invalidResponse.StatusCode);

        const string payload = "{\"object\":\"whatsapp_business_account\",\"entry\":[]}";
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var validPost = new HttpRequestMessage(
            HttpMethod.Post, "/webhooks/meta/whatsapp") { Content = content };
        validPost.Headers.Add("X-Hub-Signature-256", Sign(payload, "app-secret-value"));
        using var postResponse = await client.SendAsync(validPost);
        Assert.Equal(System.Net.HttpStatusCode.OK, postResponse.StatusCode);

        using var invalidContent = new StringContent(payload, Encoding.UTF8, "application/json");
        using var invalidPost = new HttpRequestMessage(
            HttpMethod.Post, "/webhooks/meta/whatsapp") { Content = invalidContent };
        invalidPost.Headers.Add("X-Hub-Signature-256", "sha256=invalid");
        using var invalidPostResponse = await client.SendAsync(invalidPost);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, invalidPostResponse.StatusCode);
        Assert.Equal(1, app.Services.GetRequiredService<FakeWebhookAcceptor>().AcceptedCount);
    }

    [Fact]
    public void PostSignature_RequiresExactHmacSha256OverRawBytes()
    {
        var validator = new MetaWebhookSignatureValidator();
        var payload = Encoding.UTF8.GetBytes("{\"entry\":[]}");
        const string appSecret = "test-app-secret";
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), payload);
        var signature = $"sha256={Convert.ToHexString(digest).ToLowerInvariant()}";

        Assert.True(validator.IsValid(payload, signature, appSecret));
        Assert.False(validator.IsValid(Encoding.UTF8.GetBytes("{\"entry\":[]} "), signature, appSecret));
        Assert.False(validator.IsValid(payload, "sha256=not-hex", appSecret));
        Assert.False(validator.IsValid(payload, signature, ""));
    }

    [Fact]
    public void Parser_AllowListsTextAndStatusFieldsAndUsesStableMessageKeys()
    {
        var parser = new MetaWebhookEventParser();
        const string body = """
            {
              "object":"whatsapp_business_account",
              "entry":[{
                "id":"waba-test",
                "changes":[{
                  "field":"messages",
                  "value":{
                    "metadata":{"phone_number_id":"phone-test"},
                    "contacts":[{"wa_id":"15551234567","profile":{"name":"Customer"}}],
                    "messages":[
                      {"from":"15551234567","id":"wamid.text-1","timestamp":"1790000000","type":"text","text":{"body":"Hello"}},
                      {"from":"15551234567","id":"wamid.image-1","timestamp":"1790000001","type":"image","image":{"id":"media-secret"}}
                    ],
                    "statuses":[{"id":"wamid.text-1","status":"delivered","timestamp":"1790000002"}]
                  }
                }]
              }]
            }
            """;

        var parsed = parser.Parse(Encoding.UTF8.GetBytes(body));

        Assert.Equal(3, parsed.Count);
        var text = Assert.Single(parsed, item => item.EventType == MetaWebhookEventTypes.InboundText);
        Assert.Equal("Hello", text.Text);
        Assert.Equal("Customer", text.ContactDisplayName);
        Assert.StartsWith("message:", text.EventKey);
        Assert.Equal(text.EventKey, Assert.Single(
            parser.Parse(Encoding.UTF8.GetBytes(body)), item => item.EventType == MetaWebhookEventTypes.InboundText).EventKey);
        var unsupported = Assert.Single(parsed, item => item.EventType == "UnsupportedMessage");
        Assert.Null(unsupported.Text);
        Assert.DoesNotContain("media-secret", System.Text.Json.JsonSerializer.Serialize(unsupported));
        Assert.Contains(parsed, item => item.EventType == MetaWebhookEventTypes.DeliveryStatus);
    }

    [Fact]
    public void Parser_IgnoresAdditionalCoexistenceFieldsAndStillProcessesMessagesAndStatuses()
    {
        var parser = new MetaWebhookEventParser();
        const string body = """
            {
              "object":"whatsapp_business_account",
              "entry":[{
                "id":"waba-test",
                "changes":[
                  {
                    "field":"history",
                    "value":{"messages":[{"unexpected":"historical payload"}]}
                  },
                  {
                    "field":"smb_message_echoes",
                    "value":{"events":[{"unexpected":"echo payload"}]}
                  },
                  {
                    "field":"smb_app_state_sync",
                    "value":{"contacts":[{"unexpected":"contact sync payload"}]}
                  },
                  {
                    "field":"messages",
                    "value":{
                      "metadata":{"phone_number_id":"phone-test"},
                      "messages":[
                        {"from":"15551234567","id":"wamid.coexist-text","timestamp":"1790000000","type":"text","text":{"body":"Hello"}}
                      ],
                      "statuses":[
                        {"id":"wamid.coexist-text","status":"delivered","timestamp":"1790000001"}
                      ]
                    }
                  }
                ]
              }]
            }
            """;

        var parsed = parser.Parse(Encoding.UTF8.GetBytes(body));

        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed, item => item.EventType == MetaWebhookEventTypes.InboundText);
        Assert.Contains(parsed, item => item.EventType == MetaWebhookEventTypes.DeliveryStatus);
    }

    private sealed class FakeSecretResolver : ISecretReferenceResolver
    {
        public ValueTask<string?> ResolveAsync(string secretReference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(secretReference switch
            {
                "Meta:VerifyToken" => "verify-value",
                "Meta:AppSecret" => "app-secret-value",
                _ => null
            });
    }

    private static string Sign(string payload, string secret) =>
        $"sha256={Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()}";

    private sealed class FakeWebhookAcceptor : IAcceptMetaWebhookEvent
    {
        public int AcceptedCount { get; private set; }
        public Task AcceptAsync(IReadOnlyList<NormalizedMetaEvent> events, CancellationToken cancellationToken)
        {
            AcceptedCount++;
            return Task.CompletedTask;
        }
    }
}
