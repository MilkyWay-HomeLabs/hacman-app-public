using hacman_app_back.Modules.LevelTemplate.Domain;
using Npgsql;

namespace hacman_app_back.Modules.LevelTemplate.Infrastructure;

/// <summary>
/// ADO.NET repository for maze level records.
/// </summary>
public class MazeLevelRepository : IMazeLevelRepository

{
    private readonly string _connectionString;
    private readonly ILogger<MazeLevelRepository> _logger;

    public MazeLevelRepository(string connectionString, ILogger<MazeLevelRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MazeLevel>> GetMazeLevelsByGalleryId(Guid? galleryId)
    {
        _logger.LogInformation("GetMazeLevelsByGalleryId: galleryId={GalleryId}", galleryId != Guid.Empty);

        var sql = @"
            SELECT ml.id as id, ml.gallery_id as gallery_id, 
                   ml.difficulty as difficulty, 
                   ml.width as width, ml.height as height,
                   ml.random_seed as random_seed, ml.placement_allowed_on as placement_allowed_on,
                   ml.placement_avoid_adj_walls as placement_avoid_adj_walls,
                   ml.cells as cells,
                   ml.player_start_x as player_start_x, ml.player_start_y as player_start_y,
                   ml.player_direction as player_direction,
                   ml.entities as entities,
                   ml.notes as notes,
                   ml.created_at as created_at, ml.updated_at as updated_at
            FROM maze_levels ml
            WHERE ml.gallery_id = @galleryId";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        //
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@galleryId", galleryId ?? Guid.Empty);

        var result = new List<MazeLevel>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapMazeLevel(reader));
        return result;
    }

    private MazeLevel MapMazeLevel(NpgsqlDataReader reader)
    {
        PlacementSurface placementAllowedOn = reader.IsDBNull(reader.GetOrdinal("placement_allowed_on"))
            ? PlacementSurface.road
            : Enum.Parse<PlacementSurface>(reader.GetString(reader.GetOrdinal("placement_allowed_on")));

        var level = new MazeLevel
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            GalleryId = reader.GetGuid(reader.GetOrdinal("gallery_id")),
            Difficulty = reader.IsDBNull(reader.GetOrdinal("difficulty"))
                ? Difficulty.MEDIUM
                : Enum.Parse<Difficulty>(reader.GetString(reader.GetOrdinal("difficulty"))),
            Width = reader.GetInt16(reader.GetOrdinal("width")),
            Height = reader.GetInt16(reader.GetOrdinal("height")),
            RandomSeed = reader.IsDBNull(reader.GetOrdinal("random_seed"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("random_seed")),
            PlacementAllowedOn = placementAllowedOn,
            PlacementAvoidAdjWalls = reader.GetBoolean(reader.GetOrdinal("placement_avoid_adj_walls")),
            Cells = reader.GetFieldValue<short[][]>(reader.GetOrdinal("cells")),
            PlayerStartX = reader.GetInt16(reader.GetOrdinal("player_start_x")),
            PlayerStartY = reader.GetInt16(reader.GetOrdinal("player_start_y")),
            PlayerDirection = reader.IsDBNull(reader.GetOrdinal("player_direction"))
                ? MazeDirection.right
                : Enum.Parse<MazeDirection>(reader.GetString(reader.GetOrdinal("player_direction"))),
            Entities = reader.GetFieldValue<MazeEntities>(reader.GetOrdinal("entities")),
            Notes = reader.IsDBNull(reader.GetOrdinal("notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("notes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        };

        var violations = level.Validate();
        if (violations.Count > 0)
        {
            _logger.LogWarning(
                "MazeLevel {MazeLevelId} failed invariant validation on read: {Violations}",
                level.Id, string.Join("; ", violations));
        }

        return level;
    }
}