using System.Text;
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
[RequestSizeLimit(16 * 1024)]
public sealed class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    LoginFailureWork failureWork,
    PasswordRecoveryQueue recovery,
    BloodLinkDbContext database) : Controller
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("account-login")]
    public async Task<IActionResult> Login([FromForm] LoginInput input)
    {
        const string failed = "/account/login?status=invalid";
        if (!ModelState.IsValid) return LocalRedirect(failed);
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null)
        {
            failureWork.Verify(users.PasswordHasher, input.Password);
            return LocalRedirect(failed);
        }

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

    [Authorize(Policy = AccountSessionRequirement.Policy), HttpPost("logout"), DisableRateLimiting]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var user = await users.GetUserAsync(User);
            if (user is not null && !(await users.UpdateSecurityStampAsync(user)).Succeeded)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Sign-out could not revoke all sessions. Sign in and retry.");
        }
        finally
        {
            // Clear the current browser cookie even when the backing store cannot rotate the stamp.
            await signIn.SignOutAsync();
        }
        return LocalRedirect("/account/login?status=signed-out");
    }

    [Authorize(Policy = AccountSessionRequirement.Policy), HttpPost("change-password"), EnableRateLimiting("account-password")]
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

    [AllowAnonymous, HttpPost("forgot-password"), EnableRateLimiting("account-recovery")]
    public IActionResult ForgotPassword([FromForm] ForgotPasswordInput input)
    {
        // Same response for invalid, unknown, inactive, blocked and eligible accounts.
        const string completed = "/account/forgot-password?status=requested";
        // Account lookup and variable provider latency are off the public response path.
        if (ModelState.IsValid) recovery.TryEnqueue(input.Email.Trim());
        return LocalRedirect(completed);
    }

    [AllowAnonymous, HttpPost("reset-password"), EnableRateLimiting("account-password")]
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
