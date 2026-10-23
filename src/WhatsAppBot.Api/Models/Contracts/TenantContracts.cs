namespace WhatsAppBot.Api.Models.Contracts;

public sealed record TenantRecord(
    Guid TenantId,
    string LegalName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TenantMembershipRecord(
    long TenantMembershipId,
    Guid TenantId,
    long AdminId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
