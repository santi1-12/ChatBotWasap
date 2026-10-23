using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WhatsAppBot.Api.Helpers;
using WhatsAppBot.Api.Integrations.Meta;

namespace WhatsAppBot.Integrations.Tests;

public sealed class CompletionPhaseLoggerTests
{
    [Fact]
    public async Task FailureLogIdentifiesPhaseStatusAndSafeCodeWithoutSensitiveValues()
    {
        var logger = new RecordingLogger();
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-test" }
        };
        var phaseLogger = new CompletionPhaseLogger(
            logger,
            accessor,
            new CompletionExecutionContext());

        await Assert.ThrowsAsync<MetaApiException>(() =>
            phaseLogger.RunAsync(
                "WabaSubscription",
                () => throw new MetaApiException(
                    "MetaRequestRejected",
                    false,
                    HttpStatusCode.BadRequest,
                    "meta-trace-1")));

        var message = Assert.Single(logger.Messages);
        Assert.Contains("trace-test", message);
        Assert.Contains("WabaSubscription", message);
        Assert.Contains("400", message);
        Assert.Contains("MetaRequestRejected", message);
        Assert.Contains("meta-trace-1", message);
        Assert.DoesNotContain("access-token", message);
        Assert.DoesNotContain("app-secret", message);
        Assert.DoesNotContain("authorization-code", message);
    }

    private sealed class RecordingLogger : ILogger<CompletionPhaseLogger>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose() { }
        }
    }
}
