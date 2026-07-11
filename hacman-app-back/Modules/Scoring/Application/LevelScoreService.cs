using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.LevelTemplate.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.Modules.Scoring.Api;
using hacman_app_back.Modules.Scoring.Domain;
using hacman_app_back.Modules.Scoring.Infrastructure;
using hacman_app_back.SharedKernel;
using Prometheus;

namespace hacman_app_back.Modules.Scoring.Application;

public class LevelScoreService : ILevelScoreService
{
    /// <summary>
    /// Counts level scores that were successfully persisted (post-commit), labelled by difficulty.
    /// Complements the HTTP-level metrics from <c>UseHttpMetrics</c> with a domain-level signal for
    /// the app's core write operation. Static per the prometheus-net idiom (one collector per process).
    /// </summary>
    public static readonly Counter ScoresSubmitted = Metrics.CreateCounter(
        "hacman_scores_submitted_total",
        "Total number of level scores successfully submitted, labelled by difficulty.",
        new CounterConfiguration { LabelNames = new[] { "difficulty" } });

    private readonly ILevelScoreRepository _levelScoreRepository;
    private readonly IUserGalleryRepository _userGalleryRepository;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;
    private readonly ILogger<LevelScoreService> _logger;

    public LevelScoreService(
        ILevelScoreRepository levelScoreRepository,
        IUserGalleryRepository userGalleryRepository,
        Func<IUnitOfWork> unitOfWorkFactory,
        ILogger<LevelScoreService> logger)
    {
        _levelScoreRepository = levelScoreRepository ?? throw new ArgumentNullException(nameof(levelScoreRepository));
        _userGalleryRepository =
            userGalleryRepository ?? throw new ArgumentNullException(nameof(userGalleryRepository));
        _unitOfWorkFactory = unitOfWorkFactory ?? throw new ArgumentNullException(nameof(unitOfWorkFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ScoreEntryDto>> GetScores(Guid galleryId, string? difficulty, int pageNumber,
        int pageSize)
    {
        _logger.LogInformation(
            "[LevelScoreService] GetScores called: galleryId={GalleryId}, difficulty={Difficulty}, pageNumber={PageNumber}, pageSize={PageSize}",
            galleryId, difficulty ?? "null", pageNumber, pageSize);
        Console.WriteLine(
            $"[LevelScoreService] GetScores called: galleryId={galleryId}, difficulty={difficulty ?? "null"}, pageNumber={pageNumber}, pageSize={pageSize}");

        var scores = await _levelScoreRepository.GetScoresAsync(galleryId, difficulty, pageNumber, pageSize);
        var result = scores.Select(MapToDto).ToList();

        _logger.LogInformation("[LevelScoreService] GetScores completed: {Count} results", result.Count);
        Console.WriteLine($"[LevelScoreService] GetScores completed: {result.Count} results");

        return result;
    }

    public async Task<IEnumerable<ScoreEntryDto>> GetScoresByUserId(Guid userId, Guid? galleryId = null,
        string? difficulty = null)
    {
        var scores = await _levelScoreRepository.GetScoresByUserIdAsync(userId, galleryId, difficulty);
        return scores.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ScoreEntryDto> CreateScore(Guid userId, string username, Guid galleryId, string difficultyRaw,
        long timeMs)
    {
        if (!Enum.TryParse<Difficulty>(difficultyRaw, ignoreCase: true, out var difficulty))
            throw new ArgumentException($"Invalid difficulty: '{difficultyRaw}'.", nameof(difficultyRaw));

        _logger.LogInformation(
            "CreateScore: userId={UserId}, galleryId={GalleryId}, difficulty={Difficulty}, timeMs={TimeMs}",
            userId, galleryId, difficulty, timeMs);

        var achievedAt = DateTime.UtcNow;
        var levelScore = new LevelScore
        {
            Id = Guid.NewGuid(),
            User = new User { Id = userId, Username = username },
            Gallery = new Gallery { Id = galleryId },
            Difficulty = difficulty,
            TimeMs = timeMs,
            TimeAt = achievedAt
        };

        await using var unitOfWork = _unitOfWorkFactory();
        await unitOfWork.BeginAsync();

        try
        {
            var created =
                await _levelScoreRepository.CreateAsync(levelScore, unitOfWork.Connection, unitOfWork.Transaction);
            await _userGalleryRepository.UpdateBestTimeIfBetterAsync(
                userId, galleryId, timeMs, achievedAt, unitOfWork.Connection, unitOfWork.Transaction);

            await unitOfWork.CommitAsync();

            ScoresSubmitted.WithLabels(difficulty.ToString()).Inc();

            _logger.LogInformation("CreateScore: created scoreId={ScoreId}", created.Id);
            return MapToDto(created);
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    private static ScoreEntryDto MapToDto(LevelScore score)
    {
        return new ScoreEntryDto
        {
            Username = score.User.Username ?? string.Empty,
            TimeMs = score.TimeMs ?? 0,
            AchievedAt = score.TimeAt ?? DateTime.UtcNow,
            Difficulty = score.Difficulty.ToString()
        };
    }
}