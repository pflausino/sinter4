# Requirements Document

## Introduction

Smart Search for the File Records page (`/file-records`) in SinterPrints. This feature adds a search bar that allows users to quickly locate file records by typing fragments of the file name or client name. The search uses fuzzy/partial matching, prioritizes file name matches over client name matches, and includes a redesigned page layout that integrates the search elements for improved usability.

## Glossary

- **Search_Bar**: A text input component on the `/file-records` page where users type search terms to filter file records
- **Search_Button**: A button adjacent to the Search_Bar that triggers the search execution
- **Search_Engine**: The backend logic that performs fuzzy/partial matching against file record fields
- **File_Records_Page**: The Blazor page at route `/file-records` that displays the catalog of file records
- **File_Record**: A domain entity representing an art file entry with properties including Name, Client, FileType, FlopDiskNumber, Date, and FileNumber
- **Search_Term**: The text value entered by the user in the Search_Bar
- **Match_Score**: A numeric relevance value assigned to each File_Record based on how closely it matches the Search_Term

## Requirements

### Requirement 1: Display Search Bar on File Records Page

**User Story:** As a user, I want to see a search bar prominently placed on the File Records page, so that I can quickly find specific file records without scrolling through the entire list.

#### Acceptance Criteria

1. THE File_Records_Page SHALL display the Search_Bar above the records list area
2. THE Search_Bar SHALL accept free-text input and prevent the user from entering more than 200 characters by blocking additional input beyond the limit
3. THE Search_Bar SHALL display placeholder text "Buscar por nome do arquivo ou cliente..." to guide the user
4. THE Search_Bar SHALL occupy at least 50% of the available row width on viewports wider than 768px

### Requirement 2: Search Button Execution

**User Story:** As a user, I want a search button to execute the search, so that I have explicit control over when the search runs.

#### Acceptance Criteria

1. THE File_Records_Page SHALL display the Search_Button adjacent to the Search_Bar
2. WHEN the user clicks the Search_Button and the Search_Term contains at least 1 character, THE Search_Engine SHALL execute a search using the current Search_Term value
3. WHEN the user presses the Enter key while the Search_Bar has focus and the Search_Term contains at least 1 character, THE Search_Engine SHALL execute a search using the current Search_Term value
4. IF the user activates a search while the Search_Term is empty, THEN THE File_Records_Page SHALL not execute the search and SHALL return to displaying the default record list
5. WHILE the Search_Engine is processing a search, THE Search_Button SHALL display a loading indicator and remain disabled until the search completes or 30 seconds elapse
6. IF the Search_Engine fails to return results due to a network or server error, THEN THE File_Records_Page SHALL display an error message indicating that the search could not be completed and SHALL re-enable the Search_Button

### Requirement 3: Fuzzy/Partial Matching on File Name

**User Story:** As a user, I want to search using fragments or partial text of file names, so that I can find records without remembering the exact full name.

#### Acceptance Criteria

1. WHEN a Search_Term is submitted, THE Search_Engine SHALL perform case-insensitive contiguous substring matching against the File_Record Name field
2. WHEN the Search_Term is a contiguous substring of a File_Record Name (after case-insensitive and accent-insensitive normalization), THE Search_Engine SHALL include that File_Record in the results regardless of the substring's position within the Name
3. WHEN the Search_Term contains multiple whitespace-separated words, THE Search_Engine SHALL match File_Records where the Name contains each individual word as a contiguous substring, independently of word order within the Name
4. THE Search_Engine SHALL treat accented and unaccented characters as equivalent during matching (e.g., "grafica" matches "Gráfica")
5. IF the Search_Term is empty or contains only whitespace characters, THEN THE Search_Engine SHALL return an empty result set without performing matching

### Requirement 4: Client Name as Secondary Search Criterion

**User Story:** As a user, I want the search to also consider the client name, so that I can find records when I only remember who the work was for.

#### Acceptance Criteria

