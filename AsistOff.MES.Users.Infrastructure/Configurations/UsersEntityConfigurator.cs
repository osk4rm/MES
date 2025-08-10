using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Users.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Users.Infrastructure.Configurations;

public class UsersEntityConfigurator : IEntityConfigurator
{
    public void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersEntityConfigurator).Assembly);
        
        modelBuilder.Entity<User>().ToTable("Users", "users");
    }
}
