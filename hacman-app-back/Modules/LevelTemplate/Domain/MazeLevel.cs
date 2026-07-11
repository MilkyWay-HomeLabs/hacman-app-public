namespace hacman_app_back.Modules.LevelTemplate.Domain;

public class MazeLevel
{
    public int Id { get; set; }

    public Guid GalleryId { get; set; }

    public Difficulty Difficulty { get; set; }
    public short Width { get; set; }
    public short Height { get; set; }
    public int? RandomSeed { get; set; }
    public PlacementSurface PlacementAllowedOn { get; set; } = PlacementSurface.road;
    public bool PlacementAvoidAdjWalls { get; set; }
    public short[][] Cells { get; set; } = default!;
    public short PlayerStartX { get; set; }
    public short PlayerStartY { get; set; }
    public MazeDirection PlayerDirection { get; set; } = MazeDirection.right;
    public MazeEntities Entities { get; set; } = new();
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation

    /// <summary>
    /// Checks the maze's shape/placement invariants: Cells must have Height rows of Width
    /// columns each, and the player start must land inside those bounds. Returns human-readable
    /// violations instead of throwing, so callers can decide whether to reject or just log/warn.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var violations = new List<string>();

        if (Width <= 0) violations.Add($"Width must be positive (was {Width}).");
        if (Height <= 0) violations.Add($"Height must be positive (was {Height}).");

        if (Cells == null)
        {
            violations.Add("Cells must not be null.");
        }
        else
        {
            if (Cells.Length != Height)
                violations.Add($"Cells has {Cells.Length} row(s) but Height is {Height}.");

            for (var y = 0; y < Cells.Length; y++)
            {
                var rowLength = Cells[y].Length;
                if (rowLength != Width)
                    violations.Add($"Cells row {y} has {rowLength} column(s) but Width is {Width}.");
            }
        }

        if (PlayerStartX < 0 || PlayerStartX >= Width)
            violations.Add($"PlayerStartX ({PlayerStartX}) is out of bounds for Width {Width}.");

        if (PlayerStartY < 0 || PlayerStartY >= Height)
            violations.Add($"PlayerStartY ({PlayerStartY}) is out of bounds for Height {Height}.");

        return violations;
    }
}