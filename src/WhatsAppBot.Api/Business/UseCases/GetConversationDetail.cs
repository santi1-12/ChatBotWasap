using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class GetConversationDetail(
    IWhatsAppIntegrationRepository integrationRepository,
    IMessageRepository messageRepository,
    ITenantContext tenantContext,
    IConversationAuditRepository? auditRepository = null) : IGetConversationDetail
{
    public const int MessagePageSize = 50;

    public async Task<ConversationDetailRecord> ExecuteAsync(
        long conversationId, long? beforeMessageId, CancellationToken cancellationToken)
    {
        if (conversationId < 1)
        {
            throw new BusinessValidationException("Conversation id must be positive.");
        }

        if (beforeMessageId is <= 0)
        {
            throw new BusinessValidationException("beforeMessageId must be positive when supplied.");
        }

        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var integration = await integrationRepository.GetCurrentAsync(cancellationToken);
        var detail = await messageRepository.GetHistoryAsync(
                   tenant.TenantId,
                   integration.IntegrationId,
                   conversationId,
                   beforeMessageId,
                   MessagePageSize,
                   cancellationToken)
               ?? throw new BusinessNotFoundException("Conversation");

        if (auditRepository is not null)
        {
            detail = detail with
            {
                AuditEvents = await auditRepository.ListAuditAsync(
                    tenant.TenantId, conversationId, cancellationToken)
            };
        }

        return detail;
    }
}
