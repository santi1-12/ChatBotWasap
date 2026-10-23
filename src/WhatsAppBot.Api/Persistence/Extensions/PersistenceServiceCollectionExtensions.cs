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
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageOutboxRepository, MessageOutboxRepository>();
        return services;
    }
}
