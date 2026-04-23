---
applyTo: "**/Migrations/**,**/DAL/**,**/Repositories/**,**/*Context*.cs,**/*DbContext*.cs,**/Configurations/**,**/Persistence/**"
---

# Database – EF Core / PostgreSQL

## Setup

- **PostgreSQL** via **Entity Framework Core 9** (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- Connection string is configured in `appsettings.json`:
  ```json
  "postgres": { "connectionString": "Host=...;Port=5432;Database=mes;Username=...;Password=..." }
  ```
- The `DefaultContext` (in `Shared.Infrastructure/Persistence/DefaultContext.cs`) is the single shared `DbContext`.

## Entity Conventions

All entities must implement `IEntity` (provides `Guid Id`). Tenant-scoped entities also implement `ISaasy` and syncable entities implement `ISyncable`:

```csharp
public class MyEntity : IEntity, ISaasy, ISyncable
{
    public Guid Id { get; set; }         // IEntity
    public Guid TenantId { get; set; }   // ISaasy  – always filter by this in queries
    public string? SyncId { get; set; }  // ISyncable – external system reference
    // domain properties…
}
```

- Use `required` for non-nullable string/value properties that have no meaningful default.
- Use `virtual` for navigation properties to support lazy loading.
- Initialize collection navigation properties inline: `= new List<T>()`.

## Entity Type Configurations

- Every entity has a dedicated `IEntityTypeConfiguration<T>` class.
- Configuration classes live in `Module.Infrastructure/Configurations/`.
- Register them in the module's `DbContext.OnModelCreating` or via `ApplyConfigurationsFromAssembly`.

```csharp
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(50);
        // relationships…
    }
}
```

## Repository Pattern

- **Interfaces** are defined in `Module.Core/Repositories/` and depend only on domain types.
- **Implementations** live in `Module.Infrastructure/Repositories/` and use `DefaultContext`.
- Repositories are registered as **scoped** services.

Naming conventions for repository methods:

| Operation | Method name |
|-----------|-------------|
| Get by id | `GetAsync(Guid id, CancellationToken ct)` |
| Get by unique field | `GetAsync(string field, CancellationToken ct)` |
| List all / filtered | `GetAllAsync(CancellationToken ct)` |
| Insert | `AddAsync(T entity, CancellationToken ct)` |
| Update | `UpdateAsync(T entity, CancellationToken ct)` |
| Delete | `DeleteAsync(T entity, CancellationToken ct)` |

Always pass and forward `CancellationToken`.

## Tenant Isolation

- **Always** filter queries for `ISaasy` entities by `TenantId`.
- Obtain the current tenant id from `ITenantContext` (injected into repositories).
- Never return data from one tenant to another.

```csharp
return await _context.Products
    .Where(p => p.TenantId == _tenantContext.TenantId)
    .ToListAsync(cancellationToken);
```

## Query Best Practices

- Use `.AsNoTracking()` for read-only queries.
- Use `.Include()` / `.ThenInclude()` to avoid N+1 queries.
- Do **not** expose `IQueryable` outside the repository layer.
- For paginated lists, use `.Skip()` / `.Take()` with the `Pagination` abstractions from `Shared.Abstractions`.

## Migrations

Migrations live in `AsistOff.MES.Shared.Infrastructure/Migrations/`.

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway

# Remove the last migration (if not yet applied)
dotnet ef migrations remove \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway
```

Migrations are **applied automatically on startup** via `ApplyAllPendingMigrations()` called in `Program.cs`.

## Seeders

- Seeders implement `ISeeder` from `Shared.Abstractions`.
- They run after migrations on startup.
- Use seeders for required reference data (roles, default tenant, etc.); never for test/demo data.

## Interceptors

Two EF Core save interceptors are registered globally:

| Interceptor | Purpose |
|-------------|---------|
| `AuditableEntityInterceptor` | Populates `CreatedAt` / `UpdatedAt` audit fields |
| `PublishDomainEventsInterceptor` | Dispatches domain events after `SaveChanges` |
