using System.ComponentModel.DataAnnotations;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.ContractTests;

public sealed class EmbeddedSignupContractTests
{
    [Theory]
    [InlineData(WhatsAppOnboardingType.Standard)]
    [InlineData(WhatsAppOnboardingType.Coexistence)]
    public void CompletionRequest_AllowsServerDiscoveredPhoneIdForExplicitMode(
        WhatsAppOnboardingType onboardingType)
    {
        var request = new EmbeddedSignupCompletionRequestDTO
        {
            AuthorizationCode = "single-use-code",
            WabaId = "waba-1",
            PhoneNumberId = null,
            OnboardingType = onboardingType
        };

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void CompletionRequest_RequiresExplicitOnboardingType()
    {
        var request = new EmbeddedSignupCompletionRequestDTO
        {
            AuthorizationCode = "single-use-code",
            WabaId = "waba-1"
        };

        Assert.Contains(Validate(request), error => error.MemberNames.Contains("OnboardingType"));
    }

    [Fact]
    public void CompletionRequest_RejectsUnknownOnboardingType()
    {
        var request = new EmbeddedSignupCompletionRequestDTO
        {
            AuthorizationCode = "single-use-code",
            WabaId = "waba-1",
            OnboardingType = (WhatsAppOnboardingType)100
        };

        Assert.Contains(Validate(request), error => error.MemberNames.Contains("OnboardingType"));
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
