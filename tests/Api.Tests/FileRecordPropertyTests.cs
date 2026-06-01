using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Shared.Dtos;

namespace Api.Tests;

/// <summary>
/// Property-based tests for File Records CRUD endpoints using FsCheck.
/// Feature: file-records-crud
/// </summary>
public class FileRecordPropertyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly FileType[] ValidFileTypes = Enum.GetValues<FileType>();

    public FileRecordPropertyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Generates a valid CreateFileRecordRequest from a seed.
    /// </summary>
    private static CreateFileRecordRequest GenerateCreateRequest(int seed)
    {
        var rng = new Random(seed);
        var name = GenerateNonEmptyString(rng);
        var fileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)];
        var flopDisk = rng.Next(2) == 0 ? (int?)null : rng.Next(1, 9999);
        var date = new DateTime(rng.Next(2000, 2030), rng.Next(1, 13), rng.Next(1, 28), 0, 0, 0, DateTimeKind.Utc);
        var client = GenerateNonEmptyString(rng);

        return new CreateFileRecordRequest(name, fileType, flopDisk, date, client);
    }

    /// <summary>
    /// Generates a valid UpdateFileRecordRequest from a seed.
    /// </summary>
    private static UpdateFileRecordRequest GenerateUpdateRequest(int seed)
    {
        var rng = new Random(seed);
        var name = GenerateNonEmptyString(rng);
        var fileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)];
        var flopDisk = rng.Next(2) == 0 ? (int?)null : rng.Next(1, 9999);
        var date = new DateTime(rng.Next(2000, 2030), rng.Next(1, 13), rng.Next(1, 28), 0, 0, 0, DateTimeKind.Utc);
        var client = GenerateNonEmptyString(rng);

        return new UpdateFileRecordRequest(name, fileType, flopDisk, date, client);
    }

    /// <summary>
    /// Generates a non-empty alphanumeric string from a Random instance.
    /// </summary>
    private static string GenerateNonEmptyString(Random rng)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var length = rng.Next(1, 30);
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[rng.Next(chars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Property 1: Creation round-trip preserves data
    ///
    /// For any valid file record input, creating the record via POST and then retrieving it
    /// via GET by the returned Id SHALL produce a response with all fields matching the original
    /// input and a non-empty generated Id.
    ///
    /// **Validates: Requirements 1.1, 1.6, 2.2**
    /// </summary>
    [Property(
        DisplayName = "Feature: file-records-crud, Property 1: Creation round-trip preserves data",
        MaxTest = 100)]
    public Property CreateThenGet_RoundTrip_PreservesAllFields()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            CreateAndVerifyRoundTrip(seed).GetAwaiter().GetResult();
        });
    }

    private async Task CreateAndVerifyRoundTrip(int seed)
    {
        var request = GenerateCreateRequest(seed);

        // POST to create
        var createResponse = await _client.PostAsJsonAsync("/api/file-records", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);

        // GET by returned Id
        var getResponse = await _client.GetAsync($"/api/file-records/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(retrieved);

        // Assert all fields match (service trims Name and Client)
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(request.Name.Trim(), retrieved.Name);
        Assert.Equal(request.FileType, retrieved.FileType);
        Assert.Equal(request.FlopDiskNumber, retrieved.FlopDiskNumber);
        Assert.Equal(request.Date, retrieved.Date);
        Assert.Equal(request.Client.Trim(), retrieved.Client);
    }

    /// <summary>
    /// Property 2: Validation rejects invalid inputs
    ///
    /// For any file record request where Name is empty/whitespace, OR FileType is outside
    /// the valid enum range, OR Client is empty/whitespace, the API SHALL return HTTP 400
    /// and the record SHALL NOT be persisted.
    ///
    /// **Validates: Requirements 1.2, 1.3, 1.5, 3.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: file-records-crud, Property 2: Validation rejects invalid inputs",
        MaxTest = 100)]
    public Property Create_InvalidInput_Returns400AndDoesNotPersist()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            InvalidInputReturns400(seed).GetAwaiter().GetResult();
        });
    }

    private async Task InvalidInputReturns400(int seed)
    {
        var rng = new Random(seed);
        var invalidCase = rng.Next(3);

        object invalidRequest = invalidCase switch
        {
            // Case 0: Empty/whitespace Name
            0 => new { Name = "", FileType = 0, FlopDiskNumber = (int?)null, Date = DateTime.UtcNow, Client = "ValidClient" },
            // Case 1: Empty/whitespace Client
            1 => new { Name = "ValidName", FileType = 0, FlopDiskNumber = (int?)null, Date = DateTime.UtcNow, Client = "" },
            // Case 2: Invalid FileType (outside the enum range)
            _ => new { Name = "ValidName", FileType = 99, FlopDiskNumber = (int?)null, Date = DateTime.UtcNow, Client = "ValidClient" }
        };

        var response = await _client.PostAsJsonAsync("/api/file-records", invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Property 3: List ordering by Date descending
    ///
    /// For any set of file records with distinct dates, when all are created and then the
    /// list endpoint is called, the returned records SHALL be ordered such that each record's
    /// Date is greater than or equal to the next record's Date.
    ///
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(
        DisplayName = "Feature: file-records-crud, Property 3: List ordering by Date descending",
        MaxTest = 100)]
    public Property GetAll_MultipleRecords_ReturnedOrderedByDateDescending()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            CreateMultipleAndVerifyOrder(seed).GetAwaiter().GetResult();
        });
    }

    private async Task CreateMultipleAndVerifyOrder(int seed)
    {
        var rng = new Random(seed);
        var count = rng.Next(2, 6);

        // Create multiple records with distinct dates far in the future to ensure
        // they appear in the top 100 results (ordered by date descending)
        for (int i = 0; i < count; i++)
        {
            var request = new CreateFileRecordRequest(
                $"Record_{seed}_{i}",
                ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                null,
                new DateTime(2090 + i, rng.Next(1, 13), rng.Next(1, 28), rng.Next(24), rng.Next(60), rng.Next(60), DateTimeKind.Utc),
                $"Client_{seed}_{i}"
            );

            var response = await _client.PostAsJsonAsync("/api/file-records", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // GET all records
        var listResponse = await _client.GetAsync("/api/file-records");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var allRecords = await listResponse.Content.ReadFromJsonAsync<List<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(allRecords);
        Assert.True(allRecords.Count >= 2, "Need at least 2 records to verify ordering");

        // Assert ordering: each Date >= next Date (descending) across ALL returned records
        for (int i = 0; i < allRecords.Count - 1; i++)
        {
            Assert.True(allRecords[i].Date >= allRecords[i + 1].Date,
                $"Records not in descending date order at index {i}: {allRecords[i].Date} should be >= {allRecords[i + 1].Date}");
        }
    }

    /// <summary>
    /// Property 4: Update preserves Id and persists changes
    ///
    /// For any existing file record and any valid update payload, updating the record
    /// SHALL return the same Id as the original and all other fields SHALL match the update payload.
    ///
    /// **Validates: Requirements 3.1, 3.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: file-records-crud, Property 4: Update preserves Id and persists changes",
        MaxTest = 100)]
    public Property Update_ValidPayload_PreservesIdAndUpdatesFields()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seedGen, (createSeed, updateSeed) =>
        {
            UpdateAndVerify(createSeed, updateSeed).GetAwaiter().GetResult();
        });
    }

    private async Task UpdateAndVerify(int createSeed, int updateSeed)
    {
        var createRequest = GenerateCreateRequest(createSeed);
        var updateRequest = GenerateUpdateRequest(updateSeed);

        // Create a record first
        var createResponse = await _client.PostAsJsonAsync("/api/file-records", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(created);

        // Update the record with new data
        var updateResponse = await _client.PutAsJsonAsync($"/api/file-records/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(updated);

        // Assert Id is preserved (Requirement 3.4)
        Assert.Equal(created.Id, updated.Id);

        // Assert all other fields match the update payload (Requirement 3.1)
        // Service trims Name and Client
        Assert.Equal(updateRequest.Name.Trim(), updated.Name);
        Assert.Equal(updateRequest.FileType, updated.FileType);
        Assert.Equal(updateRequest.FlopDiskNumber, updated.FlopDiskNumber);
        Assert.Equal(updateRequest.Date, updated.Date);
        Assert.Equal(updateRequest.Client.Trim(), updated.Client);
    }

    /// <summary>
    /// Property 5: Delete removes record from persistence
    ///
    /// For any existing file record, after a successful DELETE (204), a subsequent GET
    /// by the same Id SHALL return 404.
    ///
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(
        DisplayName = "Feature: file-records-crud, Property 5: Delete removes record from persistence",
        MaxTest = 100)]
    public Property Delete_ExistingRecord_SubsequentGetReturns404()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            DeleteAndVerifyGone(seed).GetAwaiter().GetResult();
        });
    }

    private async Task DeleteAndVerifyGone(int seed)
    {
        var createRequest = GenerateCreateRequest(seed);

        // Create a record
        var createResponse = await _client.PostAsJsonAsync("/api/file-records", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<FileRecordResponse>(JsonOptions);
        Assert.NotNull(created);

        // DELETE the record — assert 204
        var deleteResponse = await _client.DeleteAsync($"/api/file-records/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // GET by same Id — assert 404
        var getResponse = await _client.GetAsync($"/api/file-records/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
