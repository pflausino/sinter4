# Implementation Plan: File Records CRUD

## Overview

Full-stack CRUD implementation for File Records in SinterPrints. The plan follows a bottom-up approach: domain model → shared DTOs → infrastructure (EF Core + migration) → API service + endpoints → Blazor UI → tests. Each task builds incrementally on the previous, ensuring no orphaned code.

## Tasks

- [x] 1. Domain layer — Entity and Enum
  - [x] 1.1 Create FileType enum and FileRecord entity
    - Create `src/Domain/Enums/FileType.cs` with values: CorelDRAW, Photoshop, Illustrator, Inkscape, PDF, Other
    - Create `src/Domain/Entities/FileRecord.cs` with properties: Id (Guid), Name (string), FileType (FileType), FlopDiskNumber (int?), Date (DateTime), Client (string)
    - _Requirements: 5.1_

- [x] 2. Shared layer — DTOs
  - [x] 2.1 Create request and response DTOs
    - Create `src/Shared/Dtos/CreateFileRecordRequest.cs` as a record with DataAnnotations validation (Required, MinLength for Name and Client, EnumDataType for FileType, Required for Date)
    - Create `src/Shared/Dtos/UpdateFileRecordRequest.cs` with the same shape and validation as CreateFileRecordRequest
    - Create `src/Shared/Dtos/FileRecordResponse.cs` as a record with Id, Name, FileType, FlopDiskNumber, Date, Client
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 3.3_

- [x] 3. Infrastructure layer — EF Core mapping and migration
  - [x] 3.1 Add FileRecord DbSet and entity configuration to AppDbContext
    - Add `DbSet<FileRecord> FileRecords` property
    - Configure entity mapping in `OnModelCreating`: table "file_records", uuid PK, timestamptz for Date, nullable FlopDiskNumber, required Name and Client
    - _Requirements: 5.2, 5.3, 5.4, 5.5_

  - [x] 3.2 Create EF Core migration for file_records table
    - Run `dotnet ef migrations add AddFileRecordsTable --project src/Infrastructure --startup-project src/Api`
    - Verify the generated migration creates the expected schema
    - _Requirements: 5.6_

- [x] 4. Checkpoint — Verify build compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. API layer — Service and Endpoints
  - [x] 5.1 Create IFileRecordService interface and FileRecordService implementation
    - Create `src/Api/Services/IFileRecordService.cs` with methods: GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync
    - Create `src/Api/Services/FileRecordService.cs` implementing the interface using AppDbContext
    - GetAllAsync returns records ordered by Date descending
    - CreateAsync generates a new Guid, trims Name and Client
    - UpdateAsync returns null if not found, preserves Id
    - DeleteAsync returns false if not found
    - _Requirements: 1.1, 1.6, 1.7, 2.1, 2.2, 2.3, 3.1, 3.4, 4.1, 4.2_

  - [x] 5.2 Create ValidationFilter<T> endpoint filter
    - Create `src/Api/Filters/ValidationFilter.cs` implementing `IEndpointFilter`
    - Validate the request body using DataAnnotations `Validator.TryValidateObject`
    - Return 400 with `{ errors: [...] }` on validation failure
    - Return 400 if request body is null
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 3.3_

  - [x] 5.3 Create FileRecordEndpoints and register in Program.cs
    - Create `src/Api/Endpoints/FileRecordEndpoints.cs` with `MapFileRecordEndpoints` extension method
    - Map GET `/api/file-records` (list all), GET `/api/file-records/{id:guid}` (get by id), POST `/api/file-records` (create), PUT `/api/file-records/{id:guid}` (update), DELETE `/api/file-records/{id:guid}` (delete)
    - Apply `ValidationFilter<T>` to POST and PUT endpoints
    - Apply `RequireAuthorization("Authenticated")` to the route group
    - Register `IFileRecordService`/`FileRecordService` in DI and call `app.MapFileRecordEndpoints()` in Program.cs
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2_

