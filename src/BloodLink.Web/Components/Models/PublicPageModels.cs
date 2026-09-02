using System.ComponentModel.DataAnnotations;
using BloodLink.Application.DTOs;
using BloodLink.Domain.Enums;

namespace BloodLink.Web.Components.Models;

public sealed class FacilityRegistrationModel
{
    [Required(ErrorMessage = "Facility name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Facility type is required.")]
    public FacilityType? FacilityType { get; set; }

    [Required(ErrorMessage = "Registration number is required.")]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Region is required.")]
    public string Region { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Address is required.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact email is required."), EmailAddress(ErrorMessage = "Enter a valid contact email.")]
    public string ContactEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact phone is required."), Phone(ErrorMessage = "Enter a valid contact phone number.")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Administrator first name is required.")]
    public string AdminFirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Administrator last name is required.")]
    public string AdminLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Administrator email is required."), EmailAddress(ErrorMessage = "Enter a valid administrator email.")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Administrator phone is required."), Phone(ErrorMessage = "Enter a valid administrator phone number.")]
    public string AdminPhoneNumber { get; set; } = string.Empty;

    public RegisterFacilityRequest ToRequest() => new(
        Name.Trim(), FacilityType!.Value, RegistrationNumber.Trim(), Region,
        City.Trim(), Address.Trim(), ContactEmail.Trim(), ContactPhone.Trim(),
        AdminFirstName.Trim(), AdminLastName.Trim(), AdminEmail.Trim(), AdminPhoneNumber.Trim());
}

public sealed class LoginFormModel
{
    [Required(ErrorMessage = "Email address is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public sealed class ForgotPasswordFormModel
{
    [Required(ErrorMessage = "Email address is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
}
