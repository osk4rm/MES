# MES Agent Swarm — autonomiczny workflow wieloagentowy

Wieloagentowy cykl wytwarzania AsistOff MES przy użyciu opencode: od pomysłu,
przez specyfikację, implementację, review i e2e, aż po zielony PR gotowy do
merge przez człowieka.

Status: **półautomat z lokalnym dyspozytorem (faza 3)**. Researcher i analyst
odpalasz ręcznie z dashboardu; implementer, reviewer, e2e-tester i tracker
działają automatycznie po labelach. Merge zostaje bramką człowieka.

## 0. Szybki start

```powershell
# 0. zrestartuj opencode, żeby wczytał opencode.json i agentów (config nie jest hot-reloadowany)
opencode agent list            # sanity check: mes-analyst/.../mes-tracker + mes-e2e-tester

# 1. utwórz/zaktualizuj labele workflow (raz na repo)
pwsh -File scripts/setup-labels.ps1

# 2. panel sterowania (ręcznie: researcher, analyst, e2e)
node scripts/dashboard/server.mjs      # -> http://127.0.0.1:5178

# 3. dyspozytor (implement -> review -> e2e + tracker), w osobnym oknie
pwsh -File scripts/agent-dispatcher.ps1

# --- od tego momentu wszystko leci po labelach ---
# researcher/analyst tworzą issue; analyst nadaje ai:implement -> reszta sama

# 4. ręcznie mergujesz PR z labelami ai:ready (człowiek w pętli)
```

Uwaga: `-Auto` (auto-approve uprawnień) tylko na izolowanym klonie.

## 1. Model mentalny

Nie budujemy jednego długiego agenta robiącego wszystko. Budujemy **bezmózgich,
jednorolowych workerów**, a całą inteligencję workflow i pamięć trzymamy
w **GitHubie** (issue, PR, komentarze, labele, CI). Handoff = issue body / PR
description / review comment / label, nigdy wspólny wątek czatu.

Nowość względem fazy 1–2: **stan workflow kodujemy labelami**, a przejściami
między nimi steruje lokalny **dyspozytor**. Agenci nie dotykają labeli — to
jednoznacznie rozdziela „kto decyduje" (dyspozytor) od „kto pracuje" (agent).

## 2. Labele = maszyna stanów

| Label | Znaczenie | Ustawia |
|---|---|---|
| `ai:implement` | issue gotowe do implementacji | analyst / człowiek |
| `ai:running` | lock: agent właśnie to przetwarza | dyspozytor |
| `ai:review` | PR gotowe do review (CI zielone) | dyspozytor |
| `ai:changes` | reviewer/e2e/CI żąda poprawek | dyspozytor |
| `ai:e2e` | PR gotowe do smoke e2e (Playwright) | dyspozytor |
| `ai:ready` | CI + review + e2e zielone; do merge przez człowieka | dyspozytor |
| `ai:blocked` | eskalacja do człowieka (limit rund, brak werdyktu, brak PR) | dyspozytor |
| `ai:auto-merge` | opt-in na przyszły auto-merge (jeszcze nieaktywny) | człowiek |

`ai:running` jest jednocześnie lockiem (dyspozytor jest jednowątkowy) i
znacznikiem widoczności. Po restarcie dyspozytor zdejmuje osierocone
`ai:running`.

## 3. Roster agentów

| Agent | Rola | Mode | Trigger | Kto odpala |
|---|---|---|---|---|
| `mes-researcher` | **Szerokość**: gapy z trackera + nowe pomysły MES → 1–2 issue **bez** labela | primary | ręcznie / cyklicznie | dashboard |
| `mes-analyst` | **Głębokość**: jeden pomysł → finalny spec z AC + nadaje `ai:implement` | primary | ręcznie | dashboard |
| `mes-implementer` | issue → kod BE+FE+testy → PR | primary | `ai:implement` / `ai:changes` | dyspozytor |
| `mes-reviewer` | diff PR względem `AGENT.md` → werdykt | all | `ai:review` + CI zielone | dyspozytor |
| `mes-e2e-tester` | Playwright smoke (cały system lub obszar PR) → werdykt | all | `ai:e2e` | dyspozytor / dashboard |
| `mes-verifier` | anty-cheat: czy testy naprawdę dowodzą AC | all | ręcznie (warunkowo) | — |
| `mes-tracker` | jedyny writer `docs/feature-tracker.md` | all | **autonomicznie** (dyspozytor) | dyspozytor |

