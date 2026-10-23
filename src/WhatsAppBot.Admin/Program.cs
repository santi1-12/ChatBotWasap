using Microsoft.AspNetCore.Authentication.Cookies;
using WhatsAppBot.Admin.Authentication;
using WhatsAppBot.Admin.Configuration;
using WhatsAppBot.Admin.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<IAdminSession, AdminSession>();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "__Host-WhatsAppBot.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-WhatsAppBot.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    });
builder.Services.AddAuthorization();

builder.Services.AddOptions<AdminApiOptions>()
    .Bind(builder.Configuration.GetSection(AdminApiOptions.SectionName))
    .Validate(options =>
    {
        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || builder.Environment.IsDevelopment());
    }, "AdminApi:BaseUrl must be an absolute HTTPS URL outside Development.")
    .Validate(options => options.RequestTimeoutSeconds is > 0 and <= 120,
        "AdminApi:RequestTimeoutSeconds must be between 1 and 120.")
    .ValidateOnStart();

var apiOptions = builder.Configuration.GetSection(AdminApiOptions.SectionName).Get<AdminApiOptions>() ?? new();
if (!Uri.TryCreate(apiOptions.BaseUrl, UriKind.Absolute, out var apiUri))
{
    throw new InvalidOperationException("AdminApi:BaseUrl must be an absolute URI.");
}

builder.Services.AddHttpClient<IWhatsAppBotApiClient, WhatsAppBotApiClient>(client =>
{
    client.BaseAddress = apiUri;
    client.Timeout = TimeSpan.FromSeconds(apiOptions.RequestTimeoutSeconds);
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

public partial class Program;
