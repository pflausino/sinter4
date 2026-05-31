using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Components;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "BlazorServer";
}).AddCookie("BlazorServer", options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, BlazorAuthorizationMiddlewareResultHandler>();

builder.Services.AddScoped<ITokenStorage, ProtectedTokenStorage>();
builder.Services.AddScoped<ITokenProvider, FirebaseTokenProvider>();
builder.Services.AddScoped<AuthenticationStateProvider, FirebaseAuthStateProvider>();

builder.Services.AddHttpClient("Api", client =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7011";
    client.BaseAddress = new Uri(apiBaseUrl);
});

builder.Services.AddHttpClient("FirebaseAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Trust reverse proxy headers (X-Forwarded-For, X-Forwarded-Proto)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Serve static files (including _framework/)
app.UseStaticFiles();

app.UseAntiforgery();

app.MapHealthChecks("/health");

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// Prevents the server-side authorization middleware from issuing challenges/redirects.
/// Blazor's AuthorizeRouteView handles unauthorized state at the component level.
/// </summary>
public class BlazorAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        return next(context);
    }
}
