using System.Collections.Generic;
using System.Security.Claims;
using hacman_app_back.Modules.Identity.Api;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;

namespace hacman_app_back.Tests.Modules.Identity.Api;

[TestClass]
public class AuthClaimsParserTest
{
    [Fact]
    public void ParseClaims_ReturnsExternalAuthIdAndEmail_WhenSubClaimHasBothParts()
    {
        var claims = new List<Claim> { new("sub", "authId123,email@example.com") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        var (externalAuthId, email) = new AuthClaimsParser().ParseClaims(principal);

        Assert.Equal("authId123", externalAuthId);
        Assert.Equal("email@example.com", email);
    }

    [Fact]
    public void ParseClaims_ReturnsExternalAuthIdOnly_WhenSubClaimHasNoEmailPart()
    {
        var claims = new List<Claim> { new("sub", "authId123") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        var (externalAuthId, email) = new AuthClaimsParser().ParseClaims(principal);

        Assert.Equal("authId123", externalAuthId);
        Assert.Null(email);
    }

    [Fact]
    public void ParseClaims_FallsBackToEmailClaim_WhenSubHasNoEmailPart()
    {
        var claims = new List<Claim>
        {
            new("sub", "authId123"),
            new(ClaimTypes.Email, "fromclaim@example.com")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        var (externalAuthId, email) = new AuthClaimsParser().ParseClaims(principal);

        Assert.Equal("authId123", externalAuthId);
        Assert.Equal("fromclaim@example.com", email);
    }

    [Fact]
    public void ParseClaims_ReturnsNulls_WhenSubClaimIsMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>()));

        var (externalAuthId, email) = new AuthClaimsParser().ParseClaims(principal);

        Assert.Null(externalAuthId);
        Assert.Null(email);
    }

    [Fact]
    public void ParseClaims_ReturnsNulls_WhenSubClaimIsEmpty()
    {
        var claims = new List<Claim> { new("sub", "") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        var (externalAuthId, email) = new AuthClaimsParser().ParseClaims(principal);

        Assert.Null(externalAuthId);
        Assert.Null(email);
    }

    [Fact]
    public void ParseClaims_UsesNameIdentifier_WhenSubClaimIsMissing()
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "authId456,email2@example.com") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        var (externalAuthId, email) = new AuthClaimsParser().ParseClaims(principal);

        Assert.Equal("authId456", externalAuthId);
        Assert.Equal("email2@example.com", email);
    }
}