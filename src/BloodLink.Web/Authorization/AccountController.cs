using System.Text;
using BloodLink.Application.Security;
using BloodLink.Domain.Entities;
using BloodLink.Infrastructure.Data;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace BloodLink.Web.Authorization;

// Razor components also register POST routes for SSR forms. Prefer these explicit HTTP actions.
[Route("account", Order = -1)]
[AutoValidateAntiforgeryToken]
[EnableRateLimiting("account")]
[RequestSizeLimit(16 * 1024)]
public sealed class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    AccountAccessService access,
    IPasswordResetDelivery delivery,
    IConfiguration configuration,
    ILogger<AccountController> logger,
    BloodLinkDbContext database) : Controller
{
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginInput input)
    {
        const string failed = "/account/login?status=invalid";
        if (!ModelState.IsValid) return LocalRedirect(failed);
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null) return LocalRedirect(failed);

        var result = await signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, lockoutOnFailure: true);
        // This MVP has no MFA challenge UI: a RequiresTwoFactor result never becomes an application session.
        if (!result.Succeeded) return LocalRedirect(failed);

        user.LastLoginAtUtc = DateTime.UtcNow;
        if (!(await users.UpdateAsync(user)).Succeeded)
        {
            await signIn.SignOutAsync();
            return LocalRedirect(failed);
        }
        await AuditAsync(user, "AccountLogin");
        return LocalRedirect(user.MustChangePassword ? "/account/change-password" : SafeReturnUrl(input.ReturnUrl));
    }

    [Authorize(Policy = AccountSessionRequirement.Policy), HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return LocalRedirect("/account/login?status=signed-out");
    }

    [Authorize(Policy = AccountSessionRequirement.Policy), HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordInput input)
    {
        const string failed = "/account/change-password?status=invalid";
        if (!ModelState.IsValid || input.CurrentPassword == input.NewPassword) return LocalRedirect(failed);
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded) return LocalRedirect(failed);

        user.MustChangePassword = false;
        if (!(await users.UpdateAsync(user)).Succeeded)
        {
            await signIn.SignOutAsync();
            return LocalRedirect("/account/login");
        }
        await signIn.RefreshSignInAsync(user);
        await AuditAsync(user, "AccountPasswordChanged");
        return LocalRedirect("/account/manage?status=password-changed");
    }

    [AllowAnonymous, HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordInput input)
    {
        // Same response for invalid, unknown, inactive, blocked and eligible accounts.
        const string completed = "/account/forgot-password?status=requested";
        if (!ModelState.IsValid || !delivery.IsConfigured) return LocalRedirect(completed);
        // Never build security links from the untrusted request Host header.
        if (!Uri.TryCreate(configuration["Account:PublicOrigin"], UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            return LocalRedirect(completed);

        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || (await access.FindAsync(user.Id))?.CanSignIn != true) return LocalRedirect(completed);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = QueryHelpers.AddQueryString(new Uri(origin, "/account/reset-password").AbsoluteUri,
            new Dictionary<string, string?> { ["email"] = user.Email, ["code"] = code });
        try
        {
            await delivery.SendAsync(user.Email!, link, HttpContext.RequestAborted);
        }
        catch (Exception) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Provider failures must not enumerate accounts or expose a token in an exception page/log.
            logger.LogWarning("Password reset delivery failed. Check the configured delivery provider.");
            return LocalRedirect(completed);
        }
        return LocalRedirect(completed);
    }

    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromForm] ResetPasswordInput input)
    {
        const string failed = "/account/reset-password?status=invalid";
        if (!ModelState.IsValid) return LocalRedirect(failed);
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(input.Code)); }
        catch (FormatException) { return LocalRedirect(failed); }

        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null) return LocalRedirect(failed);
        var result = await users.ResetPasswordAsync(user, token, input.NewPassword);
        if (!result.Succeeded) return LocalRedirect(failed);

        user.MustChangePassword = false;
        if (!(await users.UpdateAsync(user)).Succeeded) return LocalRedirect(failed);
        // ResetPasswordAsync rotates the security stamp. No automatic sign-in or account activation.
        await signIn.SignOutAsync();
        await AuditAsync(user, "AccountPasswordReset");
        return LocalRedirect("/account/login?status=password-reset");
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
        && !returnUrl.Any(char.IsControl) && !returnUrl.Contains('\\')
            ? returnUrl : "/account/manage";

    private async Task AuditAsync(ApplicationUser user, string action)
    {
        database.AuditLogs.Add(new AuditLog
        {
            ActorUserId = user.Id,
            FacilityId = user.FacilityId,
            Action = action,
            EntityType = nameof(ApplicationUser),
            Summary = action,
            CreatedAtUtc = DateTime.UtcNow
        });
        await database.SaveChangesAsync(HttpContext.RequestAborted);
    }
}
