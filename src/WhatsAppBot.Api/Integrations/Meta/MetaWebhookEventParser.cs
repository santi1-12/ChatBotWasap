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
    private static readonly string[] MessageArrayNames =
    [
        "messages",
        "history",
        "events",
        "data"
    ];

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

            var changeIndex = 0;
            foreach (var change in changes.EnumerateArray())
            {
                var field = GetString(change, "field");
                if (field is null || string.Equals(field, "smb_app_state_sync", StringComparison.Ordinal))
                {
                    changeIndex++;
                    continue;
                }

                if (!change.TryGetProperty("value", out var value) ||
                    value.ValueKind != JsonValueKind.Object)
                {
                    changeIndex++;
                    continue;
                }

                var phoneNumberId = GetPhoneNumberId(value);
                if (string.IsNullOrWhiteSpace(phoneNumberId) || phoneNumberId.Length > 64 ||
                    wabaId is { Length: > 64 })
                {
                    throw new InvalidDataException("The Meta webhook is missing its phone number identifier.");
                }

                if (string.Equals(field, "messages", StringComparison.Ordinal))
                {
                    ParseMessages(value, phoneNumberId, wabaId, events);
                }
                else if (field is "history" or "smb_message_echoes")
                {
                    ParseCoexistenceMessages(
                        value, phoneNumberId, wabaId, field, entry, changeIndex, events);
                }

                changeIndex++;
            }
        }

        return events;
    }

    private static void ParseMessages(
        JsonElement value,
        string phoneNumberId,
        string? wabaId,
        ICollection<NormalizedMetaEvent> events)
    {
        var contacts = ParseContacts(value);
        var hasMessages = TryGetArray(value, "messages", out var messages);
        var hasStatuses = TryGetArray(value, "statuses", out var statuses);
        if (!hasMessages && !hasStatuses)
        {
            return;
        }

        if (hasMessages)
        {
            foreach (var message in messages.EnumerateArray())
            {
                var providerMessageId = GetString(message, "id");
                if (string.IsNullOrWhiteSpace(providerMessageId))
                {
                    throw new InvalidDataException("A Meta message is missing its provider identifier.");
                }

                events.Add(ParseInboundMessage(
                    message, phoneNumberId, wabaId, contacts, providerMessageId));
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

                var timestamp = ParseTimestamp(GetScalarString(status, "timestamp"));
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
                    statusValue)
                {
                    SourceField = "messages"
                });
            }
        }
    }

    private static NormalizedMetaEvent ParseInboundMessage(
        JsonElement message,
        string phoneNumberId,
        string? wabaId,
        IReadOnlyDictionary<string, string?> contacts,
        string providerMessageId)
    {
        var type = GetString(message, "type");
        var sender = GetString(message, "from");
        var timestamp = ParseTimestamp(GetScalarString(message, "timestamp"));
        if (providerMessageId.Length > 256 || sender is null || sender.Length > 64)
        {
            throw new InvalidDataException("A Meta message contains an invalid identifier.");
        }

        var contactName = contacts.TryGetValue(sender, out var name) ? name : null;
        if (string.Equals(type, "text", StringComparison.Ordinal))
        {
            var body = GetMessageText(message);
            if (string.IsNullOrWhiteSpace(body) || body.Length > 4096)
            {
                throw new InvalidDataException("A Meta text message is empty or too large.");
            }

            return new NormalizedMetaEvent(
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
                MessageType = MessageType.Text,
                SourceField = "messages",
                Direction = MessageDirection.Inbound,
                ActorType = ActorType.Customer,
                Origin = Origin.MetaCloudApi,
                SourceEventKey = $"message:{BuildEventKey($"{phoneNumberId}:{providerMessageId}")}"
            };
        }

        if (type is "audio" or "image")
        {
            var media = message.TryGetProperty(type, out var mediaObject) &&
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

            var messageType = type == "audio" ? MessageType.Audio : MessageType.Image;
            var eventType = messageType == MessageType.Audio
                ? MetaWebhookEventTypes.InboundAudio
                : MetaWebhookEventTypes.InboundImage;
            var eventKey = $"message:{BuildEventKey($"{phoneNumberId}:{providerMessageId}")}";
            return new NormalizedMetaEvent(
                eventKey,
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
                MediaCaption = caption,
                SourceField = "messages",
                Direction = MessageDirection.Inbound,
                ActorType = ActorType.Customer,
                Origin = Origin.MetaCloudApi,
                SourceEventKey = eventKey
            };
        }

        var unsupportedKey = $"unsupported:{BuildEventKey($"{phoneNumberId}:{providerMessageId}")}";
        return new NormalizedMetaEvent(
            unsupportedKey,
            MetaWebhookEventTypes.UnsupportedMessage,
            phoneNumberId,
            wabaId,
            sender,
            contactName,
            providerMessageId,
            null,
            timestamp,
            null)
        {
            MessageType = MessageType.Unsupported,
            SourceField = "messages",
            Direction = MessageDirection.Inbound,
            ActorType = ActorType.Customer,
            Origin = Origin.MetaCloudApi,
            SourceEventKey = unsupportedKey
        };
    }

    private static void ParseCoexistenceMessages(
        JsonElement value,
        string phoneNumberId,
        string? wabaId,
        string field,
        JsonElement entry,
        int changeIndex,
        ICollection<NormalizedMetaEvent> events)
    {
        if (!TryFindMessageArray(value, out var messages))
        {
            return;
        }

        var contacts = ParseContacts(value);
        var historyKey = GetFirstString(
            value, "history_id", "historyId", "sync_id", "syncId", "batch_id", "batchId", "session_id")
            ?? (string.Equals(field, "history", StringComparison.Ordinal)
                ? $"{phoneNumberId}:history"
                : $"{GetString(entry, "id") ?? "unknown"}:{field}:{changeIndex}");
        var historyTotalCount = GetInt(
            value, "total_count", "totalCount", "message_count", "messageCount")
            ?? messages.GetArrayLength();
        if (historyKey.Length > 256)
        {
            historyKey = historyKey[..256];
        }

        foreach (var rawMessage in messages.EnumerateArray())
        {
            var message = rawMessage;
            if (message.TryGetProperty("message", out var nested) &&
                nested.ValueKind == JsonValueKind.Object)
            {
                message = nested;
            }

            var providerMessageId = GetFirstString(message, "id", "message_id", "messageId", "wamid");
            var timestamp = ParseTimestamp(GetScalarString(message, "timestamp", "time", "created_at"));
            var from = GetFirstString(message, "from", "sender", "sender_id");
            var to = GetFirstString(message, "to", "recipient", "recipient_id");
            var isGroup = IsGroupMessage(message);
            var outbound = string.Equals(field, "smb_message_echoes", StringComparison.Ordinal) ||
                           IsOutboundHistoryMessage(message, from, phoneNumberId, value);
            var whatsappUserId = outbound ? to : from;
            if (string.IsNullOrWhiteSpace(whatsappUserId) || whatsappUserId.Length > 64)
            {
                throw new InvalidDataException("A Coexistence message is missing its WhatsApp user identifier.");
            }

            var identity = providerMessageId is not null
                ? $"provider:{providerMessageId}"
                : $"{historyKey}:{message.GetRawText()}";
            var eventType = outbound
                ? MetaWebhookEventTypes.OutboundAppMessage
                : MetaWebhookEventTypes.HistoricalMessage;
            var eventKey = $"{field}:{BuildEventKey($"{phoneNumberId}:{historyKey}:{identity}")}";
            var type = GetString(message, "type") ?? InferMessageType(message);
            var (messageType, text, mediaId, mimeType, caption) = ParseCoexistenceContent(message, type);
            var contactName = contacts.TryGetValue(whatsappUserId, out var name) ? name : null;

            events.Add(new NormalizedMetaEvent(
                eventKey,
                eventType,
                phoneNumberId,
                wabaId,
                whatsappUserId,
                contactName,
                providerMessageId,
                text,
                timestamp,
                null)
            {
                MessageType = messageType,
                MediaId = mediaId,
                MediaMimeType = mimeType,
                MediaCaption = caption,
                SourceField = field,
                Direction = outbound ? MessageDirection.Outbound : MessageDirection.Inbound,
                ActorType = outbound ? ActorType.Admin : ActorType.Customer,
                Origin = outbound ? Origin.FutureWhatsAppBusinessApp : Origin.MetaCloudApi,
                IsHistorical = string.Equals(field, "history", StringComparison.Ordinal),
                IsGroup = isGroup,
                HistoryImportKey = string.Equals(field, "history", StringComparison.Ordinal)
                    ? historyKey
                    : null,
                HistoryTotalCount = string.Equals(field, "history", StringComparison.Ordinal)
                    ? historyTotalCount
                    : 0,
                RecipientWhatsAppUserId = to,
                SourceEventKey = eventKey
            });
        }
    }

    private static (MessageType Type, string? Text, string? MediaId, string? MimeType, string? Caption)
        ParseCoexistenceContent(JsonElement message, string? type)
    {
        if (type == "text" || (type is null && message.TryGetProperty("text", out _)))
        {
            var text = GetMessageText(message);
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4096)
            {
                throw new InvalidDataException("A Coexistence text message is empty or too large.");
            }

            return (MessageType.Text, text, null, null, null);
        }

        if (type is "audio" or "image")
        {
            var media = message.TryGetProperty(type, out var mediaObject) &&
                        mediaObject.ValueKind == JsonValueKind.Object
                ? mediaObject
                : throw new InvalidDataException("A Coexistence media message is missing its media object.");
            return (
                type == "audio" ? MessageType.Audio : MessageType.Image,
                null,
                GetString(media, "id"),
                GetString(media, "mime_type"),
                GetString(media, "caption"));
        }

        return (MessageType.Unsupported, null, null, null, null);
    }

    private static bool IsOutboundHistoryMessage(
        JsonElement message, string? from, string phoneNumberId, JsonElement value)
    {
        var explicitDirection = GetFirstString(message, "direction", "message_direction");
        if (string.Equals(explicitDirection, "outbound", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(explicitDirection, "sent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (GetBoolean(message, "from_me") == true || GetBoolean(message, "fromMe") == true ||
            GetBoolean(message, "is_from_me") == true)
        {
            return true;
        }

        var businessNumber = value.TryGetProperty("metadata", out var metadata)
            ? GetString(metadata, "display_phone_number")
            : null;
        return string.Equals(from, phoneNumberId, StringComparison.Ordinal) ||
               string.Equals(from, businessNumber, StringComparison.Ordinal);
    }

    private static bool IsGroupMessage(JsonElement message) =>
        GetBoolean(message, "is_group") == true ||
        GetBoolean(message, "isGroup") == true ||
        GetFirstString(message, "group_id", "groupId") is not null ||
        string.Equals(GetFirstString(message, "chat_type", "chatType", "conversation_type"),
            "group", StringComparison.OrdinalIgnoreCase);

    private static string? InferMessageType(JsonElement message) =>
        message.TryGetProperty("text", out _) ? "text" :
        message.TryGetProperty("audio", out _) ? "audio" :
        message.TryGetProperty("image", out _) ? "image" : null;

    private static string? GetMessageText(JsonElement message)
    {
        if (message.TryGetProperty("text", out var textObject) &&
            textObject.ValueKind == JsonValueKind.Object)
        {
            return GetString(textObject, "body") ?? GetString(textObject, "text");
        }

        return GetString(message, "body") ?? GetString(message, "text");
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

    private static string? GetPhoneNumberId(JsonElement value) =>
        value.TryGetProperty("metadata", out var metadata)
            ? GetString(metadata, "phone_number_id")
            : GetFirstString(value, "phone_number_id", "phoneNumberId");

    private static bool TryFindMessageArray(JsonElement value, out JsonElement array) =>
        TryFindMessageArray(value, 0, out array);

    private static bool TryFindMessageArray(JsonElement value, int depth, out JsonElement array)
    {
        if (depth > 3)
        {
            array = default;
            return false;
        }

        foreach (var name in MessageArrayNames)
        {
            if (TryGetArray(value, name, out array))
            {
                return true;
            }
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object &&
                TryFindMessageArray(property.Value, depth + 1, out array))
            {
                return true;
            }
        }

        array = default;
        return false;
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

    private static string? GetFirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetScalarString(element, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetScalarString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetRawText();
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static int? GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number is >= 0 and <= 1_000_000 ? number : null;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out number))
            {
                return number is >= 0 and <= 1_000_000 ? number : null;
            }
        }

        return null;
    }

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
