using WhatsAppBot.Api.Models.Entities;
using WhatsAppBot.Shared.Enums;

namespace WhatsAppBot.Business.Tests;

public sealed class TenantDomainModelTests
{
    [Fact]
    public void ConversationKeepsAttentionAndLifecycleAsIndependentDimensions()
    {
        var tenantId = Guid.NewGuid();
        var conversation = new Conversation(
            42,
            Guid.NewGuid(),
            7,
            ConversationStatus.Closed,
            DateTimeOffset.UtcNow)
        {
            TenantId = tenantId,
            AttentionMode = AttentionMode.Humano,
            LifecycleStatus = LifecycleStatus.Cerrada,
            AssignedAdminId = 19,
            Version = [1, 2, 3]
        };

        Assert.Equal(tenantId, conversation.TenantId);
        Assert.Equal(AttentionMode.Humano, conversation.AttentionMode);
        Assert.Equal(LifecycleStatus.Cerrada, conversation.LifecycleStatus);
        Assert.Equal(19, conversation.AssignedAdminId);
        Assert.Equal<byte>([1, 2, 3], conversation.Version!);
        Assert.NotEqual(
            conversation.AttentionMode.ToString(),
            conversation.LifecycleStatus.ToString());
    }

    [Fact]
    public void MessageCarriesTenantActorOriginAndOutcomeMetadata()
    {
        var tenantId = Guid.NewGuid();
        var message = new Message(
            8,
            Guid.NewGuid(),
            42,
            "wamid-8",
            MessageDirection.Outbound,
            MessageType.Text,
            "Reply",
            ReplySource.Admin,
            ProcessingState.Completed,
            DeliveryState.Sent,
            DateTimeOffset.UtcNow)
        {
            TenantId = tenantId,
            ActorType = ActorType.Admin,
            Origin = Origin.AdminPanel,
            CreatedByAdminId = 19,
            OutcomeCode = "MessagingWindowClosed"
        };

        Assert.Equal(tenantId, message.TenantId);
        Assert.Equal(ActorType.Admin, message.ActorType);
        Assert.Equal(Origin.AdminPanel, message.Origin);
        Assert.Equal(19, message.CreatedByAdminId);
        Assert.Equal("MessagingWindowClosed", message.OutcomeCode);
    }

    [Fact]
    public void TenantMembershipModelsCompanyAccessWithoutComplexRoles()
    {
        var tenantId = Guid.NewGuid();
        var membership = new TenantMembership(1, tenantId, 19, true);
        var integration = new WhatsAppIntegration(
            Guid.NewGuid(), null, null, null, null, ConnectionState.NotConnected, null, false)
        {
            TenantId = tenantId,
            OnboardingType = WhatsAppOnboardingType.Coexistence
        };

        Assert.Equal(tenantId, membership.TenantId);
        Assert.Equal(tenantId, integration.TenantId);
        Assert.Equal(WhatsAppOnboardingType.Coexistence, integration.OnboardingType);
    }
}
