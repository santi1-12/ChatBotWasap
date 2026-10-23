using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Admin.Pages.Conversations;

[Authorize]
public sealed class DetailsModel(IWhatsAppBotApiClient apiClient) : PageModel
{
    private const int MessagePageSize = 50;

    public ConversationDetailDTO? Conversation { get; private set; }
    public long? BeforeMessageId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public bool HasOlderMessages => Conversation?.Messages.Count >= MessagePageSize;

    [BindProperty]
    public ConversationActionRequestDTO ActionRequest { get; set; } = new()
    {
        Action = ConversationAction.Take,
        ExpectedVersion = string.Empty
    };

    [BindProperty]
    public ManualMessageRequestDTO ManualMessage { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
        long id, long? beforeMessageId = null, CancellationToken cancellationToken = default)
    {
        BeforeMessageId = beforeMessageId;
        if (id < 1 || beforeMessageId is <= 0)
        {
            ErrorMessage = "La conversación solicitada no es válida.";
            return Page();
        }

        try
        {
            await LoadConversationAsync(id, beforeMessageId, cancellationToken);
        }
        catch (AdminApiTimeoutException)
        {
            ErrorMessage = "La consulta de la conversación tardó más de lo esperado.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. El historial no está disponible.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostActionAsync(
        long id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.ApplyConversationActionAsync(
                id, ActionRequest, cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                Conversation = response.Data;
                BeforeMessageId = null;
                SetExpectedVersion();
                SuccessMessage = "La acción de conversación se aplicó correctamente.";
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudo aplicar la acción.";
                await LoadConversationAsync(id, null, cancellationToken);
            }
        }
        catch (AdminApiTimeoutException)
        {
            ErrorMessage = "La acción tardó más de lo esperado. Consulta el estado antes de reintentar.";
            await LoadConversationAsync(id, null, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. La conversación no fue confirmada.";
            await LoadConversationAsync(id, null, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostManualReplyAsync(
        long id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.SendManualConversationMessageAsync(
                id, ManualMessage, cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                SuccessMessage = "La respuesta manual quedó pendiente de envío.";
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudo aceptar la respuesta manual.";
            }

            await LoadConversationAsync(id, null, cancellationToken);
        }
        catch (AdminApiTimeoutException)
        {
            ErrorMessage = "La respuesta manual tardó más de lo esperado. Consulta el historial antes de reintentar.";
            await LoadConversationAsync(id, null, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. La respuesta manual no fue confirmada.";
            await LoadConversationAsync(id, null, cancellationToken);
        }

        return Page();
    }

    public string StatusLabel(ConversationStatus status) => status switch
    {
        ConversationStatus.Open => "Abierta",
        ConversationStatus.Closed => "Cerrada",
        _ => "Desconocido"
    };

    public string AttentionLabel(AttentionMode mode) => mode == AttentionMode.Humano ? "Humano" : "Bot";

    public string LifecycleLabel(LifecycleStatus status) => status == LifecycleStatus.Cerrada ? "Cerrada" : "Abierta";

    public string ActorLabel(ActorType? actorType) => actorType switch
    {
        ActorType.Customer => "Cliente",
        ActorType.Bot => "Bot",
        ActorType.Admin => "Administrador",
        ActorType.System => "Sistema",
        _ => "No informado"
    };

    public string OriginLabel(Origin? origin) => origin switch
    {
        Origin.MetaCloudApi => "Meta Cloud API",
        Origin.AdminPanel => "Panel Admin",
        Origin.System => "Sistema",
        Origin.FutureWhatsAppBusinessApp => "WhatsApp Business App",
        _ => "No informado"
    };

    public string DirectionLabel(MessageDirection direction) => direction switch
    {
        MessageDirection.Inbound => "Cliente",
        MessageDirection.Outbound => "Bot",
        _ => "Mensaje"
    };

    public string MessageTypeLabel(MessageType type) => type switch
    {
        MessageType.Text => "Texto",
        MessageType.Audio => "Audio",
        MessageType.Image => "Imagen",
        _ => "No compatible"
    };

    public string ProcessingLabel(ProcessingState state) => state switch
    {
        ProcessingState.Received => "Recibido",
        ProcessingState.Processing => "Procesando",
        ProcessingState.Completed => "Procesado",
        ProcessingState.Failed => "Falló el procesamiento",
        ProcessingState.NoReply => "Sin respuesta automática",
        _ => "Estado desconocido"
    };

    public string? DeliveryLabel(DeliveryState? state) => state switch
    {
        DeliveryState.Pending => "Pendiente de envío",
        DeliveryState.Sent => "Enviado",
        DeliveryState.Delivered => "Entregado",
        DeliveryState.Read => "Leído",
        DeliveryState.Failed => "Falló la entrega",
        _ => null
    };

    public string? ReplySourceLabel(ReplySource? source) => source switch
    {
        ReplySource.FrequentResponse => "Respuesta frecuente",
        ReplySource.AI => "Inteligencia artificial",
        ReplySource.Admin => "Administrador",
        _ => null
    };

    public string? OutcomeLabel(string? code) => code switch
    {
        "MessagingWindowClosed" => "Fuera de la ventana de atención",
        "TemplateRequired" => "Se requiere una plantilla oficial",
        "BotDisabled" => "El bot estaba desactivado",
        "NoFrequentResponse" => "No hubo respuesta frecuente aplicable",
        "AiFallbackNotConfigured" => "La IA de fallback no está configurada",
        "UnsupportedMessageType" => "Tipo de mensaje no compatible",
        "MetaSendFailed" => "Meta no confirmó el envío",
        "MetaDeliveryFailed" => "Meta reportó fallo de entrega",
        "ConversationInHumanMode" => "La conversación está siendo atendida por una persona",
        "ConversationClosed" => "La conversación está cerrada",
        "ConversationVersionConflict" => "La conversación cambió antes de enviar",
        "IntegrationUnavailable" => "La integración no está disponible",
        "ManualSendFailed" => "No se pudo aceptar la respuesta manual",
        "ManualReplyRequiresHuman" => "La respuesta manual requiere atención humana",
        "ManualReplyNotAuthorized" => "El administrador no está autorizado para responder",
        "BlockingError" => "El procesamiento automático fue bloqueado",
        _ => null
    };

    public string ActionLabel(ConversationAction action) => action switch
    {
        ConversationAction.Take => "Tomar conversación",
        ConversationAction.Handoff => "Pasar a humano",
        ConversationAction.ReturnToBot => "Devolver al bot",
        ConversationAction.Close => "Cerrar",
        ConversationAction.Reopen => "Reabrir",
        _ => "Aplicar acción"
    };

    private async Task LoadConversationAsync(
        long id, long? beforeMessageId, CancellationToken cancellationToken)
    {
        var response = await apiClient.GetConversationAsync(id, beforeMessageId, cancellationToken);
        if (response?.Success == true && response.Data is not null)
        {
            Conversation = response.Data;
            SetExpectedVersion();
        }
        else
        {
            ErrorMessage ??= response?.Error?.Message ?? "No se pudo cargar la conversación.";
        }
    }

    private void SetExpectedVersion()
    {
        var version = Conversation?.Conversation.Version ?? string.Empty;
        ActionRequest.ExpectedVersion = version;
        ManualMessage.ExpectedVersion = version;
    }
}
