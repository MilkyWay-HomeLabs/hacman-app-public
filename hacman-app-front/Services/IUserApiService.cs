using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public interface IUserApiService
{
    Task<UserResponse?> GetUserByExternalAuthId();
    Task<UserResponse?> CreateNewUser(string userName);
}