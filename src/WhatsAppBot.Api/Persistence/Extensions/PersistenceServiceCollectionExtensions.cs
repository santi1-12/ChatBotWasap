using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Persistence.Repositories;

namespace WhatsAppBot.Api.Persistence.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<StoredProcedureExecutor>();
        services.AddScoped<IAdministratorRepository, AdminRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IBotConfigurationRepository, BotConfigurationRepository>();
        services.AddScoped<IFrequentResponseRepository, FrequentResponseRepository>();
        services.AddScoped<IWhatsAppIntegrationRepository, WhatsAppIntegrationRepository>();
        services.AddScoped<IWebhookInboxRepository, WebhookInboxRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationActionRepository>(provider =>
            provider.GetRequiredService<IConversationRepository>() as IConversationActionRepository
            ?? throw new InvalidOperationException("Conversation repository does not expose actions."));
        services.AddScoped<IConversationStateRepository>(provider =>
            provider.GetRequiredService<IConversationRepository>() as IConversationStateRepository
            ?? throw new InvalidOperationException("Conversation repository does not expose state queries."));
        services.AddScoped<IConversationAuditRepository>(provider =>
            provider.GetRequiredService<IConversationRepository>() as IConversationAuditRepository
            ?? throw new InvalidOperationException("Conversation repository does not expose audit queries."));
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageOutboxRepository, MessageOutboxRepository>();
        return services;
    }
}
