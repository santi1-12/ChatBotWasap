using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Api.Business.UseCases.FrequentResponses;
using WhatsAppBot.Api.Business.Workers;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Models.Entities;

namespace WhatsAppBot.Api.Business.Extensions;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<CompletionExecutionContext>();
        services.AddScoped<CompletionPhaseLogger>();
        services.AddScoped<IAdministratorAuthenticator, AuthenticateAdministrator>();
        services.AddScoped<IAdministratorBootstrapper, BootstrapAdministrator>();
        services.AddScoped<IBotConfigurationService, BotConfigurationService>();
        services.AddScoped<IFrequentResponseService, FrequentResponseService>();
        services.AddScoped<IListConversations, ListConversations>();
        services.AddScoped<IGetConversationDetail, GetConversationDetail>();
        services.AddScoped<IApplyConversationAction, ApplyConversationAction>();
        services.AddScoped<IConversationHandoffService, ConversationHandoffService>();
        services.AddScoped<ISendManualConversationMessage, SendManualConversationMessage>();
        services.AddSingleton<FrequentResponseMatcher>();
        services.AddSingleton<AiContextBuilder>();
        services.AddScoped<IBotReplyResolver, BotReplyResolver>();
        services.AddScoped<IWhatsAppIntegrationService, WhatsAppIntegrationService>();
        services.AddScoped<IAcceptMetaWebhookEvent, AcceptMetaWebhookEvent>();
        services.AddScoped<IProcessInboundText, ProcessInboundText>();
        services.AddScoped<InboundMediaIngestionService>();
        services.AddScoped<IProcessInboundAudio, ProcessInboundAudio>();
        services.AddScoped<IProcessInboundImage, ProcessInboundImage>();
        services.AddScoped<IProcessInboundMedia, ProcessInboundMedia>();
        services.AddScoped<IGetMessageMedia, GetMessageMedia>();
        services.AddScoped<IProcessMetaDeliveryStatus, ApplyMessageStatus>();
        services.AddScoped<MessageProcessingService>();
        services.AddHostedService<WebhookInboxWorker>();
        services.AddHostedService<MessageOutboxWorker>();
        services.AddHostedService<RetentionCleanupWorker>();
        services.AddSingleton<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
        return services;
    }
}
