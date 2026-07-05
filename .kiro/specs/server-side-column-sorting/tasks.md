# Implementation Plan: Server-Side Column Sorting

## Overview

Add server-side column sorting to the `/file-records` page. The API endpoints accept `sortBy` and `sortDir` query parameters, apply `ORDER BY` before pagination, and the Blazor frontend provides clickable column headers (desktop) and a sort selector (mobile) that reset and re-fetch data when the sort changes.

## Tasks

- [ ] 1. Backend: Add sorting support to API and service layer
  - [ ] 1.1 Add `ApplySort` helper method to `FileRecordService`
    - Create a static `ValidSortFields` hashset: `name`, `file_type`, `file_number`, `client`, `date`, `flop_disk_number`
    - Implement `ApplySort(IQueryable<FileRecord> query, string? sortBy, string? sortDir)` that:
      - Falls back to `date` if `sortBy` is null/invalid
      - Falls back to `asc` if `sortDir` is null/invalid (except `date` defaults to `desc`)
      - Applies null handling: NULLS LAST for ASC, NULLS FIRST for DESC (via `.OrderBy(f => f.Field == null).ThenBy(f => f.Field)` pattern)
      - Always appends `.ThenBy(f => f.Id)` as tiebreaker for stable pagination
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ] 1.2 Modify `GetPagedAsync` to accept and apply sort parameters
    - Add `string? sortBy = null, string? sortDir = null` parameters to `IFileRecordService.GetPagedAsync` and implementation
    - Replace hardcoded `OrderByDescending(f => f.Date)` with `ApplySort(query, sortBy, sortDir)`
    - Ensure `Skip(offset).Take(limit)` comes AFTER the dynamic order
    - _Requirements: 3.1, 3.6_

  - [ ] 1.3 Modify `SearchPagedAsync` to accept and apply sort parameters
    - Add `string? sortBy = null, string? sortDir = null` parameters to `IFileRecordService.SearchPagedAsync` and implementation
    - Build dynamic `ORDER BY` clause in the raw SQL using a `BuildOrderByClause` helper
    - Validate `sortBy` against `ValidSortFields`, fallback to `date DESC NULLS LAST`
    - Always append `, id` as tiebreaker in the ORDER BY clause
    - _Requirements: 3.1, 3.5, 3.6_

  - [ ] 1.4 Update `FileRecordEndpoints.cs` to pass sort parameters
    - Add `string? sortBy` and `string? sortDir` query parameters to both `GET /` and `GET /search` endpoints
    - Pass them through to the service methods
    - No validation needed at endpoint level — service handles fallback silently
    - _Requirements: 3.1, 3.2, 3.4_

  - [ ] 1.5 Update `IFileRecordService` interface
    - Update method signatures for `GetPagedAsync` and `SearchPagedAsync` to include optional sort parameters
    - _Requirements: 3.1_

- [ ] 2. Backend: Tests for sorting
  - [ ] 2.1 Write unit tests for `ApplySort` logic
    - Test each valid field with ASC and DESC direction
    - Test invalid `sortBy` falls back to `date DESC`
    - Test null `sortDir` defaults to `asc` (except `date` defaults to `desc`)
    - Test null values appear at correct position (last for ASC, first for DESC)
    - Test stable pagination: Id tiebreaker ensures deterministic ordering
    - _Requirements: 3.2, 3.3, 3.4, 3.5_

  - [ ] 2.2 Write property-based tests for sort consistency
    - **Property 1**: For any valid sortBy and direction, consecutive pages (offset=0 and offset=50) produce non-overlapping result sets
    - **Property 2**: For any invalid sortBy string, results match the default `date DESC` ordering
    - **Property 3**: For any field with nullable values, nulls appear last (ASC) or first (DESC)
    - _Requirements: 3.2, 3.5, 3.6_

  - [ ] 2.3 Write integration tests (Testcontainers PostgreSQL)
    - Seed data with varied values, nulls, and duplicates for all fields
    - Verify `GET /api/file-records?sortBy=name&sortDir=asc` returns alphabetically ordered results
    - Verify `GET /api/file-records?sortBy=date&sortDir=desc` returns newest first
    - Verify `GET /api/file-records/search?q=term&sortBy=client&sortDir=asc` works correctly
    - Verify pagination consistency: page 2 continues where page 1 ended
    - Verify invalid sortBy silently falls back to date DESC
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [ ] 3. Checkpoint: Backend tests pass
  - Run `dotnet test` and verify all sorting tests pass before proceeding to frontend.

