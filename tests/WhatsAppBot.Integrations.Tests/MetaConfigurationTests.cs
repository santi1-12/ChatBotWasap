using WhatsAppBot.Api.Infrastructure.Options;

namespace WhatsAppBot.Integrations.Tests;

public sealed class MetaConfigurationTests
{
    [Fact]
    public void CoexistenceConfig_UsesDedicatedValueWhenPresent()
    {
        var options = new MetaOptions
        {
            EmbeddedSignupConfigId = "standard-config",
            CoexistenceEmbeddedSignupConfigId = "coexistence-config"
        };

        Assert.Equal("coexistence-config", options.EffectiveCoexistenceEmbeddedSignupConfigId);
    }

    [Fact]
    public void CoexistenceConfig_FallsBackToStandardWhenDedicatedValueIsMissing()
    {
        var options = new MetaOptions
        {
            EmbeddedSignupConfigId = "standard-config"
        };

        Assert.Equal("standard-config", options.EffectiveCoexistenceEmbeddedSignupConfigId);
    }

    [Fact]
    public void CoexistenceConfig_IsUnavailableWhenBothValuesAreMissing()
    {
        var options = new MetaOptions();

        Assert.Null(options.EffectiveCoexistenceEmbeddedSignupConfigId);
    }
}
