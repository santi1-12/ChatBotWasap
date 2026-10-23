namespace WhatsAppBot.Api.Helpers;

public sealed class CompletionExecutionContext
{
    public bool ExternalMutationMayHaveOccurred { get; set; }

    public string? CurrentPhase { get; internal set; }
}
