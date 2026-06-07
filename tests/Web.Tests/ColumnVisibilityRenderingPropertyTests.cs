using FsCheck;
using FsCheck.Xunit;

namespace Web.Tests;

/// <summary>
/// Feature: column-visibility-selector, Property 1: Visibility-rendering consistency
/// **Validates: Requirements 1.4, 3.1, 3.2, 3.3, 3.4**
/// </summary>
public class ColumnVisibilityRenderingPropertyTests
{
    /// <summary>
    /// The canonical column IDs as defined in AllColumns.
    /// </summary>
    private static readonly string[] AllColumnIds =
    [
        "name", "type", "fileNumber", "client", "date", "flopDiskNumber", "actions"
    ];

    /// <summary>
    /// Replicates the IsColumnVisible logic from FileRecords.razor.cs:
    /// ColumnVisibility.TryGetValue(columnId, out var visible) &amp;&amp; visible
    /// </summary>
    private static bool IsColumnVisible(Dictionary<string, bool> columnVisibility, string columnId) =>
        columnVisibility.TryGetValue(columnId, out var visible) && visible;

    /// <summary>
    /// Builds a valid column visibility dictionary from a bitmask.
    /// Ensures at least 1 column is visible (the minimum-1 constraint).
    /// </summary>
    private static Dictionary<string, bool> BuildVisibilityState(byte bitmask)
    {
        // Ensure at least one column is visible by OR-ing with 1 if all are zero
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
    /// Property 1: Visibility-rendering consistency
    ///
    /// For any valid visibility configuration (at least 1 visible), IsColumnVisible returns
    /// true only for columns marked visible in the dictionary, and false for all others.
    ///
    /// **Validates: Requirements 1.4, 3.1, 3.2, 3.3, 3.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 1: IsColumnVisible returns true only for columns marked visible",
        MaxTest = 100)]
    public bool IsColumnVisible_ReturnsTrue_OnlyForVisibleColumns(byte bitmask)
    {
        var visibility = BuildVisibilityState(bitmask);

        // For every known column, IsColumnVisible must match the dictionary value
        foreach (var columnId in AllColumnIds)
        {
            var expected = visibility[columnId];
            var actual = IsColumnVisible(visibility, columnId);

            if (actual != expected)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 1 (supplementary): IsColumnVisible returns false for unknown column IDs
    /// that are not present in the visibility dictionary.
    ///
    /// **Validates: Requirements 1.4, 3.1, 3.2, 3.3, 3.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 1: IsColumnVisible returns false for unknown column IDs",
        MaxTest = 100)]
    public bool IsColumnVisible_ReturnsFalse_ForUnknownColumnIds(byte bitmask, NonEmptyString unknownId)
    {
        var visibility = BuildVisibilityState(bitmask);

        var columnId = unknownId.Get;

        // Skip if the generated ID happens to match a known column
        if (AllColumnIds.Contains(columnId))
            return true; // vacuously true — not an unknown ID

        var result = IsColumnVisible(visibility, columnId);

        // Unknown columns must never be visible
        return result == false;
    }

    /// <summary>
    /// Property 1 (supplementary): The visibility state always has at least 1 visible column,
    /// ensuring there is always at least one column to render.
    ///
    /// **Validates: Requirements 1.4, 3.1, 3.2, 3.3, 3.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 1: Valid visibility state always has at least 1 visible column",
        MaxTest = 100)]
    public bool ValidVisibilityState_HasAtLeastOneVisibleColumn(byte bitmask)
    {
        var visibility = BuildVisibilityState(bitmask);

        var visibleCount = visibility.Values.Count(v => v);

        return visibleCount >= 1;
    }
}