1. WHEN a Search_Term is submitted, THE Search_Engine SHALL perform case-insensitive and accent-insensitive partial matching against the File_Record Client field, using the same fragment and multi-word matching logic applied to the Name field
2. THE Search_Engine SHALL assign a higher Match_Score to File_Records where the Name field matches the Search_Term than to File_Records where only the Client field matches
3. IF a File_Record matches the Search_Term on both the Name field and the Client field, THEN THE Search_Engine SHALL assign a Match_Score equal to or greater than a Name-only match
4. THE Search_Engine SHALL return results ordered by Match_Score in descending order, so that file name matches appear before client-only matches

### Requirement 5: Search Results Display

**User Story:** As a user, I want to see the search results clearly, so that I can identify the records I am looking for.

#### Acceptance Criteria

1. WHEN the Search_Engine returns results, THE File_Records_Page SHALL display the matching File_Records in the same table/card format as the full list
2. WHEN the Search_Engine returns zero results, THE File_Records_Page SHALL display a "Nenhum resultado encontrado" message including the searched term
3. WHEN the Search_Engine returns results, THE File_Records_Page SHALL display the count of matching records and the searched term in the results summary area so the user can confirm the list is filtered
4. WHEN the Search_Bar is cleared and the search is executed, THE File_Records_Page SHALL return to displaying the full unfiltered record list as shown on initial page load, removing the results summary
5. IF the search request fails due to a network or server error, THEN THE File_Records_Page SHALL display an error message indicating the search could not be completed and SHALL preserve the previously displayed record list

### Requirement 6: Page Layout Redesign for Search Integration

**User Story:** As a user, I want the page elements arranged in a logical and visually clean layout, so that the search functionality integrates naturally with the existing controls.

#### Acceptance Criteria

1. THE File_Records_Page SHALL position the page title and subtitle at the top of the page, above all other interactive elements
2. THE File_Records_Page SHALL position the Search_Bar and Search_Button in a dedicated row below the title and above the records list, with the Search_Bar occupying the majority of the available row width
3. THE File_Records_Page SHALL position the "Nova Ficha" action button on the same row as the search elements, aligned to the right
4. WHILE the viewport width is 768px or less, THE File_Records_Page SHALL stack the Search_Bar, Search_Button, and "Nova Ficha" action button vertically, each occupying the full available width
5. THE File_Records_Page SHALL provide an ARIA label describing the search purpose on the Search_Bar (e.g., aria-label indicating "search file records") and an ARIA label on the Search_Button indicating its action (e.g., aria-label indicating "execute search")
6. THE File_Records_Page SHALL render the keyboard tab order as: Search_Bar, then Search_Button, then "Nova Ficha" action button, followed by the records list content

### Requirement 7: Search API Endpoint

**User Story:** As a developer, I want a dedicated search endpoint in the API, so that search logic executes server-side with efficient database queries.

#### Acceptance Criteria

1. THE Api SHALL expose a GET endpoint at `/api/file-records/search` that accepts a query parameter `q` representing the Search_Term and requires the same authentication as existing File_Record endpoints
2. WHEN the `q` parameter is empty, missing, or contains only whitespace, THE Api SHALL return an HTTP 200 response with an empty JSON array
3. WHEN the `q` parameter length exceeds 200 characters, THE Api SHALL return a 400 Bad Request response with an error message indicating the maximum allowed length
4. WHEN the `q` parameter contains a valid Search_Term of 1 to 200 non-whitespace-only characters, THE Api SHALL return an HTTP 200 response containing a JSON array of FileRecordResponse objects limited to a maximum of 100 records, ordered by Match_Score descending
5. THE Api SHALL perform case-insensitive and accent-insensitive partial matching against the File_Record Name and Client fields, consistent with the Search_Engine behavior defined in Requirements 3 and 4
6. IF the database is unreachable or the search query fails, THEN THE Api SHALL return an HTTP 500 response without exposing internal error details to the caller
