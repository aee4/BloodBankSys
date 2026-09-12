using BloodLink.Web.Components;
using BloodLink.Infrastructure;
using BloodLink.Web.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddBloodLinkInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<PasswordRecoveryQueue>();
builder.Services.AddHostedService(services => services.GetRequiredService<PasswordRecoveryQueue>());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
        await context.HttpContext.Response.WriteAsync("Too many account requests. Please try again later.", cancellationToken);
    foreach (var policy in new[] { "account-login", "account-recovery", "account-password" })
        options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
            policy + ":" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown"), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseMiddleware<AccountSecurityMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
