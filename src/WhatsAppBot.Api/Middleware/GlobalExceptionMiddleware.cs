using System.Text.Json;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Api.Middleware;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var traceId = context.TraceIdentifier;
            var (statusCode, code, message) = exception switch
            {
                BusinessValidationException => (
                    StatusCodes.Status400BadRequest, "ValidationFailed", exception.Message),
                BusinessNotFoundException => (
                    StatusCodes.Status404NotFound, "NotFound", exception.Message),
                ConversationConflictException => (
                    StatusCodes.Status409Conflict, "ConversationVersionConflict", exception.Message),
                ExternalDependencyException => (
                    StatusCodes.Status502BadGateway,
                    "ExternalDependencyFailure",
                    "The external service could not complete the request."),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "InternalError",
                    "The request could not be completed.")
            };
            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    "Request failed with {ExceptionType} and safe provider code {SafeCode} for trace {TraceId}",
                    exception.GetType().Name,
                    (exception as ExternalDependencyException)?.SafeCode,
                    traceId);
            }
            else
            {
                logger.LogInformation(
                    "Request rejected with {ErrorCode} for trace {TraceId}",
                    code,
                    traceId);
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            var response = ResponseE<object?>.Fail(
                new ApiErrorDTO { Code = code, Message = message },
                traceId);
            await JsonSerializer.SerializeAsync(
                context.Response.Body, response, JsonOptions, context.RequestAborted);
        }
    }
}
