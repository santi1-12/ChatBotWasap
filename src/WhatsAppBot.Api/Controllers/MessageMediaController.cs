using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/messages")]
public sealed class MessageMediaController(IGetMessageMedia getMessageMedia) : ControllerBase
{
    [HttpGet("{id:long}/media")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        var result = await getMessageMedia.ExecuteAsync(id, cancellationToken);
        return result.Status switch
        {
            MediaAccessStatus.Authorized => File(
                result.Content!, result.MimeType!, enableRangeProcessing: true),
            MediaAccessStatus.Expired => StatusCode(
                StatusCodes.Status410Gone,
                Error(MessageOutcomeCodes.MediaExpired)),
            _ => NotFound(Error("MediaNotFound"))
        };
    }

    private ResponseE<object?> Error(string code) =>
        ResponseE<object?>.Fail(
            new ApiErrorDTO
            {
                Code = code,
                Message = code == MessageOutcomeCodes.MediaExpired
                    ? "El contenido multimedia expiró."
                    : "El contenido multimedia no está disponible."
            },
            HttpContext.TraceIdentifier);
}
