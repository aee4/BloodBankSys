namespace BloodLink.Web.Authorization;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var isAccount = context.Request.Path.StartsWithSegments("/account");
        context.Response.OnStarting(() =>
        {
            // Apply at response start so antiforgery cannot weaken the frame policy.
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = isAccount ? "no-referrer" : "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