`mes-e2e-tester` używa Playwright MCP (skonfigurowany w `opencode.json`).

## 4. Pętla

```
researcher ─┐
            ├─> issue (bez labela) ──analyst──> issue [ai:implement]
analyst ────┘                                          │
                                                       ▼  label ai:implement
                                            mes-implementer ─> PR (ai/issue-N-*)
                                                       │
                              PR opened ──> dyspozytor: +ai:review
                                                       ▼  ai:review + CI zielone
                                            mes-reviewer ─> komentarz VERDICT
                                        ┌──────────────┴───────────────┐
                              CHANGES_REQUESTED                 APPROVED
                                        │                             │
                          implementer (ta sama sesja)        dyspozytor: +ai:e2e
                                        │                             ▼
                                        │                    mes-e2e-tester (Playwright)
                                        │                 ┌──────────┴──────────┐
                                        │               FAIL                   PASS
                                        └───────────────┘                +ai:ready
                                      round++                        merge (człowiek)
                                        │
                                 limit rund -> ai:blocked

  równolegle, autonomicznie:  zmiana zbioru work-itemów ─> mes-tracker ─> PR ai/tracker-sync
```

Trik kontekstowy: implementer wraca do **tej samej sesji** przy poprawkach
(`--session`, mapa issue→sessionID w stanie dyspozytora); reviewer i e2e zawsze
startują świeżo (niezależność).

## 5. Orkiestracja — dyspozytor

`scripts/agent-dispatcher.ps1` to lokalny, jednowątkowy daemon: co
`-IntervalSeconds` (domyślnie 20 s) czyta stan GitHuba i wykonuje **jedną**
akcję, wybierając wg priorytetu: `ai:changes` → `ai:review` (jeśli CI zielone)
→ `ai:e2e` → `ai:implement`.

Parametry:

| Parametr | Default | Znaczenie |
|---|---|---|
| `-IntervalSeconds` | 20 | przerwa między cyklami |
| `-MaxRounds` | 3 | limit rund review/e2e → fix na PR |
| `-Once` | — | jeden cykl i wyjście (testy, dashboard) |
| `-DryRun` | — | pokaż plan bez uruchamiania agentów i zmian labeli |
| `-Auto` | — | przekaż `--auto` do opencode (izolowany klon) |
| `-NoTracker` | — | wyłącz autonomiczny tracker |
| `-TrackerIntervalMinutes` | 30 | wymuś sync trackera co tyle, nawet bez zmian |
| `-TrackerCooldownMinutes` | 10 | minimalny odstęp między syncami trackera |

Zachowanie przy błędach:

- CI czerwone na `ai:review` → dyspozytor traktuje to jak `ai:changes`
  (implementer naprawia), bez marnowania review.
- Implementer nie otworzył PR → `ai:blocked` + komentarz.
- Reviewer/e2e bez parsowalnego werdyktu → `ai:blocked`.
- Przekroczony `-MaxRounds` → `ai:blocked` (+ komentarz) na PR i issue.

Stan (sesje, liczniki rund, ostatni sync trackera) trzymany w
`%TEMP%\opencode\dispatcher-state.json`, więc restart nie gubi rund.

### 5.1 Ręczny override

`scripts/agent-loop.ps1` zostaje jako jednorazowa pętla na jedno issue
(przydatne do debugowania promptów). Nie używa labeli stanu — to „ręczny bieg".

## 6. Feature tracker — autonomiczny

`docs/feature-tracker.md` to kanoniczna mapa zdolności (`done` / `partial` /
`proposed` / `in-progress` / `gap`).

