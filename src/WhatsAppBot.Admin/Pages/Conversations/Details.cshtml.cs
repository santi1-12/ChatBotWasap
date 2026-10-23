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
    public bool HasOlderMessages => Conversation?.Messages.Count >= MessagePageSize;

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
            var response = await apiClient.GetConversationAsync(id, beforeMessageId, cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                Conversation = response.Data;
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudo cargar la conversación.";
            }
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

    public string StatusLabel(ConversationStatus status) => status switch
    {
        ConversationStatus.Open => "Abierta",
        ConversationStatus.Closed => "Cerrada",
        _ => "Desconocido"
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
        _ => null
    };
}
