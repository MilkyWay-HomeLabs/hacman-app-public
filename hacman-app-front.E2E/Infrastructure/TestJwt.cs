using System.Text;
using System.Text.Json;

namespace hacman_app_front.E2E.Infrastructure;

/// <summary>
/// Builds a syntactically-valid but unsigned-in-practice JWT for E2E tests.
///
/// The backend's JWT bearer options (<c>hacman-app-back/Program.cs</c>) set
/// <c>ValidateIssuerSigningKey = false</c> and replace <c>SignatureValidator</c> with a delegate
/// that only parses the token (<c>new JsonWebToken(token)</c>) without checking the signature
/// bytes at all — "Signature validation is disabled for Nebula integration" per a comment there.
/// Confirmed by hand with curl against a real locally-run backend instance: a token signed with a
/// throwaway string is accepted identically to a correctly-signed one, as long as it has 3
/// base64url segments, valid JSON header/payload, and a future <c>exp</c> (the only claim that
/// <em>is</em> actually validated, via <c>ValidateLifetime = true</c>).
///
/// <c>sub</c> must be <c>"{externalAuthId},{email}"</c> — see
/// <c>hacman-app-back/Modules/Identity/Api/AuthClaimsParser.cs</c>, which splits on the comma.
/// </summary>
public static class TestJwt
{
    public static string CreateAccessToken(string externalAuthId, string email, TimeSpan? validFor = null)
    {
        var header = new { alg = "HS256", typ = "JWT" };
        var now = DateTimeOffset.UtcNow;
        var payload = new
        {
            sub = $"{externalAuthId},{email}",
            email,
            iat = now.ToUnixTimeSeconds(),
            exp = now.Add(validFor ?? TimeSpan.FromHours(1)).ToUnixTimeSeconds(),
        };

        var headerSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signatureSegment = Base64UrlEncode(Encoding.UTF8.GetBytes("e2e-test-signature-not-validated"));

        return $"{headerSegment}.{payloadSegment}.{signatureSegment}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
