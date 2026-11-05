using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppBot.Api.Business.Abstractions;
using WhatsAppBot.Api.Infrastructure.Security;
using WhatsAppBot.Api.Middleware;
using WhatsAppBot.Api.Models.Contracts;
using WhatsAppBot.Api.Models.Enums;

namespace WhatsAppBot.Api.ContractTests;

public sealed class SecurityAndFailureTests
{
    [Fact]
    public async Task InvalidTenantClaimFailsClosed()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "19"),
                    new Claim(TenantClaimTypes.TenantId, "not-a-guid")
                ],
                "ContractTest"))
        };
        var context = new TenantContext(
            new HttpContextAccessor { HttpContext = httpContext },
            new NullTenantRepository());

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnauthenticatedRequestsCannotResolveTenantContext()
    {
        var context = new TenantContext(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new NullTenantRepository());

        await Assert.ThrowsAsync<TenantContextUnavailableException>(
            () => context.GetRequiredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GlobalExceptionMiddlewareReturnsSafeFailureWithoutProviderDetails()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-failure" };
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new ExternalDependencyException("access-token-and-provider-payload", true),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.Contains("ExternalDependencyFailure", body, StringComparison.Ordinal);
        Assert.Contains("trace-failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access-token-and-provider-payload", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalExceptionMiddlewareDoesNotConvertCallerCancellationToInternalError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new OperationCanceledException(cancellation.Token),
            NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Theory]
    [InlineData(MessageOutcomeCodes.MetaRequestTimedOut)]
    [InlineData(MessageOutcomeCodes.MetaUnavailable)]
    [InlineData(MessageOutcomeCodes.MetaSendFailed)]
    public void InternalProviderFailuresRemainAllowlistedAndNonSuccessShaped(string code)
    {
        Assert.True(MessageOutcomeCodeRules.IsAllowlisted(code));
    }

    private sealed class NullTenantRepository : ITenantRepository
    {
        public Task<TenantScope?> GetActiveForAdministratorAsync(
            long administratorId,
            Guid? tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantScope?>(null);
    }
}
