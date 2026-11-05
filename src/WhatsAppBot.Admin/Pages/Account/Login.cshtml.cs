using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhatsAppBot.Admin.Authentication;
using WhatsAppBot.Admin.Services;
using WhatsAppBot.Shared.Requests;

namespace WhatsAppBot.Admin.Pages.Account;

public sealed class LoginModel(
    IWhatsAppBotApiClient apiClient,
    IAdminSession adminSession,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var response = await apiClient.LoginAsync(
                new LoginRequestDTO { Email = Input.Email, Password = Input.Password }, cancellationToken);
            if (response is not { Success: true, Data: not null })
            {
                ModelState.AddModelError(string.Empty, "El correo electrónico o la contraseña no son válidos.");
                return Page();
            }

            adminSession.SetApiAccessToken(response.Data.AccessToken);
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, Input.Email.Trim())],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
            return RedirectToPage("/Dashboard/Index");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Admin API login request failed: {FailureType}", exception.GetType().Name);
            ModelState.AddModelError(string.Empty, "No fue posible conectar con el servicio de autenticación.");
            return Page();
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Admin API login request timed out: {FailureType}", exception.GetType().Name);
            ModelState.AddModelError(string.Empty, "El servicio de autenticación no respondió a tiempo.");
            return Page();
        }
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress, MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(1024), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
