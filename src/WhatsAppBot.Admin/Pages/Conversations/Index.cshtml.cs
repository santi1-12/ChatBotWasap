using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Admin.Pages.Conversations;

[Authorize]
public sealed class IndexModel(IWhatsAppBotApiClient apiClient) : PageModel
{
    public IReadOnlyList<ConversationSummaryDTO> Conversations { get; private set; } = [];
    public int CurrentPage { get; private set; } = 1;
    public int PageSize { get; private set; } = 25;
    public int TotalCount { get; private set; }
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        CurrentPage = Math.Max(page, 1);
        PageSize = pageSize is < 1 or > 100 ? 25 : pageSize;
        await LoadAsync(cancellationToken);
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

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.ListConversationsAsync(CurrentPage, PageSize, cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                Conversations = response.Data.Items;
                CurrentPage = response.Data.Page;
                PageSize = response.Data.PageSize;
                TotalCount = response.Data.TotalCount;
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudieron cargar las conversaciones.";
            }
        }
        catch (AdminApiTimeoutException)
        {
            ErrorMessage = "La consulta de conversaciones tardó más de lo esperado.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. Las conversaciones no están disponibles.";
        }
    }
}
