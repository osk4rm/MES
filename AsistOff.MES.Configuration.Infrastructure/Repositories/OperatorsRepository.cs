using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Configuration.Infrastructure.DAL;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

public class OperatorsRepository : IOperatorsRepository
{
    private readonly ConfigurationDbContext _context;

    public OperatorsRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Operator>> BrowseAsync(Paginator<Operator> paginator, CancellationToken cancellationToken = default)
    {
        var operators = await _context.Operators
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

        return operators;
    }
}