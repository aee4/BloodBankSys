using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BloodLink.Infrastructure.Identity;

public static class BloodLinkCookieValidation
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var access = context.HttpContext.RequestServices.GetRequiredService<AccountAccessService>();
        if (context.Principal is null || !await access.ValidateSessionAsync(
                context.Principal, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        // Preserve Identity's normal stamp validation and principal renewal behavior.
        await SecurityStampValidator.ValidatePrincipalAsync(context);
    }
}
