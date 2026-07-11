using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public sealed class UserGalleriesState
{
    public IReadOnlyList<UserGallery> All { get; private set; } = Array.Empty<UserGallery>();

    public int UnlockedCount { get; private set; }

    public int PageSize { get; set; } = 10;

    public int TotalPages
        => All.Count == 0 ? 1 : (int)Math.Ceiling(All.Count / (double)PageSize);

    // Adds missing API used by components. If data loading is handled elsewhere,
    // this can remain a no-op.
    public void EnsureLoaded()
    {
        // no-op
    }

    public event Action? OnChange;

    public void Set(IEnumerable<UserGallery>? galleries)
    {
        var sorted = (galleries ?? Array.Empty<UserGallery>())
            .Where(g => g != null)
            .OrderBy(g => g.Gallery?.Id)
            .ToList();

        All = sorted;
        UnlockedCount = sorted.Count(g => g.IsUnlocked);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public IReadOnlyList<UserGallery> GetPage(int pageNumber)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageNumber > TotalPages) pageNumber = TotalPages;

        return All
            .Skip((pageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}