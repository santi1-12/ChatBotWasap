using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Options;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Integrations.Audio;

public sealed class AudioNormalizationService(
    IOptions<FfmpegOptions> options) : IAudioNormalizer
{
    public async Task<NormalizedAudio> NormalizeAsync(
        Stream audio,
        string contentType,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        var normalizedContentType = NormalizeContentType(contentType);
        if (normalizedContentType is null || !normalizedContentType.StartsWith("audio/", StringComparison.Ordinal))
        {
            throw new MediaProcessingException(MessageOutcomeCodes.MediaInvalidMime);
        }

        var settings = options.Value;
        var input = await ReadBoundedAsync(
            audio, Math.Min(maxBytes, settings.MaxInputBytes), cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ExecutablePath))
        {
            throw new MediaCapabilityUnavailableException(MessageOutcomeCodes.TranscriptionFailed);
        }

        var directory = Path.Combine(
            Path.GetTempPath(), "WhatsAppBot", $"audio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var inputPath = Path.Combine(directory, "input.bin");
        var outputPath = Path.Combine(directory, "output.wav");
        try
        {
            await File.WriteAllBytesAsync(inputPath, input, cancellationToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = settings.ExecutablePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-vn");
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add("16000");
            startInfo.ArgumentList.Add("-acodec");
            startInfo.ArgumentList.Add("pcm_s16le");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("wav");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(settings.MaxDurationSeconds.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(outputPath);

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed);
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            using var killRegistration = deadline.Token.Register(() => TryKill(process));
            var errorTask = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(deadline.Token);
                _ = await errorTask;
            }
            catch (OperationCanceledException)
            {
                await StopProcessAsync(process, errorTask);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed, true);
            }

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed);
            }

            await using var output = File.OpenRead(outputPath);
            var normalized = await ReadBoundedAsync(output, settings.MaxOutputBytes, cancellationToken);
            return new NormalizedAudio(normalized, "audio/wav");
        }
        catch (MediaProcessingException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed, true);
        }
        catch (IOException)
        {
            throw new MediaProcessingException(MessageOutcomeCodes.TranscriptionFailed, true);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(directory);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Cancellation must not be blocked by process cleanup.
        }
    }

    private static async Task StopProcessAsync(Process process, Task<string> errorTask)
    {
        TryKill(process);
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await process.WaitForExitAsync(cleanupTimeout.Token);
        }
        catch (Exception)
        {
            // The bounded operation already failed; preserve its safe outcome.
        }

        try
        {
            _ = await errorTask.WaitAsync(cleanupTimeout.Token);
        }
        catch (Exception)
        {
            // FFmpeg diagnostics are intentionally not returned to callers.
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string directory)
    {
        for (var attempt = 0; attempt < 3 && Directory.Exists(directory); attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        if (maxBytes < 1)
        {
            throw new MediaProcessingException(MessageOutcomeCodes.MediaTooLarge);
        }

        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        long size = 0;
        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            size += read;
            if (size > maxBytes)
            {
                throw new MediaProcessingException(MessageOutcomeCodes.MediaTooLarge);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static string? NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType)
            ? null
            : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
}
