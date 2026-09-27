using hacman_app_back.Modules.Identity.Domain;

namespace hacman_app_back.Modules.Identity.Application;

public interface IUserUpdater
{
    Task<User> CreateNewUserData(string externalAuthId, string username, string email);
}