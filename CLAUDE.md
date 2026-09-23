# AsistOff MES – Claude Code Instructions

This file is the entry point for **Claude Code** (and other AI agents that read `CLAUDE.md`).

## Always Read First

→ **`.github/copilot-instructions.md`** – project overview, tech stack summary, repo structure, and general conventions. Read this before any task.

## Autonomous Agent Workflow

This repo runs a multi-agent swarm (researcher → analyst → implementer → reviewer → tracker).
Before working on it, read **`docs/agent-workflow.md`** (roles, loop, dashboard, gotchas) and
**`docs/feature-tracker.md`** (canonical capability map — do not rescan the repo).

| Command | Description |
|---------|-------------|
| `node scripts/dashboard/server.mjs` | Local control panel → http://127.0.0.1:5178 |
| `pwsh -File scripts/agent-loop.ps1 -MaxRounds 3` | Run the implement→review→fix loop |
| `opencode run --agent mes-researcher "..."` | Propose new work (no label) |
| `opencode run --agent mes-tracker "..."` | Reconcile the feature tracker |

## Context-Specific Instructions

Load the relevant file(s) **only when you're working in that area** to keep context focused:

| Instruction file | Load when… |
|------------------|------------|
| `.github/instructions/architecture.instructions.md` | Any C# work – modules, CQRS, MediatR, domain events, Clean Architecture layers |
| `.github/instructions/frontend.instructions.md` | Working inside `AsistOff.MES.Web/src/` (Vue 3, TypeScript, Pinia, in-house `App*` UI components) |
| `.github/instructions/database.instructions.md` | EF Core entities, migrations, repositories, multitenancy / ISaasy |
| `.github/instructions/api.instructions.md` | Controllers, HTTP endpoints, error handling, authorization |
| `.github/instructions/testing.instructions.md` | Writing or modifying tests (backend xUnit or frontend Vitest) |
| `.github/instructions/business-features.instructions.md` | Business behavior, implemented MES features, workflows, module map, known gaps |
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
