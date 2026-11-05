using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Api.Models.Requests;
using WhatsAppBot.Api.Models.Responses;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.Abstractions;

public interface IAdministratorRepository
{
    Task<Administrator?> GetForAuthenticationAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task UpdateLastLoginAsync(long adminId, CancellationToken cancellationToken);
    Task<bool> CreateFirstAdministratorAsync(
        string normalizedEmail,
        string passwordHash,
        string tenantLegalName,
        CancellationToken cancellationToken);
}

public interface IAccessTokenIssuer
{
    IssuedAccessToken Create(long administratorId, string normalizedEmail, Guid? activeTenantId = null);
}

public interface IAdministratorAuthenticator
{
    Task<IssuedAccessToken?> AuthenticateAsync(
        AdministratorCredentials credentials, CancellationToken cancellationToken);
}

public interface IAdministratorBootstrapper
{
    Task<bool> CreateFirstAdministratorAsync(
        string email,
        string password,
        string tenantLegalName,
        CancellationToken cancellationToken);
}

public interface IBotConfigurationRepository
{
    Task<BotConfiguration> GetCurrentAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<BotConfiguration> GetForIntegrationAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken);
    Task<BotConfiguration> UpdateAsync(
        Guid tenantId, Guid integrationId, bool isBotEnabled, BotReplyMode replyMode,
        CancellationToken cancellationToken);
}

public interface IBotConfigurationService
{
    Task<BotConfiguration> GetAsync(CancellationToken cancellationToken);
    Task<BotConfiguration> GetForIntegrationAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken);
    Task<BotConfiguration> UpdateAsync(
        bool isBotEnabled, BotReplyMode replyMode, CancellationToken cancellationToken);
}

public interface IFrequentResponseRepository
{
    Task<FrequentResponsePage> ListAsync(
        Guid tenantId, Guid integrationId, int page, int pageSize, CancellationToken cancellationToken);
    Task<FrequentResponseRecord?> GetAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, CancellationToken cancellationToken);
    Task<FrequentResponseRecord> CreateAsync(
        Guid tenantId, Guid integrationId, FrequentResponseCommand command, CancellationToken cancellationToken);
    Task<FrequentResponseRecord?> UpdateAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, FrequentResponseCommand command,
        CancellationToken cancellationToken);
    Task<bool> SetActiveAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, bool isActive,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(
        Guid tenantId, Guid integrationId, long frequentResponseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FrequentResponseRecord>> ListActiveCandidatesAsync(
        Guid tenantId, Guid integrationId, CancellationToken cancellationToken);
}

public interface IFrequentResponseService
{
    Task<FrequentResponsePage> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<FrequentResponseRecord> GetAsync(long frequentResponseId, CancellationToken cancellationToken);
    Task<FrequentResponseRecord> CreateAsync(
        FrequentResponseCommand command, CancellationToken cancellationToken);
    Task<FrequentResponseRecord> UpdateAsync(
        long frequentResponseId, FrequentResponseCommand command, CancellationToken cancellationToken);
    Task<bool> SetActiveAsync(long frequentResponseId, bool isActive, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(long frequentResponseId, CancellationToken cancellationToken);
}

public interface IBotReplyResolver
{
    Task<BotReplyResolution> ResolveAsync(
        BotConfiguration configuration, string message, CancellationToken cancellationToken);
}

public interface IGetMessageMedia
{
    Task<MediaAccessResult> ExecuteAsync(long messageId, CancellationToken cancellationToken);
}
