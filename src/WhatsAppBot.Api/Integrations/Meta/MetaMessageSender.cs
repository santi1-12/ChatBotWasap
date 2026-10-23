using System.Text.Json;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Integrations.Meta;

public sealed class MetaMessageSender(
    IWhatsAppIntegrationRepository integrationRepository,
    ISecretReferenceResolver secretResolver,
    IMetaCloudApiClient cloudApiClient) : IMetaWhatsAppClient
{
    public async Task<MetaMessageSendResult> SendTextAsync(
        string phoneNumberId, string recipient, string text, CancellationToken cancellationToken)
    {
        try
        {
            var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
            if (!integration.IsActive ||
                integration.ConnectionState != ConnectionState.Connected ||
                !string.Equals(integration.PhoneNumberId, phoneNumberId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(integration.AccessTokenSecretRef))
            {
                return new MetaMessageSendResult(false, null, "MetaCredentialsMissing", false);
            }

            var accessToken = await secretResolver.ResolveAsync(integration.AccessTokenSecretRef, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new MetaMessageSendResult(false, null, "MetaCredentialsMissing", false);
            }

            return await cloudApiClient.SendTextAsync(
                accessToken, phoneNumberId, recipient, text, cancellationToken);
        }
        catch (MetaApiException exception)
        {
            return new MetaMessageSendResult(false, null, exception.SafeCode, exception.IsTransient);
        }
        catch (MetaConfigurationException)
        {
            return new MetaMessageSendResult(false, null, "MetaConfigurationMissing", false);
        }
        catch (JsonException)
        {
            return new MetaMessageSendResult(false, null, "MetaInvalidResponse", false);
        }
        catch (HttpRequestException)
        {
            return new MetaMessageSendResult(false, null, "MetaUnavailable", true);
        }
        catch (InvalidOperationException)
        {
            return new MetaMessageSendResult(false, null, "MetaSecretStoreUnavailable", true);
        }
        catch (UnauthorizedAccessException)
        {
            return new MetaMessageSendResult(false, null, "MetaSecretStoreUnavailable", true);
        }
        catch (IOException)
        {
            return new MetaMessageSendResult(false, null, "MetaSecretStoreUnavailable", true);
        }
    }
}
