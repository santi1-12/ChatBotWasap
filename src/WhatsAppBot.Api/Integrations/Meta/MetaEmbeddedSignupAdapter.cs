using Microsoft.Extensions.Options;
using System.Text.Json;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Integrations.Meta;

public sealed class MetaEmbeddedSignupAdapter(
    IMetaCloudApiClient metaClient,
    ISecretStore secretStore,
    IWhatsAppIntegrationRepository integrationRepository,
    IOptions<MetaOptions> options,
    CompletionPhaseLogger phaseLogger,
    CompletionExecutionContext executionContext) : IMetaEmbeddedSignupAdapter
{
    public async Task<MetaSignupResult> CompleteAsync(
        string authorizationCode,
        string wabaId,
        string? phoneNumberId,
        WhatsAppOnboardingType onboardingType,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = options.Value;
            var signupConfigId = onboardingType switch
            {
                WhatsAppOnboardingType.Standard => settings.EmbeddedSignupConfigId,
                WhatsAppOnboardingType.Coexistence =>
                    settings.EffectiveCoexistenceEmbeddedSignupConfigId,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(settings.AppId) ||
                string.IsNullOrWhiteSpace(settings.AppSecret) ||
                string.IsNullOrWhiteSpace(signupConfigId) ||
                string.IsNullOrWhiteSpace(settings.VerifyToken))
            {
                phaseLogger.LogFailure("CompletionConfiguration", "MetaConfigurationMissing");
                throw new MetaConfigurationException("Meta Embedded Signup server configuration is incomplete.");
            }

            if (string.IsNullOrWhiteSpace(authorizationCode) ||
                string.IsNullOrWhiteSpace(wabaId) ||
                (phoneNumberId is not null && string.IsNullOrWhiteSpace(phoneNumberId)))
            {
                throw new MetaConfigurationException("Meta Embedded Signup returned incomplete asset identifiers.");
            }

            executionContext.AuthorizationCodeAttempted = true;
            var token = await phaseLogger.RunAsync(
                "OAuthCodeExchange",
                () => metaClient.ExchangeAuthorizationCodeAsync(authorizationCode, cancellationToken));
            if (string.IsNullOrWhiteSpace(phoneNumberId))
            {
                phoneNumberId = await phaseLogger.RunAsync("PhoneNumberDiscovery", async () =>
                {
                    var numbers = await metaClient.GetPhoneNumbersAsync(token, wabaId, cancellationToken);
                    return numbers.Count switch
                    {
                        0 => throw new ExternalDependencyException("MetaPhoneNumberNotFound"),
                        1 => numbers[0].PhoneNumberId,
                        _ => throw new ExternalDependencyException("MetaPhoneNumberSelectionAmbiguous")
                    };
                });
            }
            else
            {
                phaseLogger.LogSkipped("PhoneNumberDiscovery");
            }

            await phaseLogger.RunAsync("PhoneNumberValidation", async () =>
            {
                if (!await metaClient.PhoneNumberBelongsToWabaAsync(
                        token, wabaId, phoneNumberId, cancellationToken))
                {
                    throw new ExternalDependencyException("MetaPhoneNumberNotInWaba");
                }
            });

            var phone = await phaseLogger.RunAsync(
                "PhoneNumberDetails",
                () => metaClient.GetPhoneNumberAsync(token, phoneNumberId, cancellationToken));
            if (onboardingType == WhatsAppOnboardingType.Standard)
            {
                await phaseLogger.RunAsync("StandardPhoneRegistration", async () =>
                {
                    executionContext.ExternalMutationMayHaveOccurred = true;
                    await metaClient.RegisterPhoneNumberAsync(token, phoneNumberId, cancellationToken);
                });
            }
            else
            {
                phaseLogger.LogSkipped("StandardPhoneRegistration");
            }

            await phaseLogger.RunAsync("WabaSubscription", async () =>
            {
                executionContext.ExternalMutationMayHaveOccurred = true;
                await metaClient.SubscribeWabaAsync(token, wabaId, cancellationToken);
            });

            var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
            var tokenReference = await phaseLogger.RunAsync(
                "SecretStoreWrite",
                async () =>
                {
                    try
                    {
                        return await secretStore.StoreAsync(
                            "whatsapp-business-access-token", token, cancellationToken);
                    }
                    catch (InvalidOperationException)
                    {
                        throw new ExternalDependencyException("SecretStoreUnavailable");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new ExternalDependencyException("SecretStoreUnavailable");
                    }
                    catch (IOException)
                    {
                        throw new ExternalDependencyException("SecretStoreUnavailable");
                    }
                });
            return new MetaSignupResult(
                integration.IntegrationId,
                wabaId,
                phoneNumberId,
                phone.DisplayPhoneNumber,
                phone.VerifiedName,
                tokenReference,
                "Meta:AppSecret",
                "Meta:VerifyToken");
        }
        catch (MetaApiException exception)
        {
            throw new ExternalDependencyException(exception.SafeCode, exception.IsTransient);
        }
        catch (MetaConfigurationException)
        {
            throw new ExternalDependencyException("MetaConfigurationMissing");
        }
        catch (JsonException)
        {
            throw new ExternalDependencyException("MetaInvalidResponse");
        }
    }
}
