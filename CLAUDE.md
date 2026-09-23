# AsistOff MES – Claude Code Instructions

This file is the entry point for **Claude Code** (and other AI agents that read `CLAUDE.md`).

## Always Read First

→ **`.github/copilot-instructions.md`** – project overview, tech stack summary, repo structure, and general conventions. Read this before any task.

## Autonomous Agent Workflow

This repo runs a multi-agent swarm (researcher → analyst → implementer → reviewer → tracker).
Before working on it, read **`docs/agent-workflow.md`** (roles, loop, dashboard, gotchas) and
**`docs/feature-tracker.md`** (canonical capability map — do not rescan the repo).

The swarm is **label-driven**: `scripts/agent-dispatcher.ps1` watches GitHub and runs
implementer → reviewer → e2e-tester as labels change, and reconciles the feature
tracker autonomously. The dashboard only starts `researcher`, `analyst` and
`e2e-tester` by hand.

| Command | Description |
|---------|-------------|
| `pwsh -File scripts/setup-labels.ps1` | Create/update the `ai:*` workflow labels |
| `node scripts/dashboard/server.mjs` | Local control panel → http://127.0.0.1:5178 |
| `pwsh -File scripts/agent-dispatcher.ps1` | Run the label-driven dispatcher (implement→review→e2e + tracker) |
| `pwsh -File scripts/agent-dispatcher.ps1 -Once -DryRun` | Show the next planned action without running it |
| `pwsh -File scripts/e2e/app.ps1 -Action start` | Start backend+frontend for e2e smoke tests |
| `docker compose up -d --build swarm` | Run dashboard + dispatcher 24/7 in Docker (isolated clone; needs `GH_TOKEN` in `.env`) |
| `pwsh -File scripts/agent-loop.ps1 -MaxRounds 3` | Manual override: one-issue implement→review→fix loop |
| `opencode run --agent mes-researcher "..."` | Append new capabilities to the tracker (no issue) |
| `opencode run --agent mes-analyst "..."` | Turn the first actionable tracker gap into an `ai:implement` issue |

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
