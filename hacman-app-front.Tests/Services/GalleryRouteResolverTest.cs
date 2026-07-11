using hacman_app_front.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class GalleryRouteResolverTest
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HacmanDictionary:BookGroupName"] = "Type 1",
            ["HacmanDictionary:CharacterGroupName"] = "Type 2",
            ["HacmanDictionary:CosplayGroupName"] = "Type 3"
        }).Build();

    [Theory]
    [InlineData("Type 1", "book")]
    [InlineData("Type 2", "character")]
    [InlineData("Type 3", "cosplay")]
    public void ResolveRouteSegment_MapsConfiguredTypeName_ToRouteSegment(string typeName, string expected)
    {
        var route = GalleryRouteResolver.ResolveRouteSegment(BuildConfig(), typeName);

        Assert.Equal(expected, route);
    }

    [Theory]
    [InlineData("type 1", "book")]
    [InlineData("TYPE 2", "character")]
    public void ResolveRouteSegment_IsCaseInsensitive(string typeName, string expected)
    {
        var route = GalleryRouteResolver.ResolveRouteSegment(BuildConfig(), typeName);

        Assert.Equal(expected, route);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Type 99")]
    public void ResolveRouteSegment_FallsBackToDefault_WhenUnmappedOrEmpty(string? typeName)
    {
        var route = GalleryRouteResolver.ResolveRouteSegment(BuildConfig(), typeName);

        Assert.Equal(GalleryRouteResolver.DefaultRouteSegment, route);
        Assert.Equal("book", route);
    }

    [Fact]
    public void ResolveRouteSegment_UsesHardcodedDefaults_WhenConfigMissing()
    {
        var emptyConfig = new ConfigurationBuilder().Build();

        Assert.Equal("book", GalleryRouteResolver.ResolveRouteSegment(emptyConfig, "Book"));
        Assert.Equal("character", GalleryRouteResolver.ResolveRouteSegment(emptyConfig, "Character"));
        Assert.Equal("cosplay", GalleryRouteResolver.ResolveRouteSegment(emptyConfig, "Cosplay"));
    }
}