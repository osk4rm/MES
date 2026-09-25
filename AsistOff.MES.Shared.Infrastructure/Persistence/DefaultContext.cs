using System.Linq.Expressions;
using AsistOff.MES.Configuration.Domain.Entities;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.DAL;
using AsistOff.MES.Shared.Infrastructure.Interceptors;
using AsistOff.MES.Shared.Infrastructure.Persistence.Entities;
using AsistOff.MES.Users.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsistOff.MES.Shared.Infrastructure.Persistence;

public class DefaultContext : DbContext
{
    private readonly IEnumerable<IEntityConfigurator> _entityConfigurators;
    private readonly PublishDomainEventsInterceptor _publishDomainEventsInterceptor;
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;
    private readonly SaasyEntityInterceptor _saasyEntityInterceptor;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<OperatorShiftAssignment> OperatorShiftAssignments { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Operator> Operators { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<MeasureUnit> MeasureUnits { get; set; }
    public DbSet<ProductGroup> ProductGroups { get; set; }
    public DbSet<ProductMeasureUnit> ProductMeasureUnits { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductPrice> ProductPrices { get; set; }
    public DbSet<AuditEvent> AuditEvents { get; set; }

    public DefaultContext(
        DbContextOptions<DefaultContext> options,
        IEnumerable<IEntityConfigurator> entityConfigurators,
        PublishDomainEventsInterceptor publishDomainEventsInterceptor,
        AuditableEntityInterceptor auditableEntityInterceptor,
        SaasyEntityInterceptor saasyEntityInterceptor,
        ICurrentTenantAccessor tenantAccessor)
        : base(options)
    {
        _entityConfigurators = entityConfigurators;
        _publishDomainEventsInterceptor = publishDomainEventsInterceptor;
        _auditableEntityInterceptor = auditableEntityInterceptor;
        _saasyEntityInterceptor = saasyEntityInterceptor;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// Tenant identifier used by the global query filter. Exposed as a
    /// DbContext property so that EF Core treats it as a query parameter
    /// (re‑evaluated per query) instead of a baked‑in constant.
    /// </summary>
    public Guid CurrentTenantId => _tenantAccessor.CurrentTenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var configurator in _entityConfigurators)
        {
            configurator.ConfigureEntities(modelBuilder);
        }

        ApplyTenantQueryFilter(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
        optionsBuilder.AddInterceptors(_saasyEntityInterceptor);
        optionsBuilder.AddInterceptors(_publishDomainEventsInterceptor);
        base.OnConfiguring(optionsBuilder);
    }

    /// <summary>
    /// Applies a global EF Core query filter <c>e =&gt; e.TenantId == currentTenantId</c>
    /// to every entity type implementing <see cref="ISaasy"/>. This is the
    /// primary defense against cross‑tenant data leakage: any <c>DbSet&lt;T&gt;</c>
    /// query, regardless of repository implementation, will be constrained to
    /// the ambient tenant.
    ///
    /// When no tenant is present (startup, background jobs) the accessor
    /// returns <see cref="Guid.Empty"/>, causing queries against tenant‑scoped
    /// tables to return zero rows — a safe default.
    /// </summary>
    private void ApplyTenantQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISaasy).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantIdProperty = Expression.Property(parameter, nameof(ISaasy.TenantId));
            var contextExpression = Expression.Constant(this);
            var currentTenantId = Expression.Property(contextExpression, nameof(CurrentTenantId));
            var body = Expression.Equal(tenantIdProperty, currentTenantId);
            var lambda = Expression.Lambda(body, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}
