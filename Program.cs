using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Pressings.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
var userSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreshCare", "appsettings.Local.json");
builder.Configuration.AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var hostingPort) && hostingPort is > 0 and <= 65535)
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostingPort}");
builder.Services.AddDataProtection().SetApplicationName("FreshCare");
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AccessRepository>();
builder.Services.AddSingleton<ClientCommunicationService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FreshCare.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Configuration.GetValue<bool>("Cookies:Secure")
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;
            if (principal?.IsInRole("staff") != true) return;
            if (!int.TryParse(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var repository = context.HttpContext.RequestServices.GetRequiredService<AccessRepository>();
            var account = repository.FindAdmin(userId);
            if (account is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var roles = repository.GetAdminRoles(userId);
            var oldRoles = principal.FindAll(System.Security.Claims.ClaimTypes.Role)
                .Select(claim => claim.Value).Where(Pressings.Services.StaffRoleCatalog.IsKnown).ToHashSet(StringComparer.Ordinal);
            if (!oldRoles.SetEquals(roles) || !string.Equals(principal.Identity?.Name, account.DisplayName, StringComparison.Ordinal))
            {
                context.ReplacePrincipal(Pressings.Services.StaffClaimsFactory.Create(account, roles));
                context.ShouldRenew = true;
            }
        };
    });
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Configuration["Antiforgery:CookieName"] ?? "FreshCare.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Configuration.GetValue<bool>("Cookies:Secure")
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddAuthorization(Pressings.Services.StaffPolicies.Configure);

var app = builder.Build();
app.Services.GetRequiredService<AccessRepository>().Initialize();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
