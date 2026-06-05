# Implementation Plan: Smart Search File Records

## Overview

Add a smart search capability to the `/file-records` Blazor page. The implementation adds a `GET /api/file-records/search?q=` endpoint with PostgreSQL `unaccent` + `ILIKE` for accent/case-insensitive matching, a C# scoring algorithm prioritizing Name over Client matches, and a redesigned page layout integrating the search bar with existing controls.

## Tasks

- [x] 1. Infrastructure: Enable PostgreSQL `unaccent` extension
  - [x] 1.1 Create EF Core migration to enable the `unaccent` extension
    - Run `dotnet ef migrations add AddUnaccentExtension --project src/Infrastructure --startup-project src/Api`
    - The migration `Up` method should execute `CREATE EXTENSION IF NOT EXISTS unaccent;`
    - The migration `Down` method should execute `DROP EXTENSION IF EXISTS unaccent;`
    - _Requirements: 7.5_

- [x] 2. Backend: Implement search service and endpoint
  - [x] 2.1 Add `SearchAsync` method to `IFileRecordService` and implement scoring logic in `FileRecordService`
    - Add `Task<List<FileRecordResponse>> SearchAsync(string searchTerm)` to `IFileRecordService`
    - Implement `SearchAsync` in `FileRecordService` with:
      - Split search term into whitespace-separated words
      - Query PostgreSQL using `unaccent(lower(...)) ILIKE unaccent(lower('%term%'))` on Name and Client columns for each term
      - Load candidates into memory and score with `ComputeScore` (Name=10pts/term, Client=5pts/term, both=15pts/term, all-in-Name bonus=+5)
      - Filter out zero-score records, order by score descending, take max 100
    - Add static helper `RemoveDiacritics(string)` using `System.Globalization` normalization for in-memory scoring
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 7.4, 7.5_

  - [x] 2.2 Add `GET /api/file-records/search` endpoint in `FileRecordEndpoints.cs`
    - Add `group.MapGet("/search", ...)` that accepts query parameter `q`
    - If `q` is empty, null, or whitespace-only → return `Results.Ok(Array.Empty<FileRecordResponse>())`
    - If `q.Length > 200` → return `Results.BadRequest(new { error = "Search term must not exceed 200 characters" })`
    - Otherwise call `service.SearchAsync(q)` and return `Results.Ok(results)`
    - Wrap in try/catch for database errors → return `Results.StatusCode(500)` without internal details
    - Place this route BEFORE the `/{id:guid}` route to avoid route conflicts
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.6_

  - [x] 2.3 Write property test: Substring matching inclusivity
    - **Property 1: Substring matching inclusivity**
    - **Validates: Requirements 3.1, 3.2, 3.4, 4.1**
    - Generate random FileRecords and random substrings of their Name/Client fields (after normalization)
    - Assert the record appears in search results when searching with that substring

  - [x] 2.4 Write property test: Multi-word independent matching
    - **Property 2: Multi-word independent matching**
    - **Validates: Requirements 3.3**
    - Generate FileRecords whose Name contains multiple known words; search with those words in shuffled order
    - Assert the record is included in results regardless of word order

  - [x] 2.5 Write property test: Whitespace input returns empty results
    - **Property 3: Whitespace input returns empty results**
    - **Validates: Requirements 3.5, 7.2**
    - Generate strings composed entirely of whitespace characters (spaces, tabs, newlines)
    - Assert SearchAsync returns an empty list without errors

  - [x] 2.6 Write property test: Scoring invariants
    - **Property 4: Scoring invariants**
    - **Validates: Requirements 4.2, 4.3**
    - Generate two FileRecords: one matching on Name only, one matching on Client only
    - Assert Name-match record has strictly higher score
    - Generate a record matching on both Name and Client
    - Assert its score is >= a Name-only match

  - [x] 2.7 Write property test: Results ordered and capped
    - **Property 5: Results are ordered by score descending with maximum 100 results**
    - **Validates: Requirements 4.4, 7.4**
    - Generate a dataset of 150+ records, run a search
    - Assert returned list is sorted by score descending and length ≤ 100

  - [x] 2.8 Write property test: Excessive input length rejected
    - **Property 6: Excessive input length is rejected**
    - **Validates: Requirements 7.3**
    - Generate strings with length > 200
    - Assert the search endpoint returns HTTP 400

  - [x] 2.9 Write unit tests for search service and endpoint
    - `SearchAsync_WithSingleTerm_ReturnsMatchingRecords`
    - `SearchAsync_WithMultipleWords_ReturnsRecordsMatchingAllWords`
    - `SearchAsync_CaseInsensitive_MatchesRegardlessOfCase`
    - `SearchAsync_AccentInsensitive_MatchesWithoutAccents`
    - `SearchAsync_EmptyTerm_ReturnsEmptyList`
    - `SearchAsync_NameMatchScoredHigherThanClientOnlyMatch`
    - `ComputeScore_BothFieldsMatch_ScoreGreaterOrEqualToNameOnly`
    - `SearchEndpoint_MissingQ_Returns200EmptyArray`
    - `SearchEndpoint_QLengthExceeds200_Returns400`
    - `SearchEndpoint_Unauthenticated_Returns401`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 7.1, 7.2, 7.3_

