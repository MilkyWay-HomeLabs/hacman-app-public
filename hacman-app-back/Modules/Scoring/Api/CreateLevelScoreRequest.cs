namespace hacman_app_back.Modules.Scoring.Api;

public class CreateLevelScoreRequest
{
    public required Guid GalleryId { get; set; }
    public required string Difficulty { get; set; }
    public required long TimeMs { get; set; }
}