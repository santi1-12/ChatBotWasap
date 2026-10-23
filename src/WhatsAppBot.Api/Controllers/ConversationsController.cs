using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Enums;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Responses;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/conversations")]
public sealed class ConversationsController(
    IListConversations listConversations,
    IGetConversationDetail getConversationDetail) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ResponseE<PagedResultDTO<ConversationSummaryDTO>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResponseE<PagedResultDTO<ConversationSummaryDTO>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await listConversations.ExecuteAsync(page, pageSize, cancellationToken);
        var response = new PagedResultDTO<ConversationSummaryDTO>
        {
            Items = result.Items.Select(Map).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };
        return Ok(ResponseE<PagedResultDTO<ConversationSummaryDTO>>.Ok(
            response, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ResponseE<ConversationDetailDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseE<object?>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResponseE<ConversationDetailDTO>>> Get(
        long id,
        [FromQuery] long? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await getConversationDetail.ExecuteAsync(id, beforeMessageId, cancellationToken);
        return Ok(ResponseE<ConversationDetailDTO>.Ok(Map(result), HttpContext.TraceIdentifier));
    }

    private static ConversationSummaryDTO Map(ConversationSummaryRecord record) => new()
    {
        TenantId = record.TenantId,
        ConversationId = record.ConversationId,
        Contact = record.Contact,
        LastMessageAtUtc = record.LastMessageAtUtc,
        Status = record.Status,
        AttentionMode = record.AttentionMode,
        LifecycleStatus = record.LifecycleStatus,
        AssignedAdminId = record.AssignedAdminId,
        Version = record.Version,
        LastMessagePreview = record.LastMessagePreview
    };

    private static ConversationDetailDTO Map(ConversationDetailRecord record) => new()
    {
        Conversation = Map(record.Conversation),
        Messages = record.Messages.Select(Map).ToArray(),
        AuditEvents = []
    };

    private static MessageDTO Map(ConversationMessageRecord record) => new()
    {
        TenantId = record.TenantId,
        MessageId = record.MessageId,
        Direction = record.Direction,
        MessageType = record.MessageType,
        ActorType = record.ActorType,
        Origin = record.Origin,
        CreatedByAdminId = record.CreatedByAdminId,
        SourceInboundMessageId = record.SourceInboundMessageId,
        Text = record.Text,
        Transcription = record.Transcription,
        MediaAvailable = record.MediaAvailable,
        MediaUrl = null,
        ReplySource = record.ReplySource,
        ProcessingState = record.ProcessingState,
        DeliveryState = record.DeliveryState,
        OutcomeCode = MapSafeOutcomeCode(record.OutcomeCode),
        CreatedAtUtc = record.CreatedAtUtc
    };

    private static string? MapSafeOutcomeCode(string? code) => code switch
        {
            _ when MessageOutcomeCodeRules.IsAllowlisted(code) => code,
            _ => null
        };
}
