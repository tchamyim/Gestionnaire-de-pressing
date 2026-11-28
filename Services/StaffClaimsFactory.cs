using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pressings.Models;

namespace Pressings.Services;

public static class StaffClaimsFactory
{
    public static ClaimsPrincipal Create(AdminAccount account, IEnumerable<string> roleCodes)
    {
        var roles = roleCodes.Distinct(StringComparer.Ordinal).Where(StaffRoleCatalog.IsKnown).ToList();
        var displayName = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Email : account.DisplayName;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, "staff"),
            new("display_role", string.Join(" · ", roles.Select(StaffRoleCatalog.NameOf)))
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
