using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class SendManualConversationMessage(
    IMessageOutboxRepository outboxRepository,
    ITenantContext tenantContext) : ISendManualConversationMessage
{
    public async Task<ManualConversationMessageResult> ExecuteAsync(
        long conversationId,
        string text,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        if (conversationId < 1 || string.IsNullOrWhiteSpace(text) || text.Trim().Length > 4096)
        {
            throw new BusinessValidationException("A conversation and non-empty manual message are required.");
        }

        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var result = await outboxRepository.CreateAdminPendingAsync(
            tenant.TenantId,
            conversationId,
            tenant.AdministratorId,
            text.Trim(),
            expectedVersion,
            cancellationToken);

        if (!result.Found)
        {
            throw new BusinessNotFoundException("Conversation");
        }

        if (result.Conflict)
        {
            throw new ConversationConflictException();
        }

        if (!result.Inserted || result.MessageId is null || result.CreatedAtUtc is null)
        {
            throw new ManualReplyRejectedException(
                result.OutcomeCode ?? MessageOutcomeCodes.ManualSendFailed);
        }

        return new ManualConversationMessageResult(
            tenant.TenantId,
            result.MessageId.Value,
            conversationId,
            text.Trim(),
            tenant.AdministratorId,
            result.CreatedAtUtc.Value,
            ProcessingState.Processing,
            DeliveryState.Pending);
    }
}
