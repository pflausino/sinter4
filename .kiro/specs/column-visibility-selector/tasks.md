# Implementation Plan: Column Visibility Selector

## Overview

Add a "Campos" dropdown to the FileRecords page toolbar that allows users to toggle column visibility via checkboxes. The implementation is entirely client-side within the existing Blazor component — no API changes needed. A JS interop module handles click-outside detection for closing the dropdown.

## Tasks

- [x] 1. Define column model and visibility state in code-behind
  - [x] 1.1 Add ColumnDefinition record and AllColumns static array to FileRecords.razor.cs
    - Create `record ColumnDefinition(string Id, string Label, bool DefaultVisible)`
    - Define `static readonly ColumnDefinition[] AllColumns` with all 7 columns and their default visibility
    - Add `Dictionary<string, bool> ColumnVisibility` property
    - Add `bool IsColumnSelectorOpen` property
    - Add `ElementReference ColumnSelectorRef` and `DotNetObjectReference<FileRecords>? _columnSelectorDotNetRef`
    - _Requirements: 1.2, 2.1, 2.2_

  - [x] 1.2 Implement visibility state management methods
    - Add `InitializeColumnVisibility()` method that populates dictionary from AllColumns defaults
    - Call `InitializeColumnVisibility()` in `OnInitializedAsync`
    - Add `ToggleColumnVisibility(string columnId)` with guard for unknown IDs and minimum-1 constraint
    - Add `VisibleColumnCount()` helper
    - Add `IsColumnVisible(string columnId)` query method
    - Add `IsColumnDisabled(string columnId)` method for last-visible-column check
    - _Requirements: 1.4, 1.5, 2.1, 2.2, 2.3, 4.1, 4.2_

  - [x] 1.3 Implement dropdown open/close logic with JS interop callbacks
    - Add `ToggleColumnSelector()` async method that toggles `IsColumnSelectorOpen` and calls JS interop
    - Add `[JSInvokable] CloseColumnSelector()` method for click-outside callback
    - Add `HandleColumnSelectorKeyDown(KeyboardEventArgs e)` for Escape key handling
    - Update `DisposeAsync` to call `ColumnSelector.close()` and dispose `_columnSelectorDotNetRef`
    - _Requirements: 5.1, 5.2, 5.4_

- [x] 2. Create JavaScript interop module
  - [x] 2.1 Create columnSelector.js in wwwroot
    - Create `src/Web/wwwroot/js/columnSelector.js`
    - Implement `window.ColumnSelector.open(element, dotNetRef)` that registers a `mousedown` listener with setTimeout delay
    - Implement `window.ColumnSelector.close()` that removes the listener
    - Click-outside detection uses `element.contains(e.target)` and invokes `CloseColumnSelector` via dotNetRef
    - _Requirements: 5.1, 5.2_

  - [x] 2.2 Register the JS module in the application
    - Add `<script src="js/columnSelector.js"></script>` to the host page or `App.razor`
    - _Requirements: 5.1_

- [x] 3. Implement dropdown UI in Razor template
  - [x] 3.1 Add the Campos dropdown markup to FileRecords.razor
    - Add `<div class="column-selector">` wrapper with `@ref="ColumnSelectorRef"` and `@onkeydown`
    - Add "Campos" button with `aria-haspopup="listbox"`, `aria-expanded`, `aria-controls`, active class toggle
    - Add conditional dropdown panel with `role="listbox"` and `aria-label="Selecionar colunas visíveis"`
    - Render checkbox labels for each column in `AllColumns` with disabled state for last-visible
    - Place the dropdown in the toolbar row (between search and "Nova Ficha" button)
    - _Requirements: 1.1, 1.2, 1.3, 4.3, 5.3_

- [x] 4. Apply conditional column rendering
  - [x] 4.1 Wrap desktop table columns with IsColumnVisible checks
    - Wrap each `<th>` in `thead` with `@if (IsColumnVisible("columnId"))`
    - Wrap each `<td>` in `tbody` foreach loop with matching `@if (IsColumnVisible("columnId"))`
    - Ensure column order follows AllColumns definition order
    - _Requirements: 1.4, 3.1, 3.2, 3.3, 3.5, 3.6_

  - [x] 4.2 Wrap mobile card view fields with IsColumnVisible checks
    - Wrap card header name/type with visibility checks for "name" and "type"
    - Wrap each card-detail div with corresponding visibility check
    - Wrap card-actions div with visibility check for "actions"
    - _Requirements: 3.4, 3.5, 3.6_

