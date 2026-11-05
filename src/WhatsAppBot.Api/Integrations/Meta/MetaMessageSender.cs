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
        Guid tenantId,
        Guid integrationId,
        string phoneNumberId,
        string recipient,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var integration = await integrationRepository.GetByPhoneNumberIdAsync(
                tenantId, integrationId, phoneNumberId, cancellationToken);
            if (!integration.IsActive ||
                integration.ConnectionState != ConnectionState.Connected ||
                !string.Equals(integration.PhoneNumberId, phoneNumberId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(integration.AccessTokenSecretRef))
            {
                return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaCredentialsMissing, false);
            }

            var accessToken = await secretResolver.ResolveAsync(integration.AccessTokenSecretRef, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaCredentialsMissing, false);
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
            return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaConfigurationMissing, false);
        }
        catch (JsonException)
        {
            return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaInvalidResponse, false);
        }
        catch (HttpRequestException)
        {
            return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaUnavailable, true);
        }
        catch (InvalidOperationException)
        {
            return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaSecretStoreUnavailable, true);
        }
        catch (UnauthorizedAccessException)
        {
            return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaSecretStoreUnavailable, true);
        }
        catch (IOException)
        {
            return new MetaMessageSendResult(false, null, MessageOutcomeCodes.MetaSecretStoreUnavailable, true);
        }
    }
}
