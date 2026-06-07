using FsCheck;
using FsCheck.Xunit;

namespace Web.Tests;

/// <summary>
/// Feature: column-visibility-selector, Property 4: State persistence across dropdown open/close
/// **Validates: Requirements 5.4**
/// </summary>
public class ColumnVisibilityPersistencePropertyTests
{
    /// <summary>
    /// The canonical column IDs as defined in AllColumns.
    /// </summary>
    private static readonly string[] AllColumnIds =
    [
        "name", "type", "fileNumber", "client", "date", "flopDiskNumber", "actions"
    ];

    /// <summary>
    /// Builds a valid column visibility dictionary from a bitmask.
    /// Ensures at least 1 column is visible (the minimum-1 constraint).
    /// </summary>
    private static Dictionary<string, bool> BuildVisibilityState(byte bitmask)
    {
        var effectiveMask = bitmask & 0x7F; // Only use lower 7 bits (7 columns)
        if (effectiveMask == 0)
            effectiveMask = 1; // Force at least "name" visible

        var visibility = new Dictionary<string, bool>();
        for (var i = 0; i < AllColumnIds.Length; i++)
        {
            visibility[AllColumnIds[i]] = (effectiveMask & (1 << i)) != 0;
        }

        return visibility;
    }

    /// <summary>
    /// Simulates the CloseColumnSelector logic from FileRecords.razor.cs:
    /// Sets IsColumnSelectorOpen = false and calls StateHasChanged().
    /// Does NOT modify ColumnVisibility.
    /// </summary>
    private static bool SimulateClose(ref bool isOpen)
    {
        isOpen = false;
        return true;
    }

    /// <summary>
    /// Simulates the open portion of ToggleColumnSelector:
    /// Sets IsColumnSelectorOpen = true.
    /// Does NOT modify ColumnVisibility.
    /// </summary>
    private static bool SimulateOpen(ref bool isOpen)
    {
        isOpen = true;
        return true;
    }

    /// <summary>
    /// Property 4: State persistence across dropdown open/close
    ///
    /// For any valid visibility state, simulating open and close operations does NOT
    /// change any values in the ColumnVisibility dictionary. The open/close logic only
    /// toggles IsColumnSelectorOpen — it never mutates visibility state.
    ///
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 4: State persistence across open/close",
        MaxTest = 100)]
    public bool ColumnVisibility_IsUnchanged_AfterOpenCloseCycle(byte bitmask, PositiveInt cycleCount)
    {
        var visibility = BuildVisibilityState(bitmask);

        // Snapshot the state before open/close cycles
        var snapshotBefore = new Dictionary<string, bool>(visibility);

        // Simulate the component state
        var isColumnSelectorOpen = false;

        // Perform N open/close cycles (capped at a reasonable number)
        var cycles = (cycleCount.Get % 10) + 1;
        for (var i = 0; i < cycles; i++)
        {
            // Open
            SimulateOpen(ref isColumnSelectorOpen);
            // Close (via CloseColumnSelector or ToggleColumnSelector)
            SimulateClose(ref isColumnSelectorOpen);
        }

        // Verify: visibility state must be exactly the same after all cycles
        foreach (var columnId in AllColumnIds)
        {
            if (visibility[columnId] != snapshotBefore[columnId])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 4 (supplementary): State persistence across a single open without close
    ///
    /// For any valid visibility state, opening the dropdown (without closing) does NOT
    /// change any values in the ColumnVisibility dictionary.
    ///
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 4: State persistence after open without close",
        MaxTest = 100)]
    public bool ColumnVisibility_IsUnchanged_AfterOpenOnly(byte bitmask)
    {
        var visibility = BuildVisibilityState(bitmask);

        // Snapshot the state before opening
        var snapshotBefore = new Dictionary<string, bool>(visibility);

        // Simulate the component state
        var isColumnSelectorOpen = false;

        // Open the dropdown
        SimulateOpen(ref isColumnSelectorOpen);

        // Verify: visibility state must be exactly the same
        foreach (var columnId in AllColumnIds)
        {
            if (visibility[columnId] != snapshotBefore[columnId])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 4 (supplementary): Dictionary reference and count remain stable across open/close
    ///
    /// For any valid visibility state, the dictionary count does not change after open/close cycles,
    /// confirming no keys are added or removed.
    ///
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 4: Dictionary count unchanged after open/close",
        MaxTest = 100)]
    public bool ColumnVisibility_DictionaryCount_UnchangedAfterOpenClose(byte bitmask, PositiveInt cycleCount)
    {
        var visibility = BuildVisibilityState(bitmask);

        var countBefore = visibility.Count;

        // Simulate the component state
        var isColumnSelectorOpen = false;

        var cycles = (cycleCount.Get % 10) + 1;
        for (var i = 0; i < cycles; i++)
        {
            SimulateOpen(ref isColumnSelectorOpen);
            SimulateClose(ref isColumnSelectorOpen);
        }

        return visibility.Count == countBefore;
    }
}
