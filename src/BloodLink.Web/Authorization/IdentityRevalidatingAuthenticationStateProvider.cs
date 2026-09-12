using BloodLink.Infrastructure.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace BloodLink.Web.Authorization;

public class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var access = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var principal = authenticationState.User;
        var account = await access.FindAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        // Account setup uses static HTTP forms. Existing operational circuits must lose authority
        // when staff activation or mandatory password state changes, even if the stamp is unchanged.
        return access.MatchesSession(principal, account) && account?.CanOperate == true;
    }
}
