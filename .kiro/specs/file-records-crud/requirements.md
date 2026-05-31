# Requirements Document

## Introduction

This feature adds a complete CRUD (Create, Read, Update, Delete) for File Records to SinterPrints. A File Record represents a registered art file with metadata such as name, type, associated client, date, and an optional floppy disk number for legacy files. The implementation spans the full stack: domain entity, DTOs, validation, EF Core persistence, API endpoints, and Blazor UI pages.

## Glossary

- **File_Record**: A domain entity representing a registered art file with metadata (name, type, client, date, and optional legacy disk number)
- **FileType**: An enumeration of supported art file formats (e.g., CorelDRAW, Photoshop, Illustrator, Inkscape, PDF, Other)
- **API**: The ASP.NET Core Minimal API backend that exposes HTTP endpoints for File Record operations
- **UI**: The Blazor Server frontend that provides pages for managing File Records
- **File_Record_Service**: The application service responsible for orchestrating File Record persistence operations
- **Validator**: The component responsible for enforcing data integrity rules on File Record input

## Requirements

### Requirement 1: Create a File Record

**User Story:** As a user, I want to create a new file record with metadata, so that I can register an art file in the system.

#### Acceptance Criteria

1. WHEN the user submits a valid file record creation request, THE API SHALL persist the File_Record with a new generated uuid and return the created resource with HTTP status 201
2. WHEN the user submits a creation request with a missing or empty Name field, THE Validator SHALL reject the request and THE API SHALL return HTTP status 400 with a descriptive error message
3. WHEN the user submits a creation request with an invalid FileType value, THE Validator SHALL reject the request and THE API SHALL return HTTP status 400 with a descriptive error message
4. WHEN the user submits a creation request with a missing Date field, THE Validator SHALL reject the request and THE API SHALL return HTTP status 400 with a descriptive error message
5. WHEN the user submits a creation request with a missing or empty Client field, THE Validator SHALL reject the request and THE API SHALL return HTTP status 400 with a descriptive error message
6. WHEN the user submits a creation request with a FlopDiskNumber value, THE API SHALL persist the File_Record with the provided FlopDiskNumber
7. WHEN the user submits a creation request without a FlopDiskNumber value, THE API SHALL persist the File_Record with FlopDiskNumber as null

### Requirement 2: Read File Records

**User Story:** As a user, I want to list and view file records, so that I can find and inspect registered art files.

#### Acceptance Criteria

1. WHEN the user requests the list of file records, THE API SHALL return all File_Records ordered by Date descending with HTTP status 200
2. WHEN the user requests a specific file record by Id, THE API SHALL return the matching File_Record with HTTP status 200
3. WHEN the user requests a file record by an Id that does not exist, THE API SHALL return HTTP status 404

### Requirement 3: Update a File Record

**User Story:** As a user, I want to update an existing file record, so that I can correct or change its metadata.

#### Acceptance Criteria

1. WHEN the user submits a valid update request for an existing File_Record, THE API SHALL persist the updated fields and return the updated resource with HTTP status 200
2. WHEN the user submits an update request for a File_Record Id that does not exist, THE API SHALL return HTTP status 404
3. WHEN the user submits an update request with invalid data, THE Validator SHALL reject the request and THE API SHALL return HTTP status 400 with a descriptive error message
4. THE API SHALL preserve the original Id of the File_Record during an update operation

### Requirement 4: Delete a File Record

**User Story:** As a user, I want to delete a file record, so that I can remove entries that are no longer needed.

#### Acceptance Criteria

1. WHEN the user submits a delete request for an existing File_Record, THE API SHALL remove the record from persistence and return HTTP status 204
2. WHEN the user submits a delete request for a File_Record Id that does not exist, THE API SHALL return HTTP status 404

### Requirement 5: Domain Model and Persistence

**User Story:** As a developer, I want a well-defined domain entity and EF Core mapping, so that file records are stored correctly in PostgreSQL.

#### Acceptance Criteria

1. THE File_Record entity SHALL have the following fields: Id (Guid), Name (string), FileType (enum), FlopDiskNumber (nullable int), Date (DateTime), Client (string)
2. THE EF Core mapping SHALL store the File_Record in a table named "file_records" with snake_case column names
3. THE EF Core mapping SHALL use uuid as the primary key column type for Id
4. THE EF Core mapping SHALL use timestamptz as the column type for Date
5. THE EF Core mapping SHALL configure FlopDiskNumber as a nullable column
6. WHEN a new migration is created, THE Infrastructure project SHALL include the migration for the file_records table

### Requirement 6: Blazor UI for File Records

**User Story:** As a user, I want a web interface to manage file records, so that I can create, view, edit, and delete records without using the API directly.

#### Acceptance Criteria

1. THE UI SHALL provide a page that lists all file records in a table with columns for Name, FileType, Client, Date, and FlopDiskNumber
2. THE UI SHALL provide a form for creating a new file record with fields for Name, FileType (dropdown), FlopDiskNumber (optional), Date, and Client
3. THE UI SHALL provide a form for editing an existing file record pre-populated with current values
4. WHEN the user confirms deletion of a file record, THE UI SHALL send a delete request and remove the record from the displayed list
5. WHEN a form validation error occurs, THE UI SHALL display the error message next to the corresponding field
6. THE UI SHALL use the existing Blazor component patterns including code-behind for complex logic

### Requirement 7: Automated Tests

**User Story:** As a developer, I want focused tests for the file records CRUD, so that I can verify correctness and prevent regressions.

#### Acceptance Criteria

1. THE test suite SHALL include endpoint tests that verify HTTP status codes and response bodies for all CRUD operations on File_Records
2. THE test suite SHALL include validation tests that verify rejection of invalid input with appropriate error messages
3. THE test suite SHALL follow the existing test naming convention: MethodUnderTest_Scenario_ExpectedResult
4. THE test suite SHALL use the existing CustomWebApplicationFactory with InMemory database for API tests
