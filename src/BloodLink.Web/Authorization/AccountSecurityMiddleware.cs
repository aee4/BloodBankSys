using System.Security.Claims;
using BloodLink.Infrastructure.Identity;

namespace BloodLink.Web.Authorization;

public sealed class AccountSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AccountAccessService access)
    {
        // Account pages contain credentials or tokens; never cache them or forward reset URLs as referrers.
        if (context.Request.Path.StartsWithSegments("/account"))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var account = await access.FindAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier), context.RequestAborted);
            if (account?.User.MustChangePassword == true && !IsPasswordChangeAction(context.Request.Path))
            {
                if (HttpMethods.IsGet(context.Request.Method) && !context.Request.Path.StartsWithSegments("/_blazor"))
                    context.Response.Redirect("/account/change-password");
                else
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Password change required.");
                }
                return;
            }
        }

        await next(context);
    }

    private static bool IsPasswordChangeAction(PathString path) =>
        path.Equals("/account/change-password", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/account/logout", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/account/access-denied", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/_framework/blazor.web.js", StringComparison.OrdinalIgnoreCase);
}