- [ ] 4. Frontend: Add sort state and API integration
  - [ ] 4.1 Add sort state to `FileRecords.razor.cs`
    - Add properties: `CurrentSortField` (default: `"date"`), `CurrentSortDirection` (default: `"desc"`)
    - Add `SortBy(string field)` method: if same field → toggle direction; if new field → set ASC; then reset and reload
    - Add helpers: `GetAriaSortValue(string field)`, `GetSortIcon(string field)`
    - _Requirements: 1.2, 1.3, 1.5_

  - [ ] 4.2 Modify `LoadRecords`, `LoadMoreItems`, and `ExecuteSearch` to include sort params
    - Append `&sortBy={CurrentSortField}&sortDir={CurrentSortDirection}` to all API fetch URLs
    - On sort change, reset `_observerInitialized = false` and clear `Records` before fetching
    - _Requirements: 4.1, 4.2, 4.3_

  - [ ] 4.3 Update table headers in `FileRecords.razor` to be clickable with sort indicators
    - Wrap each column header (except Ações) with `@onclick="() => SortBy("field")"` and `class="sortable"`
    - Add `aria-sort` attribute per column
    - Display sort indicator: ▲ for ASC, ▼ for DESC on active column; subtle ↕ on inactive sortable columns
    - Add `cursor: pointer` to `.sortable` class
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ] 4.4 Add mobile sort selector above cards view
    - Add a `<div class="mobile-sort-control">` that is visible only at ≤768px (CSS `display: none` on desktop)
    - Include a `<select>` for sort field and a button to toggle direction
    - Bind to `CurrentSortField` and `CurrentSortDirection`; on change, call `SortBy`
    - _Requirements: 5.1, 5.2, 5.3_

  - [ ] 4.5 Add CSS for sortable headers and mobile sort control
    - Style `.sortable` with pointer cursor, hover highlight
    - Style `.sort-indicator` as inline element next to header text
    - Style `.sorted` to visually distinguish the active sort column
    - Style `.mobile-sort-control` as a flex row with select + button
    - Media queries: hide `.mobile-sort-control` above 768px, hide table header sort on ≤768px
    - _Requirements: 2.3, 2.4, 5.1, 5.2_

- [ ] 5. Frontend: Tests for sorting UI
  - [ ] 5.1 Write bUnit component tests
    - Column headers render with `sortable` class and `aria-sort` attribute
    - Clicking a column header calls API with correct `sortBy`/`sortDir` params
    - Clicking same column toggles direction (verify second click sends `desc`)
    - Sort indicator shows correct arrow for active column
    - `aria-sort` values are correct after sort changes
    - Ações column is NOT clickable/sortable
    - Mobile sort selector renders at small viewport
    - Mobile sort change triggers API call with correct params
    - Sort change resets records list and shows loading state
    - Infinite scroll (LoadMoreItems) includes sort parameters
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.5, 4.1, 4.2, 4.3, 5.2, 5.3_

- [ ] 6. Final checkpoint: All tests pass
  - Run `make test` and verify everything is green.

## Notes

- The existing `GetAllAsync` method (non-paginated, legacy) is NOT modified since it's not used by the page.
- The search endpoint's raw SQL `ORDER BY` clause must be built safely — only whitelisted field names are allowed (no user input in SQL).
- The `Id` tiebreaker in ORDER BY ensures deterministic pagination (no duplicate/missing records across pages).
- The mobile sort selector only includes columns that exist in the domain — it does not depend on column visibility settings.
- Sort state persists across search/clear cycles within the same page session. Navigating away resets to default.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.5"] },
    { "id": 2, "tasks": ["1.4"] },
    { "id": 3, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 4, "tasks": ["4.1", "4.2"] },
    { "id": 5, "tasks": ["4.3", "4.4", "4.5"] },
    { "id": 6, "tasks": ["5.1"] }
  ]
}
```
