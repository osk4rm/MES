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
# researcher dopisuje nowe gapy do trackera; analyst tworzy issue + ai:implement -> reszta sama

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
| `ai:verify` | PR po review, do weryfikacji testów (anty-cheat) | dyspozytor |
| `ai:changes` | reviewer/verifier/e2e/CI żąda poprawek | dyspozytor |
| `ai:e2e` | PR gotowe do smoke e2e (Playwright) | dyspozytor |
| `ai:ready` | CI + review + verify + e2e zielone; do merge przez człowieka | dyspozytor |
| `ai:blocked` | eskalacja do człowieka (limit rund, brak werdyktu, brak PR) | dyspozytor |
| `ai:auto-merge` | opt-in na przyszły auto-merge (jeszcze nieaktywny) | człowiek |

`ai:running` jest jednocześnie lockiem (dyspozytor jest jednowątkowy) i
znacznikiem widoczności. Po restarcie dyspozytor zdejmuje **tylko wygasłe**
locki (TTL `-RunningTtlMinutes`); świeże zostawia. Agenci nie dotykają labeli
— stary tekst o `mes-implementer` w tej roli był nieaktualny.

## 3. Roster agentów

| Agent | Rola | Mode | Trigger | Kto odpala |
|---|---|---|---|---|
| `mes-researcher` | **Szerokość**: nowe pomysły MES → nowe wiersze `gap` w trackerze (**bez** issue) | primary | ręcznie / cyklicznie | dashboard |
| `mes-analyst` | **Głębokość**: pierwszy wykonalny `gap` z trackera (lub najstarsza nieolabelowana propozycja) → issue z AC + `ai:implement` | primary | ręcznie | dashboard |
| `mes-implementer` | issue → kod BE+FE+testy → PR | primary | `ai:implement` / `ai:changes` | dyspozytor |
| `mes-reviewer` | diff PR względem `AGENT.md` → werdykt | all | `ai:review` + CI zielone | dyspozytor |
| `mes-verifier` | anty-cheat: czy testy naprawdę dowodzą AC (read-only w automacie) → werdykt | all | `ai:verify` | dyspozytor (automatycznie) / ręcznie |
| `mes-e2e-tester` | Playwright smoke (cały system lub obszar PR) → werdykt | all | `ai:e2e` | dyspozytor / dashboard |
| `mes-tracker` | jedyny writer `docs/feature-tracker.md` | all | **autonomicznie** (dyspozytor) | dyspozytor |

`mes-e2e-tester` używa Playwright MCP (skonfigurowany w `opencode.json`).

## 4. Pętla

```
researcher ──> tracker `gap` rows ──analyst──> issue [ai:implement]
                                                       │
                                                       ▼  label ai:implement
                                            mes-implementer ─> PR (ai/issue-N-*)
                                                       │
                               PR opened ──> dyspozytor: +ai:review
                                                       ▼  ai:review + CI zielone
                                            mes-reviewer ─> komentarz VERDICT
                                        ┌──────────────┴───────────────┐
                              CHANGES_REQUESTED                 APPROVED
                                        │                             │
                          implementer (ta sama sesja)        dyspozytor: +ai:verify
                                        │                             ▼
                                        │                 mes-verifier (read-only)
                                        │              ┌─────────┴──────────┐
                                        │     TESTS_INSUFFICIENT      TESTS_SOUND
                                        │              │                    ▼
                                        │              │         dyspozytor: +ai:e2e
                                        │              │                    ▼
                                        │              │   mes-e2e-tester (Playwright)
                                        │              │  ┌──────────┴──────────┐
                                        │              │FAIL                   PASS
                                        └──────────────┘                  +ai:ready
                                      round++                        merge (człowiek)
                                        │
                                 limit rund -> ai:blocked

  równolegle, autonomicznie:  zmiana zbioru work-itemów ─> mes-tracker ─> PR ai/tracker-sync
```

