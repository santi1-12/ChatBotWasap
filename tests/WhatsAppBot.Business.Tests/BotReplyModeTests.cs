using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Business.Services;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Api.Business.UseCases;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Business.Tests;

public sealed class BotReplyModeTests
{
    [Theory]
    [InlineData(BotReplyMode.FaqOnly)]
    [InlineData(BotReplyMode.FaqThenAi)]
    public async Task FaqModes_UseAnActiveMatchForOriginalTextAndTranscription(BotReplyMode mode)
    {
        var response = Candidate(1, "horario", "Atendemos de 9 a 5");
        var repository = new FakeFrequentResponseRepository([response]);
        var resolver = new BotReplyResolver(repository, new FrequentResponseMatcher());

        var textResult = await resolver.ResolveAsync(Configuration(mode), "horario", CancellationToken.None);
        var transcriptionResult = await resolver.ResolveAsync(Configuration(mode), "horario", CancellationToken.None);

        Assert.Equal(BotReplyResolutionKind.FrequentResponse, textResult.Kind);
        Assert.Equal(textResult.Kind, transcriptionResult.Kind);
        Assert.Equal(response, transcriptionResult.FrequentResponse);
    }

    [Fact]
    public async Task FaqOnly_MissNeverRequestsAiFallback()
    {
        var resolver = new BotReplyResolver(new FakeFrequentResponseRepository([]), new FrequentResponseMatcher());

        var result = await resolver.ResolveAsync(
            Configuration(BotReplyMode.FaqOnly), "unknown question", CancellationToken.None);

        Assert.Equal(BotReplyResolutionKind.NoReply, result.Kind);
    }

    [Theory]
    [InlineData(BotReplyMode.FaqOnly, BotReplyResolutionKind.NoReply)]
    [InlineData(BotReplyMode.FaqThenAi, BotReplyResolutionKind.AiFallbackRequired)]
    [InlineData(BotReplyMode.AiOnly, BotReplyResolutionKind.AiFallbackRequired)]
    public async Task TextAndTranscriptMissesFollowTheSameModePolicy(
        BotReplyMode mode, BotReplyResolutionKind expected)
    {
        var resolver = new BotReplyResolver(new FakeFrequentResponseRepository([]), new FrequentResponseMatcher());

        var text = await resolver.ResolveAsync(Configuration(mode), "unknown request", CancellationToken.None);
        var transcript = await resolver.ResolveAsync(Configuration(mode), "unknown request", CancellationToken.None);

        Assert.Equal(expected, text.Kind);
        Assert.Equal(text.Kind, transcript.Kind);
        Assert.Equal(mode == BotReplyMode.AiOnly, transcript.FaqEvaluationSkipped);
    }

    [Fact]
    public async Task FaqThenAi_MissSignalsFutureFallbackWithoutCallingAnAiProvider()
    {
        var repository = new FakeFrequentResponseRepository([]);
        var resolver = new BotReplyResolver(repository, new FrequentResponseMatcher());

        var result = await resolver.ResolveAsync(
            Configuration(BotReplyMode.FaqThenAi), "unknown question", CancellationToken.None);

        Assert.Equal(BotReplyResolutionKind.AiFallbackRequired, result.Kind);
        Assert.Equal(1, repository.CandidateQueries);
    }

    [Fact]
    public async Task AiOnly_SkipsFaqEvaluation()
    {
        var repository = new FakeFrequentResponseRepository([Candidate(1, "hello", "faq")]);
        var resolver = new BotReplyResolver(repository, new FrequentResponseMatcher());

        var result = await resolver.ResolveAsync(Configuration(BotReplyMode.AiOnly), "hello", CancellationToken.None);

        Assert.Equal(BotReplyResolutionKind.AiFallbackRequired, result.Kind);
        Assert.True(result.FaqEvaluationSkipped);
        Assert.Equal(0, repository.CandidateQueries);
    }

    [Fact]
    public async Task DisabledBot_DoesNotQueryFrequentResponses()
    {
        var repository = new FakeFrequentResponseRepository([Candidate(1, "hello", "faq")]);
        var resolver = new BotReplyResolver(repository, new FrequentResponseMatcher());

        var result = await resolver.ResolveAsync(
            Configuration(BotReplyMode.FaqThenAi) with { IsBotEnabled = false },
            "hello",
            CancellationToken.None);

        Assert.Equal(BotReplyResolutionKind.NoReply, result.Kind);
        Assert.Equal(0, repository.CandidateQueries);
    }

    private static BotConfiguration Configuration(BotReplyMode mode) => new(Guid.Empty, true, mode)
    {
        TenantId = Guid.Empty
    };

    private static FrequentResponseRecord Candidate(long id, string expression, string answer) =>
        new(new FrequentResponse(id, Guid.Empty, "intent", answer, 1, null, true), [expression]);

    private sealed class FakeFrequentResponseRepository(IReadOnlyList<FrequentResponseRecord> candidates)
        : IFrequentResponseRepository
    {
        public int CandidateQueries { get; private set; }
        public Task<IReadOnlyList<FrequentResponseRecord>> ListActiveCandidatesAsync(
            Guid tenantId, Guid integrationId, CancellationToken cancellationToken)
        {
            CandidateQueries++;
            return Task.FromResult(candidates);
        }
        public Task<FrequentResponsePage> ListAsync(
            Guid tenantId, Guid integrationId, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<FrequentResponseRecord?> GetAsync(
            Guid tenantId, Guid integrationId, long frequentResponseId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<FrequentResponseRecord> CreateAsync(
            Guid tenantId, Guid integrationId, FrequentResponseCommand command,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<FrequentResponseRecord?> UpdateAsync(
            Guid tenantId, Guid integrationId, long frequentResponseId, FrequentResponseCommand command,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<bool> SetActiveAsync(
            Guid tenantId, Guid integrationId, long frequentResponseId, bool isActive,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<bool> DeleteAsync(
            Guid tenantId, Guid integrationId, long frequentResponseId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
