using hacman_app_front.Domain;
using Xunit;

namespace hacman_app_front.Tests.Components;

/// <summary>Trivial DTOs whose optional fields aren't touched by any deserialization test.</summary>
public class DomainSmokeTest
{
    [Fact]
    public void LookupValue_DescriptionIsSettable()
    {
        var value = new LookupValue { Name = "Book", Description = "A description" };

        Assert.Equal("A description", value.Description);
    }

    [Fact]
    public void UserData_PropertiesAreSettable()
    {
        var data = new UserData { UserName = "neo", Login = "neo.anderson" };

        Assert.Equal("neo", data.UserName);
        Assert.Equal("neo.anderson", data.Login);
    }
}
