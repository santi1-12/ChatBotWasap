using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.UseCases;

public sealed class BotReplyResolver(
    IFrequentResponseRepository repository,
    FrequentResponseMatcher matcher) : IBotReplyResolver
{
    public async Task<BotReplyResolution> ResolveAsync(
        BotConfiguration configuration, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(message);

        if (!configuration.IsBotEnabled)
        {
            return new BotReplyResolution(BotReplyResolutionKind.NoReply);
        }

        if (configuration.ReplyMode == BotReplyMode.AiOnly)
        {
            return new BotReplyResolution(BotReplyResolutionKind.AiFallbackRequired, FaqEvaluationSkipped: true);
        }

        var candidates = await repository.ListActiveCandidatesAsync(
            configuration.TenantId
                ?? throw new InvalidOperationException("Bot configuration is not assigned to a Tenant."),
            configuration.IntegrationId,
            cancellationToken);
        var match = matcher.Match(message, candidates);
        if (match is not null)
        {
            return new BotReplyResolution(BotReplyResolutionKind.FrequentResponse, match);
        }

        return configuration.ReplyMode == BotReplyMode.FaqOnly
            ? new BotReplyResolution(BotReplyResolutionKind.NoReply)
            : new BotReplyResolution(BotReplyResolutionKind.AiFallbackRequired);
    }
}
