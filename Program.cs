using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Pressings.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
var userSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreshCare", "appsettings.Local.json");
builder.Configuration.AddJsonFile(userSettingsPath, optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
var secureCookies = builder.Configuration.GetValue<bool?>("Cookies:Secure") ?? !builder.Environment.IsDevelopment();
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var hostingPort) && hostingPort is > 0 and <= 65535)
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostingPort}");
builder.Services.AddDataProtection().SetApplicationName("FreshCare");
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<AccessRepository>();
builder.Services.AddSingleton<ClientCommunicationService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Trop de tentatives de connexion. Réessayez dans une minute.", cancellationToken);
    };
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "FreshCare.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = secureCookies
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
    });
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Configuration["Antiforgery:CookieName"] ?? "FreshCare.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookies
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddAuthorization();

var app = builder.Build();
app.Services.GetRequiredService<AccessRepository>().Initialize();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
