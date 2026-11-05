using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;

namespace WhatsAppBot.Api.Webhooks;

public static class MetaWhatsAppWebhookEndpoint
{
    private const string Route = "/webhooks/meta/whatsapp";

    public static void MapMetaWhatsAppWebhook(this WebApplication app)
    {
        var maxBodyBytes = app.Services.GetRequiredService<IOptions<MetaOptions>>()
            .Value.WebhookMaxBodyBytes;
        if (maxBodyBytes is < 1024 or > 1048576)
        {
            throw new InvalidOperationException("Meta:WebhookMaxBodyBytes must be between 1024 and 1048576.");
        }

        app.MapGet(Route, VerifyChallengeAsync).AllowAnonymous();
        app.MapPost(Route, ReceiveEventAsync)
            .AllowAnonymous()
            .WithMetadata(new RequestSizeLimitAttribute(maxBodyBytes));
    }

    private static async Task<IResult> VerifyChallengeAsync(
        HttpRequest request,
        ISecretReferenceResolver secretResolver,
        IMetaWebhookChallengeValidator challengeValidator,
        CancellationToken cancellationToken)
    {
        var mode = request.Query["hub.mode"].ToString();
        var suppliedToken = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();
        var expectedToken = await secretResolver.ResolveAsync("Meta:VerifyToken", cancellationToken);
        if (string.IsNullOrWhiteSpace(challenge) ||
            string.IsNullOrWhiteSpace(expectedToken) ||
            !challengeValidator.IsValid(mode, suppliedToken, expectedToken))
        {
            return Results.StatusCode(string.IsNullOrWhiteSpace(expectedToken)
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status403Forbidden);
        }

        return Results.Text(challenge, "text/plain", Encoding.UTF8);
    }

    private static async Task<IResult> ReceiveEventAsync(
        HttpRequest request,
        ISecretReferenceResolver secretResolver,
        IMetaWebhookSignatureValidator signatureValidator,
        IMetaWebhookEventParser eventParser,
        IAcceptMetaWebhookEvent eventAcceptor,
        IOptions<MetaOptions> options,
        CancellationToken cancellationToken)
    {
        var maxBodyBytes = options.Value.WebhookMaxBodyBytes;
        var contentType = request.ContentType?.Split(';', 2)[0].Trim();
        if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }
        var sizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = maxBodyBytes;
        }
        if (request.ContentLength is > 0 && request.ContentLength > maxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        await using var body = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (body.Length + read > maxBodyBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var appSecret = await secretResolver.ResolveAsync("Meta:AppSecret", cancellationToken);
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var payload = body.ToArray();
        var signature = request.Headers["X-Hub-Signature-256"].ToString();
        if (!signatureValidator.IsValid(payload, signature, appSecret))
        {
            return Results.Unauthorized();
        }

        IReadOnlyList<Models.Contracts.NormalizedMetaEvent> events;
        try
        {
            events = eventParser.Parse(payload);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest();
        }

        await eventAcceptor.AcceptAsync(events, cancellationToken);
        return Results.Ok();
    }

}
