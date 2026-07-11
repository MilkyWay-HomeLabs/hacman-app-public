namespace hacman_app_back.Modules.Identity.Domain;

public class User
{
    public virtual Guid Id { get; set; }
    public virtual string? ExternalAuthId { get; set; } = string.Empty;
    public virtual string? Username { get; set; }
    public virtual string? Email { get; set; }
    public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public virtual DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}