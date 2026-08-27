using System.Security.Claims;
using BloodLink.Application.Interfaces;
using BloodLink.Infrastructure.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BloodLink.Infrastructure.Identity;

public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider,
    IDbContextFactory<BloodLinkDbContext> dbContextFactory) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => GetPrincipal();

    public string? UserId => IsAuthenticated
        ? Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles => IsAuthenticated
        ? Principal!
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        : Array.Empty<string>();

    public Guid? FacilityId => GetCurrentUser()?.FacilityId;

    public bool IsActive => GetCurrentUser()?.IsActive == true;

    public bool IsInRole(string roleName) =>
        IsAuthenticated
        && !string.IsNullOrWhiteSpace(roleName)
        && Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    public bool BelongsToFacility(Guid facilityId) =>
        IsAuthenticated && FacilityId == facilityId;

    private ApplicationUser? GetCurrentUser()
    {
        var userId = UserId;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        using var dbContext = dbContextFactory.CreateDbContext();

        return dbContext.Users
            .AsNoTracking()
            .SingleOrDefault(user => user.Id == userId);
    }

    private ClaimsPrincipal? GetPrincipal()
    {
        try
        {
            var authenticationStateTask = authenticationStateProvider.GetAuthenticationStateAsync();

            return authenticationStateTask.IsCompletedSuccessfully
                ? authenticationStateTask.Result.User
                : null;
        }
        catch (InvalidOperationException) when (
            authenticationStateProvider is ServerAuthenticationStateProvider)
        {
            return httpContextAccessor.HttpContext?.User;
        }
    }
}