- researcher / analyst czytają tracker **najpierw** i nie skanują repo.
- `mes-tracker` to **jedyny writer**. Dyspozytor uruchamia go sam:
  - gdy zmieni się **sygnatura** zbioru otwartych issue/PR (nowe issue,
    zmiana labeli, merge/zamknięcie) i minął cooldown, albo
  - gdy od ostatniego sync minął `-TrackerIntervalMinutes`.
- Sync publikuje PR na branchu `ai/tracker-sync` (aktualizuje istniejący PR,
  jeśli jest otwarty). Tracker PR nie jest liczony do sygnatury, żeby nie
  wywołać pętli.
- implementer / reviewer / e2e nie dotykają trackera (minimalne PR-y).

## 7. End-to-end (Playwright)

`mes-e2e-tester` + skill **mes-e2e**:

- scope = `gh pr diff --name-only` zmapowany na obszary (tabela w skillu);
  zmiany w routerze / `http.ts` / layoutcie / auth → **cały system**;
- stack: `pwsh -File scripts/e2e/app.ps1 -Action start|stop|status`
  (backend `:5243`, frontend `:5173`, login `admin@dev.local` / `Passw0rd!`);
- werdykt w komentarzu PR: `VERDICT: E2E_PASS | E2E_FAIL | E2E_BLOCKED`.

Jeśli backend nie wstanie (np. brak PostgreSQL), werdykt to `E2E_BLOCKED`, a
dyspozytor eskaluje `ai:blocked` — e2e nigdy nie „przechodzi" po cichu.

## 8. Dashboard

```powershell
node scripts/dashboard/server.mjs      # -> http://127.0.0.1:5178
```

Ręcznie odpalasz **tylko**: `mes-researcher`, `mes-analyst`, `mes-e2e-tester`
(reszta jest zdarzeniowa). Panel pokazuje:

- **Pipeline**: otwarte issue/PR z labelami `ai:*`, ich etap i status CI;
- stan **dyspozytora** (start/stop, interwał, max rund) i **aplikacji**
  (start/stop/status backendu i frontendu dla e2e);
- uruchomione procesy i live logi (`%TEMP%\opencode\*.log`).

Prompty domyślne siedzą w `scripts/dashboard/index.html` (`DEFAULTS`) — edycja
bez restartu serwera.

### 8.1 Docker — dashboard i dyspozytor „zawsze dostępne"

Usługa `swarm` w `docker-compose.yml` uruchamia dashboard **i** dyspozytora
w kontenerze, który pracuje na **izolowanym klonie repo** w wolumenie
`swarm_work` (nie dotyka Twojego Windowsowego working tree).

```powershell
# 1. token GitHuba (raz): skopiuj .env.example -> .env i wpisz GH_TOKEN
gh auth token                     # wartość do .env

# 2. zamknij hostowy dashboard (jeśli chodzi) — inaczej zajmie port 5178
# 3. zbuduj i włącz (restart: unless-stopped => wstaje z Dockerem)
docker compose up -d --build swarm

# podgląd logów / restart
docker compose logs -f swarm
```

- Obraz: .NET 10 SDK, Node 20, PowerShell 7, git, gh, opencode, Playwright
  (chromium) — implementer/reviewer/e2e mają wszystko, czego potrzebują.
- Auth: `GH_TOKEN` (gh + `git push`) oraz zmontowane z hosta `~/.config/opencode`
  i `~/.local/share/opencode` (config i auth opencode).
- Docker socket jest zamontowany, żeby `dotnet test` (Testcontainers) działał.
- e2e gada z `postgres` z compose przez `postgres__connectionString`
  (nadpisywane env-em), więc dev seed (`admin@dev.local` / `Passw0rd!`) działa.
- **Kontener klonuje repo z GitHuba**, więc najpierw wypchnij zmiany w
  `scripts/`, `.opencode/` i `docs/` — inaczej kontener widzi stary `master`.
- Zmienne: `SWARM_REPO_URL`, `SWARM_REPO_BRANCH`, `SWARM_AUTOSTART_DISPATCHER`,
  `DASHBOARD_PORT` (patrz `.env.example`).

## 9. Guardrails

