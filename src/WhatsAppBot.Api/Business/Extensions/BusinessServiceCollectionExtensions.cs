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
        services.AddSingleton<FrequentResponseMatcher>();
        services.AddScoped<IBotReplyResolver, BotReplyResolver>();
        services.AddScoped<IWhatsAppIntegrationService, WhatsAppIntegrationService>();
        services.AddScoped<IAcceptMetaWebhookEvent, AcceptMetaWebhookEvent>();
        services.AddScoped<IProcessInboundText, ProcessInboundText>();
        services.AddScoped<IProcessInboundMedia, PreserveInboundMedia>();
        services.AddScoped<IProcessMetaDeliveryStatus, PreserveMetaDeliveryStatus>();
        services.AddScoped<MessageProcessingService>();
        services.AddHostedService<WebhookInboxWorker>();
        services.AddHostedService<MessageOutboxWorker>();
        services.AddSingleton<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
        return services;
    }
}
