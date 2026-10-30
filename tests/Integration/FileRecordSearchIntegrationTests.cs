using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Dtos;
using Testcontainers.PostgreSql;

namespace Integration;

/// <summary>
/// Test authentication handler that auto-authenticates all requests.
/// </summary>
public class IntegrationTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTestScheme";

    public IntegrationTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "integration-test-user"),
            new Claim(ClaimTypes.Name, "Integration Test User"),
            new Claim(ClaimTypes.Email, "integration@test.com")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// WebApplicationFactory configured with a real PostgreSQL container via Testcontainers.
/// Applies EF Core migrations and seeds test data.
/// </summary>
public class PostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private static readonly string FakeServiceAccountPath;

    static PostgresWebApplicationFactory()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem().ReplaceLineEndings("\\n");

        var fakeServiceAccountJson = $$"""
        {
            "type": "service_account",
            "project_id": "test-project-id",
            "private_key_id": "test-key-id",
            "private_key": "{{privateKeyPem}}",
            "client_email": "test@test-project-id.iam.gserviceaccount.com",
            "client_id": "123456789",
            "auth_uri": "https://accounts.google.com/o/oauth2/auth",
            "token_uri": "https://oauth2.googleapis.com/token"
        }
        """;

        FakeServiceAccountPath = Path.Combine(Path.GetTempPath(), "fake-firebase-sa-integration.json");
        File.WriteAllText(FakeServiceAccountPath, fakeServiceAccountJson);

        if (FirebaseAdmin.FirebaseApp.DefaultInstance is null)
        {
            try
            {
                FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
                {
                    Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(fakeServiceAccountJson)
                });
            }
            catch (ArgumentException)
            {
                // Already initialized
            }
        }
    }

    /// <summary>
    /// Whether to include the test auth handler. Set to false for unauthenticated tests.
    /// </summary>
    public bool UseAuthentication { get; set; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Firebase:ProjectId", "test-project-id");
        builder.UseSetting("Firebase:ServiceAccountPath", FakeServiceAccountPath);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // Register with Npgsql pointing to the container
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString())
                       .UseSnakeCaseNamingConvention());
        });

        if (UseAuthentication)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                    IntegrationTestAuthHandler.SchemeName, _ => { });
            });
        }
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Apply migrations and seed data
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        await SeedDataAsync(dbContext);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private static async Task SeedDataAsync(AppDbContext dbContext)
    {
        var records = new List<FileRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Gráfica Premium",
                FileType = FileType.CorelDRAW,
                Client = "João Silva",
                Date = DateTime.SpecifyKind(new DateTime(2024, 1, 15), DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "São Paulo Design",
                FileType = FileType.Illustrator,
                Client = "Maria Conceição",
                Date = DateTime.SpecifyKind(new DateTime(2024, 2, 20), DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Cartão Visita Empresarial",
                FileType = FileType.Photoshop,
                Client = "André Müller",
                Date = DateTime.SpecifyKind(new DateTime(2024, 3, 10), DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Banner Promoção Verão",
                FileType = FileType.CorelDRAW,
                Client = "Café Açaí Ltda",
                Date = DateTime.SpecifyKind(new DateTime(2024, 4, 5), DateTimeKind.Utc)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Logo Redesign Final",
                FileType = FileType.Inkscape,
                Client = "TechSoluções",
                Date = DateTime.SpecifyKind(new DateTime(2024, 5, 1), DateTimeKind.Utc)
            }
        };

        dbContext.FileRecords.AddRange(records);
        await dbContext.SaveChangesAsync();
    }
}

public class FileRecordSearchIntegrationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileRecordSearchIntegrationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.UseAuthentication = true;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Search_Pagination_ReturnsRequestedPageAndTotalCount()
    {
        var marker = $"Pagination{Guid.NewGuid():N}";

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 1; i <= 5; i++)
            {
                dbContext.FileRecords.Add(new FileRecord
                {
                    Id = Guid.NewGuid(),
                    Name = $"{marker} Record {i}",
                    FileType = FileType.CorelDRAW,
                    Client = "Pagination Test",
                    Date = DateTime.SpecifyKind(new DateTime(2024, 1, i), DateTimeKind.Utc)
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/file-records/search?q={marker}&offset=2&limit=2");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();

        Assert.NotNull(paginated);
        Assert.Equal(5, paginated.TotalCount);
        Assert.True(paginated.HasMore);
        Assert.Collection(
            paginated.Items,
            item => Assert.Equal($"{marker} Record 3", item.Name),
            item => Assert.Equal($"{marker} Record 2", item.Name));
    }

    [Fact]
    public async Task Search_UnaccentExtensionWorks_FindsAccentedRecordsWithoutAccents()
    {
        // Search "grafica" should find "Gráfica Premium" (accent-insensitive via unaccent)
        var response = await _client.GetAsync("/api/file-records/search?q=grafica");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Contains(results, r => r.Name.Contains("Gráfica"));
    }

    [Fact]
    public async Task Search_AccentInsensitive_FindsRecordWithAccentedSearchTerm()
    {
        // Search "são" should find "São Paulo Design"
        var response = await _client.GetAsync("/api/file-records/search?q=s%C3%A3o");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Contains(results, r => r.Name.Contains("São Paulo"));
    }

    [Fact]
    public async Task Search_CaseInsensitive_FindsRegardlessOfCase()
    {
        // Search "BANNER" should find "Banner Promoção Verão"
        var response = await _client.GetAsync("/api/file-records/search?q=BANNER");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Contains(results, r => r.Name.Contains("Banner"));
    }

    [Fact]
    public async Task Search_ByClientName_FindsMatchingRecords()
    {
        // Search "muller" (without umlaut) should find record with client "André Müller"
        var response = await _client.GetAsync("/api/file-records/search?q=muller");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Contains(results, r => r.Client.Contains("Müller"));
    }

    [Fact]
    public async Task Search_EndToEnd_MultiWordQuery_ReturnsMatchingRecords()
    {
        // Search "paulo design" should find "São Paulo Design"
        var response = await _client.GetAsync("/api/file-records/search?q=paulo%20design");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Contains(results, r => r.Name == "São Paulo Design");
    }

    [Theory]
    [InlineData("Horto {0} Dente", "HORTO{0}DENTE")]   // query without spaces, record with spaces
    [InlineData("Hortodente{0}", "HORTO DENTE{0}")]    // query with spaces, record without
    [InlineData("Horto-Dente {0}", "hortodente{0}")]   // punctuation in the record is ignored
    [InlineData("Gráfica Horto {0}", "grafica-horto{0}")] // punctuation and accents in the query
    public async Task Search_IgnoresSpacesAndPunctuation_FindsRecordByClient(string clientPattern, string queryPattern)
    {
        var marker = $"m{Guid.NewGuid():N}";
        var client = string.Format(clientPattern, marker);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.FileRecords.Add(new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = "Apostila",
                FileType = FileType.PDF,
                Client = client,
                Date = DateTime.SpecifyKind(new DateTime(2026, 6, 17), DateTimeKind.Utc)
            });
            await dbContext.SaveChangesAsync();
        }

        var query = Uri.EscapeDataString(string.Format(queryPattern, marker));
        var response = await _client.GetAsync($"/api/file-records/search?q={query}");

        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        Assert.Single(paginated.Items, r => r.Client == client);
        Assert.Equal(1, paginated.TotalCount);
    }

    [Fact]
    public async Task Search_NumericTerm_FindsRecordByFileNumber()
    {
        var fileNumber = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        await AddRecordAsync("Folheto", "Cliente", fileNumber: fileNumber);

        var paginated = await SearchAsync(fileNumber.ToString());

        Assert.Single(paginated.Items, r => r.FileNumber == fileNumber);
        Assert.Equal(1, paginated.TotalCount);
    }

    [Fact]
    public async Task Search_NumericTerm_FindsRecordByFlopDiskNumber()
    {
        var diskNumber = Random.Shared.Next(1_000_000_000, int.MaxValue);
        await AddRecordAsync("Folheto", "Cliente", flopDiskNumber: diskNumber);

        var paginated = await SearchAsync(diskNumber.ToString());

        Assert.Single(paginated.Items, r => r.FlopDiskNumber == diskNumber);
        Assert.Equal(1, paginated.TotalCount);
    }

    [Fact]
    public async Task Search_NumericAndTextTerms_MustAllMatch()
    {
        var fileNumber = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var marker = $"m{Guid.NewGuid():N}";
        await AddRecordAsync($"Folheto {marker}", "Cliente", fileNumber: fileNumber);
        await AddRecordAsync("Cartão", "Cliente", fileNumber: fileNumber);

        var paginated = await SearchAsync($"{fileNumber} {marker}");

        Assert.Single(paginated.Items);
        Assert.Equal($"Folheto {marker}", paginated.Items[0].Name);
    }

    [Fact]
    public async Task Duplicates_MatchNameIgnoringCaseAccentsAndSurroundingSpaces()
    {
        var marker = $"m{Guid.NewGuid():N}";
        var existingId = await AddRecordAsync($"Folheto Promoção {marker}", "Cliente");
        await AddRecordAsync($"Folheto Promoção {marker} 2", "Cliente");

        var query = Uri.EscapeDataString($"  folheto PROMOCAO {marker} ");
        var matches = await _client.GetFromJsonAsync<List<FileRecordResponse>>($"/api/file-records/duplicates?name={query}");

        Assert.NotNull(matches);
        Assert.Single(matches, r => r.Id == existingId);

        var excluded = await _client.GetFromJsonAsync<List<FileRecordResponse>>(
            $"/api/file-records/duplicates?name={query}&excludeId={existingId}");
        Assert.NotNull(excluded);
        Assert.Empty(excluded);
    }

    private async Task<Guid> AddRecordAsync(string name, string client, long fileNumber = 1, int? flopDiskNumber = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = new FileRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            FileType = FileType.PDF,
            Client = client,
            FileNumber = fileNumber,
            FlopDiskNumber = flopDiskNumber,
            Date = DateTime.SpecifyKind(new DateTime(2026, 6, 17), DateTimeKind.Utc)
        };
        dbContext.FileRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return record.Id;
    }

    private async Task<PaginatedResponse<FileRecordResponse>> SearchAsync(string q)
    {
        var response = await _client.GetAsync($"/api/file-records/search?q={Uri.EscapeDataString(q)}");
        response.EnsureSuccessStatusCode();
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        return paginated;
    }

    [Fact]
    public async Task Search_Unauthenticated_Returns401()
    {
        // Create a client without the test auth handler
        var unauthenticatedFactory = new PostgresWebApplicationFactory { UseAuthentication = false };
        await unauthenticatedFactory.InitializeAsync();

        try
        {
            var client = unauthenticatedFactory.CreateClient();
            var response = await client.GetAsync("/api/file-records/search?q=test");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            await unauthenticatedFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Search_QueryTooLong_Returns400()
    {
        var longQuery = new string('a', 201);
        var response = await _client.GetAsync($"/api/file-records/search?q={longQuery}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/file-records/search?q=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_WhitespaceOnly_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/file-records/search?q=%20%20%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_NoQParameter_Returns200WithEmptyArray()
    {
        var response = await _client.GetAsync("/api/file-records/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(paginated);
        var results = paginated.Items;

        Assert.NotNull(results);
        Assert.Empty(results);
    }
}
