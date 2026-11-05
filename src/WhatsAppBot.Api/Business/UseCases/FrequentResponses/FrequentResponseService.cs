using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.UseCases.FrequentResponses;

public sealed class FrequentResponseService(
    IBotConfigurationRepository botConfigurationRepository,
    IFrequentResponseRepository repository,
    ITenantContext tenantContext) : IFrequentResponseService
{
    public async Task<FrequentResponsePage> ListAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var configuration = await botConfigurationRepository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        return await repository.ListAsync(
            tenant.TenantId, configuration.IntegrationId, page, pageSize, cancellationToken);
    }

    public async Task<FrequentResponseRecord> GetAsync(
        long frequentResponseId, CancellationToken cancellationToken)
    {
        ValidateId(frequentResponseId);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var configuration = await botConfigurationRepository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        return await repository.GetAsync(
                tenant.TenantId, configuration.IntegrationId, frequentResponseId, cancellationToken)
            ?? throw new BusinessNotFoundException("Frequent response");
    }

    public async Task<FrequentResponseRecord> CreateAsync(
        FrequentResponseCommand command, CancellationToken cancellationToken)
    {
        var validCommand = Validate(command);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var configuration = await botConfigurationRepository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        return await repository.CreateAsync(
            tenant.TenantId, configuration.IntegrationId, validCommand, cancellationToken);
    }

    public async Task<FrequentResponseRecord> UpdateAsync(
        long frequentResponseId, FrequentResponseCommand command, CancellationToken cancellationToken)
    {
        ValidateId(frequentResponseId);
        var validCommand = Validate(command);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var configuration = await botConfigurationRepository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        return await repository.UpdateAsync(
                tenant.TenantId, configuration.IntegrationId, frequentResponseId, validCommand, cancellationToken)
            ?? throw new BusinessNotFoundException("Frequent response");
    }

    public async Task<bool> SetActiveAsync(
        long frequentResponseId, bool isActive, CancellationToken cancellationToken)
    {
        ValidateId(frequentResponseId);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var configuration = await botConfigurationRepository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        if (!await repository.SetActiveAsync(
                tenant.TenantId, configuration.IntegrationId, frequentResponseId, isActive, cancellationToken))
        {
            throw new BusinessNotFoundException("Frequent response");
        }

        return true;
    }

    public async Task<bool> DeleteAsync(long frequentResponseId, CancellationToken cancellationToken)
    {
        ValidateId(frequentResponseId);
        var tenant = await tenantContext.GetRequiredAsync(cancellationToken);
        var configuration = await botConfigurationRepository.GetCurrentAsync(tenant.TenantId, cancellationToken);
        if (!await repository.DeleteAsync(
                tenant.TenantId, configuration.IntegrationId, frequentResponseId, cancellationToken))
        {
            throw new BusinessNotFoundException("Frequent response");
        }

        return true;
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new BusinessValidationException("Page must be positive and pageSize must be between 1 and 100.");
        }
    }

    private static void ValidateId(long id)
    {
        if (id < 1)
        {
            throw new BusinessValidationException("Frequent response id must be positive.");
        }
    }

    private static FrequentResponseCommand Validate(FrequentResponseCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.QuestionOrIntent))
        {
            throw new BusinessValidationException("Question or intent is required.");
        }
        if (string.IsNullOrWhiteSpace(command.AnswerText))
        {
            throw new BusinessValidationException("Answer text is required.");
        }
        if (command.Expressions is null)
        {
            throw new BusinessValidationException("At least one non-empty expression is required.");
        }
        if (command.Expressions.Any(string.IsNullOrWhiteSpace))
        {
            throw new BusinessValidationException("Expressions must contain only non-empty values.");
        }

        var question = command.QuestionOrIntent.Trim();
        var answer = command.AnswerText.Trim();
        var category = string.IsNullOrWhiteSpace(command.Category) ? null : command.Category.Trim();
        var expressions = command.Expressions
            .Select(expression => expression.Trim())
            .ToArray();

        if (question.Length is < 1 or > 500)
        {
            throw new BusinessValidationException("Question or intent must contain 1 to 500 characters.");
        }
        if (command.Expressions.Count == 0 || expressions.Length == 0)
        {
            throw new BusinessValidationException("At least one non-empty expression is required.");
        }
        if (expressions.Any(expression => expression.Length > 500))
        {
            throw new BusinessValidationException("Expressions must not exceed 500 characters.");
        }
        if (expressions.Select(FrequentResponseMatcher.Normalize).Distinct(StringComparer.Ordinal).Count()
            != expressions.Length)
        {
            throw new BusinessValidationException("Expressions must be unique after normalization.");
        }
        if (command.Priority is < 0 or > 1000)
        {
            throw new BusinessValidationException("Priority must be between 0 and 1000.");
        }
        if (category?.Length > 100)
        {
            throw new BusinessValidationException("Category must not exceed 100 characters.");
        }

        return command with
        {
            QuestionOrIntent = question,
            Expressions = expressions,
            AnswerText = answer,
            Category = category
        };
    }
}
