using Microsoft.AspNetCore.Authorization;

namespace BloodLink.Infrastructure.Identity;

public sealed class AccountSessionRequirement : IAuthorizationRequirement
{
    public const string Policy = "AccountSession";
}

public sealed class AccountSessionHandler(AccountAccessService access)
    : AuthorizationHandler<AccountSessionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AccountSessionRequirement requirement)
    {
        if (await access.ValidateSessionAsync(context.User)) context.Succeed(requirement);
    }
}
