using Api.Endpoints;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapHealthEndpoints();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes
public partial class Program { }
