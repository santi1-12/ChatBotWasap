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
    public void CoexistenceConfig_DoesNotReuseStandardFlowWhenDedicatedValueIsMissing()
    {
        var options = new MetaOptions
        {
            EmbeddedSignupConfigId = "standard-config"
        };

        Assert.Null(options.EffectiveCoexistenceEmbeddedSignupConfigId);
    }

    [Fact]
    public void CoexistenceConfig_IsUnavailableWhenBothValuesAreMissing()
    {
        var options = new MetaOptions();

        Assert.Null(options.EffectiveCoexistenceEmbeddedSignupConfigId);
    }
}
