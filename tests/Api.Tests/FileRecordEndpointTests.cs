using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Enums;
using Shared.Dtos;

namespace Api.Tests;

public class FileRecordEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileRecordEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static CreateFileRecordRequest ValidCreateRequest(string? suffix = null) => new(
        Name: $"Test File {suffix ?? Guid.NewGuid().ToString()[..8]}",
        FileType: FileType.CorelDRAW,
        FlopDiskNumber: 42,
        Date: new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        Client: "Test Client"
    );

    private async Task<FileRecordResponse> CreateRecordAsync(CreateFileRecordRequest? request = null)
    {
        var req = request ?? ValidCreateRequest();
        var response = await _client.PostAsJsonAsync("/api/file-records", req);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        return created!;
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithCreatedRecord()
    {
        // Arrange
        var request = ValidCreateRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/file-records", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(request.Name, created.Name);
        Assert.Equal(request.FileType, created.FileType);
        Assert.Equal(request.FlopDiskNumber, created.FlopDiskNumber);
        Assert.Equal(request.Date, created.Date);
        Assert.Equal(request.Client, created.Client);
    }

    [Fact]
    public async Task Create_MissingName_Returns400()
    {
        // Arrange — send payload with name omitted (deserializes as null, validation rejects it)
        var payload = new { fileType = 0, flopDiskNumber = 1, date = "2024-01-01T00:00:00Z", client = "Client" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/file-records", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhitespaceName_Returns400()
    {
        var request = ValidCreateRequest() with { Name = "   " };

        var response = await _client.PostAsJsonAsync("/api/file-records", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhitespaceClient_Returns400()
    {
        var request = ValidCreateRequest() with { Client = "\t  " };

        var response = await _client.PostAsJsonAsync("/api/file-records", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingDate_Returns400()
    {
        // Arrange — send JSON with date explicitly null
        var payload = new { name = "File", fileType = 0, flopDiskNumber = (int?)null, date = (string?)null, client = "Client" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/file-records", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidFileType_Returns400()
    {
        // Arrange — use an integer outside the valid enum range
        var payload = new { name = "File", fileType = 999, flopDiskNumber = (int?)null, date = "2024-01-01T00:00:00Z", client = "Client" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/file-records", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingClient_Returns400()
    {
        // Arrange
        var payload = new { name = "File", fileType = 0, flopDiskNumber = (int?)null, date = "2024-01-01T00:00:00Z" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/file-records", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutFlopDiskNumber_PersistsAsNull()
    {
        // Arrange
        var request = new CreateFileRecordRequest(
            Name: "No Flop Disk",
            FileType: FileType.PDF,
            FlopDiskNumber: null,
            Date: new DateTime(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            Client: "Client Without Disk"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/file-records", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Null(created.FlopDiskNumber);
    }

    [Fact]
    public async Task GetAll_ReturnsAllRecords()
    {
        // Arrange — create two records with unique names
        var suffix = Guid.NewGuid().ToString()[..8];
        var req1 = new CreateFileRecordRequest($"GetAll-A-{suffix}", FileType.Photoshop, null, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), "ClientA");
        var req2 = new CreateFileRecordRequest($"GetAll-B-{suffix}", FileType.Illustrator, 5, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), "ClientB");
        await _client.PostAsJsonAsync("/api/file-records", req1);
        await _client.PostAsJsonAsync("/api/file-records", req2);

        // Act
        var response = await _client.GetAsync("/api/file-records");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(paginated);
        var records = paginated.Items;
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Name == $"GetAll-A-{suffix}");
        Assert.Contains(records, r => r.Name == $"GetAll-B-{suffix}");
    }

    [Fact]
    public async Task GetById_ExistingId_Returns200()
    {
        // Arrange
        var created = await CreateRecordAsync();

        // Act
        var response = await _client.GetAsync($"/api/file-records/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await response.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(record);
        Assert.Equal(created.Id, record.Id);
        Assert.Equal(created.Name, record.Name);
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/file-records/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ValidRequest_Returns200()
    {
        // Arrange
        var created = await CreateRecordAsync();
        var updateRequest = new UpdateFileRecordRequest(
            Name: "Updated Name",
            FileType: FileType.Inkscape,
            FlopDiskNumber: 99,
            Date: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Client: "Updated Client"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/file-records/{created.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal(FileType.Inkscape, updated.FileType);
        Assert.Equal(99, updated.FlopDiskNumber);
        Assert.Equal("Updated Client", updated.Client);
    }

    [Fact]
    public async Task Update_NonExistentId_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateFileRecordRequest(
            Name: "Name",
            FileType: FileType.PDF,
            FlopDiskNumber: null,
            Date: new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Client: "Client"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/file-records/{nonExistentId}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidData_Returns400()
    {
        // Arrange
        var created = await CreateRecordAsync();
        var payload = new { name = "", fileType = 0, date = "2024-01-01T00:00:00Z", client = "Client" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/api/file-records/{created.Id}", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WhitespaceName_Returns400()
    {
        var created = await CreateRecordAsync();
        var request = new UpdateFileRecordRequest(
            Name: "   ",
            FileType: FileType.PDF,
            FlopDiskNumber: null,
            Date: new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Client: "Client"
        );

        var response = await _client.PutAsJsonAsync($"/api/file-records/{created.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WhitespaceClient_Returns400()
    {
        var created = await CreateRecordAsync();
        var request = new UpdateFileRecordRequest(
            Name: "Name",
            FileType: FileType.PDF,
            FlopDiskNumber: null,
            Date: new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Client: "\r\n"
        );

        var response = await _client.PutAsJsonAsync($"/api/file-records/{created.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingId_Returns204()
    {
        // Arrange
        var created = await CreateRecordAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/file-records/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's actually gone
        var getResponse = await _client.GetAsync($"/api/file-records/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentId_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/file-records/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