- [x] 6. Checkpoint — Verify API builds and endpoints are wired
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Blazor UI — File Records pages
  - [x] 7.1 Create File Records list page
    - Create `src/Web/Components/Pages/FileRecords.razor` with route `/file-records` and `[Authorize]` attribute
    - Create `src/Web/Components/Pages/FileRecords.razor.cs` code-behind
    - Display records in a table with columns: Name, FileType, Client, Date, FlopDiskNumber
    - Add "New" button linking to `/file-records/new`
    - Add Edit and Delete action buttons per row
    - Delete uses a confirmation dialog before sending the request
    - Use the existing `"Api"` named HttpClient to call GET `/api/file-records`
    - _Requirements: 6.1, 6.4, 6.6_

  - [x] 7.2 Create File Record create page
    - Create `src/Web/Components/Pages/FileRecordCreate.razor` with route `/file-records/new` and `[Authorize]` attribute
    - Create `src/Web/Components/Pages/FileRecordCreate.razor.cs` code-behind
    - Use `EditForm` with `DataAnnotationsValidator` for client-side validation
    - Fields: Name (text input), FileType (select dropdown), FlopDiskNumber (optional number input), Date (InputDate), Client (text input)
    - On valid submit, POST to `/api/file-records` and navigate to `/file-records`
    - Display validation errors next to corresponding fields
    - _Requirements: 6.2, 6.5, 6.6_

  - [x] 7.3 Create File Record edit page
    - Create `src/Web/Components/Pages/FileRecordEdit.razor` with route `/file-records/{id}/edit` and `[Authorize]` attribute
    - Create `src/Web/Components/Pages/FileRecordEdit.razor.cs` code-behind
    - Load existing record via GET `/api/file-records/{id}` and pre-populate the form
    - Use `EditForm` with `DataAnnotationsValidator` for client-side validation
    - On valid submit, PUT to `/api/file-records/{id}` and navigate to `/file-records`
    - Display validation errors next to corresponding fields
    - _Requirements: 6.3, 6.5, 6.6_

- [x] 8. Checkpoint — Verify full-stack builds
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Automated tests
  - [x] 9.1 Set up test authentication handler in CustomWebApplicationFactory
    - Add a `TestAuthHandler` that auto-authenticates all requests in the test environment
    - Register the handler in `CustomWebApplicationFactory` via `ConfigureServices`
    - Ensure FileRecord endpoints are accessible without real JWT tokens in tests
    - _Requirements: 7.4_

  - [x] 9.2 Write example-based endpoint tests for File Records CRUD
    - Create `tests/Api.Tests/FileRecordEndpointTests.cs`
    - Test `Create_ValidRequest_Returns201WithCreatedRecord`
    - Test `Create_MissingName_Returns400`
    - Test `Create_MissingDate_Returns400`
    - Test `Create_InvalidFileType_Returns400`
    - Test `Create_MissingClient_Returns400`
    - Test `Create_WithoutFlopDiskNumber_PersistsAsNull`
    - Test `GetAll_ReturnsAllRecords`
    - Test `GetById_ExistingId_Returns200`
    - Test `GetById_NonExistentId_Returns404`
    - Test `Update_ValidRequest_Returns200`
    - Test `Update_NonExistentId_Returns404`
    - Test `Update_InvalidData_Returns400`
    - Test `Delete_ExistingId_Returns204`
    - Test `Delete_NonExistentId_Returns404`
    - Follow naming convention: `MethodUnderTest_Scenario_ExpectedResult`
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 9.3 Write property test: Creation round-trip preserves data
    - **Property 1: Creation round-trip preserves data**
    - **Validates: Requirements 1.1, 1.6, 2.2**
    - Create `tests/Api.Tests/FileRecordPropertyTests.cs`
    - Use FsCheck to generate valid CreateFileRecordRequest inputs
    - POST to create, then GET by returned Id, assert all fields match

  - [x] 9.4 Write property test: Validation rejects invalid inputs
    - **Property 2: Validation rejects invalid inputs**
    - **Validates: Requirements 1.2, 1.3, 1.5, 3.3**
    - Generate requests with empty/whitespace Name or Client, or invalid FileType enum values
    - Assert API returns 400 and record is not persisted

  - [x] 9.5 Write property test: List ordering by Date descending
    - **Property 3: List ordering by Date descending**
    - **Validates: Requirements 2.1**
    - Create multiple records with distinct random dates
    - GET all and assert returned list is ordered by Date descending

  - [x] 9.6 Write property test: Update preserves Id and persists changes
    - **Property 4: Update preserves Id and persists changes**
    - **Validates: Requirements 3.1, 3.4**
    - Create a record, then update with new valid data
    - Assert returned Id is unchanged and all other fields match the update payload

  - [x] 9.7 Write property test: Delete removes record from persistence
    - **Property 5: Delete removes record from persistence**
    - **Validates: Requirements 4.1**
    - Create a record, DELETE it (assert 204), then GET by same Id (assert 404)

- [x] 10. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Example-based tests validate specific scenarios and edge cases
- All tests use the existing `CustomWebApplicationFactory` with InMemory database
- The project already has FsCheck 3.3.3 + FsCheck.Xunit as dependencies
- EFCore.NamingConventions handles snake_case column naming automatically

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["3.2"] },
    { "id": 4, "tasks": ["5.1", "5.2"] },
    { "id": 5, "tasks": ["5.3"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 7, "tasks": ["9.1"] },
    { "id": 8, "tasks": ["9.2", "9.3", "9.4", "9.5", "9.6", "9.7"] }
  ]
}
```
