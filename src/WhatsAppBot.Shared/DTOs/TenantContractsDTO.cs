namespace WhatsAppBot.Shared.DTOs;

public sealed class TenantDTO
{
    public Guid TenantId { get; init; }
    public required string LegalName { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class TenantMembershipDTO
{
    public long TenantMembershipId { get; init; }
    public Guid TenantId { get; init; }
    public long AdminId { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}
