using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Infrastructure.MediaStorage;

public sealed class LocalPrivateMediaStorage(
    IOptions<MediaStorageOptions> options) : IMediaStorage
{
    private static readonly Regex ReferencePattern = new(
        "^media/[a-f0-9]{32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<MediaStorageObject> StorePrivateAsync(
        Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new MediaProcessingException("MediaInvalidMime");
        }

        var root = GetRootPath();
        Directory.CreateDirectory(root);
        var identifier = Guid.NewGuid().ToString("N");
        var storageReference = $"media/{identifier}";
        var temporaryPath = Path.Combine(root, $".{identifier}.tmp");
        var finalPath = GetFilePath(storageReference, root);
        long size = 0;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));
        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    var read = await content.ReadAsync(buffer, deadline.Token);
                    if (read == 0)
                    {
                        break;
                    }

                    size += read;
                    if (size > options.Value.MaxFileBytes)
                    {
                        throw new MediaProcessingException("MediaTooLarge");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                }

                await output.FlushAsync(deadline.Token);
            }

            File.Move(temporaryPath, finalPath);
            return new MediaStorageObject(storageReference, size);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MediaProcessingException("MediaStorageFailed", true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                    // Cleanup must not replace the original provider/storage failure.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cleanup must not replace the original provider/storage failure.
                }
            }
        }
    }

    public Task<Stream?> OpenReadPrivateAsync(
        string storageReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetFilePath(storageReference, GetRootPath());
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeletePrivateAsync(string storageReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetFilePath(storageReference, GetRootPath());
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetRootPath()
    {
        var configured = options.Value.PrivateContainer;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WhatsAppBot",
                "private-media")
            : Path.GetFullPath(configured);
    }

    private static string GetFilePath(string storageReference, string root)
    {
        if (!ReferencePattern.IsMatch(storageReference))
        {
            throw new ArgumentException("The private media reference is invalid.", nameof(storageReference));
        }

        return Path.Combine(root, storageReference["media/".Length..]);
    }
}
