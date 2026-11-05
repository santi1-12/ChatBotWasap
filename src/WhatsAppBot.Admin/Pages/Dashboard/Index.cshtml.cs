using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Admin.Pages.Dashboard;

[Authorize]
public sealed class IndexModel(IWhatsAppBotApiClient apiClient) : PageModel
{
    private const int RecentActivityPageSize = 5;

    public WhatsAppIntegrationDTO? Integration { get; private set; }
    public BotConfigurationDTO? BotConfiguration { get; private set; }
    public TenantDTO? Tenant { get; private set; }
    public IReadOnlyList<ConversationSummaryDTO> RecentConversations { get; private set; } = [];
    public int ConversationCount { get; private set; }
    public bool IntegrationAvailable { get; private set; }
    public bool BotConfigurationAvailable { get; private set; }
    public bool TenantAvailable { get; private set; }
    public bool ActivityAvailable { get; private set; }
    public string? IntegrationError { get; private set; }
    public string? BotConfigurationError { get; private set; }
    public string? TenantError { get; private set; }
    public string? ActivityError { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            LoadIntegrationAsync(cancellationToken),
            LoadBotConfigurationAsync(cancellationToken),
            LoadTenantAsync(cancellationToken),
            LoadRecentActivityAsync(cancellationToken));
    }

    public string ConnectionLabel => !IntegrationAvailable
        ? "No disponible"
        : Integration?.ConnectionState switch
        {
            ConnectionState.NotConnected => "No conectado",
            ConnectionState.Configuring => "Pendiente",
            ConnectionState.Connected => "Conectado",
            ConnectionState.Error => "Error",
            ConnectionState.Unknown => "Estado indeterminado",
            _ => "Estado desconocido"
        };

    public string ConnectionTitle => !IntegrationAvailable
        ? "Estado no disponible"
        : Integration?.ConnectionState switch
        {
            ConnectionState.Connected => Integration.DisplayName ?? Integration.BusinessPhoneNumber ?? "Conectado",
            ConnectionState.Configuring => "Configuración en curso",
            ConnectionState.Error => "Error de conexión",
            ConnectionState.Unknown => "Estado indeterminado",
            _ => "No conectado"
        };

    public string ConnectionCaption => Integration switch
    {
        null => "La API no devolvió el estado de la conexión.",
        { ConnectionState: ConnectionState.Connected, BusinessPhoneNumber: not null } integration =>
            $"Número empresarial: {integration.BusinessPhoneNumber}",
        { ConnectionState: ConnectionState.Connected } => "La conexión fue confirmada por la API.",
        { ConnectionState: ConnectionState.NotConnected } => "No hay una integración activa para este Tenant.",
        { ConnectionState: ConnectionState.Configuring } => "La conexión está siendo configurada.",
        { ConnectionState: ConnectionState.Error } => "La API reportó un error de conexión.",
        _ => "La API reportó un estado que requiere revisión."
    };

    public string BotLabel => !BotConfigurationAvailable
        ? "No disponible"
        : BotConfiguration?.IsBotEnabled == true ? "Activo" : "Inactivo";

    public string BotTitle => !BotConfigurationAvailable
        ? "Estado no disponible"
        : BotConfiguration?.IsBotEnabled == true ? "Bot activo" : "Bot desactivado";

    public string BotCaption => BotConfiguration is null
        ? "La API no devolvió la configuración del bot."
        : $"Modo: {ReplyModeLabel(BotConfiguration.ReplyMode)}";

    public string TenantLabel => !TenantAvailable
        ? "No disponible"
        : Tenant?.IsActive == true ? "Activo" : "Inactivo";

    public string TenantTitle => Tenant?.LegalName ?? "Tenant no disponible";

    public string TenantCaption => Tenant is null
        ? "La API no devolvió el Tenant efectivo."
        : $"Identificador: {Tenant.TenantId:D}";

    public string StatusLabel(ConversationStatus status) => status switch
    {
        ConversationStatus.Open => "Abierta",
        ConversationStatus.Closed => "Cerrada",
        _ => "Desconocido"
    };

    public string AttentionLabel(AttentionMode mode) => mode == AttentionMode.Humano ? "Humano" : "Bot";

    public string ReplyModeLabel(BotReplyMode mode) => mode switch
    {
        BotReplyMode.FaqOnly => "Solo respuestas configuradas",
        BotReplyMode.FaqThenAi => "Respuestas configuradas + IA fallback",
        BotReplyMode.AiOnly => "Solo IA",
        _ => "Desconocido"
    };

    private async Task LoadIntegrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.GetWhatsAppIntegrationAsync(cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                IntegrationAvailable = true;
                Integration = response.Data;
            }
            else
            {
                IntegrationError = response?.Error?.Message ?? "No se pudo consultar el estado de WhatsApp.";
            }
        }
        catch (AdminApiTimeoutException)
        {
            IntegrationError = "La consulta de WhatsApp tardó más de lo esperado.";
        }
        catch (HttpRequestException)
        {
            IntegrationError = "No se pudo contactar la API para consultar WhatsApp.";
        }
    }

    private async Task LoadBotConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.GetBotConfigurationAsync(cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                BotConfigurationAvailable = true;
                BotConfiguration = response.Data;
            }
            else
            {
                BotConfigurationError = response?.Error?.Message ?? "No se pudo consultar la configuración del bot.";
            }
        }
        catch (AdminApiTimeoutException)
        {
            BotConfigurationError = "La consulta de configuración tardó más de lo esperado.";
        }
        catch (HttpRequestException)
        {
            BotConfigurationError = "No se pudo contactar la API para consultar el bot.";
        }
    }

    private async Task LoadTenantAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.GetCurrentTenantAsync(cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                TenantAvailable = true;
                Tenant = response.Data;
            }
            else
            {
                TenantError = response?.Error?.Message ?? "No se pudo consultar el Tenant efectivo.";
            }
        }
        catch (AdminApiTimeoutException)
        {
            TenantError = "La consulta del Tenant tardó más de lo esperado.";
        }
        catch (HttpRequestException)
        {
            TenantError = "No se pudo contactar la API para consultar el Tenant.";
        }
    }

    private async Task LoadRecentActivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.ListConversationsAsync(1, RecentActivityPageSize, cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                ActivityAvailable = true;
                RecentConversations = response.Data.Items;
                ConversationCount = response.Data.TotalCount;
            }
            else
            {
                ActivityError = response?.Error?.Message ?? "No se pudo consultar la actividad reciente.";
            }
        }
        catch (AdminApiTimeoutException)
        {
            ActivityError = "La consulta de actividad tardó más de lo esperado.";
        }
        catch (HttpRequestException)
        {
            ActivityError = "No se pudo contactar la API para consultar la actividad.";
        }
    }
}
