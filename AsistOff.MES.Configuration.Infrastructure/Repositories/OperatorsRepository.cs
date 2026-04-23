using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Configuration.Domain.Repositories;
using AsistOff.MES.Shared.Infrastructure.Persistence;
using AsistOff.MES.Shared.Abstractions.Extensions;
using AsistOff.MES.Shared.Abstractions.Pagination;
using LinqKit;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Configuration.Infrastructure.Repositories;

public class OperatorsRepository : IOperatorsRepository
{
    private readonly DefaultContext _context;

    public OperatorsRepository(DefaultContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Operator>> BrowseAsync(Paginator<Operator> paginator, CancellationToken cancellationToken = default)
    {
        var operators = await _context.Operators
            .Include(o => o.Department)
            .PageFilter(paginator)
            .ToListAsync(cancellationToken);

        return operators;
    }

    public async Task<int> CountAsync(ExpressionStarter<Operator> predicate, CancellationToken cancellationToken = default)
    {
        return await _context.Operators.Where(predicate).CountAsync(cancellationToken);
    }

    public async Task<Operator?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Operators
            .Include(o => o.Department)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Operator> AddAsync(Operator operatorEntity, CancellationToken cancellationToken = default)
    {
        _context.Operators.Add(operatorEntity);
        await _context.SaveChangesAsync(cancellationToken);
        return operatorEntity;
    }

    public async Task UpdateAsync(Operator operatorEntity, CancellationToken cancellationToken = default)
    {
        _context.Operators.Update(operatorEntity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var operatorEntity = await GetByIdAsync(id, cancellationToken);
        if (operatorEntity is not null)
        {
            _context.Operators.Remove(operatorEntity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}