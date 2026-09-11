namespace BloodLink.Application.Security;

public interface IPasswordResetDelivery
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default);
}
