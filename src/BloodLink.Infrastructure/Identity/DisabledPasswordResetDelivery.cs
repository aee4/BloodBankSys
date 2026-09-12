using BloodLink.Application.Security;

namespace BloodLink.Infrastructure.Identity;

// Deliberately never writes a reset token to logs, disk, or a public development endpoint.
public sealed class DisabledPasswordResetDelivery : IPasswordResetDelivery
{
    public bool IsConfigured => false;
    public Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
