using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ProcessCoexistenceMessage(
    IWhatsAppIntegrationRepository integrationRepository,
    IContactRepository contactRepository,
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IBotConfigurationService botConfigurationService) : IProcessCoexistenceMessage
{
    public async Task<MetaEventProcessingResult> ProcessAsync(
        NormalizedMetaEvent coexistenceEvent, CancellationToken cancellationToken)
    {
        if (coexistenceEvent.EventType is not (
                MetaWebhookEventTypes.HistoricalMessage or MetaWebhookEventTypes.OutboundAppMessage) ||
            (coexistenceEvent.EventType == MetaWebhookEventTypes.HistoricalMessage && !coexistenceEvent.IsHistorical) ||
            (coexistenceEvent.EventType == MetaWebhookEventTypes.OutboundAppMessage && coexistenceEvent.IsHistorical) ||
            string.IsNullOrWhiteSpace(coexistenceEvent.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(coexistenceEvent.WhatsAppUserId) ||
            string.IsNullOrWhiteSpace(coexistenceEvent.SourceEventKey) ||
            coexistenceEvent.ProviderTimestampUtc is null)
        {
            throw new BusinessValidationException("Only complete Coexistence message events can be processed.");
        }

        var tenantId = coexistenceEvent.TenantId
            ?? throw new BusinessValidationException("The claimed Coexistence event has no TenantId.");
        var integrationId = coexistenceEvent.IntegrationId
            ?? throw new BusinessValidationException("The claimed Coexistence event has no IntegrationId.");
        var integration = await integrationRepository.GetByPhoneNumberIdAsync(
            tenantId, integrationId, coexistenceEvent.PhoneNumberId, cancellationToken);
        if (!integration.IsActive ||
            integration.ConnectionState != ConnectionState.Connected ||
            !string.Equals(integration.PhoneNumberId, coexistenceEvent.PhoneNumberId, StringComparison.Ordinal))
        {
            return coexistenceEvent.IsHistorical
                ? new MetaEventProcessingResult(HistoryImportOutcomes.Skipped)
                : MetaEventProcessingResult.None;
        }

        if (integration.OnboardingType != WhatsAppOnboardingType.Coexistence)
        {
            return coexistenceEvent.IsHistorical
                ? new MetaEventProcessingResult(HistoryImportOutcomes.Skipped)
                : MetaEventProcessingResult.None;
        }

        if (coexistenceEvent.IsHistorical &&
            !integration.HistoryImportRequested)
        {
            return new MetaEventProcessingResult(HistoryImportOutcomes.Skipped);
        }

        if (coexistenceEvent.IsGroup)
        {
            return coexistenceEvent.IsHistorical
                ? new MetaEventProcessingResult(HistoryImportOutcomes.Skipped)
                : MetaEventProcessingResult.None;
        }

        var contactId = await contactRepository.UpsertWhatsAppAsync(
            integration.IntegrationId,
            tenantId,
            coexistenceEvent.WhatsAppUserId,
            coexistenceEvent.ContactDisplayName,
            cancellationToken);
        var configuration = await botConfigurationService.GetForIntegrationAsync(
            tenantId, integration.IntegrationId, cancellationToken);
        var conversationId = await conversationRepository.GetOrCreateCoexistenceAsync(
            integration.IntegrationId,
            tenantId,
            contactId,
            coexistenceEvent.ProviderTimestampUtc.Value,
            configuration.IsBotEnabled,
            coexistenceEvent.EventType == MetaWebhookEventTypes.OutboundAppMessage,
            cancellationToken);

        var message = await messageRepository.InsertCoexistenceAsync(
            tenantId,
            integration.IntegrationId,
            conversationId,
            coexistenceEvent,
            cancellationToken);
        if (!coexistenceEvent.IsHistorical)
        {
            return MetaEventProcessingResult.None;
        }

        return new MetaEventProcessingResult(
            message.Inserted ? HistoryImportOutcomes.Imported : HistoryImportOutcomes.Duplicate);
    }
}