Trik kontekstowy: implementer wraca do **tej samej sesji** przy poprawkach
(`--session`, mapa issue→sessionID w stanie dyspozytora); reviewer, verifier
i e2e zawsze startują świeżo (niezależność).

Werdykty są ścisłe: dyspozytor parsuje `VERDICT: ...` i wymaga **jednoznaczności**
— brak werdyktu albo kilka różnych werdyktów w jednym output/komentarzu
(`AMBIGUOUS`) eskaluje do `ai:blocked`. Agenci mają pisać werdykt w osobnej linii
i nie cytować alternatywy.

## 5. Orkiestracja — dyspozytor

`scripts/agent-dispatcher.ps1` to lokalny, jednowątkowy daemon: co
`-IntervalSeconds` (domyślnie 20 s) czyta stan GitHuba i wykonuje **jedną**
akcję, wybierając wg priorytetu: `ai:changes` → `ai:review` (jeśli CI zielone)
→ `ai:verify` → `ai:e2e` → `ai:implement`.

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
| `-RunningTtlMinutes` | 30 | locki `ai:running` starsze niż tyle są uznawane za osierocone i czyszczone przy starcie; świeże są zostawiane (mogą należeć do żywego agenta) |

Zachowanie przy błędach:

- CI czerwone na `ai:review` → dyspozytor traktuje to jak `ai:changes`
  (implementer naprawia), bez marnowania review.
- Implementer nie otworzył PR → `ai:blocked` + komentarz.
- Reviewer/e2e bez parsowalnego werdyktu → `ai:blocked`.
- Przekroczony `-MaxRounds` → `ai:blocked` (+ komentarz) na PR i issue.

Stan (sesje, liczniki rund, timestampy locków `ai:running`, ostatni sync
trackera) trzymany w `%TEMP%\opencode\dispatcher-state.json`, więc restart nie
gubi rund. Przy starcie dyspozytor czyści **tylko wygasłe** locki `ai:running`
(starsze niż `-RunningTtlMinutes` wg timestampu lokalnego i `updatedAt` z GitHuba);
świeże locki zostawia — mogą należeć do żywego agenta na innym hoście.
Nie odpalaj dwóch dyspozytorów na tym samym repo.

### 5.1 Ręczny override

`scripts/agent-loop.ps1` zostaje jako jednorazowa pętla na jedno issue
(przydatne do debugowania promptów). Nie używa labeli stanu — to „ręczny bieg".

## 6. Feature tracker — autonomiczny

`docs/feature-tracker.md` to kanoniczna mapa zdolności (`done` / `partial` /
`proposed` / `in-progress` / `gap`).

- researcher / analyst czytają tracker **najpierw** i nie skanują repo.
- `mes-researcher` tylko **dopisuje** nowe wiersze `gap` (nie tworzy issue).
- `mes-analyst` bierze pierwszy wykonalny `gap` (wszystkie zależności `done`)
  albo najstarszą nieolabelowaną propozycję i tworzy issue z `ai:implement`.
- `mes-tracker` to **jedyny writer** statusów i work itemów. Dyspozytor uruchamia go sam:
  - gdy zmieni się **sygnatura** zbioru otwartych issue/PR (nowe issue,
    zmiana labeli z wyłączeniem tranzytowego `ai:running`, merge/zamknięcie)
    i minął cooldown, albo
  - gdy od ostatniego sync minął `-TrackerIntervalMinutes`.
- Sync jest **pomijany przy brudnym working tree** (`git status --porcelain` niepuste)
  — tracker wymaga czystego drzewa, więc dyspozytor loguje `tracker skipped` i próbuje
  w następnym cyklu, zamiast marnować run agenta.
- Sync publikuje PR na branchu `ai/tracker-sync` bazującym na **default branch repo**
  (`gh repo view --json defaultBranchRef`, obecnie `master` — nie zakładaj `main`;
  jeśli branch/PR już istnieje, aktualizuje go zamiast otwierać nowy).
  Tracker PR nie jest liczony do sygnatury, żeby nie wywołać pętli.
