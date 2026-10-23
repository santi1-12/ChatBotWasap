using WhatsAppBot.Api.Models.Contracts;
using Microsoft.Data.SqlClient;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class FrequentResponseRepositoryTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task BotConfiguration_ChangesEnabledStateAndEachReplyModeThenRestoresOriginal()
    {
        var original = await fixture.ConfigurationRepository.GetCurrentAsync(CancellationToken.None);
        try
        {
            foreach (var mode in Enum.GetValues<WhatsAppBot.Shared.Enums.BotReplyMode>())
            {
                var changed = await fixture.ConfigurationRepository.UpdateAsync(
                    original.IntegrationId, !original.IsBotEnabled, mode, CancellationToken.None);
                var reread = await fixture.ConfigurationRepository.GetCurrentAsync(CancellationToken.None);
                Assert.Equal(!original.IsBotEnabled, changed.IsBotEnabled);
                Assert.Equal(mode, reread.ReplyMode);
            }
        }
        finally
        {
            await fixture.ConfigurationRepository.UpdateAsync(
                original.IntegrationId, original.IsBotEnabled, original.ReplyMode, CancellationToken.None);
        }
    }

    [Fact]
    public async Task CreateUpdateToggleListAndDelete_PersistInLocalDb()
    {
        var configuration = await fixture.ConfigurationRepository.GetCurrentAsync(CancellationToken.None);
        var initialTotal = (await fixture.FrequentResponseRepository.ListAsync(
            configuration.IntegrationId, 1, 100, CancellationToken.None)).TotalCount;
        var unique = Guid.NewGuid().ToString("N");
        var command = new FrequentResponseCommand(
            $"Persistence test {unique}",
            [$"expression {unique}"],
            $"Answer {unique}",
            73,
            "Tests",
            true);
        var created = await fixture.FrequentResponseRepository.CreateAsync(
            configuration.IntegrationId, command, CancellationToken.None);

        try
        {
            Assert.Equal(command.AnswerText, created.Response.AnswerText);
            Assert.Equal(command.Expressions, created.Expressions);
            Assert.Contains(
                (await fixture.FrequentResponseRepository.ListAsync(
                    configuration.IntegrationId, 1, 100, CancellationToken.None)).Items,
                item => item.Response.FrequentResponseId == created.Response.FrequentResponseId);
            var beyondEnd = await fixture.FrequentResponseRepository.ListAsync(
                configuration.IntegrationId, 100, 25, CancellationToken.None);
            Assert.Equal(initialTotal + 1, beyondEnd.TotalCount);
            Assert.Empty(beyondEnd.Items);

            var updatedCommand = command with
            {
                AnswerText = $"Edited {unique}",
                Expressions = [$"updated {unique}"],
                Priority = 88,
                IsActive = true
            };
            var updated = await fixture.FrequentResponseRepository.UpdateAsync(
                configuration.IntegrationId,
                created.Response.FrequentResponseId,
                updatedCommand,
                CancellationToken.None);
            Assert.NotNull(updated);
            Assert.Equal(updatedCommand.AnswerText, updated!.Response.AnswerText);
            Assert.Equal(updatedCommand.Expressions, updated.Expressions);
            Assert.Equal(created.Response.CreatedAtUtc, updated.Response.CreatedAtUtc);
            Assert.True(updated.Response.ModifiedAtUtc >= created.Response.ModifiedAtUtc);

            var invalidUpdate = updatedCommand with
            {
                Expressions = [$"duplicate {unique}", $"  DUPLICATE   {unique} "]
            };
            await Assert.ThrowsAsync<SqlException>(() => fixture.FrequentResponseRepository.UpdateAsync(
                configuration.IntegrationId,
                created.Response.FrequentResponseId,
                invalidUpdate,
                CancellationToken.None));
            var afterRollback = await fixture.FrequentResponseRepository.GetAsync(
                configuration.IntegrationId, created.Response.FrequentResponseId, CancellationToken.None);
            Assert.NotNull(afterRollback);
            Assert.Equal(updatedCommand.Expressions, afterRollback!.Expressions);

            await Task.Delay(10);
            Assert.True(await fixture.FrequentResponseRepository.SetActiveAsync(
                configuration.IntegrationId,
                created.Response.FrequentResponseId,
                false,
                CancellationToken.None));
            var deactivated = await fixture.FrequentResponseRepository.GetAsync(
                configuration.IntegrationId, created.Response.FrequentResponseId, CancellationToken.None);
            Assert.True(deactivated!.Response.ModifiedAtUtc > updated.Response.ModifiedAtUtc);
            Assert.DoesNotContain(
                await fixture.FrequentResponseRepository.ListActiveCandidatesAsync(
                    configuration.IntegrationId, CancellationToken.None),
                item => item.Response.FrequentResponseId == created.Response.FrequentResponseId);
        }
        finally
        {
            await fixture.FrequentResponseRepository.DeleteAsync(
                configuration.IntegrationId, created.Response.FrequentResponseId, CancellationToken.None);
        }

        Assert.Null(await fixture.FrequentResponseRepository.GetAsync(
            configuration.IntegrationId, created.Response.FrequentResponseId, CancellationToken.None));
    }

    [Fact]
    public async Task ActiveCandidates_AreScopedOrderedByPriorityAndExcludeInactiveRows()
    {
        var configuration = await fixture.ConfigurationRepository.GetCurrentAsync(CancellationToken.None);
        var unique = Guid.NewGuid().ToString("N");
        var low = await fixture.FrequentResponseRepository.CreateAsync(
            configuration.IntegrationId,
            new FrequentResponseCommand($"Low {unique}", [$"phrase {unique}"], "Low", 10, null, true),
            CancellationToken.None);
        var high = await fixture.FrequentResponseRepository.CreateAsync(
            configuration.IntegrationId,
            new FrequentResponseCommand($"High {unique}", [$"phrase {unique}"], "High", 90, null, true),
            CancellationToken.None);

        try
        {
            var candidates = await fixture.FrequentResponseRepository.ListActiveCandidatesAsync(
                configuration.IntegrationId, CancellationToken.None);
            var matching = candidates.Where(item => item.Expressions.Contains($"phrase {unique}")).ToArray();
            Assert.Equal(
                [high.Response.FrequentResponseId, low.Response.FrequentResponseId],
                matching.Select(item => item.Response.FrequentResponseId));

            await fixture.FrequentResponseRepository.SetActiveAsync(
                configuration.IntegrationId, high.Response.FrequentResponseId, false, CancellationToken.None);
            candidates = await fixture.FrequentResponseRepository.ListActiveCandidatesAsync(
                configuration.IntegrationId, CancellationToken.None);
            Assert.DoesNotContain(candidates, item => item.Response.FrequentResponseId == high.Response.FrequentResponseId);
        }
        finally
        {
            await fixture.FrequentResponseRepository.DeleteAsync(
                configuration.IntegrationId, low.Response.FrequentResponseId, CancellationToken.None);
            await fixture.FrequentResponseRepository.DeleteAsync(
                configuration.IntegrationId, high.Response.FrequentResponseId, CancellationToken.None);
        }
    }
}
