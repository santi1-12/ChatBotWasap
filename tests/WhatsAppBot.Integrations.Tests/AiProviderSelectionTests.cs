using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Extensions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Integrations;
using WhatsAppBot.Api.Integrations.Extensions;

namespace WhatsAppBot.Integrations.Tests;

public sealed class AiProviderSelectionTests
{
    [Theory]
    [InlineData(AiProviderNames.OpenAI)]
    [InlineData(AiProviderNames.GitHubCopilot)]
    public void SupportedProviderUsesOneExplicitSelector(string providerName)
    {
        using var serviceProvider = BuildProvider(providerName);

        var generator = serviceProvider.GetRequiredService<IAiReplyGenerator>();

        Assert.IsType<AiReplyGeneratorSelector>(generator);
    }

    [Fact]
    public void UnknownProviderFailsOptionsValidationInsteadOfUsingLastRegistration()
    {
        using var serviceProvider = BuildProvider("UnknownProvider");

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IAiReplyGenerator>());

        Assert.Contains("AI:Provider", exception.Message);
    }

    private static ServiceProvider BuildProvider(string providerName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WhatsAppBot"] = "Server=(localdb)\\MSSQLLocalDB;Database=WhatsAppBot;",
                ["Authentication:Issuer"] = "WhatsAppBot.Api",
                ["Authentication:Audience"] = "WhatsAppBot.Admin",
                ["Authentication:SigningKey"] = "development-signing-key-with-at-least-32-bytes",
                ["Meta:GraphApiVersion"] = "v24.0",
                ["AI:Provider"] = providerName
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var environment = new TestHostEnvironment();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddInfrastructure(configuration, environment);
        services.AddIntegrations();
        return services.BuildServiceProvider();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "WhatsAppBot.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
