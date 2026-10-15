using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Application.Web.Authentication;

public sealed class WebSessionRevalidation(ILoggerFactory loggerFactory, WebSessionTickets tickets)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(10);

    protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken) =>
        Task.FromResult(tickets.AccessToken(authenticationState.User.FindFirst(WebSessionTickets.SessionClaim)?.Value) is not null);
}
