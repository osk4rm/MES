# AsistOff MES — Technical Specification

Authoritative end-to-end description of the system as built. The first five
pages cover architecture, module boundaries, the domain model, multitenancy,
and authentication/RBAC. The remaining four cover the HTTP surface,
deployment and operations, observability, and frontend architecture.

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
- [Endpoint catalog](endpoints.md) — every controller and route per module
  with auth and permission requirements, the ProblemDetails contract, and
  the paging, sorting, and filtering conventions.
- [Deployment and operations](deployment.md) — compose topology, the gated
  migration job, backup and restore, environment separation, and container
  hardening.
- [Health and observability](observability.md) — live versus ready probes,
  correlation ID flow, OpenTelemetry traces and metrics, and business
  meters including OEE latency.
- [Frontend architecture](frontend.md) — Vue 3 plus Vite plus Pinia SPA,
  in-house `App*` components, cookie session handling, route guards, error
  boundary and resilience, and i18n.

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
  the production boot gate.
- [../production-runbook.md](../production-runbook.md) — shopfloor operating
  procedures that consume the domain model described in
  [Domain model](domain-model.md).
- [../verification-recipes-orders.md](../verification-recipes-orders.md),
  [../benchmark-recipes-orders.md](../benchmark-recipes-orders.md) — recipe
  and Production Order verification evidence.
- [../manual/README.md](../manual/README.md) — the end-user manual; the spec
  describes the same entities from the builder/integrator point of view.