- [x] 5. Add CSS styles for the column selector
  - [x] 5.1 Add column selector styles to the page stylesheet
    - Style `.column-selector` container with relative positioning
    - Style `.btn-campos` button and `.btn-campos.active` state
    - Style `.column-selector-dropdown` with absolute positioning, border, shadow, z-index
    - Style `.column-option` labels and `.column-option.disabled` appearance
    - Ensure dropdown is responsive and accessible on mobile
    - _Requirements: 1.1, 4.3, 5.3_

- [x] 6. Checkpoint - Verify core functionality
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Write property-based tests
  - [x] 7.1 Write property test for visibility-rendering consistency
    - **Property 1: Visibility-rendering consistency**
    - For any valid visibility configuration (at least 1 visible), verify IsColumnVisible returns true only for columns marked visible in the dictionary
    - Use FsCheck to generate arbitrary `Dictionary<string, bool>` states with the minimum-1 constraint
    - **Validates: Requirements 1.4, 3.1, 3.2, 3.3, 3.4**

  - [x] 7.2 Write property test for minimum visible column invariant
    - **Property 2: Minimum visible column invariant**
    - For any sequence of toggle operations, verify VisibleColumnCount never drops below 1
    - Generate arbitrary sequences of column IDs to toggle and verify invariant holds after each operation
    - **Validates: Requirements 1.5, 4.1, 4.2**

  - [x] 7.3 Write property test for column order preservation
    - **Property 3: Column order preservation**
    - For any sequence of hide/show operations, verify visible columns always appear in canonical AllColumns order
    - Generate random toggle sequences and check resulting visible columns maintain relative order
    - **Validates: Requirements 3.5**

  - [x] 7.4 Write property test for state persistence across open/close
    - **Property 4: State persistence across dropdown open/close**
    - For any visibility state, verify that calling open/close does not mutate the ColumnVisibility dictionary
    - Generate arbitrary visibility states and simulate open/close cycles
    - **Validates: Requirements 5.4**

- [x] 8. Write unit tests
  - [x] 8.1 Write xUnit unit tests for visibility state management
    - `InitializeColumnVisibility_SetsCorrectDefaults` — 6 visible, 1 hidden (Nº Disquete)
    - `ToggleColumnVisibility_HidesVisibleColumn` — unchecking a visible column hides it
    - `ToggleColumnVisibility_ShowsHiddenColumn` — checking a hidden column shows it
    - `ToggleColumnVisibility_LastVisibleColumn_NoOp` — cannot hide the last column
    - `IsColumnDisabled_LastVisibleColumn_ReturnsTrue`
    - `IsColumnDisabled_MultipleVisible_ReturnsFalse`
    - `ToggleColumnVisibility_UnknownColumnId_NoOp`
    - `VisibleColumnCount_ReturnsCorrectCount`
    - _Requirements: 1.4, 1.5, 2.1, 2.2, 4.1, 4.2_

- [x] 9. Write bUnit component tests
  - [x] 9.1 Write bUnit tests for dropdown rendering and interaction
    - Verify "Campos" button renders in toolbar
    - Verify clicking "Campos" opens dropdown with 7 checkboxes
    - Verify default state: 6 checked, Nº Disquete unchecked
    - Verify unchecking a column removes its `<th>` and `<td>` elements
    - Verify checking Nº Disquete adds the column to the table
    - Verify last visible column checkbox has `disabled` attribute
    - Verify Escape key closes dropdown
    - Verify ARIA attributes: `aria-haspopup`, `aria-expanded`, `aria-controls`, `role="listbox"`, `role="option"`
    - Verify active button styling when dropdown is open
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.2, 4.1, 4.3, 5.2, 5.3_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- No API changes are needed — this feature is entirely client-side
- The JS interop module (`columnSelector.js`) must be registered before it can be used
- FsCheck.Xunit and bUnit are already available in the `tests/Web.Tests` project

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "2.2"] },
    { "id": 2, "tasks": ["1.3", "3.1"] },
    { "id": 3, "tasks": ["4.1", "4.2", "5.1"] },
    { "id": 4, "tasks": ["7.1", "7.2", "7.3", "7.4", "8.1"] },
    { "id": 5, "tasks": ["9.1"] }
  ]
}
```
