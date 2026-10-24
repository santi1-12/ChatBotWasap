using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Models.Contracts;

namespace WhatsAppBot.Api.Business.Services;

public sealed class WhatsAppMessagingPolicy : IWhatsAppMessagingPolicy
{
    private readonly TimeSpan customerServiceWindow;

    public WhatsAppMessagingPolicy(TimeSpan? customerServiceWindow = null)
    {
        this.customerServiceWindow = customerServiceWindow ?? TimeSpan.FromHours(24);
        if (this.customerServiceWindow <= TimeSpan.Zero || this.customerServiceWindow > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(customerServiceWindow));
        }
    }

    public MessagingPolicyDecision Evaluate(DateTimeOffset lastCustomerMessageAtUtc, DateTimeOffset nowUtc)
    {
        if (lastCustomerMessageAtUtc.Offset != TimeSpan.Zero || nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("WhatsApp messaging policy timestamps must be UTC.");
        }

        var age = nowUtc - lastCustomerMessageAtUtc;
        return age >= TimeSpan.Zero && age < customerServiceWindow
            ? new MessagingPolicyDecision(true, null)
            : new MessagingPolicyDecision(false, MessageOutcomeCodes.MessagingWindowClosed);
    }
}
