using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Infrastructure.MediaStorage;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Integrations.Tests;

public sealed class MediaStorageTests
{
    [Fact]
    public async Task LocalStorage_UsesPrivateReferenceAndRoundTripsContent()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storage = new LocalPrivateMediaStorage(Options.Create(new MediaStorageOptions
            {
                PrivateContainer = root,
                MaxFileBytes = 1024
            }));
            var content = new MemoryStream([1, 2, 3, 4]);

            var stored = await storage.StorePrivateAsync(content, "image/jpeg", CancellationToken.None);

            Assert.StartsWith("media/", stored.StorageReference);
            Assert.DoesNotContain("http", stored.StorageReference, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(4, stored.SizeBytes);
            await using (var read = await storage.OpenReadPrivateAsync(
                             stored.StorageReference, CancellationToken.None))
            {
                Assert.NotNull(read);
                Assert.Equal([1, 2, 3, 4], await ReadAllAsync(read!));
            }

            await storage.DeletePrivateAsync(stored.StorageReference, CancellationToken.None);
            Assert.Null(await storage.OpenReadPrivateAsync(stored.StorageReference, CancellationToken.None));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task LocalStorage_RejectsContentAboveConfiguredLimit()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var storage = new LocalPrivateMediaStorage(Options.Create(new MediaStorageOptions
            {
                PrivateContainer = root,
                MaxFileBytes = 3
            }));

            var exception = await Assert.ThrowsAsync<MediaProcessingException>(() =>
                storage.StorePrivateAsync(new MemoryStream([1, 2, 3, 4]), "audio/ogg", CancellationToken.None));

            Assert.Equal(MessageOutcomeCodes.MediaTooLarge, exception.SafeCode);
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    [Fact]
    public async Task LocalStorage_RejectsUntrustedReferences()
    {
        var root = CreateTemporaryDirectory();
        var storage = new LocalPrivateMediaStorage(Options.Create(new MediaStorageOptions
        {
            PrivateContainer = root
        }));

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                storage.OpenReadPrivateAsync("../../public/file", CancellationToken.None));
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WhatsAppBotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }
}
