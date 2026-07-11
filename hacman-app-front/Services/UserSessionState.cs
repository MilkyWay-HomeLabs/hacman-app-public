using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public class UserSessionState
{
    public string? AccessToken { get; set; }
    public UserResponse? CurrentUser { get; set; }
}