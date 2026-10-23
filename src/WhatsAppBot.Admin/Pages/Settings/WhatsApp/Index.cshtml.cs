using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Shared.DTOs;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Admin.Pages.Settings.WhatsApp;

[Authorize]
public sealed class IndexModel(IWhatsAppBotApiClient apiClient) : PageModel
{
    public WhatsAppIntegrationDTO? Integration { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public string? PendingMessage { get; private set; }
    public bool ApiResponseAvailable { get; private set; }
    public bool IsConnected => HasCompleteConnection(Integration);
    public bool ShowConfigurationMissing =>
        ApiResponseAvailable && Integration is not null && !HasSignupConfiguration(Integration);

    [BindProperty, Required, MaxLength(2048)]
    public string AuthorizationCode { get; set; } = string.Empty;

    [BindProperty, Required, MaxLength(64)]
    public string WabaId { get; set; } = string.Empty;

    [BindProperty, MaxLength(64)]
    public string? PhoneNumberId { get; set; }

    [BindProperty, Required, EnumDataType(typeof(WhatsAppOnboardingType))]
    public WhatsAppOnboardingType? OnboardingType { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadIntegrationAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCompleteSignupAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadIntegrationAsync(cancellationToken);
            ErrorMessage = "Meta no devolvió los datos necesarios para completar la conexión.";
            return Page();
        }

        try
        {
            var response = await apiClient.CompleteWhatsAppSignupAsync(
                new EmbeddedSignupCompletionRequestDTO
                {
                    AuthorizationCode = AuthorizationCode,
                    WabaId = WabaId,
                    PhoneNumberId = string.IsNullOrWhiteSpace(PhoneNumberId) ? null : PhoneNumberId,
                    OnboardingType = OnboardingType
                },
                cancellationToken);
            if (response?.Success != true || response.Data is null)
            {
                await LoadIntegrationAsync(cancellationToken);
                ErrorMessage ??= response?.Error?.Message ?? "No se pudo completar la conexión de Meta.";
                return Page();
            }

            Integration = null;
            await LoadIntegrationAsync(cancellationToken);
            if (IsConnected)
            {
                SuccessMessage = "WhatsApp Business quedó conectado mediante el proceso oficial de Meta.";
            }
            else
            {
                ErrorMessage ??= "Meta finalizó el proceso, pero la API no confirmó la integración persistida.";
            }
            return Page();
        }
        catch (AdminApiTimeoutException)
        {
            PendingMessage =
                "La confirmación de Meta está tardando. No se confirmó un fallo definitivo; se consultará el estado persistido.";
            await LoadIntegrationAsync(cancellationToken);
            if (IsConnected)
            {
                PendingMessage =
                    "La respuesta de completion tardó, pero una consulta posterior confirmó la integración persistida.";
            }
            return Page();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. La conexión no fue confirmada.";
            await LoadIntegrationAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDisconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await apiClient.DisconnectWhatsAppAsync(cancellationToken);
            SuccessMessage = "La integración se desconectó y el bot quedó desactivado.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. La integración no fue modificada.";
        }

        await LoadIntegrationAsync(cancellationToken);
        return Page();
    }

    public string ConnectionLabel(ConnectionState? state) =>
        state == ConnectionState.Connected && !IsConnected
            ? "Error"
            : state switch
    {
        ConnectionState.NotConnected or null => "No conectado",
        ConnectionState.Configuring => "Pendiente",
        ConnectionState.Connected => "Conectado",
        ConnectionState.Error => "Error",
        ConnectionState.Unknown => "Estado indeterminado",
        _ => "Estado desconocido"
    };

    private async Task LoadIntegrationAsync(CancellationToken cancellationToken)
    {
        Integration = null;
        ApiResponseAvailable = false;
        try
        {
            var response = await apiClient.GetWhatsAppIntegrationAsync(cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                ApiResponseAvailable = true;
                Integration = response.Data;
                ErrorMessage ??= ConnectionErrorMessage(Integration.ConnectionErrorCode);
            }
            else
            {
                ErrorMessage ??= response?.Error?.Message ?? "No se pudo consultar el estado de WhatsApp.";
            }
        }
        catch (AdminApiTimeoutException)
        {
            ErrorMessage ??= "No se pudo consultar la API dentro del tiempo esperado.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage ??= "No se pudo contactar la API.";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage ??= "No se pudo consultar la API dentro del tiempo esperado.";
        }
    }

    private static bool HasSignupConfiguration(WhatsAppIntegrationDTO integration) =>
        !string.IsNullOrWhiteSpace(integration.MetaAppId) &&
        !string.IsNullOrWhiteSpace(integration.MetaGraphApiVersion) &&
        (!string.IsNullOrWhiteSpace(integration.EmbeddedSignupConfigId) ||
         !string.IsNullOrWhiteSpace(integration.CoexistenceEmbeddedSignupConfigId));

    private static bool HasCompleteConnection(WhatsAppIntegrationDTO? integration) =>
        integration?.ConnectionState == ConnectionState.Connected &&
        !string.IsNullOrWhiteSpace(integration.WabaId) &&
        !string.IsNullOrWhiteSpace(integration.PhoneNumberId) &&
        !string.IsNullOrWhiteSpace(integration.BusinessPhoneNumber) &&
        integration.HasCredentialReference &&
        integration.ConnectedAtUtc.HasValue;

    private static string? ConnectionErrorMessage(string? errorCode) => errorCode switch
    {
        "MetaAuthorizationFailed" => "Meta no pudo completar la autorización del negocio.",
        "MetaPhoneNumberNotFound" => "Meta no devolvió un número empresarial asociado al WABA.",
        "MetaPhoneNumberSelectionAmbiguous" =>
            "Meta devolvió varios números y no se puede determinar con seguridad cuál conectar.",
        "MetaPhoneNumberNotInWaba" => "El número seleccionado no pertenece al WABA autorizado.",
        "SecretStoreUnavailable" => "No se pudo guardar de forma segura la credencial de WhatsApp.",
        "MetaConfigurationMissing" => "La configuración de Meta del servidor está incompleta.",
        "IntegrationPersistenceNotConfirmed" =>
            "El backend no pudo confirmar la integración persistida.",
        "IntegrationPersistenceFailed" => "No se pudo persistir el resultado de la integración.",
        "MetaRequestRejected" => "Meta rechazó una operación requerida para completar la conexión.",
        "CompletionCanceled" =>
            "La solicitud fue cancelada antes de confirmar la conexión. Consulta el estado antes de reintentar.",
        "CompletionTimedOut" =>
            "Meta tardó más de lo esperado. Consulta el estado persistido antes de reintentar.",
        "CompletionStateUnknown" =>
            "La operación externa pudo haber avanzado, pero la conexión no pudo confirmarse. Consulta el estado persistido.",
        "CompletionStatePersistenceFailed" =>
            "La operación terminó sin poder persistir su diagnóstico. Consulta el estado antes de reintentar.",
        _ => null
    };
}
