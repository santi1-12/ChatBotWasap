using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
