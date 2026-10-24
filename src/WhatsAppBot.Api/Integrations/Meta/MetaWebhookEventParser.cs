using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Api.Integrations.Meta;

public sealed class MetaWebhookEventParser : IMetaWebhookEventParser
{
    private static readonly HashSet<string> IgnoredFields = new(StringComparer.Ordinal)
    {
        "history",
        "smb_app_state_sync",
        "smb_message_echoes"
    };

    public IReadOnlyList<NormalizedMetaEvent> Parse(ReadOnlyMemory<byte> payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!TryGetArray(root, "entry", out var entries))
        {
            throw new InvalidDataException("The Meta webhook is missing its entry list.");
        }

        var events = new List<NormalizedMetaEvent>();
        foreach (var entry in entries.EnumerateArray())
        {
            var wabaId = GetString(entry, "id");
            if (!TryGetArray(entry, "changes", out var changes))
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                var field = GetString(change, "field");
                if (field is null || IgnoredFields.Contains(field) ||
                    !string.Equals(field, "messages", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!change.TryGetProperty("value", out var value) ||
                    value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var hasMessages = TryGetArray(value, "messages", out var messages);
                var hasStatuses = TryGetArray(value, "statuses", out var statuses);
                if (!hasMessages && !hasStatuses)
                {
                    continue;
                }

                var phoneNumberId = value.TryGetProperty("metadata", out var metadata)
                    ? GetString(metadata, "phone_number_id")
                    : null;
                if (string.IsNullOrWhiteSpace(phoneNumberId) || phoneNumberId.Length > 64 ||
                    wabaId is { Length: > 64 })
                {
                    throw new InvalidDataException("The Meta webhook is missing its phone number identifier.");
                }

                var contacts = ParseContacts(value);
                if (hasMessages)
                {
                    foreach (var message in messages.EnumerateArray())
                    {
                        var providerMessageId = GetString(message, "id");
                        if (string.IsNullOrWhiteSpace(providerMessageId))
                        {
                            throw new InvalidDataException("A Meta message is missing its provider identifier.");
                        }

                        var type = GetString(message, "type");
                        var sender = GetString(message, "from");
                        var timestamp = ParseTimestamp(GetString(message, "timestamp"));
                        if (providerMessageId.Length > 256 || sender is null || sender.Length > 64)
                        {
                            throw new InvalidDataException("A Meta message contains an invalid identifier.");
                        }
                        var contactName = sender is not null && contacts.TryGetValue(sender, out var name)
                            ? name
                            : null;
                        if (string.Equals(type, "text", StringComparison.Ordinal))
                        {
                            var body = message.TryGetProperty("text", out var textObject) &&
                                       textObject.ValueKind == JsonValueKind.Object
                                ? GetString(textObject, "body")
                                : null;
                            if (string.IsNullOrWhiteSpace(body) || body.Length > 4096)
                            {
                                throw new InvalidDataException("A Meta text message is empty or too large.");
                            }

                            events.Add(new NormalizedMetaEvent(
                                $"message:{BuildEventKey($"{phoneNumberId}:{providerMessageId}")}",
                                MetaWebhookEventTypes.InboundText,
                                phoneNumberId,
                                wabaId,
                                sender,
                                contactName,
                                providerMessageId,
                                body,
                                timestamp,
                                null)
                            {
                                MessageType = MessageType.Text
                            });
                        }
                        else if (string.Equals(type, "audio", StringComparison.Ordinal) ||
                                 string.Equals(type, "image", StringComparison.Ordinal))
                        {
                            var media = message.TryGetProperty(type!, out var mediaObject) &&
                                        mediaObject.ValueKind == JsonValueKind.Object
                                ? mediaObject
                                : throw new InvalidDataException("A Meta media message is missing its media object.");
                            var mediaId = GetString(media, "id");
                            var mimeType = GetString(media, "mime_type");
                            var caption = GetString(media, "caption");
                            if (string.IsNullOrWhiteSpace(mediaId) || mediaId.Length > 128 ||
                                (mimeType is not null && mimeType.Length > 128) ||
                                (caption is not null && caption.Length > 4096))
                            {
                                throw new InvalidDataException("A Meta media message contains invalid metadata.");
                            }

                            var messageType = string.Equals(type, "audio", StringComparison.Ordinal)
                                ? MessageType.Audio
                                : MessageType.Image;
                            var eventType = messageType == MessageType.Audio
                                ? MetaWebhookEventTypes.InboundAudio
                                : MetaWebhookEventTypes.InboundImage;
                            events.Add(new NormalizedMetaEvent(
                                $"message:{BuildEventKey($"{phoneNumberId}:{providerMessageId}")}",
                                eventType,
                                phoneNumberId,
                                wabaId,
                                sender,
                                contactName,
                                providerMessageId,
                                null,
                                timestamp,
                                null)
                            {
                                MessageType = messageType,
                                MediaId = mediaId,
                                MediaMimeType = mimeType,
                                MediaCaption = caption
                            });
                        }
                        else
                        {
                            events.Add(new NormalizedMetaEvent(
                                $"unsupported:{BuildEventKey($"{phoneNumberId}:{providerMessageId}")}",
                                "UnsupportedMessage",
                                phoneNumberId,
                                wabaId,
                                sender,
                                contactName,
                                providerMessageId,
                                null,
                                timestamp,
                                null)
                            {
                                MessageType = MessageType.Unsupported
                            });
                        }
                    }
                }

                if (hasStatuses)
                {
                    foreach (var status in statuses.EnumerateArray())
                    {
                        var providerMessageId = GetString(status, "id");
                        var statusValue = GetString(status, "status");
                        if (string.IsNullOrWhiteSpace(providerMessageId) ||
                            providerMessageId.Length > 200 ||
                            string.IsNullOrWhiteSpace(statusValue) ||
                            statusValue.Length > 32)
                        {
                            throw new InvalidDataException("A Meta status event is malformed.");
                        }

                        var rawTimestamp = GetString(status, "timestamp");
                        var timestamp = ParseTimestamp(rawTimestamp);
                        var recipientId = GetString(status, "recipient_id");
                        if (recipientId is { Length: > 64 })
                        {
                            throw new InvalidDataException("A Meta status event contains an invalid recipient identifier.");
                        }
                        var unixTimestamp = timestamp.ToUnixTimeSeconds();
                        events.Add(new NormalizedMetaEvent(
                            $"status:{BuildEventKey($"{phoneNumberId}:{providerMessageId}:{statusValue}:{unixTimestamp}")}",
                            MetaWebhookEventTypes.DeliveryStatus,
                            phoneNumberId,
                            wabaId,
                            recipientId,
                            null,
                            providerMessageId,
                            null,
                            timestamp,
                            statusValue));
                    }
                }
            }
        }

        return events;
    }

    private static Dictionary<string, string?> ParseContacts(JsonElement value)
    {
        var contacts = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!TryGetArray(value, "contacts", out var contactArray))
        {
            return contacts;
        }

        foreach (var contact in contactArray.EnumerateArray())
        {
            var id = GetString(contact, "wa_id");
            if (id is null || id.Length > 64)
            {
                continue;
            }

            var profileName = contact.TryGetProperty("profile", out var profile)
                ? GetString(profile, "name")
                : null;
            contacts[id] = profileName is { Length: > 256 } ? profileName[..256] : profileName;
        }

        return contacts;
    }

    private static bool TryGetArray(JsonElement element, string name, out JsonElement array)
    {
        if (element.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset ParseTimestamp(string? value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var unixTimestamp) ||
            unixTimestamp > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            throw new InvalidDataException("A Meta event has an invalid timestamp.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
    }

    private static string BuildEventKey(string stableProviderIdentity) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableProviderIdentity)))
            .ToLowerInvariant();
}