- Max `-MaxRounds` rund review/e2e → fix; potem `ai:blocked` + komentarz.
- Nigdy push do `main`/`master` — zawsze branch + PR (default branch to `master`).
- Bramka obiektywna = CI (`ci.yml`: `dotnet build/test` + `npm run build`),
  potem review (subiektywna) i e2e (obserwacja UI).
- Bramka człowieka = merge PR `ai:ready`. `ai:auto-merge` jest zarezerwowany na
  przyszłość.
- `--auto` tylko na izolowanym klonie/runnerze.
- Reviewer i e2e dostają tylko artefakt (diff/PR), nie historię implementera.

## 10. Rollout

1. ~~Faza 1 — ręcznie.~~ 2. ~~Faza 2 — półautomat `agent-loop.ps1`.~~
3. **Faza 3 (obecna) — dyspozytor po labelach, e2e, autonomiczny tracker,
   merge za zgodą człowieka.**
4. Faza 4 — GitHub Actions event-driven (`issues.labeled` / `pull_request`),
   researcher na cronie, opcjonalny auto-merge zielonych.

## 11. Pliki

```
opencode.json                                  # model, instructions, permission, agent, mcp (playwright)
.opencode/agent/mes-{analyst,researcher,implementer,reviewer,verifier,tracker,e2e-tester}.md
.opencode/skills/mes-issue-spec/SKILL.md       # szablon spec/AC
.opencode/skills/mes-pr-review/SKILL.md        # checklista review (multi-tenancy)
.opencode/skills/mes-e2e/SKILL.md              # scope map + smoke + werdykt e2e
docs/feature-tracker.md                        # kanoniczna mapa zdolności
scripts/agent-dispatcher.ps1                   # dyspozytor (label state machine + tracker)
scripts/agent-loop.ps1                         # ręczny override na jedno issue
scripts/setup-labels.ps1                       # tworzy/aktualizuje labele workflow
scripts/e2e/app.ps1                            # start/stop/status stacku dla e2e
scripts/dashboard/server.mjs                   # panel: pipeline, dyspozytor, app, logi
scripts/dashboard/index.html                   # UI (prompty w DEFAULTS)
docker/swarm/Dockerfile                        # obraz swarm (dashboard+dyspozytor+agenty)
docker/swarm/entrypoint.sh                     # klon wolumenu + auth + start
.env.example                                   # GH_TOKEN i overrides dla compose
.github/workflows/ci.yml                       # bramka CI
.github/workflows/ai-implement.yml             # (faza 4, jeszcze nie ma)
```

## 12. Gotchas (handoff)

- **Default branch to `master`**, nie `main` — CI i guardraile łapią oba.
- **PowerShell + natywne komendy**: listy pól do `gh` cytuj jako `--json 'a,b'`;
  skrypty używają `$ErrorActionPreference = 'Continue'`, bo `opencode`/`gh`
  piszą na stderr.
- **Werdykt**: bierzemy **ostatnie** wystąpienie `VERDICT: ...`; dyspozytor
  dodatkowo fallbackuje do ostatniego komentarza PR, gdy agent nie wypisze go
  na stdout.
- **Config opencode nie jest hot-reloadowany** — zmiany `opencode.json`/agentów
  wymagają restartu; prompty dashboardu — tylko reload.
- **Dyspozytor jest jednowątkowy** — nie odpalaj dwóch naraz na tym samym repo.
- **e2e wymaga PostgreSQL** (dev seed `admin@dev.local` / `Passw0rd!`); bez DB
  werdykt to `E2E_BLOCKED`.
- Model `deepseek-v4.1-flash` radzi sobie z CRUD; trudniejsze taski (lifecycle,
  migracje) warto weryfikować.

## Powiązane dokumenty

- [`AGENT.md`](../AGENT.md) — do/don't dla agentów
- [`.github/copilot-instructions.md`](../.github/copilot-instructions.md) — kontekst projektu
- [`docs/glossary.md`](glossary.md) — słownik domenowy
- [`.github/instructions/`](../.github/instructions/) — instrukcje obszarowe
- [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — bramka CI
- [`scripts/agent-dispatcher.ps1`](../scripts/agent-dispatcher.ps1) — dyspozytor
