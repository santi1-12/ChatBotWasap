using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class BotConfigurationService(
    IBotConfigurationRepository repository,
    ITenantContext tenantContext) : IBotConfigurationService
{
    public async Task<BotConfiguration> GetAsync(CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        return await repository.GetCurrentAsync(tenant.TenantId, cancellationToken);
    }

    public Task<BotConfiguration> GetForIntegrationAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken) =>
        repository.GetForIntegrationAsync(tenantId, integrationId, cancellationToken);

    public async Task<BotConfiguration> UpdateAsync(
        bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken)
    {
        if (!ReplyModeRules.IsSupported(replyMode))
        {
            throw new BusinessValidationException("The selected reply mode is not supported.");
        }

        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var current = await repository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        return await repository.UpdateAsync(
            tenant.TenantId, current.IntegrationId, isBotEnabled, replyMode, cancellationToken);
    }
}
