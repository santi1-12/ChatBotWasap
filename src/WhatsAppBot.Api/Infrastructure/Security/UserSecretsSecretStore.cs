using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration.UserSecrets;
using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Infrastructure.Security;

public sealed class UserSecretsSecretStore(
    IConfiguration configuration,
    IHostEnvironment environment) : ISecretStore
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public async ValueTask<string?> ResolveAsync(
        string secretReference, CancellationToken cancellationToken)
    {
        var configuredValue = configuration[secretReference];
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue;
        }

        if (!environment.IsDevelopment())
        {
            return null;
        }

        var root = await ReadSecretsAsync(cancellationToken);
        JsonNode? current = root;
        foreach (var segment in secretReference.Split(ConfigurationPath.KeyDelimiter))
        {
            current = current?[segment];
        }

        return current?.GetValue<string>();
    }

    public async Task<string> StoreAsync(
        string secretName, string secretValue, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "A managed writable secret-store adapter must be configured outside Development.");
        }
        if (string.IsNullOrWhiteSpace(secretName) || string.IsNullOrWhiteSpace(secretValue))
        {
            throw new ArgumentException("A secret name and value are required.");
        }

        var safeName = new string(secretName
            .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            .Take(48)
            .ToArray());
        if (safeName.Length == 0)
        {
            throw new ArgumentException("The secret name must contain letters or digits.", nameof(secretName));
        }
        var secretReference = $"Meta:BusinessAccessTokens:{safeName}:{Guid.NewGuid():N}";
        var path = GetSecretsPath();
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var root = await ReadSecretsFileAsync(path, cancellationToken);
            var current = (JsonObject)root;
            var segments = secretReference.Split(ConfigurationPath.KeyDelimiter);
            foreach (var segment in segments[..^1])
            {
                current[segment] ??= new JsonObject();
                current = current[segment] as JsonObject
                    ?? throw new InvalidDataException("The User Secrets configuration contains an invalid key structure.");
            }
            current[segments[^1]] = secretValue;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            FileLock.Release();
        }

        return secretReference;
    }

    public async Task DeleteAsync(string secretReference, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "A managed writable secret-store adapter must be configured outside Development.");
        }
        if (!secretReference.StartsWith("Meta:BusinessAccessTokens:", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only the integration's business-token secret can be removed here.",
                nameof(secretReference));
        }

        var path = GetSecretsPath();
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var root = await ReadSecretsFileAsync(path, cancellationToken);
            JsonObject? current = root;
            var segments = secretReference.Split(ConfigurationPath.KeyDelimiter);
            foreach (var segment in segments[..^1])
            {
                current = current?[segment] as JsonObject;
                if (current is null)
                {
                    return;
                }
            }

            if (!current.Remove(segments[^1]))
            {
                return;
            }

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<JsonObject> ReadSecretsAsync(CancellationToken cancellationToken)
    {
        var path = GetSecretsPath();
        return File.Exists(path)
            ? await ReadSecretsFileAsync(path, cancellationToken)
            : new JsonObject();
    }

    private static async Task<JsonObject> ReadSecretsFileAsync(
        string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken) as JsonObject
            ?? new JsonObject();
    }

    private static string GetSecretsPath()
    {
        var secretsId = typeof(UserSecretsSecretStore).Assembly
            .GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;
        if (string.IsNullOrWhiteSpace(secretsId))
        {
            throw new InvalidOperationException("The API User Secrets identifier is not configured.");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "UserSecrets",
            secretsId,
            "secrets.json");
    }
}
