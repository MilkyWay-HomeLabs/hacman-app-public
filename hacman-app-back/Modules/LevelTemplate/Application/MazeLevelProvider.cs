using hacman_app_back.Modules.LevelTemplate.Api;
using hacman_app_back.Modules.LevelTemplate.Domain;
using hacman_app_back.Modules.LevelTemplate.Infrastructure;

namespace hacman_app_back.Modules.LevelTemplate.Application;

public class MazeLevelProvider : IMazeLevelProvider
{
    private readonly IMazeLevelRepository _mazeLevelRepository;
    private readonly ILogger<MazeLevelProvider> _logger;

    public MazeLevelProvider(IMazeLevelRepository mazeLevelRepository, ILogger<MazeLevelProvider> logger)
    {
        _mazeLevelRepository = mazeLevelRepository ?? throw new ArgumentNullException(nameof(mazeLevelRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<MazeLevelDto>> GetMazeLevelsByGalleryId(Guid? galleryId = null)
    {
        if (galleryId == null)
        {
            return new List<MazeLevelDto>();
        }

        var scores = await _mazeLevelRepository.GetMazeLevelsByGalleryId(galleryId);
        return scores.Select(MapToDto).ToList();
    }

    private static MazeLevelDto MapToDto(MazeLevel level)
    {
        return new MazeLevelDto
        {
            Id = level.Id,
            GalleryId = level.GalleryId,
            Difficulty = level.Difficulty.ToString(),
            Width = level.Width,
            Height = level.Height,
            RandomSeed = level.RandomSeed,
            PlacementAllowedOn = level.PlacementAllowedOn.ToString(),
            PlacementAvoidAdjWalls = level.PlacementAvoidAdjWalls,
            Cells = level.Cells,
            PlayerStartX = level.PlayerStartX,
            PlayerStartY = level.PlayerStartY,
            PlayerDirection = level.PlayerDirection.ToString(),
            Dots = level.Entities.Dots,
            Enemies = level.Entities.Enemies,
            Buffs = level.Entities.Buffs,
            Debuffs = level.Entities.Debuffs,
            Notes = level.Notes,
            CreatedAt = level.CreatedAt,
            UpdatedAt = level.UpdatedAt
        };
    }
}