- [x] 3. Checkpoint - Backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Frontend: Redesign page layout and add search functionality
  - [x] 4.1 Redesign `FileRecords.razor` page layout with search row
    - Restructure the page header into: Title row (h1 + subtitle) → Search row (Search_Bar + Search_Button + "Nova Ficha" button) → Results area
    - Add `<input>` with `type="text"`, `maxlength="200"`, `placeholder="Buscar por nome do arquivo ou cliente..."`, `aria-label="Buscar fichas de arquivo"`
    - Add Search_Button with `aria-label="Executar busca"` adjacent to the Search_Bar
    - Move "Nova Ficha" button to the same search row, aligned right
    - Ensure tab order: Search_Bar → Search_Button → Nova Ficha → records list
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 6.1, 6.2, 6.3, 6.5, 6.6_

  - [x] 4.2 Add responsive CSS for search row and stacking at ≤768px
    - Search_Bar occupies ≥50% row width on viewports > 768px
    - At ≤768px: stack Search_Bar, Search_Button, and "Nova Ficha" vertically, full width
    - Style loading spinner inside Search_Button when disabled
    - _Requirements: 1.4, 6.4_

  - [x] 4.3 Implement search execution logic in `FileRecords.razor.cs`
    - Add state properties: `SearchTerm`, `IsSearching`, `HasSearchError`, `IsSearchActive`, `SearchResultCount`, `SearchedTerm`
    - Implement `ExecuteSearch()`: call `GET /api/file-records/search?q={encoded}`, update state
    - Implement `ClearSearch()`: reset state, reload full list via `LoadRecords()`
    - Bind Search_Button click and Enter keypress to `ExecuteSearch()`
    - If `SearchTerm` is empty on submit, call `ClearSearch()` instead
    - Disable Search_Button and show spinner while `IsSearching` is true
    - On API error: set `HasSearchError = true`, preserve previous list, re-enable button
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 5.5_

  - [x] 4.4 Add search results display: summary, empty state, and error messages
    - When `IsSearchActive && SearchResultCount > 0`: show "X resultados para '{SearchedTerm}'"
    - When `IsSearchActive && SearchResultCount == 0`: show "Nenhum resultado encontrado para '{SearchedTerm}'"
    - When `HasSearchError`: show "Não foi possível completar a busca. Tente novamente."
    - When search is cleared: remove results summary, show full list
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 4.5 Write bUnit component tests for search functionality
    - Search bar renders with correct placeholder and maxlength
    - Search button triggers API call with non-empty input
    - Enter key triggers search
    - Loading state shown during search
    - Error message displayed on failure
    - "Nenhum resultado encontrado" shown for zero results
    - Results summary shows count and searched term
    - Clearing search restores full list
    - ARIA labels present on search elements
    - Tab order: Search_Bar → Search_Button → Nova Ficha
    - Responsive layout stacks at ≤768px
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.5, 5.2, 5.3, 6.5, 6.6_

- [x] 5. Checkpoint - Full integration works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Integration testing with real PostgreSQL
  - [x] 6.1 Write integration tests using Testcontainers + WebApplicationFactory
    - Verify `unaccent` extension works correctly with real PostgreSQL
    - Verify end-to-end search with seeded data (accent-insensitive, case-insensitive)
    - Verify authentication is required on the search endpoint (returns 401 without token)
    - Verify `q` length > 200 returns 400
    - Verify empty/whitespace `q` returns 200 with empty array
    - _Requirements: 3.1, 3.2, 3.4, 7.1, 7.2, 7.3, 7.5, 7.6_

- [x] 7. Final checkpoint - All tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (FsCheck + xUnit, following existing `FileRecordPropertyTests` patterns)
- Unit tests validate specific examples and edge cases
- The search endpoint route must be registered BEFORE `/{id:guid}` to prevent ASP.NET routing conflicts
- The `RemoveDiacritics` helper is used for in-memory scoring; PostgreSQL `unaccent` handles DB-level normalization
- Integration tests require Testcontainers PostgreSQL to validate the `unaccent` extension behavior

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "4.1"] },
    { "id": 1, "tasks": ["2.1", "4.2"] },
    { "id": 2, "tasks": ["2.2", "4.3"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.5", "2.6", "2.7", "2.8", "2.9", "4.4"] },
    { "id": 4, "tasks": ["4.5", "6.1"] }
  ]
}
```
