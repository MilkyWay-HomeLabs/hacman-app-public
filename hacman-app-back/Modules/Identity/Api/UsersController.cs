using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hacman_app_back.Modules.Identity.Api;

/// <summary>
/// Manages user accounts.
/// </summary>
[ApiController]
[Route("api/v1/users")]
public class UsersController(
    IUserProvider provider,
    IUserUpdater updater,
    IAuthClaimsParser authClaimsParser,
    ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves a user by their internal identifier.
    /// </summary>
    /// <param name="id">The user's GUID.</param>
    /// <returns>The matching <see cref="User"/>, or 404 if not found.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<User>> Get(Guid id)
    {
        var item = await provider.GetByIdAsync(id);

        if (item == null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    /// <summary>
    /// Retrieves the authenticated user by the external auth ID embedded in the JWT.
    /// </summary>
    /// <returns>The matching <see cref="User"/>, or 401/404 on failure.</returns>
    [HttpGet("external-auth-id")]
    [Authorize]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByExternalAuthId()
    {
        var (externalAuthId, _) = authClaimsParser.ParseClaims(User);

        if (string.IsNullOrEmpty(externalAuthId))
        {
            logger.LogWarning("GetByExternalAuthId: missing or invalid sub claim");
            return Unauthorized(new { message = "Invalid token claims: sub is missing or invalid" });
        }

        var user = await provider.GetByExternalAuthIdAsync(externalAuthId);

        if (user == null)
        {
            logger.LogInformation("GetByExternalAuthId: no user found for externalAuthId={ExternalAuthId}",
                externalAuthId);
            return NotFound();
        }

        return Ok(user);
    }

    /// <summary>
    /// Creates a new user account for the authenticated identity.
    /// </summary>
    /// <param name="request">The username to assign.</param>
    /// <returns>The created <see cref="User"/> at its canonical URL.</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var (externalAuthId, email) = authClaimsParser.ParseClaims(User);

        logger.LogInformation("Create: externalAuthId={ExternalAuthId}, username={Username}", externalAuthId,
            request.Username);

        if (string.IsNullOrEmpty(externalAuthId))
        {
            logger.LogWarning("Create: missing externalAuthId in claims");
            return Unauthorized(new { message = "Invalid token claims: sub is missing or invalid" });
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            logger.LogWarning("Create: username is missing");
            return BadRequest(new { message = "Username is required" });
        }

        var userEmail = !string.IsNullOrEmpty(email) ? email : $"user{externalAuthId}@hacman.local";

        try
        {
            var user = await updater.CreateNewUserData(externalAuthId, request.Username, userEmail);
            logger.LogInformation("Create: user created id={UserId}", user.Id);
            return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Create: conflict for externalAuthId={ExternalAuthId}", externalAuthId);
            return BadRequest(new { message = ex.Message });
        }
    }
}