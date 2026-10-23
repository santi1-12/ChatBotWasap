using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class BotConfigurationService(IBotConfigurationRepository repository) : IBotConfigurationService
{
    public Task<BotConfiguration> GetAsync(CancellationToken cancellationToken) =>
        repository.GetCurrentAsync(cancellationToken);

    public Task<BotConfiguration> GetForIntegrationAsync(
        Guid integrationId, CancellationToken cancellationToken) =>
        repository.GetForIntegrationAsync(integrationId, cancellationToken);

    public async Task<BotConfiguration> UpdateAsync(
        bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken)
    {
        if (!ReplyModeRules.IsSupported(replyMode))
        {
            throw new BusinessValidationException("The selected reply mode is not supported.");
        }

        var current = await repository.GetCurrentAsync(cancellationToken);
        return await repository.UpdateAsync(current.IntegrationId, isBotEnabled, replyMode, cancellationToken);
    }
}
