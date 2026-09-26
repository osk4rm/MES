using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Models.DomainEvents;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Infrastructure.Outbox;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AsistOff.MES.Shared.Infrastructure.Interceptors
{
    /// <summary>
    /// Stage-only outbox interceptor (slice 2, #259). Domain events collected
    /// from tracked <see cref="IHasDomainEvents"/> entities are staged as
    /// <see cref="OutboxMessage"/> rows via <c>context.Add</c> inside
    /// <c>SavingChangesAsync</c>, so they commit in the same transaction as
    /// the primary write and a rollback removes them together with the entity
    /// changes. Staged rows share the source entity's <c>TenantId</c>
    /// (falling back to the ambient tenant), so staging can never cross
    /// tenant boundaries; the <c>SaasyEntityInterceptor</c> (registered
    /// before this one) has already backfilled and validated that id.
    ///
    /// Nothing is published here: the <c>OutboxRelayService</c> dispatches
    /// staged rows through MediatR after commit, so a rolled-back transaction
    /// results in zero dispatches and ghost events are impossible by
    /// construction. Staged events are cleared from the entities once staged,
    /// so a retry of the same <c>DbContext</c> can never stage duplicates.
    /// Contexts whose model has no outbox set (e.g.
    /// <c>MultitenancyDbContext</c>) skip staging; their entities carry no
    /// domain events, so nothing is lost.
    /// </summary>
    public class PublishDomainEventsInterceptor(
        IDateTimeProvider dateTimeProvider,
        IGuidProvider guidProvider,
        ICurrentTenantAccessor tenantAccessor) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            throw new InvalidOperationException(
                "Synchronous SaveChanges is not supported. Use SaveChangesAsync instead.");
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            StageOutboxMessages(eventData.Context);

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void StageOutboxMessages(DbContext? dbContext)
        {
            if (dbContext is null)
            {
                return;
            }

            if (dbContext.Model.FindEntityType(typeof(OutboxMessage)) is null)
            {
                foreach (var untracked in dbContext.ChangeTracker.Entries<IHasDomainEvents>())
                {
                    untracked.Entity.ClearDomainEvents();
                }

                return;
            }

            var entitiesWithDomainEvents = dbContext.ChangeTracker.Entries<IHasDomainEvents>()
                .Where(entry => entry.Entity.DomainEvents.Any())
                .Select(entry => entry.Entity)
                .ToList();

            if (entitiesWithDomainEvents.Count == 0)
            {
                return;
            }

            var occurredOnUtc = dateTimeProvider.UtcNow;

            foreach (var entity in entitiesWithDomainEvents)
            {
                var tenantId = (entity as ISaasy)?.TenantId ?? Guid.Empty;
                if (tenantId == Guid.Empty && !tenantAccessor.TryGetTenantId(out tenantId))
                {
                    throw new InvalidOperationException(
                        $"Cannot stage outbox messages for '{entity.GetType().Name}' without a tenant context.");
                }

                var staged = OutboxStager.Stage(entity.DomainEvents, tenantId, occurredOnUtc, guidProvider.NewGuid);
                foreach (var message in staged)
                {
                    dbContext.Add(message);
                }
            }

            foreach (var entity in entitiesWithDomainEvents)
            {
                entity.ClearDomainEvents();
            }
        }
    }
}