- implementer / reviewer / e2e nie dotykają trackera (minimalne PR-y).

## 7. End-to-end (Playwright)

`mes-e2e-tester` + skill **mes-e2e**:

- scope = `gh pr diff --name-only` zmapowany na obszary (tabela w skillu);
  zmiany w routerze / `http.ts` / layoutcie / auth → **cały system**;
- stack: `pwsh -File scripts/e2e/app.ps1 -Action start|stop|status`
  (backend `:5243`, frontend `:5173`, login `admin@dev.local` / `Passw0rd!`);
- wymagania lokalne (DB `docker compose up -d postgres`, porty, precedence
  env > user secrets): **`docs/e2e-local-setup.md`** (single source of truth
  dla skryptu i skilla);
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

- Max `-MaxRounds` rund review/verify/e2e → fix; potem `ai:blocked` + komentarz.
- Nigdy push do `main`/`master` — zawsze branch + PR (default branch to `master`).
- Bramka obiektywna = CI (`ci.yml`: `dotnet build/test` + `npm run build`),
  potem review (subiektywna) i e2e (obserwacja UI).
- Bramka człowieka = merge PR `ai:ready`. `ai:auto-merge` jest zarezerwowany na
  przyszłość.
- `--auto` tylko na izolowanym klonie/runnerze.
- Reviewer i e2e dostają tylko artefakt (diff/PR), nie historię implementera.

## 10. Rollout

