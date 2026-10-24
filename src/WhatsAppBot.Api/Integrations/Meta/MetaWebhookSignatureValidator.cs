using System.Security.Cryptography;
using System.Text;
using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Integrations.Meta;

public sealed class MetaWebhookSignatureValidator : IMetaWebhookSignatureValidator
{
    public bool IsValid(ReadOnlySpan<byte> payload, string? signature, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
            signature.Length != "sha256=".Length + 64 ||
            !signature.StartsWith("sha256=", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(appSecret))
        {
            return false;
        }

        byte[] suppliedDigest;
        try
        {
            suppliedDigest = Convert.FromHexString(signature.AsSpan("sha256=".Length));
        }
        catch (FormatException)
        {
            return false;
        }
        if (suppliedDigest.Length != 32)
        {
            return false;
        }

        Span<byte> expectedDigest = stackalloc byte[32];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), payload, expectedDigest);
        return CryptographicOperations.FixedTimeEquals(expectedDigest, suppliedDigest);
    }
}
