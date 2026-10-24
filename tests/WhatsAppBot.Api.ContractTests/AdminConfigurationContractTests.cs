using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Api.ContractTests;

public sealed class AdminConfigurationContractTests : IClassFixture<AdminApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient _client;

    public AdminConfigurationContractTests(AdminApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task AdminConfiguration_RejectsAnonymousRequests()
    {
        using var response = await _client.GetAsync("/api/v1/admin/whatsapp/bot-configuration");
        using var faqResponse = await _client.GetAsync("/api/v1/admin/frequent-responses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, faqResponse.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CanReadAndUpdateBotConfiguration()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("ContractTest");

        using var get = await _client.GetAsync("/api/v1/admin/whatsapp/bot-configuration");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var current = await get.Content.ReadFromJsonAsync<ResponseE<BotConfigurationDTO>>(JsonOptions);
        Assert.True(current?.Success);
        Assert.Equal(BotReplyMode.FaqThenAi, current!.Data!.ReplyMode);

        using var invalidUpdate = await _client.PutAsJsonAsync(
            "/api/v1/admin/whatsapp/bot-configuration", new { isBotEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        var invalidEnvelope = await invalidUpdate.Content.ReadFromJsonAsync<ResponseE<object?>>();
        Assert.False(invalidEnvelope!.Success);
        Assert.Equal("ValidationFailed", invalidEnvelope.Error!.Code);

        using var update = await _client.PutAsJsonAsync(
            "/api/v1/admin/whatsapp/bot-configuration",
            new { isBotEnabled = true, replyMode = "FaqOnly" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var changed = await update.Content.ReadFromJsonAsync<ResponseE<BotConfigurationDTO>>(JsonOptions);
        Assert.True(changed?.Success);
        Assert.True(changed!.Data!.IsBotEnabled);
        Assert.Equal(BotReplyMode.FaqOnly, changed.Data.ReplyMode);
    }

    [Fact]
    public async Task AuthenticatedAdmin_CanPerformFrequentResponseCrud_AndValidationUsesResponseEnvelope()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("ContractTest");
        var request = new FrequentResponseRequestDTO
        {
            QuestionOrIntent = "Delivery hours",
            Expressions = ["delivery hours", "shipping time"],
            AnswerText = "Orders ship daily.",
            Priority = 80,
            Category = "Shipping",
            IsActive = true
        };

        using var invalid = await _client.PostAsJsonAsync(
            "/api/v1/admin/frequent-responses",
            new FrequentResponseRequestDTO
            {
                QuestionOrIntent = "Invalid",
                Expressions = [],
                AnswerText = "No expression"
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var validation = await invalid.Content.ReadFromJsonAsync<ResponseE<object?>>();
        Assert.False(validation!.Success);
        Assert.Equal("ValidationFailed", validation.Error!.Code);

        using var createdResponse = await _client.PostAsJsonAsync("/api/v1/admin/frequent-responses", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<ResponseE<FrequentResponseDTO>>();
        Assert.True(created?.Success);
        var id = created!.Data!.FrequentResponseId;

        using var listedResponse = await _client.GetAsync("/api/v1/admin/frequent-responses?page=1&pageSize=25");
        var listed = await listedResponse.Content.ReadFromJsonAsync<ResponseE<PagedResultDTO<FrequentResponseDTO>>>();
        Assert.Contains(listed!.Data!.Items, item => item.FrequentResponseId == id);

        request = new FrequentResponseRequestDTO
        {
            QuestionOrIntent = "Updated shipping",
            Expressions = ["shipping update"],
            AnswerText = "Updated response.",
            Priority = 90,
            Category = "Orders",
            IsActive = true
        };
        using var updatedResponse = await _client.PutAsJsonAsync($"/api/v1/admin/frequent-responses/{id}", request);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ResponseE<FrequentResponseDTO>>();
        Assert.Equal("Updated shipping", updated!.Data!.QuestionOrIntent);

        using var toggleResponse = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/frequent-responses/{id}/active",
            new FrequentResponseStateDTO { IsActive = false });
        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);
        var toggled = await toggleResponse.Content.ReadFromJsonAsync<ResponseE<bool>>();
        Assert.True(toggled!.Data);

        using var deleteResponse = await _client.DeleteAsync($"/api/v1/admin/frequent-responses/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    public AdminApiFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__WhatsAppBot",
            "Server=localhost;Database=WhatsAppBotContractTests;Trusted_Connection=True;TrustServerCertificate=True;");
        Environment.SetEnvironmentVariable(
            "Authentication__SigningKey",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        Environment.SetEnvironmentVariable("Authentication__Issuer", "WhatsAppBot.ContractTests");
        Environment.SetEnvironmentVariable("Authentication__Audience", "WhatsAppBot.ContractTests");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBotConfigurationService>();
            services.RemoveAll<IFrequentResponseService>();
            services.AddSingleton<IBotConfigurationService, FakeBotConfigurationService>();
            services.AddSingleton<IFrequentResponseService, FakeFrequentResponseService>();
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

public sealed class ContractTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.Authorization != "ContractTest")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "contract-admin")], Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}

internal sealed class FakeBotConfigurationService : IBotConfigurationService
{
    private BotConfiguration _configuration = new(Guid.NewGuid(), false, BotReplyMode.FaqThenAi);

    public Task<BotConfiguration> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_configuration);

    public Task<BotConfiguration> GetForIntegrationAsync(
        Guid integrationId, CancellationToken cancellationToken) => GetAsync(cancellationToken);

    public Task<BotConfiguration> UpdateAsync(
        bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken)
    {
        _configuration = _configuration with { IsBotEnabled = isBotEnabled, ReplyMode = replyMode };
        return Task.FromResult(_configuration);
    }
}

internal sealed class FakeFrequentResponseService : IFrequentResponseService
{
    private readonly Dictionary<long, FrequentResponseRecord> _responses = [];
    private long _nextId;

    public Task<FrequentResponsePage> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = _responses.Values.OrderByDescending(item => item.Response.Priority)
            .ThenBy(item => item.Response.FrequentResponseId).ToArray();
        return Task.FromResult(new FrequentResponsePage(
            all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), page, pageSize, all.Length));
    }

    public Task<FrequentResponseRecord> GetAsync(long frequentResponseId, CancellationToken cancellationToken) =>
        Task.FromResult(_responses[frequentResponseId]);

    public Task<FrequentResponseRecord> CreateAsync(
        FrequentResponseCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var response = new FrequentResponse(++_nextId, Guid.Empty, command.QuestionOrIntent,
            command.AnswerText, command.Priority, command.Category, command.IsActive)
        {
            CreatedAtUtc = now,
            ModifiedAtUtc = now
        };
        var record = new FrequentResponseRecord(response, command.Expressions);
        _responses.Add(response.FrequentResponseId, record);
        return Task.FromResult(record);
    }

    public Task<FrequentResponseRecord> UpdateAsync(
        long frequentResponseId, FrequentResponseCommand command, CancellationToken cancellationToken)
    {
        var original = _responses[frequentResponseId].Response;
        var updated = original with
        {
            QuestionOrIntent = command.QuestionOrIntent,
            AnswerText = command.AnswerText,
            Priority = command.Priority,
            Category = command.Category,
            IsActive = command.IsActive,
            ModifiedAtUtc = DateTimeOffset.UtcNow
        };
        var record = new FrequentResponseRecord(updated, command.Expressions);
        _responses[frequentResponseId] = record;
        return Task.FromResult(record);
    }

    public Task<bool> SetActiveAsync(long frequentResponseId, bool isActive, CancellationToken cancellationToken)
    {
        var current = _responses[frequentResponseId];
        _responses[frequentResponseId] = current with
        {
            Response = current.Response with { IsActive = isActive, ModifiedAtUtc = DateTimeOffset.UtcNow }
        };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(long frequentResponseId, CancellationToken cancellationToken) =>
        Task.FromResult(_responses.Remove(frequentResponseId));
}