1. ~~Faza 1 — ręcznie.~~ 2. ~~Faza 2 — półautomat `agent-loop.ps1`.~~
3. ~~Faza 3 — dyspozytor po labelach, e2e, autonomiczny tracker.~~
4. **Faza 4 (obecna) — GitHub Actions event-driven (patrz §13), lokalny
   dyspozytor jako fallback/debug, merge za zgodą człowieka.**

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
.github/workflows/ai-swarm.yml                 # faza 4: implement/review/verify/fix/e2e + cron researcher/tracker
scripts/ci/swarm-lib.sh                        # wspólne helpery CI (locki, labele, werdykty, CI wait)
```

## 12. Gotchas (handoff)

- **Default branch wykrywaj dynamicznie** (`gh repo view --json defaultBranchRef`;
  obecnie `master`), nie zakładaj `main` — CI łapie oba, ale skrypty i agenci muszą
  bazować na faktycznym defaulcie.
- **PowerShell + natywne komendy**: listy pól do `gh` cytuj jako `--json 'a,b'`;
  skrypty używają `$ErrorActionPreference = 'Continue'`, bo `opencode`/`gh`
  piszą na stderr.
- **Werdykt musi być jednoznaczny**: wiele różnych `VERDICT: ...` w jednym
  outpucie to `AMBIGUOUS` → `ai:blocked`. Przy braku werdyktu na stdout
  dyspozytor i CI fallbackują do komentarzy PR.
- **Config opencode nie jest hot-reloadowany** — zmiany `opencode.json`/agentów
  wymagają restartu; prompty dashboardu — tylko reload.
- **Dyspozytor jest jednowątkowy** — nie odpalaj dwóch naraz na tym samym repo.
  Lock `ai:running` ma TTL (`-RunningTtlMinutes`, default 30 min): restart czyści
  tylko wygasłe locki, świeże zostawia. Sygnatura trackera ignoruje `ai:running`,
  a tracker skipuje się przy brudnym drzewie.
- **e2e wymaga PostgreSQL** (dev seed `admin@dev.local` / `Passw0rd!`); bez DB
  werdykt to `E2E_BLOCKED`.
- Model `opencode/muse-spark-1.3-contributor-free` (Muse Spark 1.3 Free) jest
  defaultem dla wszystkich agentów (`opencode.json` + `.opencode/agent/*.md`);
  trudniejsze taski (lifecycle, migracje) warto weryfikować verifierem.

## 13. Faza 4 — event-driven (GitHub Actions)

`.github/workflows/ai-swarm.yml` robi to samo co lokalny dyspozytor, ale
zamiast pollingu reaguje na eventy. Wspólne helpery (locki, labele, ścisłe
werdykty, czekanie na CI) żyją w `scripts/ci/swarm-lib.sh`.

| Event | Job | Efekt |
|---|---|---|
| issue `labeled ai:implement` | `implement` | implementer → PR + `ai:review` (brak PR → `ai:blocked`) |
| PR `labeled ai:review` / `synchronize` z `ai:review` / koniec CI (`workflow_run`) | `review` | czeka na CI (max 10 min) → reviewer → `ai:verify` / `ai:changes` |
| PR `labeled ai:verify` | `verify` | verifier read-only → `ai:e2e` / `ai:changes` |
| PR `labeled ai:changes` | `fix` | guard rund (liczy failure-verdykty w komentarzach, limit `MAX_ROUNDS=3`) → implementer fix → `ai:review` |
| PR `labeled ai:e2e` | `e2e` | Postgres service + stack + tester → `ai:ready` / `ai:changes` / `ai:blocked` |
| PR `labeled ai:ready` | `merge` | czeka na CI → squash-merge + delete-branch (czerwone CI → z powrotem `ai:review`, konflikt → `ai:blocked`) |
| push na default / cron co 30 min | `sweep` | najstarszy `ai:implement` bez locka wraca do kolejki, gdy jest wolny slot |
| push na default / cron co 30 min | `analyst` | pusta kolejka + backlog < `BACKLOG_MAX=5` + gap/proposal w zasięgu = `mes-analyst` specuje następną pracę (labeluje tylko pierwszy odblokowany); inaczej zielone wyjście bez sesji agenta |
| cron pn 06:00 UTC | `researcher` | gap rows → zwykły PR do mergu przez człowieka |
| cron codziennie 05:30 UTC | `tracker` | sync trackera → PR `ai/tracker-sync` |
| `workflow_dispatch` | dowolny | ręczny trigger (zastępuje przyciski dashboardu w CI) |

Zasady:

- **Sekrety**: `OPENCODE_API_KEY` (opencode.ai/auth) w Settings → Secrets →
  Actions. Bez niego joby padają z jawnym błędem. `GITHUB_TOKEN` jest automatyczny.
- **`SWARM_PAT` (zdecydowanie zalecane w publicznym repo)**: fine-grained PAT
  (Settings → Developer settings → Personal access tokens → Fine-grained,
  tylko to repo: Contents read+write, Pull requests read+write, Issues
  read+write) zapisany jako sekret `SWARM_PAT`. Workflow używa go do operacji
  `gh` (`GH_TOKEN: SWARM_PAT || GITHUB_TOKEN`), więc PR-y otwiera collaborator,
  a nie `github-actions[bot]` — bez tego każdy bot-PR staje na „Approve and
  run", a runy po approve **nie emitują eventów `workflow_run`**, więc kolejka
  cichnie (review czeka → timeout → stoi). Bez sekretu wszystko dalej działa,
  tylko z ręcznym approve.
- **Przegrany wyścig o lock wychodzi na zielono**: dwa joby na ten sam
  item (np. `labeled` + koniec CI naraz) — posiadacz locka pracuje, drugi kończy
  `exit 0` z notką w logu. Jeśli coś wisi w `ai:running` bez żywego runa,
  człowiek zdejmuje labelkę i dokłada trigger z powrotem.
- **Checkbox**: Settings → Actions → General → Workflow permissions → zaznacz
  **„Allow GitHub Actions to create and approve pull requests"**. Bez tego
  implement/researcher/tracker nie otworzą PR-a (API odmawia
  `createPullRequest`). Gdy brakuje, job sam przechodzi w `ai:blocked`
  z instrukcją, a nie wiesza się ani nie mieli minut.
- **Concurrency**: jedna kolejka na issue/PR (`cancel-in-progress: false`) —
  odpowiednik jednowątkowego dyspozytora. `ai:running` jest lockiem między
  runnerem CI a lokalnym dyspozytorem: przegrany wyścig kończy się zielono
  (`exit 0` z notką), ponawiasz go zdejmując i dokładając label-trigger.
- **Limit równoległości (`MAX_PARALLEL=3`)**: implement i sweep odmawiają nowej
  pracy, gdy ≥3 itemy trzymają `ai:running`. Odmowa to zielone wyjście —
  labelka `ai:implement` zostaje, a sweep (push na default + cron co 30 min)
  dobiera najstarszy czekający issue bez locka. Limit widać w dashboardzie
  (badge „kolejka"). Lokalny dyspozytor limitu nie egzekwuje — to rola CI.
- **Auto-merge**: `ai:ready` + zielone CI = squash-merge z kasowaniem brancha,
  bez człowieka. Dashboard pokazuje `ready` do momentu mergu.
- **Fast-path dla docs-only**: review `APPROVED` + diff tylko `docs/**`/`*.md`
  = prosto do `ai:ready` (bez verify/e2e — nie ma runtime'u do testowania).
  Weryfikator i tester słusznie odmawiają klepnięcia pustki (`E2E_BLOCKED`),
  więc takie PR-y nie jadą dalej torem kodowym.
- **Puszujący to współpracownik, nie bot**: joby implement/fix przepinają
  `origin` na `SWARM_PAT`, bo push tokenem `GITHUB_TOKEN` (aktor
  `github-actions[bot]`) zawiesza każdy run CI w `action_required`
  (wymaga kliknięcia approve) i pętla fixów nigdy nie widzi zielonego.
- **Czekanie na CI patrzy tylko na workflow `ci`** dla head SHA danego PR-a.
  Własne checki `ai-swarm` są ignorowane — szum z labelki-locka potrafił
  stworzyć kolejkujący się no-op run, którego pending zatruwał wait
  (samozakleszczenie kończące się timeoutem i demotowaniem `ai:ready`).
- **Auto-cleanup locków**: sweep czyści `ai:running` starsze niż
  `STALE_LOCK_MINUTES=45` (znacznik = czas labela w timeline). Zgubiony job
  nie blokuje slotu w nieskończoność.
- **Serializacja migracji EF**: max 1 PR z plikami `Migrations/` w locie.
  Dwa równoległe PR-y z migracjami zipper-mergują się w
  `DefaultContextModelSnapshot.cs` i psują mastera. Implement i sweep czekają
  aż migracyjny PR się zmerguje (PR-y z `ai:blocked` nie blokują kolejki).
- **Integralność merge'y** (guardrails anty-„dziwne konflikty z masterem"):
  - joby mergujące (`implement`, `fix`) robią `git fetch` + `git merge
    origin/master` NA PEŁNYM klonie (`fetch-depth: 0` — płytki klon nie ma
    merge-base i git wymyśla fikcyjne konflikty); konflikty trafiają do
    agenta z kontraktem (`swarm_conflict_rules`): nigdy nie zostawiać
    markerów konfliktu, snapshotu EF nie wolno mergować ręcznie
    (regeneracja przez `dotnet ef migrations remove/add --context
    DefaultContext`), a pliki rejestrowe (`i18n.ts`, `sitemap.ts`,
    `ApiContracts.cs`) mergujemy jako unię obu stron.
  - przed pushem `swarm_check_conflict_markers` — jakikolwiek `<<<<<<<`,
    `=======` czy `>>>>>>>` w drzewie zatrzymuje PR (runda naprawcza, potem
    `ai:blocked`). Ten sam check jest w `ci.yml` (job `guard`), więc
    zepsuta resolucja nigdy nie przejdzie na zielono.
  - `fix` checkoutuje gałąź PR-a (`refs/pull/N/head`), NIE `refs/pull/N/merge`
    — merge ref nie istnieje, gdy PR konfliktuje z masterem, a to właśnie
    taki PR fix musi naprawić. Review/verify/e2e przed użyciem merge refa
    sprawdzają `swarm_pr_mergeable` i przy `CONFLICTING` odsyłają do `ai:changes`.
  - `ai:ready` + `gh pr merge` z konfliktem = `ai:changes` (nie `ai:blocked`);
    tylko nie-konfliktowe błędy mergu idą do człowieka.
- **Ostatni werdykt wygrywa**: fallback werdyktu z komentarzy PR-a bierze
  OSTATNI (`swarm_last_verdict_stdin`), nie „więcej niż jeden = AMBIGUOUS" —
  PR z `CHANGES_REQUESTED` w rundzie 1 i `APPROVED` w rundzie 2 jest
  zaaprobowany, nie zablokowany.
- **Red CI wraca do pętli**: `review` nasłuchuje też `workflow_run` z
  `conclusion == failure` — wcześniej czerwone CI po timeoutcie waita
  zostawiało PR w `ai:review` na zawsze.
- **Samouzupełniająca kolejka**: pusty `ai:implement` + backlog poniżej
  `BACKLOG_MAX=5` + gap w trackerze lub nielabelowany proposal = job `analyst`
  sam startuje `mes-analyst` w CI. Pętla nie staje po wyczerpaniu issuesów;
  gdy tracker nie ma gapów ani proposali, job kończy się zielono bez odpalania
  agenta (tania bramka w bashu, nie sesja).
- **Krojenie issuesów** (reguły w `mes-issue-spec` + `mes-analyst`): jeden issue
  = jeden PR do zreviewowania w <30 min. Duże tematy to serie `(1/3)` z
  `depends on`, jedna migracja EF na serię (pierwszy slice) — równoległe PR-y
  z migracjami konfliktują snapshot.
- **CI fast-path**: PR-y tylko-dokumentacyjne (`*.md`, `docs/**`) skipują joby
  backend/frontend przez `dorny/paths-filter` (run zielony w ~1 min);
  `swarm_wait_ci` traktuje skip jako pass. `cancel-in-progress` kasuje
  zdezaktualizowane runy po nowym pushu.
- **`--auto`**: runnery CI to izolowane klony, więc agenci lecą z
  `opencode --auto` (permission files dalej bronią pusha na default branch).
- **Brak session affinity w CI**: fix w CI startuje świeżą sesję z promptem
  „przeczytaj komentarze PR" (lokalny dyspozytor trzyma `--session`
  implementera — patrz §4).
- **E2E w CI**: service `postgres:16` na `localhost:5432`, backend dostaje
  `postgres__connectionString` env-em; stack startuje się z `nohup` w tym samym
  kroku co agent (procesy przeżywają tool calle). `E2E_BLOCKED` (problem
  infrastruktury) = `ai:ready` z notką, nie `ai:blocked` — review + verify
  już przeszły. Bez DB werdykt to `E2E_BLOCKED`.
- Lokalny dyspozytor zostaje jako **fallback/debug** — nie odpalaj go
  równolegle z zielonym CI na tych samych labelach, bo będziecie się mijać
  lockami (to akurat bezpieczne, ale hałaśliwe).

## Powiązane dokumenty

- [`AGENT.md`](../AGENT.md) — do/don't dla agentów
- [`.github/copilot-instructions.md`](../.github/copilot-instructions.md) — kontekst projektu
- [`docs/glossary.md`](glossary.md) — słownik domenowy
- [`.github/instructions/`](../.github/instructions/) — instrukcje obszarowe
- [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — bramka CI
- [`.github/workflows/ai-swarm.yml`](../.github/workflows/ai-swarm.yml) — faza 4 event-driven
- [`scripts/ci/swarm-lib.sh`](../scripts/ci/swarm-lib.sh) — helpery CI
- [`scripts/agent-dispatcher.ps1`](../scripts/agent-dispatcher.ps1) — dyspozytor (fallback)
