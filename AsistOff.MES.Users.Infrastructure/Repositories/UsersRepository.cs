using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

public class UsersRepository(DefaultContext context) : IUsersRepository
{
    private readonly DbSet<User> _users = context.Users;

    public Task<User?> GetAsync(Guid id)
        => _users.SingleOrDefaultAsync(x => x.Id == id);

    public Task<User?> GetAsync(string email) =>
        _users.SingleOrDefaultAsync(x => x.Email == email);

    public Task<User?> GetForAuthenticationAsync(string email, CancellationToken cancellationToken = default) =>
        _users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

    public async Task AddAsync(User user)
    {
        await _users.AddAsync(user);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _users.Update(user);
        await context.SaveChangesAsync();
    }
}