using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class ProcessInboundText(
    IWhatsAppIntegrationRepository integrationRepository,
    IContactRepository contactRepository,
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IBotConfigurationService botConfigurationService,
    IBotReplyResolver replyResolver,
    IMessageOutboxRepository outboxRepository,
    IWhatsAppMessagingPolicy messagingPolicy,
    IClock clock,
    IConversationHandoffService handoffService) : IProcessInboundText
{
    public async Task ProcessAsync(
        NormalizedMetaEvent inboundEvent, CancellationToken cancellationToken)
    {
        if (inboundEvent.EventType != MetaWebhookEventTypes.InboundText ||
            string.IsNullOrWhiteSpace(inboundEvent.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(inboundEvent.WhatsAppUserId) ||
            string.IsNullOrWhiteSpace(inboundEvent.ProviderMessageId) ||
            string.IsNullOrWhiteSpace(inboundEvent.Text) ||
            inboundEvent.ProviderTimestampUtc is null)
        {
            throw new BusinessValidationException("Only complete inbound text events can be processed.");
        }

        var integration = await integrationRepository.GetByPhoneNumberIdAsync(
            inboundEvent.PhoneNumberId, cancellationToken);
        if (!integration.IsActive ||
            integration.ConnectionState != ConnectionState.Connected ||
            !string.Equals(integration.PhoneNumberId, inboundEvent.PhoneNumberId, StringComparison.Ordinal))
        {
            return;
        }

        var tenantId = integration.TenantId
            ?? throw new InvalidOperationException("The WhatsApp integration is not assigned to a Tenant.");
        var contactId = await contactRepository.UpsertWhatsAppAsync(
            integration.IntegrationId,
            tenantId,
            inboundEvent.WhatsAppUserId,
            inboundEvent.ContactDisplayName,
            cancellationToken);
        var configuration = await botConfigurationService.GetForIntegrationAsync(
            integration.IntegrationId, cancellationToken);
        var conversationId = await conversationRepository.GetOrCreateAsync(
            integration.IntegrationId,
            tenantId,
            contactId,
            inboundEvent.ProviderTimestampUtc.Value,
            configuration.IsBotEnabled,
            cancellationToken);

        var inboundMessage = await messageRepository.InsertInboundAsync(
            integration.IntegrationId, conversationId, inboundEvent, cancellationToken);

        if (!configuration.IsBotEnabled)
        {
            await handoffService.TryHandoffAsync(
                tenantId, conversationId,
                ConversationHandoffReasonCodes.BusinessRule, cancellationToken);
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId, ProcessingState.NoReply, MessageOutcomeCodes.BotDisabled, cancellationToken);
            return;
        }

        if (IsCustomerRequestingHuman(inboundEvent.Text))
        {
            await handoffService.TryHandoffAsync(
                tenantId, conversationId,
                ConversationHandoffReasonCodes.CustomerRequestedHuman, cancellationToken);
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId, ProcessingState.NoReply,
                MessageOutcomeCodes.ConversationInHumanMode, cancellationToken);
            return;
        }

        BotReplyResolution resolution;
        try
        {
            resolution = await replyResolver.ResolveAsync(
                configuration, inboundEvent.Text, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await handoffService.TryHandoffAsync(
                tenantId, conversationId,
                ConversationHandoffReasonCodes.BlockingError, cancellationToken);
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId, ProcessingState.NoReply,
                MessageOutcomeCodes.BlockingError, cancellationToken);
            return;
        }

        if (resolution.Kind == BotReplyResolutionKind.NoReply)
        {
            await handoffService.TryHandoffAsync(
                tenantId, conversationId,
                ConversationHandoffReasonCodes.BotUnableToResolve, cancellationToken);
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId,
                ProcessingState.NoReply,
                MessageOutcomeCodes.NoFrequentResponse,
                cancellationToken);
            return;
        }

        if (resolution.Kind == BotReplyResolutionKind.AiFallbackRequired)
        {
            await handoffService.TryHandoffAsync(
                tenantId, conversationId,
                ConversationHandoffReasonCodes.BotUnableToResolve, cancellationToken);
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId,
                ProcessingState.NoReply,
                MessageOutcomeCodes.AiFallbackNotConfigured,
                cancellationToken);
            return;
        }

        var response = resolution.FrequentResponse
            ?? throw new InvalidOperationException("The FAQ matcher returned a response without its source.");
        var answer = response.Response.AnswerText;
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("The FAQ matcher returned an empty answer candidate.");
        }

        var policy = messagingPolicy.Evaluate(inboundEvent.ProviderTimestampUtc.Value, clock.UtcNow);
        if (!policy.IsAllowed)
        {
            await handoffService.TryHandoffAsync(
                tenantId, conversationId,
                ConversationHandoffReasonCodes.BusinessRule, cancellationToken);
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId, ProcessingState.NoReply, policy.OutcomeCode, cancellationToken);
            return;
        }

        var outbound = await outboxRepository.CreatePendingAsync(
            inboundMessage.MessageId,
            integration.IntegrationId,
            conversationId,
            answer,
            ReplySource.FrequentResponse,
            response.Response.FrequentResponseId,
            cancellationToken);
        if (!outbound.Inserted && outbound.MessageId is null)
        {
            await messageRepository.SetProcessingOutcomeAsync(
                inboundMessage.MessageId,
                ProcessingState.NoReply,
                outbound.OutcomeCode ?? MessageOutcomeCodes.MessagingWindowClosed,
                cancellationToken);
        }
    }

    private static bool IsCustomerRequestingHuman(string text)
    {
        var normalized = text.Trim().ToUpperInvariant();
        return normalized.Contains("HUMANO", StringComparison.Ordinal) ||
               normalized.Contains("AGENTE", StringComparison.Ordinal) ||
               normalized.Contains("ASESOR", StringComparison.Ordinal) ||
               normalized.Contains("PERSONA", StringComparison.Ordinal) ||
               normalized.Contains("REPRESENTANTE", StringComparison.Ordinal) ||
               normalized.Contains("SOPORTE", StringComparison.Ordinal);
    }
}
