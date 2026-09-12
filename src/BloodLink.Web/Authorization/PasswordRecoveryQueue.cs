using System.Text;
using System.Threading.Channels;
using BloodLink.Application.Security;
using BloodLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace BloodLink.Web.Authorization;

// Bounded, best-effort queue: no tokens in queued items, no request-scoped services retained.
public sealed class PasswordRecoveryQueue(IServiceScopeFactory scopes, ILogger<PasswordRecoveryQueue> logger)
    : BackgroundService
{
    private readonly Channel<string> requests = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });

    public bool TryEnqueue(string email)
    {
        if (requests.Writer.TryWrite(email)) return true;
        logger.LogWarning("Password recovery queue is unavailable. A recovery request was not queued.");
        return false;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        requests.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in requests.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await ProcessAsync(email, timeout.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Never log provider exceptions, account identifiers, request URLs or tokens.
                logger.LogWarning("Password recovery processing failed. Check the configured database and delivery provider.");
            }
        }
    }

    private async Task ProcessAsync(string email, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var delivery = scope.ServiceProvider.GetRequiredService<IPasswordResetDelivery>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!delivery.IsConfigured) return;
        // Never build security links from the untrusted request Host header.
        if (!Uri.TryCreate(configuration["Account:PublicOrigin"], UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment))
            return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var access = scope.ServiceProvider.GetRequiredService<AccountAccessService>();
        var user = await users.FindByEmailAsync(email);
        if (user is null || (await access.FindAsync(user.Id, cancellationToken))?.CanSignIn != true) return;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = QueryHelpers.AddQueryString(new Uri(origin, "/account/reset-password").AbsoluteUri,
            new Dictionary<string, string?> { ["email"] = user.Email, ["code"] = code });
        await delivery.SendAsync(user.Email!, link, cancellationToken);
    }
}
