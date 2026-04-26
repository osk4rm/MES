# AsistOff MES – Claude Code Instructions

This file is the entry point for **Claude Code** (and other AI agents that read `CLAUDE.md`).

## Always Read First

→ **`.github/copilot-instructions.md`** – project overview, tech stack summary, repo structure, and general conventions. Read this before any task.

## Context-Specific Instructions

Load the relevant file(s) **only when you're working in that area** to keep context focused:

| Instruction file | Load when… |
|------------------|------------|
| `.github/instructions/architecture.instructions.md` | Any C# work – modules, CQRS, MediatR, domain events, Clean Architecture layers |
| `.github/instructions/frontend.instructions.md` | Working inside `AsistOff.MES.Web/src/` (Vue 3, TypeScript, Pinia, PrimeVue) |
| `.github/instructions/database.instructions.md` | EF Core entities, migrations, repositories, multitenancy / ISaasy |
| `.github/instructions/api.instructions.md` | Controllers, HTTP endpoints, error handling, authorization |
| `.github/instructions/testing.instructions.md` | Writing or modifying tests (backend xUnit or frontend Vitest) |
| `.github/instructions/production-recipes.instructions.md` | Any work in the Recipes / Production module (entities, CRUD, frontend views, Skills, OperationTemplates) |

## Running the Project

| Command | Description |
|---------|-------------|
| `dotnet run --project AsistOff.MES.Gateway` | Start the backend API (port 5080) |
| `cd AsistOff.MES.Web && npm run dev` | Start the frontend dev server |
| `cd AsistOff.MES.Web && npm run build` | Production build (`vue-tsc` + Vite) |

## Useful Dotnet CLI Commands

```bash
# Add EF Core migration
dotnet ef migrations add <Name> \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway

# Remove last migration (if not yet applied)
dotnet ef migrations remove \
  --project AsistOff.MES.Shared.Infrastructure \
  --startup-project AsistOff.MES.Gateway
```

## Keeping Instructions Up to Date

If a convention, pattern, or part of the stack changes during your session, **update the relevant instruction file** before finishing. Small, accurate docs are more valuable than large, stale ones.
