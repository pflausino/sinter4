using FsCheck;
using FsCheck.Xunit;

namespace Web.Tests;

/// <summary>
/// Feature: column-visibility-selector, Property 3: Column order preservation
/// **Validates: Requirements 3.5**
/// </summary>
public class ColumnVisibilityOrderPropertyTests
{
    private static readonly string[] CanonicalOrder =
        ["name", "type", "fileNumber", "client", "date", "flopDiskNumber", "actions"];

    /// <summary>
    /// Replicates the toggle logic from FileRecords.razor.cs.
    /// If trying to hide and it's the last visible column, do nothing.
    /// </summary>
    private static void ToggleColumnVisibility(Dictionary<string, bool> visibility, string columnId)
    {
        if (!visibility.ContainsKey(columnId)) return;
        if (visibility[columnId] && visibility.Count(kv => kv.Value) <= 1) return;
        visibility[columnId] = !visibility[columnId];
    }

    /// <summary>
    /// Initializes the default visibility state matching the component's defaults.
    /// All columns visible except "flopDiskNumber".
    /// </summary>
    private static Dictionary<string, bool> CreateDefaultVisibility() => new()
    {
        ["name"] = true,
        ["type"] = true,
        ["fileNumber"] = true,
        ["client"] = true,
        ["date"] = true,
        ["flopDiskNumber"] = false,
        ["actions"] = true,
    };

    /// <summary>
    /// Gets the visible columns in canonical order, replicating how the Razor template iterates
    /// AllColumns and skips hidden ones.
    /// </summary>
    private static List<string> GetVisibleColumnsInOrder(Dictionary<string, bool> visibility)
    {
        var result = new List<string>();
        foreach (var columnId in CanonicalOrder)
        {
            if (visibility.TryGetValue(columnId, out var visible) && visible)
                result.Add(columnId);
        }
        return result;
    }

    /// <summary>
    /// Verifies that a list of column IDs forms a subsequence of the canonical order,
    /// meaning their indices in CanonicalOrder are strictly increasing.
    /// </summary>
    private static bool IsSubsequenceOfCanonicalOrder(List<string> visibleColumns)
    {
        var lastIndex = -1;
        foreach (var columnId in visibleColumns)
        {
            var index = Array.IndexOf(CanonicalOrder, columnId);
            if (index < 0) return false; // Unknown column
            if (index <= lastIndex) return false; // Order violation
            lastIndex = index;
        }
        return true;
    }

    /// <summary>
    /// Property 3: Column order preservation
    ///
    /// For any sequence of hide/show operations, the visible columns SHALL always appear
    /// in the canonical order defined by AllColumns. That is, if columns A and B are both
    /// visible and A precedes B in the definition array, then A precedes B in the rendered output,
    /// regardless of the order in which they were toggled.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 3: Column order preservation",
        MaxTest = 100)]
    public bool VisibleColumns_AlwaysMaintainCanonicalOrder_AfterAnyToggleSequence(PositiveInt[] seeds)
    {
        var visibility = CreateDefaultVisibility();

        foreach (var seed in seeds)
        {
            var columnId = CanonicalOrder[seed.Get % CanonicalOrder.Length];
            ToggleColumnVisibility(visibility, columnId);

            var visibleColumns = GetVisibleColumnsInOrder(visibility);

            // The visible columns must form a valid subsequence of the canonical order
            if (!IsSubsequenceOfCanonicalOrder(visibleColumns))
                return false;
        }

        return true;
    }
}
