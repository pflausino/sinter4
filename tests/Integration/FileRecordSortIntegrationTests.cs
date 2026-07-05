using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Dtos;

namespace Integration;

/// <summary>
/// Integration tests for server-side column sorting against real PostgreSQL via Testcontainers.
///
/// Each test seeds its own records tagged with a unique marker and queries the search endpoint
/// (with `?q={marker}`) to isolate its dataset. This validates sort behavior end-to-end:
/// EF query translation, PostgreSQL ORDER BY, NULLS LAST/FIRST, and pagination.
/// </summary>
public class FileRecordSortIntegrationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileRecordSortIntegrationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.UseAuthentication = true;
        _client = _factory.CreateClient();
    }

    // -------- Sort by name --------

    [Fact]
    public async Task Search_SortByNameAsc_ReturnsAlphabeticalOrder()
    {
        var marker = $"NameAsc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            (Name: $"{marker} Charlie", Client: "X"),
            (Name: $"{marker} Alpha", Client: "X"),
            (Name: $"{marker} Bravo", Client: "X"));

        var page = await GetSortedAsync(marker, "name", "asc");

        Assert.Collection(page.Items,
            r => Assert.Equal($"{marker} Alpha", r.Name),
            r => Assert.Equal($"{marker} Bravo", r.Name),
            r => Assert.Equal($"{marker} Charlie", r.Name));
    }

    [Fact]
    public async Task Search_SortByNameDesc_ReturnsReverseAlphabeticalOrder()
    {
        var marker = $"NameDesc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            (Name: $"{marker} Alpha", Client: "X"),
            (Name: $"{marker} Charlie", Client: "X"),
            (Name: $"{marker} Bravo", Client: "X"));

        var page = await GetSortedAsync(marker, "name", "desc");

        Assert.Collection(page.Items,
            r => Assert.Equal($"{marker} Charlie", r.Name),
            r => Assert.Equal($"{marker} Bravo", r.Name),
            r => Assert.Equal($"{marker} Alpha", r.Name));
    }

    // -------- Sort by date (nullable) --------

    [Fact]
    public async Task Search_SortByDateAsc_ReturnsOldestFirst()
    {
        var marker = $"DateAsc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} A", Client = "X", Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} B", Client = "X", Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} C", Client = "X", Date = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        var page = await GetSortedAsync(marker, "date", "asc");

        Assert.Equal(3, page.Items.Count);
        // Ascending: 1900-01-01 (sentinel) → 2024-01-01 → 2024-03-01
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), page.Items[0].Date);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), page.Items[1].Date);
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), page.Items[2].Date);
    }

    [Fact]
    public async Task Search_SortByDateDesc_ReturnsNewestFirst()
    {
        var marker = $"DateDesc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} A", Client = "X", Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} B", Client = "X", Date = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} C", Client = "X", Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) });

        var page = await GetSortedAsync(marker, "date", "desc");

        Assert.Equal(3, page.Items.Count);
        // Descending: 2024-03-01 → 2024-01-01 → 1900-01-01 (sentinel)
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), page.Items[0].Date);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), page.Items[1].Date);
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), page.Items[2].Date);
    }

    // -------- Sort by client --------

    [Fact]
    public async Task Search_SortByClientAsc_ReturnsAlphabeticalOrder()
    {
        var marker = $"ClientAsc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            (Name: $"{marker} 1", Client: "Zeta"),
            (Name: $"{marker} 2", Client: "Alpha"),
            (Name: $"{marker} 3", Client: "Mike"));

        var page = await GetSortedAsync(marker, "client", "asc");

        Assert.Collection(page.Items,
            r => Assert.Equal("Alpha", r.Client),
            r => Assert.Equal("Mike", r.Client),
            r => Assert.Equal("Zeta", r.Client));
    }

    // -------- Sort by file number (nullable) --------

    [Fact]
    public async Task Search_SortByFileNumberAsc_ReturnsAscendingNumeric()
    {
        var marker = $"FNoAsc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} A", Client = "X", FileNumber = 2000 },
            new SortSeed { Name = $"{marker} B", Client = "X", FileNumber = 0 },
            new SortSeed { Name = $"{marker} C", Client = "X", FileNumber = 1000 });

        var page = await GetSortedAsync(marker, "file_number", "asc");

        Assert.Equal(3, page.Items.Count);
        // 0 (sentinel) < 1000 < 2000
        Assert.Equal(0, page.Items[0].FileNumber);
        Assert.Equal(1000, page.Items[1].FileNumber);
        Assert.Equal(2000, page.Items[2].FileNumber);
    }

    // -------- Sort by flop disk number (nullable) --------

    [Fact]
    public async Task Search_SortByFlopDiskNumberDesc_PlacesNullsFirst()
    {
        var marker = $"DiskDesc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} A", Client = "X", FlopDiskNumber = 5 },
            new SortSeed { Name = $"{marker} B", Client = "X", FlopDiskNumber = null },
            new SortSeed { Name = $"{marker} C", Client = "X", FlopDiskNumber = 20 });

        var page = await GetSortedAsync(marker, "flop_disk_number", "desc");

        Assert.Equal(3, page.Items.Count);
        Assert.Null(page.Items[0].FlopDiskNumber);
        Assert.Equal(20, page.Items[1].FlopDiskNumber);
        Assert.Equal(5, page.Items[2].FlopDiskNumber);
    }

    // -------- Sort by file type (enum) --------

    [Fact]
    public async Task Search_SortByFileTypeAsc_ReturnsByEnumValueAscending()
    {
        var marker = $"TypeAsc{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} A", Client = "X", FileType = FileType.Illustrator }, // 2
            new SortSeed { Name = $"{marker} B", Client = "X", FileType = FileType.CorelDRAW },   // 0
            new SortSeed { Name = $"{marker} C", Client = "X", FileType = FileType.Photoshop });  // 1

        var page = await GetSortedAsync(marker, "file_type", "asc");

        Assert.Collection(page.Items,
            r => Assert.Equal(FileType.CorelDRAW, r.FileType),
            r => Assert.Equal(FileType.Photoshop, r.FileType),
            r => Assert.Equal(FileType.Illustrator, r.FileType));
    }

    // -------- Pagination consistency --------

    [Fact]
    public async Task Search_PaginationWithSort_ConsecutivePagesAreDisjointAndOrdered()
    {
        var marker = $"Pag{Guid.NewGuid():N}";
        var seeds = new List<SortSeed>();
        // Insert 10 records with alphabetical names A..J
        for (var c = 'A'; c <= 'J'; c++)
        {
            seeds.Add(new SortSeed
            {
                Name = $"{marker} {c}",
                Client = "X",
                Date = new DateTime(2024, 1, c - 'A' + 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        await SeedRecordsAsync(seeds.ToArray());

        var page1 = await GetSortedAsync(marker, "name", "asc", offset: 0, limit: 3);
        var page2 = await GetSortedAsync(marker, "name", "asc", offset: 3, limit: 3);
        var page3 = await GetSortedAsync(marker, "name", "asc", offset: 6, limit: 3);

        // First page: A, B, C
        Assert.Collection(page1.Items,
            r => Assert.EndsWith(" A", r.Name),
            r => Assert.EndsWith(" B", r.Name),
            r => Assert.EndsWith(" C", r.Name));

        // Second page: D, E, F (no overlap with page 1)
        Assert.Collection(page2.Items,
            r => Assert.EndsWith(" D", r.Name),
            r => Assert.EndsWith(" E", r.Name),
            r => Assert.EndsWith(" F", r.Name));

        // Third page: G, H, I (no overlap with page 2)
        Assert.Collection(page3.Items,
            r => Assert.EndsWith(" G", r.Name),
            r => Assert.EndsWith(" H", r.Name),
            r => Assert.EndsWith(" I", r.Name));

        // Sanity: no id appears in more than one page
        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(r => r.Id).ToList();
        Assert.Equal(allIds.Count, allIds.Distinct().Count());
    }

    // -------- Fallback: invalid sortBy --------

    [Fact]
    public async Task Search_InvalidSortBy_FallsBackToDateDesc()
    {
        var marker = $"Invalid{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} A", Client = "X", Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} B", Client = "X", Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} C", Client = "X", Date = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc) });

        // Invalid sortBy should force date DESC regardless of sortDir="asc"
        var page = await GetSortedAsync(marker, sortBy: "not_a_field", sortDir: "asc");

        Assert.Collection(page.Items,
            r => Assert.EndsWith(" B", r.Name), // 2024-03-01 (most recent)
            r => Assert.EndsWith(" C", r.Name), // 2024-02-01
            r => Assert.EndsWith(" A", r.Name)); // 2024-01-01 (oldest)
    }

    [Fact]
    public async Task Search_NullSortBy_UsesDefaultDateDesc()
    {
        var marker = $"NullSort{Guid.NewGuid():N}";
        await SeedRecordsAsync(
            new SortSeed { Name = $"{marker} Old", Client = "X", Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SortSeed { Name = $"{marker} New", Client = "X", Date = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) });

        // No sort params — should default to date DESC
        var response = await _client.GetAsync(
            $"/api/file-records/search?q={marker}&offset=0&limit=10");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(page);

        Assert.Collection(page.Items,
            r => Assert.EndsWith(" New", r.Name),
            r => Assert.EndsWith(" Old", r.Name));
    }

    // -------- Helpers --------

    private async Task<PaginatedResponse<FileRecordResponse>> GetSortedAsync(
        string marker, string sortBy, string sortDir, int offset = 0, int limit = 50)
    {
        var url = $"/api/file-records/search?q={marker}&sortBy={sortBy}&sortDir={sortDir}&offset={offset}&limit={limit}";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>();
        Assert.NotNull(page);
        return page;
    }

    private async Task SeedRecordsAsync(params (string Name, string Client)[] seeds)
    {
        var records = seeds.Select(s => new SortSeed { Name = s.Name, Client = s.Client }).ToArray();
        await SeedRecordsAsync(records);
    }

    private async Task SeedRecordsAsync(params SortSeed[] seeds)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var seed in seeds)
        {
            dbContext.FileRecords.Add(new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = seed.Name,
                Client = seed.Client,
                FileType = seed.FileType,
                FlopDiskNumber = seed.FlopDiskNumber,
                Date = seed.Date,
                FileNumber = seed.FileNumber
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed class SortSeed
    {
        public required string Name { get; init; }
        public required string Client { get; init; }
        public FileType FileType { get; init; } = FileType.CorelDRAW;
        public int? FlopDiskNumber { get; init; }
        public DateTime Date { get; init; } = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public int FileNumber { get; init; }
    }
}
