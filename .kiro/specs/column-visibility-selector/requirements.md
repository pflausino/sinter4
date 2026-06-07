# Requirements Document

## Introduction

This feature adds a column visibility selector to the file-records page (Fichas de Arquivo). Users will be able to show or hide table columns via a dropdown combobox labeled "Campos" containing checkboxes for each column. This improves usability by letting users focus on the information most relevant to them while keeping less-used columns hidden by default.

## Glossary

- **Column_Visibility_Selector**: A dropdown combobox UI component labeled "Campos" that contains checkboxes allowing users to toggle the visibility of individual table columns.
- **File_Records_Page**: The Blazor page at `/file-records` that displays a paginated table of file record entries.
- **Column**: A vertical data field in the file records table (e.g., Nome, Tipo, Nº Arquivo, Cliente, Data, Nº Disquete, Ações).
- **Visible_Column**: A column whose checkbox is checked in the Column_Visibility_Selector, causing the column to be rendered in the table.
- **Hidden_Column**: A column whose checkbox is unchecked in the Column_Visibility_Selector, causing the column to not be rendered in the table.

## Requirements

### Requirement 1: Display Column Visibility Selector

**User Story:** As a user, I want to see a dropdown labeled "Campos" on the file-records page, so that I can control which columns are displayed in the table.

#### Acceptance Criteria

1. THE File_Records_Page SHALL display a Column_Visibility_Selector dropdown labeled "Campos" in the toolbar area above the table
2. WHEN the user clicks the "Campos" dropdown, THE Column_Visibility_Selector SHALL display a list of checkboxes, one for each available column: Nome, Tipo, Nº Arquivo, Cliente, Data, Nº Disquete, and Ações
3. THE Column_Visibility_Selector SHALL be accessible via keyboard navigation and comply with ARIA combobox patterns
4. WHEN the user unchecks a column checkbox, THE File_Records_Page SHALL immediately hide that column from the table, and WHEN the user checks a column checkbox, THE File_Records_Page SHALL immediately show that column in the table
5. IF the user attempts to uncheck the last remaining checked column checkbox, THEN THE Column_Visibility_Selector SHALL keep that checkbox checked and prevent the column from being hidden, ensuring at least 1 column remains visible at all times

### Requirement 2: Default Column Visibility State

**User Story:** As a user, I want the most relevant columns to be visible by default when I load the page, so that I can immediately see the most important information.

#### Acceptance Criteria

1. WHEN the File_Records_Page loads, THE Column_Visibility_Selector SHALL have the following columns checked (visible) by default: Nome, Tipo, Nº Arquivo, Cliente, Data, and Ações, and THE File_Records_Page SHALL render only these columns in the table
2. WHEN the File_Records_Page loads, THE Column_Visibility_Selector SHALL have the Nº Disquete column unchecked (hidden) by default, and THE File_Records_Page SHALL not render the Nº Disquete column header or cell data in the table
3. WHEN the user navigates to the File_Records_Page, THE Column_Visibility_Selector SHALL reset to the default visibility state regardless of any prior column visibility changes made during the session

### Requirement 3: Toggle Column Visibility

**User Story:** As a user, I want to check or uncheck column checkboxes to show or hide the corresponding columns in the table, so that I can customize my view.

#### Acceptance Criteria

1. WHEN the user checks a Hidden_Column checkbox, THE File_Records_Page SHALL render that column in the table without requiring a page reload
2. WHEN the user unchecks a Visible_Column checkbox, THE File_Records_Page SHALL remove that column from the table without requiring a page reload
3. WHILE a column is hidden, THE File_Records_Page SHALL not render the column header or any cell data for that column in the desktop table view
4. WHILE a column is hidden, THE File_Records_Page SHALL not render the corresponding detail field in the mobile card view
5. WHEN the user re-shows a previously hidden column, THE File_Records_Page SHALL render that column in its original position relative to the other columns as defined in Requirement 1
6. WHILE additional records are loaded via infinite scroll, THE File_Records_Page SHALL apply the current column visibility state to all newly rendered rows

### Requirement 4: Minimum Visible Columns

**User Story:** As a user, I want to be prevented from hiding all columns, so that the table always displays meaningful content.

#### Acceptance Criteria

1. WHILE only one Visible_Column remains checked, THE Column_Visibility_Selector SHALL disable the checkbox of that last visible column so that it cannot be unchecked via mouse click or keyboard interaction
2. WHEN the user checks an additional column while only one Visible_Column was checked, THE Column_Visibility_Selector SHALL re-enable the previously disabled checkbox within the same rendering cycle
3. WHILE a checkbox is disabled due to being the last Visible_Column, THE Column_Visibility_Selector SHALL display that checkbox with a visually distinct disabled appearance (e.g., grayed out) indicating it cannot be unchecked

### Requirement 5: Selector Interaction Behavior

**User Story:** As a user, I want the dropdown to close when I click outside of it, so that it does not obstruct my view of the table.

#### Acceptance Criteria

1. WHILE the Column_Visibility_Selector is open, WHEN the user clicks outside the Column_Visibility_Selector, THE Column_Visibility_Selector SHALL close the dropdown within 100 milliseconds
2. WHILE the Column_Visibility_Selector is open, WHEN the user presses the Escape key, THE Column_Visibility_Selector SHALL close the dropdown and return focus to the "Campos" button
3. WHILE the Column_Visibility_Selector is open, THE File_Records_Page SHALL apply a visually distinct style to the "Campos" button that differentiates it from its closed state
4. WHEN the Column_Visibility_Selector is closed and subsequently reopened, THE Column_Visibility_Selector SHALL display the same checkbox checked/unchecked states that were present when it was last closed
