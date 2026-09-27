namespace hacman_app_front.Services;

/// <summary>
/// Maps a gallery <c>Type.Name</c> (e.g. "Type 1"/"Book") to the frontend route segment of its
/// dedicated gallery-type page ("book"/"character"/"cosplay").
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
    /// Splits a <c>HacmanDictionary:*GroupName</c> config value into its comma-separated aliases
    /// (trimmed, blanks dropped). Lets one environment recognise several naming conventions for the
    /// same group at once -- e.g. "Type 1,Book" -- without test and prod needing to agree on which
    /// literal <c>gallery_type_enum</c> value a gallery actually uses.
    /// </summary>
    public static IReadOnlyList<string> SplitAliases(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Resolves the route segment for the given gallery type name. Returns
    /// <see cref="DefaultRouteSegment"/> when <paramref name="typeName"/> is null/empty or does not
    /// match any configured group alias.
    /// </summary>
    public static string ResolveRouteSegment(IConfiguration configuration, string? typeName, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            logger?.LogWarning("GalleryRouteResolver: null/empty type name, falling back to '{Route}'",
                DefaultRouteSegment);
            return DefaultRouteSegment;
        }

        var bookGroups = SplitAliases(configuration["HacmanDictionary:BookGroupName"] ?? "Book");
        var characterGroups = SplitAliases(configuration["HacmanDictionary:CharacterGroupName"] ?? "Character");
        var cosplayGroups = SplitAliases(configuration["HacmanDictionary:CosplayGroupName"] ?? "Cosplay");

        if (bookGroups.Any(g => string.Equals(typeName, g, StringComparison.OrdinalIgnoreCase))) return "book";
        if (characterGroups.Any(g => string.Equals(typeName, g, StringComparison.OrdinalIgnoreCase))) return "character";
        if (cosplayGroups.Any(g => string.Equals(typeName, g, StringComparison.OrdinalIgnoreCase))) return "cosplay";

        logger?.LogWarning(
            "GalleryRouteResolver: type name '{TypeName}' matched no configured group, falling back to '{Route}'",
            typeName, DefaultRouteSegment);
        return DefaultRouteSegment;
    }
}