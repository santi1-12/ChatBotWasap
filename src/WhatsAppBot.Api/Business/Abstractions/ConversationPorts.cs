using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Requests;

namespace WhatsAppBot.Api.Business.Abstractions;

public interface IListConversations
{
    Task<ConversationPage> ExecuteAsync(
        int page, int pageSize, CancellationToken cancellationToken);
}

public interface IGetConversationDetail
{
    Task<ConversationDetailRecord> ExecuteAsync(
        long conversationId, long? beforeMessageId, CancellationToken cancellationToken);
}

public interface IApplyConversationAction
{
    Task ApplyAsync(
        long conversationId,
        ConversationActionCommand command,
        CancellationToken cancellationToken);
}

public interface IConversationActionRepository
{
    Task<ConversationActionApplyResult> ApplyAsync(
        Guid tenantId,
        long conversationId,
        ConversationActionCommand command,
        long actorAdminId,
        CancellationToken cancellationToken);

    Task<bool> SetAssignmentAsync(
        Guid tenantId,
        long conversationId,
        long? assignedAdminId,
        string expectedVersion,
        long actorAdminId,
        CancellationToken cancellationToken);

    Task RecordAuditAsync(
        Guid tenantId,
        long conversationId,
        ConversationAuditEventCommand command,
        CancellationToken cancellationToken);
}
