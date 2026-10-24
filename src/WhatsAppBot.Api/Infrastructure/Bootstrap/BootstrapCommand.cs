using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Infrastructure.Bootstrap;

public sealed class BootstrapCommand(
    IConfiguration configuration,
    IAdministratorBootstrapper bootstrapper,
    ILogger<BootstrapCommand> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var email = configuration["Bootstrap:AdminEmail"];
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "Set Bootstrap:AdminEmail in User Secrets or a protected environment variable before bootstrapping.");
        }

        var tenantLegalName = configuration["Bootstrap:TenantLegalName"]?.Trim();
        if (string.IsNullOrWhiteSpace(tenantLegalName))
        {
            tenantLegalName = "Initial Tenant";
        }

        var password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            password = ReadPassword();
        }

        if (password.Length < 14)
        {
            throw new InvalidOperationException("The bootstrap password must contain at least 14 characters.");
        }

        var created = await bootstrapper.CreateFirstAdministratorAsync(
            email, password, tenantLegalName, cancellationToken);
        if (!created)
        {
            logger.LogWarning("Administrator bootstrap refused because an administrator already exists.");
            Console.Error.WriteLine("Bootstrap refused: an administrator already exists.");
            Environment.ExitCode = 1;
            return;
        }

        logger.LogInformation("The first administrator was created.");
        Console.WriteLine("The first administrator was created. Remove Bootstrap:AdminEmail from User Secrets.");
    }

    private static string ReadPassword()
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                "Provide BOOTSTRAP_ADMIN_PASSWORD through a protected process environment variable when input is non-interactive.");
        }

        Console.Write("Initial administrator password (input hidden): ");
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(chars.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
            }
        }
    }
}
