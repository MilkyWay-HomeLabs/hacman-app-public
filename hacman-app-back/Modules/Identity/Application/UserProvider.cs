using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.Identity.Infrastructure;

namespace hacman_app_back.Modules.Identity.Application;

public class UserProvider : IUserProvider
{
    private readonly IUserRepository _repository;

    public UserProvider(IUserRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<User?> GetByIdAsync(Guid id) => _repository.GetByIdAsync(id);

    public Task<User?> GetByExternalAuthIdAsync(String externalAuthId) =>
        _repository.GetByExternalAuthIdAsync(externalAuthId);
}