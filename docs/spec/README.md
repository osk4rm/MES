# AsistOff MES — Technical Specification (Slice 1 of 2)

Authoritative end-to-end description of the system as built. Slice 1 covers
architecture, module boundaries, the domain model, multitenancy, and
authentication/RBAC. Slice 2 adds the endpoint catalog, deployment and
operations, observability, and frontend architecture.

## Pages

- [Architecture](architecture.md) — modular monolith, layer split, Gateway
  wiring, request pipeline order.
- [Module boundaries](modules.md) — one row per module: responsibility,
  projects, `IModule` registration, controllers.
- [Domain model](domain-model.md) — key entities per module with identifiers,
  tenant scope, and lifecycles.
- [Multitenancy](multitenancy.md) — `ISaasy`, the global query filter, the
  save interceptor, the tenant pipeline behavior, and the pre-auth bypass.
- [Authentication and RBAC](auth-rbac.md) — `ITenantRequest` versus
  `IAllowAnonymousRequest`, JWT claims, the interim permission model, and
  the default-deny `RequirePermission` enforcement.

## Conventions used in every page

- **Code references** name the source file or type (for example
  `AsistOff.MES.Gateway/Program.cs`, `DefaultContext.ApplyTenantQueryFilter`).
  Every statement was verified against the current checkout.
- **Tenant scope** is explicit: `tenant-scoped` means the entity implements
  `ISaasy` and carries `TenantId`; `shared` means one row serves all tenants
  (today only the `Tenant` registry itself).
- **Glossary terms** follow [../glossary.md](../glossary.md): Tenant,
  Production Order, Work Center, Operation, Routing, BOM, Lot / Serial,
  Genealogy, Confirmation, RW / PW, OEE.

## Related documents (not duplicated here)

- [../adr/0001-modular-monolith.md](../adr/0001-modular-monolith.md),
  [../adr/0002-multi-tenancy-strategy.md](../adr/0002-multi-tenancy-strategy.md),
  [../adr/0003-auth-jwt-and-rbac.md](../adr/0003-auth-jwt-and-rbac.md) —
  the architecture decisions this spec describes.
- [../deployment.md](../deployment.md) — environments, configuration, and
  the production boot gate (covered in slice 2).
- [../production-runbook.md](../production-runbook.md) — shopfloor operating
  procedures that consume the domain model described in
  [Domain model](domain-model.md).
- [../verification-recipes-orders.md](../verification-recipes-orders.md),
  [../benchmark-recipes-orders.md](../benchmark-recipes-orders.md) — recipe
  and Production Order verification evidence.
- [../manual/README.md](../manual/README.md) — the end-user manual; the spec
  describes the same entities from the builder/integrator point of view.

## Slice 2 preview (not in scope here)

Slice 2 adds four pages under this directory using the same conventions
(English Markdown, relative links, tables for reference data, text diagrams
only): `endpoints.md` (controller-to-handler catalog), `deployment.md`
(deploy, backup, container hardening), `observability.md` (health probes,
correlation IDs, OpenTelemetry), and `frontend.md` (SPA architecture).
