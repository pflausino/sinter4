using FsCheck;
using FsCheck.Xunit;

namespace Web.Tests;

/// <summary>
/// Feature: column-visibility-selector, Property 2: Minimum visible column invariant
/// **Validates: Requirements 1.5, 4.1, 4.2**
/// </summary>
public class ColumnVisibilityMinimumPropertyTests
{
    private static readonly string[] ColumnIds =
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
    /// Property 2: Minimum visible column invariant
    ///
    /// For any sequence of toggle operations applied starting from the default visibility state,
    /// the number of visible columns SHALL always remain >= 1 after each toggle operation.
    ///
    /// **Validates: Requirements 1.5, 4.1, 4.2**
    /// </summary>
    [Property(
        DisplayName = "Feature: column-visibility-selector, Property 2: Minimum visible column invariant",
        MaxTest = 100)]
    public bool VisibleColumnCount_NeverDropsBelowOne_ForAnyToggleSequence(PositiveInt[] seeds)
    {
        var visibility = CreateDefaultVisibility();

        foreach (var seed in seeds)
        {
            var columnId = ColumnIds[seed.Get % ColumnIds.Length];
            ToggleColumnVisibility(visibility, columnId);

            var visibleCount = visibility.Count(kv => kv.Value);
            if (visibleCount < 1)
                return false;
        }

        return true;
    }
}
