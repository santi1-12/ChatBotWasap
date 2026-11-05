using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Persistence.Repositories;

namespace WhatsAppBot.Api.ContractTests;

public sealed class ConversationCompositionTests
{
    [Fact]
    public void ConversationRepository_ExposesAuditRepositoryPort()
    {
        Assert.True(typeof(IConversationAuditRepository).IsAssignableFrom(typeof(ConversationRepository)));
    }
}
