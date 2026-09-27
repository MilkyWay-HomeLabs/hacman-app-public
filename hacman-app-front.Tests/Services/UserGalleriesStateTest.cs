using hacman_app_front.Domain;
using hacman_app_front.Services;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class UserGalleriesStateTest
{
    private static UserGallery CreateUserGallery(Guid? galleryId = null, bool isUnlocked = false)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsUnlocked = isUnlocked,
            Gallery = new Gallery { Id = galleryId ?? Guid.NewGuid() }
        };

    [Fact]
    public void All_IsEmpty_BeforeAnySet()
    {
        var state = new UserGalleriesState();

        Assert.Empty(state.All);
        Assert.Equal(0, state.UnlockedCount);
    }

    [Fact]
    public void Set_WithNull_ResultsInEmptyState()
    {
        var state = new UserGalleriesState();
        state.Set([CreateUserGallery()]);

        state.Set(null);

        Assert.Empty(state.All);
        Assert.Equal(0, state.UnlockedCount);
    }

    [Fact]
    public void Set_SortsGalleriesByGalleryId()
    {
        var state = new UserGalleriesState();
        var idA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
        var idB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");
        var idC = Guid.Parse("cccccccc-0000-0000-0000-000000000000");

        state.Set([
            CreateUserGallery(idC),
            CreateUserGallery(idA),
            CreateUserGallery(idB)
        ]);

        Assert.Equal([idA, idB, idC], state.All.Select(g => g.Gallery.Id));
    }

    [Fact]
    public void Set_CountsUnlockedGalleries()
    {
        var state = new UserGalleriesState();

        state.Set([
            CreateUserGallery(isUnlocked: true),
            CreateUserGallery(isUnlocked: false),
            CreateUserGallery(isUnlocked: true)
        ]);

        Assert.Equal(2, state.UnlockedCount);
        Assert.Equal(3, state.All.Count);
    }

    [Fact]
    public void Set_RaisesOnChange()
    {
        var state = new UserGalleriesState();
        var notified = false;
        state.OnChange += () => notified = true;

        state.Set([CreateUserGallery()]);

        Assert.True(notified);
    }

    [Fact]
    public void TotalPages_IsOne_WhenEmpty()
    {
        var state = new UserGalleriesState();

        Assert.Equal(1, state.TotalPages);
    }

    [Fact]
    public void TotalPages_RoundsUpToFullPage()
    {
        var state = new UserGalleriesState { PageSize = 10 };
        state.Set(Enumerable.Range(0, 11).Select(_ => CreateUserGallery()));

        Assert.Equal(2, state.TotalPages);
    }

    [Fact]
    public void GetPage_ReturnsRequestedSlice()
    {
        var state = new UserGalleriesState { PageSize = 2 };
        var ids = Enumerable.Range(0, 5)
            .Select(i => Guid.Parse($"{i}{i}{i}{i}{i}{i}{i}{i}-0000-0000-0000-000000000000"))
            .ToList();
        state.Set(ids.Select(id => CreateUserGallery(id)));

        var page2 = state.GetPage(2);

        Assert.Equal(2, page2.Count);
        Assert.Equal([ids[2], ids[3]], page2.Select(g => g.Gallery.Id));
    }

    [Fact]
    public void GetPage_ClampsPageNumberBelowOne_ToFirstPage()
    {
        var state = new UserGalleriesState { PageSize = 2 };
        state.Set(Enumerable.Range(0, 4).Select(_ => CreateUserGallery()));

        var page = state.GetPage(0);

        Assert.Equal(state.GetPage(1), page);
    }

    [Fact]
    public void GetPage_ClampsPageNumberAboveTotal_ToLastPage()
    {
        var state = new UserGalleriesState { PageSize = 2 };
        state.Set(Enumerable.Range(0, 5).Select(_ => CreateUserGallery()));

        var page = state.GetPage(99);

        Assert.Single(page); // last page holds the 5th item only
        Assert.Equal(state.GetPage(state.TotalPages), page);
    }
}