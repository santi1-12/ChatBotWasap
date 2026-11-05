using System.Text.Json;
using System.Text.Json.Serialization;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.ContractTests;

public sealed class WhatsAppIntegrationContractTests
{
    [Fact]
    public void ConnectedContractContainsSafeConnectionMetadataWithoutSecrets()
    {
        var integration = new WhatsAppIntegrationDTO
        {
            TenantId = Guid.NewGuid(),
            WabaId = "waba-1",
            PhoneNumberId = "phone-1",
            BusinessPhoneNumber = "+15550000000",
            ConnectionState = ConnectionState.Connected,
            OnboardingType = WhatsAppOnboardingType.Coexistence,
            ConnectedAtUtc = DateTimeOffset.UtcNow,
            BotEnabled = true,
            HasCredentialReference = true,
            EffectiveEmbeddedSignupConfigId = "coexistence-config"
        };

        var json = JsonSerializer.Serialize(integration, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });

        Assert.Contains("\"onboardingType\":\"Coexistence\"", json, StringComparison.Ordinal);
        Assert.Contains("\"connectedAtUtc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"effectiveEmbeddedSignupConfigId\":\"coexistence-config\"", json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MetaAppSecret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ConnectionState.Unknown)]
    [InlineData(ConnectionState.Error)]
    [InlineData(ConnectionState.Configuring)]
    [InlineData(ConnectionState.NotConnected)]
    public void NonConfirmedStatesAreRepresentedWithoutConnectedMetadata(ConnectionState state)
    {
        var integration = new WhatsAppIntegrationDTO
        {
            ConnectionState = state,
            BotEnabled = false,
            ConnectedAtUtc = null,
            OnboardingType = null
        };

        Assert.NotEqual(ConnectionState.Connected, integration.ConnectionState);
        Assert.Null(integration.ConnectedAtUtc);
    }
}
