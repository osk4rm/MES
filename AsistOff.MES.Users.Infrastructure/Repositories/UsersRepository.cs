using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using AsistOff.MES.Users.Infrastructure.DAL;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Repositories;

public class UsersRepository : IUsersRepository
{
    private readonly UsersDbContext _context;
    private readonly DbSet<User> _users;

    public UsersRepository(UsersDbContext context)
    {
        _context = context;
        _users = context.Users;
    }

    public Task<User?> GetAsync(Guid id)
        => _users.SingleOrDefaultAsync(x => x.Id == id);

    public Task<User?> GetAsync(string email) => 
        _users.SingleOrDefaultAsync(x => x.Email == email);

    public async Task AddAsync(User user)
    {
        await _users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _users.Update(user);
        await _context.SaveChangesAsync();
    }
}