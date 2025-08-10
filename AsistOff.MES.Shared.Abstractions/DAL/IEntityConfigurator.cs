using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Abstractions.DAL;

public interface IEntityConfigurator
{
    void ConfigureEntities(ModelBuilder modelBuilder);
}
