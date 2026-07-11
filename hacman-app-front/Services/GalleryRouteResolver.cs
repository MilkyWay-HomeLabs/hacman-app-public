namespace hacman_app_front.Services;

/// <summary>
/// Maps a gallery <c>Type.Name</c> (e.g. "Type 1"/"Type 2"/"Type 3") to the frontend
/// route segment of its dedicated gallery-type page ("book"/"character"/"cosplay").
///
/// This is the inverse of the <c>HacmanDictionary:{Book,Character,Cosplay}GroupName</c>
/// configuration that <see cref="hacman_app_front.Components.Shared.GalleryTypePage"/> uses
/// to filter by type name. It replaces the retired <c>Archive</c> catch-all page: gallery-detail
/// navigation now targets the explicit "/book"/"/character"/"/cosplay" routes directly.
/// </summary>
public static class GalleryRouteResolver
{
    /// <summary>Route segment served when a type name matches no configured group. Also the historical default.</summary>
    public const string DefaultRouteSegment = "book";

    /// <summary>
    /// Resolves the route segment for the given gallery type name. Returns
    /// <see cref="DefaultRouteSegment"/> when <paramref name="typeName"/> is null/empty or does not
    /// match any configured group name.
    /// </summary>
    public static string ResolveRouteSegment(IConfiguration configuration, string? typeName, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            logger?.LogWarning("GalleryRouteResolver: null/empty type name, falling back to '{Route}'",
                DefaultRouteSegment);
            return DefaultRouteSegment;
        }

        var bookGroup = configuration["HacmanDictionary:BookGroupName"] ?? "Book";
        var characterGroup = configuration["HacmanDictionary:CharacterGroupName"] ?? "Character";
        var cosplayGroup = configuration["HacmanDictionary:CosplayGroupName"] ?? "Cosplay";

        if (string.Equals(typeName, bookGroup, StringComparison.OrdinalIgnoreCase)) return "book";
        if (string.Equals(typeName, characterGroup, StringComparison.OrdinalIgnoreCase)) return "character";
        if (string.Equals(typeName, cosplayGroup, StringComparison.OrdinalIgnoreCase)) return "cosplay";

        logger?.LogWarning(
            "GalleryRouteResolver: type name '{TypeName}' matched no configured group, falling back to '{Route}'",
            typeName, DefaultRouteSegment);
        return DefaultRouteSegment;
    }
}