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
    public WhatsAppMigrationPreflightDTO? MigrationPreflight { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public string? PendingMessage { get; private set; }
    public bool ApiResponseAvailable { get; private set; }
    public bool IsConnected => HasCompleteConnection(Integration);
    public bool IsMigrationBlocking => Integration?.TransitionState is not (
        TransitionState.None or TransitionState.Completed or null);
    public bool CanCancelMigration =>
        IsMigrationBlocking && Integration is not null && !Integration.MigrationExternalMutationOccurred;
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

    [BindProperty]
    public bool ImportHistory { get; set; }

    [BindProperty, MaxLength(256)]
    public string? MigrationApprovalReference { get; set; }

    [BindProperty, MaxLength(2048)]
    public string? MigrationOperationalNotes { get; set; }

    [BindProperty]
    public TransitionState MigrationNextState { get; set; } = TransitionState.AwaitingCoexistenceSignup;

    [BindProperty, MaxLength(256)]
    public string? MigrationExternalOperationReferenceId { get; set; }

    [BindProperty, MaxLength(100)]
    public string MigrationSafeErrorCode { get; set; } = "MigrationStateUnknown";

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
                    OnboardingType = OnboardingType,
                    ImportHistory = ImportHistory
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

    public async Task<IActionResult> OnPostBeginMigrationAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadIntegrationAsync(cancellationToken);
            return Page();
        }

        try
        {
            var response = await apiClient.BeginWhatsAppMigrationAsync(
                new BeginWhatsAppMigrationRequestDTO
                {
                    ApprovalReference = MigrationApprovalReference,
                    OperationalNotes = MigrationOperationalNotes
                },
                cancellationToken);
            if (response?.Success == true)
            {
                SuccessMessage = "La migración interna quedó preparada. No se ejecutó ninguna operación externa.";
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudo preparar la migración interna.";
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. La migración interna no fue modificada.";
        }

        await LoadIntegrationAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelMigrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.CancelWhatsAppMigrationAsync(cancellationToken);
            if (response?.Success == true)
            {
                SuccessMessage = "La migración interna fue cancelada. No se ejecutó ninguna operación externa.";
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "La migración no se pudo cancelar.";
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. La migración interna no fue modificada.";
        }

        await LoadIntegrationAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkExternalMutationAsync(CancellationToken cancellationToken)
    {
        if (Integration?.MigrationId is null)
        {
            ErrorMessage = "No hay una migración interna activa.";
            await LoadIntegrationAsync(cancellationToken);
            return Page();
        }

        try
        {
            var response = await apiClient.MarkWhatsAppMigrationExternalMutationAsync(
                new WhatsAppMigrationExternalMutationRequestDTO
                {
                    MigrationId = Integration.MigrationId.Value,
                    NextState = MigrationNextState,
                    ExternalOperationReferenceId = MigrationExternalOperationReferenceId
                },
                cancellationToken);
            if (response?.Success == true)
            {
                SuccessMessage = "La fase externa fue registrada. Este control no ejecuta ninguna operación sobre Meta.";
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudo registrar la fase externa.";
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. El estado de migración no fue modificado.";
        }

        await LoadIntegrationAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkMigrationUnknownAsync(CancellationToken cancellationToken)
    {
        if (Integration?.MigrationId is null)
        {
            ErrorMessage = "No hay una migración interna activa.";
            await LoadIntegrationAsync(cancellationToken);
            return Page();
        }

        try
        {
            var response = await apiClient.MarkWhatsAppMigrationUnknownAsync(
                new WhatsAppMigrationUnknownRequestDTO
                {
                    MigrationId = Integration.MigrationId.Value,
                    SafeErrorCode = MigrationSafeErrorCode,
                    ExternalOperationReferenceId = MigrationExternalOperationReferenceId
                },
                cancellationToken);
            if (response?.Success == true)
            {
                SuccessMessage = "La migración quedó bloqueada en estado indeterminado. No se ejecutó ninguna operación externa.";
            }
            else
            {
                ErrorMessage = response?.Error?.Message ?? "No se pudo registrar el estado indeterminado.";
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo contactar la API. El estado de migración no fue modificado.";
        }

        await LoadIntegrationAsync(cancellationToken);
        return Page();
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
        !ApiResponseAvailable
            ? "Estado no disponible"
            : IsMigrationBlocking
            ? "Migración interna en curso"
            : state == ConnectionState.Connected && !IsConnected
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
        MigrationPreflight = null;
        ApiResponseAvailable = false;
        try
        {
            var response = await apiClient.GetWhatsAppIntegrationAsync(cancellationToken);
            if (response?.Success == true && response.Data is not null)
            {
                ApiResponseAvailable = true;
                Integration = response.Data;
                ErrorMessage ??= ConnectionErrorMessage(Integration.ConnectionErrorCode);
                await LoadMigrationPreflightAsync(cancellationToken);
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

    private async Task LoadMigrationPreflightAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await apiClient.GetWhatsAppMigrationPreflightAsync(cancellationToken);
            if (response?.Success == true)
            {
                MigrationPreflight = response.Data;
            }
        }
        catch (AdminApiTimeoutException)
        {
            // The integration status remains useful when the optional preflight query times out.
        }
        catch (HttpRequestException)
        {
            // The integration status remains useful when the optional preflight query fails.
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
        integration.TenantId.HasValue &&
        integration.OnboardingType.HasValue &&
        integration.HasCredentialReference &&
        integration.ConnectedAtUtc.HasValue &&
        string.IsNullOrWhiteSpace(integration.ConnectionErrorCode);

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
