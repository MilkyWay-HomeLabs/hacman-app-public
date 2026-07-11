namespace hacman_app_back.Modules.LevelTemplate.Domain;

public class EntityDefinition
{
    public virtual int Id { get; set; }
    public virtual String Category { get; set; } = default!; // enum entity_category
    public virtual String TypeKey { get; set; } = default!;
    public virtual String StyleKey { get; set; } = default!;
    public virtual String? Effect { get; set; } = default!;
    public virtual bool PickupByPlayer { get; set; } = true;
    public virtual bool PickupByEnemy { get; set; } = false;
    public virtual bool DestroyOnTouch { get; set; } = true;
}