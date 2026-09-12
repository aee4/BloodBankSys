using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BloodLink.Infrastructure.Identity;

// Immutable, process-local verification material; never persisted as an Identity account.
public sealed class LoginFailureWork
{
    private readonly string hash;

    public LoginFailureWork(IOptions<PasswordHasherOptions> options)
    {
        hash = new PasswordHasher<ApplicationUser>(options).HashPassword(
            new ApplicationUser(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public void Verify(IPasswordHasher<ApplicationUser> hasher, string password)
    {
        // Always discard the result. This work can never authenticate anyone.
        _ = hasher.VerifyHashedPassword(new ApplicationUser(), hash, password);
    }
}
