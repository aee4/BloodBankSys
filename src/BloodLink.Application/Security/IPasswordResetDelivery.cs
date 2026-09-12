namespace BloodLink.Application.Security;

public interface IPasswordResetDelivery
{
    bool IsConfigured { get; }
    /// <summary>Deliver a sensitive reset URL without logging it. Honor cancellation and bound provider network timeouts.</summary>
    Task SendAsync(string email, string resetUrl, CancellationToken cancellationToken = default);
}
