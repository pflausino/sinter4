namespace Web.Tests;

/// <summary>
/// Unit tests for column visibility state management logic.
/// Tests the InitializeColumnVisibility, ToggleColumnVisibility, IsColumnVisible,
/// IsColumnDisabled, and VisibleColumnCount methods as defined in FileRecords.razor.cs.
///
/// Requirements: 1.4, 1.5, 2.1, 2.2, 4.1, 4.2
/// </summary>
public class ColumnVisibilityStateTests
{
    #region Test Helper — replicates the exact logic from FileRecords.razor.cs

    private record ColumnDefinition(string Id, string Label, bool DefaultVisible);

    private static readonly ColumnDefinition[] AllColumns =
    [
        new("name", "Nome", DefaultVisible: true),
        new("type", "Tipo", DefaultVisible: true),
        new("fileNumber", "Nº Arquivo", DefaultVisible: true),
        new("client", "Cliente", DefaultVisible: true),
        new("date", "Data", DefaultVisible: true),
        new("flopDiskNumber", "Nº Disquete", DefaultVisible: false),
        new("actions", "Ações", DefaultVisible: true),
    ];

    private Dictionary<string, bool> _columnVisibility = new();

    private void InitializeColumnVisibility()
    {
        _columnVisibility = AllColumns.ToDictionary(c => c.Id, c => c.DefaultVisible);
    }

    private void ToggleColumnVisibility(string columnId)
    {
        if (!_columnVisibility.ContainsKey(columnId)) return;
        if (_columnVisibility[columnId] && VisibleColumnCount() <= 1) return;
        _columnVisibility[columnId] = !_columnVisibility[columnId];
    }

    private int VisibleColumnCount() => _columnVisibility.Count(kv => kv.Value);

    private bool IsColumnVisible(string columnId) =>
        _columnVisibility.TryGetValue(columnId, out var visible) && visible;

    private bool IsColumnDisabled(string columnId) =>
        _columnVisibility.TryGetValue(columnId, out var visible) && visible && VisibleColumnCount() <= 1;

    #endregion

    [Fact]
    public void InitializeColumnVisibility_SetsCorrectDefaults()
    {
        // Arrange & Act
        InitializeColumnVisibility();

        // Assert — 6 columns visible, 1 hidden (flopDiskNumber / Nº Disquete)
        Assert.Equal(7, _columnVisibility.Count);
        Assert.Equal(6, VisibleColumnCount());

        Assert.True(_columnVisibility["name"]);
        Assert.True(_columnVisibility["type"]);
        Assert.True(_columnVisibility["fileNumber"]);
        Assert.True(_columnVisibility["client"]);
        Assert.True(_columnVisibility["date"]);
        Assert.False(_columnVisibility["flopDiskNumber"]);
        Assert.True(_columnVisibility["actions"]);
    }

    [Fact]
    public void ToggleColumnVisibility_HidesVisibleColumn()
    {
        // Arrange
        InitializeColumnVisibility();
        Assert.True(_columnVisibility["name"]);

        // Act
        ToggleColumnVisibility("name");

        // Assert
        Assert.False(_columnVisibility["name"]);
    }

    [Fact]
    public void ToggleColumnVisibility_ShowsHiddenColumn()
    {
        // Arrange
        InitializeColumnVisibility();
        Assert.False(_columnVisibility["flopDiskNumber"]);

        // Act
        ToggleColumnVisibility("flopDiskNumber");

        // Assert
        Assert.True(_columnVisibility["flopDiskNumber"]);
    }

    [Fact]
    public void ToggleColumnVisibility_LastVisibleColumn_NoOp()
    {
        // Arrange — hide all columns except one
        InitializeColumnVisibility();
        ToggleColumnVisibility("name");
        ToggleColumnVisibility("type");
        ToggleColumnVisibility("fileNumber");
        ToggleColumnVisibility("client");
        ToggleColumnVisibility("date");
        // "flopDiskNumber" is already hidden
        // Only "actions" remains visible
        Assert.Equal(1, VisibleColumnCount());
        Assert.True(_columnVisibility["actions"]);

        // Act — try to hide the last visible column
        ToggleColumnVisibility("actions");

        // Assert — column remains visible, state unchanged
        Assert.True(_columnVisibility["actions"]);
        Assert.Equal(1, VisibleColumnCount());
    }

    [Fact]
    public void IsColumnDisabled_LastVisibleColumn_ReturnsTrue()
    {
        // Arrange — hide all except "actions"
        InitializeColumnVisibility();
        ToggleColumnVisibility("name");
        ToggleColumnVisibility("type");
        ToggleColumnVisibility("fileNumber");
        ToggleColumnVisibility("client");
        ToggleColumnVisibility("date");
        Assert.Equal(1, VisibleColumnCount());

        // Act & Assert
        Assert.True(IsColumnDisabled("actions"));
    }

    [Fact]
    public void IsColumnDisabled_MultipleVisible_ReturnsFalse()
    {
        // Arrange — default state with 6 visible columns
        InitializeColumnVisibility();
        Assert.True(VisibleColumnCount() > 1);

        // Act & Assert — no column should be disabled when multiple are visible
        Assert.False(IsColumnDisabled("name"));
        Assert.False(IsColumnDisabled("type"));
        Assert.False(IsColumnDisabled("fileNumber"));
        Assert.False(IsColumnDisabled("client"));
        Assert.False(IsColumnDisabled("date"));
        Assert.False(IsColumnDisabled("actions"));
    }

    [Fact]
    public void ToggleColumnVisibility_UnknownColumnId_NoOp()
    {
        // Arrange
        InitializeColumnVisibility();
        var originalState = new Dictionary<string, bool>(_columnVisibility);

        // Act
        ToggleColumnVisibility("unknownColumn");
        ToggleColumnVisibility("");
        ToggleColumnVisibility("nonExistentId");

        // Assert — state unchanged
        Assert.Equal(originalState, _columnVisibility);
    }

    [Fact]
    public void VisibleColumnCount_ReturnsCorrectCount()
    {
        // Arrange
        InitializeColumnVisibility();

        // Assert — default has 6 visible
        Assert.Equal(6, VisibleColumnCount());

        // Act — show the hidden column
        ToggleColumnVisibility("flopDiskNumber");

        // Assert — now 7 visible
        Assert.Equal(7, VisibleColumnCount());

        // Act — hide two columns
        ToggleColumnVisibility("name");
        ToggleColumnVisibility("type");

        // Assert — now 5 visible
        Assert.Equal(5, VisibleColumnCount());
    }
}
