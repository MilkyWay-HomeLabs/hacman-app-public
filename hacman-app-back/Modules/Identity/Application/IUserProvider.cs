using hacman_app_back.Modules.Identity.Domain;

namespace hacman_app_back.Modules.Identity.Application;

public interface IUserProvider
{
    Task<User?> GetByIdAsync(Guid id);

    Task<User?> GetByExternalAuthIdAsync(String externalAuthId);
}