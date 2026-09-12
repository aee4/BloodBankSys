using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BloodLink.Infrastructure.Identity;

public sealed class BloodLinkSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    AccountAccessService access,
    LoginFailureWork failureWork)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        await base.CanSignInAsync(user) && (await access.FindAsync(user.Id))?.CanSignIn == true;

    public override async Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
    {
        var result = await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
        // Identity skips password verification for accounts denied by its pre-sign-in checks.
        // A failure that triggers lockout may do extra work; this is not a constant-time guarantee.
        if (result.IsNotAllowed || result.IsLockedOut)
            failureWork.Verify(UserManager.PasswordHasher, password);
        return result;
    }
}
