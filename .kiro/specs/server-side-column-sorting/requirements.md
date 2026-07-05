# Requirements Document

## Introduction

Server-side column sorting for the File Records table (`/file-records`) in SinterPrints. This feature allows users to sort the table by any visible column by clicking the column header. Since the table uses lazy loading (infinite scroll with paginated API calls), sorting must happen server-side to ensure correct ordering across all pages.

## Glossary

- **Sortable_Column**: A table column header that the user can click to change the sort order of the records
- **Sort_Indicator**: A visual arrow/chevron displayed on the active Sortable_Column indicating the current sort direction
- **Sort_Direction**: The ordering applied to results — ascending (ASC) or descending (DESC)
- **Sort_Field**: The backend field name corresponding to the clicked column (e.g., `name`, `file_type`, `date`, `client`, `file_number`, `flop_disk_number`)
- **Default_Sort**: The initial sort applied when the page loads — currently `date DESC NULLS LAST`
- **Lazy_Loading**: The existing infinite scroll mechanism that fetches records in pages of 50

## Requirements

### Requirement 1: Clickable Column Headers for Sorting

**User Story:** As a user, I want to click on any column header to sort the table by that column, so that I can organize records in the order most useful to me.

#### Acceptance Criteria

1. THE File_Records_Page SHALL render each visible column header (Nome, Tipo, Nº Arquivo, Cliente, Data, Nº Disquete) as a clickable Sortable_Column
2. WHEN the user clicks a Sortable_Column that is NOT the current sort column, THE table SHALL sort by that column in ascending order (ASC)
3. WHEN the user clicks the Sortable_Column that IS already the current sort column, THE table SHALL toggle the Sort_Direction (ASC → DESC → ASC)
4. THE "Ações" column SHALL NOT be sortable
5. THE Default_Sort when the page first loads SHALL be `date DESC` (most recent first), matching the current behavior

### Requirement 2: Sort Direction Indicator

**User Story:** As a user, I want to see which column is currently sorted and in which direction, so that I understand the current ordering at a glance.

#### Acceptance Criteria

1. THE active Sortable_Column header SHALL display a Sort_Indicator showing the current Sort_Direction (e.g., ▲ for ASC, ▼ for DESC)
2. Sortable_Column headers that are NOT the active sort column SHALL display a neutral indicator (e.g., subtle ↕ or no indicator) to communicate they are sortable
3. THE Sort_Indicator SHALL be visually distinct and readable at all viewport sizes
4. THE Sortable_Column header SHALL use `cursor: pointer` to indicate interactivity
5. THE Sortable_Column header SHALL have an `aria-sort` attribute set to `ascending`, `descending`, or `none` as appropriate

### Requirement 3: Server-Side Sort Execution

**User Story:** As a developer, I want sorting to happen on the server via API query parameters, so that paginated/lazy-loaded results are correctly ordered across all fetched pages.

#### Acceptance Criteria

1. THE API endpoints `GET /api/file-records` and `GET /api/file-records/search` SHALL accept optional query parameters `sortBy` (field name) and `sortDir` (`asc` or `desc`)
2. WHEN `sortBy` is not provided or is invalid, THE API SHALL use the Default_Sort (`date DESC NULLS LAST`)
3. THE API SHALL only accept valid Sort_Field values: `name`, `file_type`, `file_number`, `client`, `date`, `flop_disk_number`
4. WHEN `sortDir` is not provided or is invalid, THE API SHALL default to `asc`
5. THE API SHALL handle NULL values consistently: `NULLS LAST` for ASC, `NULLS FIRST` for DESC
6. THE sort parameters SHALL be applied BEFORE pagination (offset/limit), ensuring consistent ordering across pages

### Requirement 4: Integration with Lazy Loading

**User Story:** As a user, I want sorting to work seamlessly with the infinite scroll, so that when I change the sort order all loaded and future records reflect the new ordering.

#### Acceptance Criteria

1. WHEN the user changes the sort column or direction, THE File_Records_Page SHALL reset the loaded records and fetch from offset 0 with the new sort parameters
2. WHEN the infinite scroll triggers to load more records, THE request SHALL include the current `sortBy` and `sortDir` parameters to maintain ordering consistency
3. WHEN a search is active and the user changes sort, THE search results SHALL be re-fetched from offset 0 with the new sort parameters applied
4. WHILE a sort change is loading, THE table SHALL show the existing loading state (same as initial load)

### Requirement 5: Responsive Behavior

**User Story:** As a user on mobile, I want sorting controls to be available even in the card view layout.

#### Acceptance Criteria

1. ON viewports wider than 768px (desktop table view), sorting SHALL be triggered by clicking column headers
2. ON viewports 768px or narrower (mobile card view), THE File_Records_Page SHALL display a sort control (dropdown or segmented control) above the cards list allowing the user to select sort field and direction
3. THE mobile sort control SHALL show the currently active Sort_Field and Sort_Direction

