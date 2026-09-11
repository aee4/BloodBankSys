using System.ComponentModel.DataAnnotations;

namespace BloodLink.Web.Authorization;

public sealed class LoginInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(256)] public string Password { get; set; } = "";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
}

public sealed class ChangePasswordInput
{
    [Required, StringLength(256)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(256)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}

public sealed class ResetPasswordInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(4096)] public string Code { get; set; } = "";
    [Required, StringLength(256)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}
