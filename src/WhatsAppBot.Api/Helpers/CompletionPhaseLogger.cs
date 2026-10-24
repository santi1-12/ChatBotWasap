using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WhatsAppBot.Api.Integrations.Meta;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Helpers;

public sealed class CompletionPhaseLogger(
    ILogger<CompletionPhaseLogger> logger,
    IHttpContextAccessor httpContextAccessor,
    CompletionExecutionContext executionContext)
{
    public async Task RunAsync(string phase, Func<Task> operation)
    {
        await RunAsync<object?>(phase, async () =>
        {
            await operation();
            return null;
        });
    }

    public async Task<T> RunAsync<T>(string phase, Func<Task<T>> operation)
    {
        var previousPhase = executionContext.CurrentPhase;
        executionContext.CurrentPhase = phase;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation();
            stopwatch.Stop();
            logger.LogInformation(
                "Completion phase {TraceId} {Phase} {Outcome} in {DurationMs} ms",
                GetTraceId(), phase, "Succeeded", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            LogFailure(phase, stopwatch.ElapsedMilliseconds, exception);
            throw;
        }
        finally
        {
            executionContext.CurrentPhase = previousPhase;
        }
    }

    public void LogSkipped(string phase)
    {
        logger.LogInformation(
            "Completion phase {TraceId} {Phase} {Outcome} in {DurationMs} ms",
            GetTraceId(), phase, "Skipped", 0);
    }

    public void LogFailure(
        string phase,
        string safeCode,
        int? statusCode = null,
        string? metaRequestId = null)
    {
        logger.LogWarning(
            "Completion phase {TraceId} {Phase} {Outcome} in {DurationMs} ms; Meta HTTP {MetaStatusCode}; SafeCode {SafeCode}; MetaRequestId {MetaRequestId}",
            GetTraceId(), phase, "Failed", 0, statusCode, safeCode, metaRequestId);
    }

    private void LogFailure(string phase, long durationMs, Exception exception)
    {
        var diagnostic = GetDiagnostic(exception);
        logger.LogWarning(
            "Completion phase {TraceId} {Phase} {Outcome} in {DurationMs} ms; Meta HTTP {MetaStatusCode}; SafeCode {SafeCode}; MetaRequestId {MetaRequestId}; FailureType {FailureType}",
            GetTraceId(),
            phase,
            "Failed",
            durationMs,
            diagnostic.StatusCode,
            diagnostic.SafeCode,
            diagnostic.MetaRequestId,
            exception.GetType().Name);
    }

    private string GetTraceId()
    {
        var traceIdentifier = httpContextAccessor.HttpContext?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(traceIdentifier))
        {
            return traceIdentifier;
        }

        return Activity.Current?.TraceId.ToString() ?? "unknown";
    }

    private string GetCancellationCode()
    {
        return httpContextAccessor.HttpContext?.RequestAborted.IsCancellationRequested == true
            ? "CompletionCanceled"
            : "CompletionTimedOut";
    }

    private (int? StatusCode, string SafeCode, string? MetaRequestId) GetDiagnostic(Exception exception) =>
        exception switch
        {
            MetaApiException metaException => (
                metaException.StatusCode is null ? null : (int)metaException.StatusCode.Value,
                metaException.SafeCode,
                metaException.MetaRequestId),
            MetaConfigurationException => (null, "MetaConfigurationMissing", null),
            ExternalDependencyException dependencyException =>
                (null, dependencyException.SafeCode, null),
            JsonException => (null, "MetaInvalidResponse", null),
            OperationCanceledException => (null, GetCancellationCode(), null),
            _ => (null, "CompletionPhaseFailed", null)
        };
}
