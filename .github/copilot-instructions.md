# AsistOff MES – AI Agent Instructions

## Project Overview

AsistOff MES is a **Manufacturing Execution System** built as a **modular monolith** with a Vue 3 SPA frontend and an ASP.NET Core backend.

## Tech Stack

| Layer        | Technology                                         |
|--------------|----------------------------------------------------|
| Backend      | .NET 10, ASP.NET Core, C#                           |
| Frontend     | Vue 3, TypeScript, Vite                            |
| Database     | PostgreSQL via Entity Framework Core 10             |
| Auth         | JWT Bearer tokens                                  |
| CQRS         | MediatR                                            |
| Validation   | Custom pipeline behaviors + `IRequestValidator<T>` |
| State (FE)   | Pinia                                              |
| UI Library   | PrimeVue 4                                         |
| HTTP client  | Axios                                              |
| i18n         | vue-i18n                                           |

## Repository Structure

```
AsistOff.MES.Gateway/               # Entry point – wires up all modules (Program.cs)
AsistOff.MES.Shared.Abstractions/   # Interfaces & contracts shared across modules
AsistOff.MES.Shared.Infrastructure/ # Cross-cutting infrastructure (EF Core, auth, messaging)
AsistOff.MES.Multitenancy/          # Tenant management module
AsistOff.MES.Configuration.*/       # Configuration module (products, warehouses, departments…)
AsistOff.MES.Users.*/               # Users module (authentication, user CRUD)
AsistOff.MES.Web/                   # Vue 3 SPA frontend
```

Each domain module follows the `*.Core` / `*.Application` / `*.Infrastructure` / `*.Api` layer split.

## General Conventions

### C\#

- Use the `required` keyword for non-nullable properties that have no default value.
- All async methods use the `Async` suffix and accept a `CancellationToken` parameter.
- Throw typed domain exceptions from `AsistOff.MES.Shared.Abstractions.Exceptions` (e.g. `NotFoundException`, `ValidationException`).
- Register services in the module's `DependencyInjection.cs`.
- Namespace matches folder structure.

### TypeScript / Vue

- Use `<script setup lang="ts">` in every SFC.
- Prefer `const` over `let`; avoid `var`.
- Async functions in services/composables return typed `Promise<T>`.

## Contextual Instructions (load when relevant)

More detailed guidance lives in `.github/instructions/`. Load a file when you start working in that area:

| File | When to use |
|------|-------------|
| `architecture.instructions.md` | C# module structure, CQRS, Clean Architecture, domain events |
| `frontend.instructions.md`     | Vue 3 / TypeScript work inside `AsistOff.MES.Web/src/` |
| `database.instructions.md`     | EF Core entities, migrations, repositories, multitenancy |
| `api.instructions.md`          | Controllers, HTTP endpoints, error handling, auth |
| `testing.instructions.md`      | Writing or modifying tests |
| `production-recipes.instructions.md` | Any work in the Recipes / Production module (entities, CRUD, frontend views, Skills, OperationTemplates) |

## Keeping These Files Up to Date

These instruction files are living documentation. **If you discover that a convention, pattern, or part of the tech stack has changed during a session, update the relevant instruction file so future sessions start with accurate context.** The main file (`.github/copilot-instructions.md`) should stay high-level and stable; push detail changes to the appropriate file in `.github/instructions/`.
