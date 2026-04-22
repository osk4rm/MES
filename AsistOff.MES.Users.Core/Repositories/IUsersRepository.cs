using AsistOff.MES.Users.Core.Entities;

namespace AsistOff.MES.Users.Core.Repositories;

public interface IUsersRepository
{
    Task<User?> GetAsync(Guid id);
    Task<User?> GetAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}