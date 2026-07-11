using hacman_app_back.Modules.LevelTemplate.Domain;
using Xunit;

namespace hacman_app_back.Tests.Modules.LevelTemplate.Domain;

public class MazeLevelTest
{
    private static MazeLevel ValidLevel() => new()
    {
        Width = 3,
        Height = 2,
        Cells = new[]
        {
            new short[] { 0, 0, 0 },
            new short[] { 0, 0, 0 }
        },
        PlayerStartX = 1,
        PlayerStartY = 1
    };

    [Fact]
    public void Validate_ReturnsNoViolations_WhenLevelIsWellFormed()
    {
        var level = ValidLevel();

        var violations = level.Validate();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReturnsViolation_WhenCellsRowCountDoesNotMatchHeight()
    {
        var level = ValidLevel();
        level.Cells = new[] { new short[] { 0, 0, 0 } }; // 1 row instead of 2

        var violations = level.Validate();

        Assert.Contains(violations, v => v.Contains("row(s)"));
    }

    [Fact]
    public void Validate_ReturnsViolation_WhenCellsRowLengthDoesNotMatchWidth()
    {
        var level = ValidLevel();
        level.Cells[0] = new short[] { 0, 0 }; // 2 columns instead of 3

        var violations = level.Validate();

        Assert.Contains(violations, v => v.Contains("column(s)"));
    }

    [Fact]
    public void Validate_ReturnsViolation_WhenCellsIsNull()
    {
        var level = ValidLevel();
        level.Cells = null!;

        var violations = level.Validate();

        Assert.Contains(violations, v => v.Contains("Cells must not be null"));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    public void Validate_ReturnsViolation_WhenPlayerStartXIsOutOfBounds(short x, short y)
    {
        var level = ValidLevel();
        level.PlayerStartX = x;
        level.PlayerStartY = y;

        var violations = level.Validate();

        Assert.Contains(violations, v => v.Contains("PlayerStartX"));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void Validate_ReturnsViolation_WhenPlayerStartYIsOutOfBounds(short x, short y)
    {
        var level = ValidLevel();
        level.PlayerStartX = x;
        level.PlayerStartY = y;

        var violations = level.Validate();

        Assert.Contains(violations, v => v.Contains("PlayerStartY"));
    }

    [Fact]
    public void Validate_ReturnsMultipleViolations_WhenSeveralInvariantsAreBroken()
    {
        var level = new MazeLevel
        {
            Width = 0,
            Height = 0,
            Cells = null!,
            PlayerStartX = -1,
            PlayerStartY = -1
        };

        var violations = level.Validate();

        Assert.True(violations.Count >= 4);
    }
}