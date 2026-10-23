using WhatsAppBot.Api.Models.Contracts;

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
