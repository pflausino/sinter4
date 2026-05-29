using System.Text.Json;
using Api.Endpoints;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var projectId = builder.Configuration["Firebase:ProjectId"];

if (string.IsNullOrWhiteSpace(projectId))
    throw new InvalidOperationException(
        "Required configuration key 'Firebase:ProjectId' is missing or empty");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{projectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{projectId}",
            ValidateAudience = true,
            ValidAudience = projectId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var error = context.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException => ("token_expired", "The token has expired."),
                    _ when context.Request.Headers.ContainsKey("Authorization")
                        => ("invalid_token", "The token is invalid."),
                    _ => ("missing_token", "No authentication token was provided.")
                };

                var json = JsonSerializer.Serialize(new { error = error.Item1, message = error.Item2 });
                return context.Response.WriteAsync(json);
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Authenticated", policy =>
        policy.RequireAuthenticatedUser());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();

app.MapGet("/api/me", (HttpContext context) =>
{
    var uid = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
    return Results.Ok(new { uid, email });
}).RequireAuthorization("Authenticated");

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes
public partial class Program { }
