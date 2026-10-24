using System.Text;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Business.Services;

public sealed class AiContextBuilder
{
    public string Build(
        string currentCustomerMessage,
        ConversationDetailRecord? history,
        int maximumCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentCustomerMessage);
        if (maximumCharacters < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        }

        var builder = new StringBuilder();
        builder.AppendLine("Contexto permitido de una conversación de WhatsApp.");
        builder.AppendLine("No inventes datos ni reveles secretos. Si no puedes resolver, indícalo brevemente.");
        builder.AppendLine("Historial textual reciente:");

        if (history is not null)
        {
            foreach (var message in history.Messages.Where(message =>
                         message.MessageType == MessageType.Text ||
                         !string.IsNullOrWhiteSpace(message.Transcription)))
            {
                var content = message.Text ?? message.Transcription;
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                builder.Append(message.Direction == MessageDirection.Inbound ? "Cliente: " : "Asistente: ");
                builder.AppendLine(content);
            }
        }

        builder.Append("Cliente: ").AppendLine(currentCustomerMessage.Trim());
        var result = builder.ToString();
        return result.Length <= maximumCharacters
            ? result
            : result[^maximumCharacters..];
    }
}
