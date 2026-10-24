using System.Security.Cryptography;
using System.Text;
using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Integrations.Meta;

public sealed class MetaWebhookChallengeValidator : IMetaWebhookChallengeValidator
{
    public bool IsValid(string mode, string suppliedToken, string expectedToken)
    {
        if (!string.Equals(mode, "subscribe", StringComparison.Ordinal) ||
            string.IsNullOrEmpty(suppliedToken) ||
            string.IsNullOrEmpty(expectedToken))
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
