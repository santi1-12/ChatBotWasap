using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Infrastructure.Extensions;
using WhatsAppBot.Api.Infrastructure.Options;

namespace WhatsAppBot.Integrations.Tests;

public sealed class OptionsValidationTests
{
    [Fact]
    public void InvalidMetaTimeoutFailsExplicitly()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Meta:RequestTimeoutSeconds"] = "0"
        });

        var options = provider.GetRequiredService<IOptions<MetaOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("Meta:RequestTimeoutSeconds", exception.Message);
    }

    [Fact]
    public void InvalidFfmpegBudgetFailsExplicitly()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["FFmpeg:MaxInputBytes"] = "0"
        });

        var options = provider.GetRequiredService<IOptions<FfmpegOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("FFmpeg:MaxInputBytes", exception.Message);
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string?>(overrides)
        {
            ["ConnectionStrings:WhatsAppBot"] = "Server=(localdb)\\MSSQLLocalDB;Database=WhatsAppBot;",
            ["Authentication:Issuer"] = "WhatsAppBot.Api",
            ["Authentication:Audience"] = "WhatsAppBot.Admin",
            ["Authentication:SigningKey"] = "development-signing-key-with-at-least-32-bytes",
            ["Meta:GraphApiVersion"] = "v24.0"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration, new TestHostEnvironment());
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
