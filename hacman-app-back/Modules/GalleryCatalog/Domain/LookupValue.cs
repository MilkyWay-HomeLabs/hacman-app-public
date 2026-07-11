namespace hacman_app_back.Modules.GalleryCatalog.Domain;

public class LookupValue
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual string Name { get; set; } = string.Empty;
    public virtual string? Description { get; set; }
}