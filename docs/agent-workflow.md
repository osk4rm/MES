# MES Agent Swarm — autonomiczny workflow wieloagentowy

Plan wdrożenia w pełni autonomicznego, wieloagentowego cyklu wytwarzania
AsistOff MES przy użyciu opencode. Cel: eksperyment „totalnie autonomicznej
implementacji przez AI" — od pomysłu/researchu, przez issue, implementację,
review, aż po zielony PR.

Status: **scaffold gotowy (faza 1–2)**. Utworzone: `opencode.json`,
`.opencode/agent/mes-{analyst,researcher,implementer,reviewer,verifier}.md`,
`.opencode/skills/mes-{issue-spec,pr-review}/`, `scripts/agent-loop.ps1`,
labele GitHub `ai:implement` / `ai:blocked`. GitHub Actions = faza 4 (jeszcze nie).
Decyzje: orkiestracja lokalnym skryptem PowerShell, model domyślny
`opencode-go/deepseek-v4.1-flash` wszędzie.

## 0. Szybki start

```powershell
# 0. zrestartuj opencode, żeby wczytał opencode.json i agentów (config nie jest hot-reloadowany)
opencode agent list            # sanity check: powinny być mes-analyst/.../mes-verifier

# 1. wygeneruj propozycje (bez labela ai:implement)
opencode run --agent mes-researcher "Zaproponuj 3 kolejne inkrementalne funkcje MES."

# 2. zaakceptuj wybrane issue ręcznie
gh issue edit <N> --add-label "ai:implement"

# 3. odpal pętlę (implement -> review -> fix, max 3 rundy)
pwsh -File scripts/agent-loop.ps1 -MaxRounds 3

# 4. ręcznie zmerguj zielony PR; issue z labelem ai:blocked wymaga Twojej uwagi

# 5. (opcjonalnie) uzgodnij feature tracker z GitHubem
opencode run --agent mes-tracker "Uzgodnij docs/feature-tracker.md z GitHubem i otwórz PR."
```

Uwaga: `-Auto` (auto-approve uprawnień) tylko na izolowanym klonie.

---

## 1. Model mentalny

Nie budujemy jednego długiego agenta robiącego wszystko. Budujemy **bezmózgich,
jednorolowych workerów**, a całą inteligencję workflow i pamięć trzymamy
w **GitHubie** (issue, PR, komentarze review, labele, CI).

Konsekwencje:

- **Kontekst jest izolowany z definicji** — każdy worker to świeża sesja
  `opencode run`, seedowana wyłącznie: swoim promptem + plikami `instructions`
  + treścią artefaktu (issue/PR).
- **Stan przechodzi przez artefakty, nie przez historię czatu.** To jest
  mechanizm „pamiętania o contextach agentów": handoff = issue body / PR
  description / review comment, nie wspólny wątek.
- Każdy worker jest niezależny, wymienny i testowalny osobno.

## 2. Roster agentów

| Agent | Rola | Mode | Uprawnienia | Trigger |
|---|---|---|---|---|
| `mes-researcher` | **Szerokość**: przegląda domenę (wiersze `gap` z trackera **oraz** nowe pomysły MES/ISA-95) → proponuje 1–2 issue, **bez** labela | primary | read-only + `webfetch`/`websearch` + `gh issue create` | cyklicznie / ręcznie |
| `mes-analyst` | **Głębokość**: bierze JEDEN pomysł (propozycję lub issue #N) → finalna specyfikacja z AC + **nadaje** `ai:implement` | primary | read-only; `bash` tylko `gh issue *` | ręcznie / po researchu |
| `mes-implementer` | Bierze issue → kod BE+FE+testy → `dotnet build/test`, `npm run build` → branch + PR | primary | `edit: allow`, `bash: allow` (deny: push do main, force push, `rm -rf`) | label `ai:implement` |
| `mes-reviewer` | Review diffa PR względem `AGENT.md`/multi-tenancy → `gh pr review` | subagent | `edit: deny`; `bash` tylko `gh pr *`, `git diff` | PR opened / label `ai:review` |
| `mes-verifier` (opcjonalny) | Niezależnie weryfikuje „czy testy nie są oszukane", dorzuca brakujące przypadki | subagent | `edit` tylko w `tests/**`; bash: testy | po review |
| `mes-tracker` | Uzgadnia `docs/feature-tracker.md` z GitHubem i kodem; jedyny writer trackera | all | `edit` tylko tracker; `gh`/`git` | po pętli / cyklicznie |

Testy pisze **implementer** (zgodnie z wymaganiem). `mes-verifier` to
bezpiecznik anty-cheat (wykrywanie wyłączonych/usuniętych testów), a nie
codzienny krok.

## 3. Pętla ping-pong

```
mes-researcher ──> issue (#N) ──label ai:implement──> mes-implementer
                                                        │  branch + PR
                                                        ▼
                                             CI: build + test (bramka obiektywna)
                                                        │
                                         label ai:review ▼
                                                   mes-reviewer ──> gh pr review (comment / request-changes)
                                                        │
                       ┌──────── jeśli changes requested (max 3 rundy) ────────┐
                       ▼                                                       │
             mes-implementer (--continue TEJ SAMEJ sesji, zna swój kod) ──────┘
                       │
                       ▼  CI green + review approved
                merge (start: bramka człowieka; potem auto-merge)
```

Trik kontekstowy:

- **Implementer wraca do tej samej sesji** przy poprawkach po review
  (`opencode run --continue` / `-s <sessionID>`) — ma już kod w kontekście.
- **Reviewer zawsze startuje świeżo** — musi być niezależny; dostaje tylko diff.
- Analyst / researcher zawsze świeżo.

## 4. Orkiestracja

Rekomendowany start: **lokalny skrypt PowerShell** (`scripts/agent-loop.ps1`).
Działa na Twoim opencode, używa Twoich modeli, daje pełną kontrolę i widoczność
każdego kroku. GitHub Actions (event-driven) to faza późniejsza — te same pliki
agentów, inny „silnik".

### 4.1 Szkic sterownika

```powershell
# scripts/agent-loop.ps1
$maxRounds = 3
$round = 0
$sessionId = $null

# 1. Wybierz issue do implementacji
$issue = gh issue list --label "ai:implement" --json number,title,body |
         ConvertFrom-Json | Select-Object -First 1
if (-not $issue) { Write-Host "Brak issue z label ai:implement"; exit 0 }

# 2. Implementacja (świeża sesja), tworzy branch + PR
$implOut = opencode run --agent mes-implementer --format json `
    "Zaimplementuj issue #$($issue.number). Przeczytaj je przez 'gh issue view $($issue.number)'. " `
    "Po implementacji uruchom 'dotnet build/test' i 'npm run build', potem otwórz PR." |
    Out-String
# wyciągnij numer PR i sessionID z JSON eventów (sessionID potrzebny do --continue)

# 3. Pętla review -> fix
while ($round -lt $maxRounds) {
    $round++
    $review = opencode run --agent mes-reviewer --format json `
        "Zrób review PR #$pr. Tylko diff. Werdykt przez 'gh pr review'." | Out-String

    if ($review -match "approved") { break }

    # implementer kontynuuje SWOJĄ sesję
    opencode run --continue --session $sessionId `
        "Popraw PR #$pr wg review z 'gh pr view $pr --comments'. Uruchom testy."
}

# 4. Eskalacja
if ($round -ge $maxRounds) { gh issue edit $issue.number --add-label "ai:blocked" }
```

Uwaga: dokładny sposób wyciągnięcia `sessionID` i numeru PR zależy od formatu
`--format json` — do dopracowania przy implementacji fazy 2.

### 4.2 GitHub Actions (faza 4)

Te same agenty odpalane z workflow na event `issues.labeled` / `pull_request`.
Zalety: działa bez Twojej maszyny, naturalna izolacja, CI na tej samej maszynie.
Wady: sekret API key w repo, wolniejsze. Nie robimy tego na starcie.

### 4.3 Wariant odrzucony

Jeden primary „orchestrator" wołający subagenty przez Task tool — dobre do demo,
ale jedna długa sesja puchnie kontekstowo i nie przetrwa restartu. Nie na produkcję.

## 5. Strategia kontekstu

- Globalnie w `opencode.json` → `instructions`: `AGENT.md`,
  `.github/copilot-instructions.md`, `docs/glossary.md`. Każdy agent dostaje to
  automatycznie.
- Instrukcje obszarowe (`architecture`, `database`, `frontend`,
  `production-recipes`) ładuj **per-agent**, nie globalnie:
  - implementer: wszystkie,
  - reviewer: `architecture` + `api` + `testing`,
  - researcher/analyst: żadne (potrzebują glossary, nie kodu).
- Research zewnętrzny (MCP `context7`, GitHub MCP, `webfetch`) tylko dla
  researcher/analyst.
- **Nigdy nie reużywaj sesji między różnymi rolami.** Jedyna legalna
  kontynuacja: implementer ↔ implementer po review.
- Duże pliki (glossary, instrukcje) wchodzą przez `instructions`/`references`,
  nie przez wklejanie do promptu.

## 5.1 Feature tracker (żeby nie skanować repo od zera)

`docs/feature-tracker.md` to kanoniczna, trwała mapa zdolności systemu:
`done` / `partial` / `proposed` / `in-progress` / `gap`, z numerami issue/PR.

- **researcher / analyst** czytają tracker **najpierw** i nie skanują całego
  repo — pracują po wierszach `gap` i tylko weryfikują pojedyncze wiersze.
- **mes-tracker** to **jedyny writer** trackera: uzgadnia statusy z GitHubem i
  kodem, publikuje przez PR na branchu `ai/tracker-sync`.
- **implementer / reviewer** nie dotykają trackera (minimalne PR-y).

Uruchomienie: `opencode run --agent mes-tracker "..."` lub
`pwsh -File scripts/agent-loop.ps1 -SyncTracker`.

## 6. Pliki (utworzone; Actions = faza 4)

```
opencode.json                                  # model, instructions, permission, agent, mcp
.opencode/agent/mes-analyst.md
.opencode/agent/mes-researcher.md
.opencode/agent/mes-implementer.md
.opencode/agent/mes-reviewer.md
.opencode/agent/mes-verifier.md
.opencode/agent/mes-tracker.md
.opencode/skills/mes-issue-spec/SKILL.md       # szablon spec/AC dla analityka
.opencode/skills/mes-pr-review/SKILL.md        # checklista review (multi-tenancy!)
docs/feature-tracker.md                        # kanoniczna mapa zdolności
scripts/agent-loop.ps1                         # orkiestrator
scripts/dashboard/server.mjs                    # lokalny dashboard (API, zero deps)
scripts/dashboard/index.html                   # UI dashboardu (prompty w DEFAULTS)
.github/workflows/ai-implement.yml             # (faza 4)
```

### 6.1 Szkic `opencode.json`

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "model": "opencode-go/deepseek-v4.1-flash",
  "instructions": ["AGENT.md", ".github/copilot-instructions.md", "docs/glossary.md"],
  "permission": {
    "bash": { "*": "allow", "rm -rf *": "deny", "git push --force*": "deny", "git push -f*": "deny" },
    "edit": "allow",
    "external_directory": "allow",
    "webfetch": "allow",
    "websearch": "allow"
  }
}
```

> Uwaga: to globalny, wygodny profil „bez pytań". Ograniczenia per-agent
> (m.in. `deny` na push do `main`/`master`) żyją w plikach `.opencode/agent/*.md`
> i **nadpisują** ten globalny profil.

### 6.2 Szkic `mes-reviewer.md`

```markdown
---
description: Reviews PRs for AsistOff MES against AGENT.md and multi-tenancy rules.
mode: subagent
model: opencode-go/deepseek-v4.1-flash
permission:
  edit: deny
  bash: { "*": ask, "git diff*": allow, "gh pr diff*": allow, "gh pr review*": allow, "gh pr view*": allow }
---
Jesteś surowym reviewerem AsistOff MES. Sprawdzasz wyłącznie diff PR-a:
- ITenantRequest albo IAllowAnonymousRequest na każdym nowym MediatR request,
- ISaasy + brak ręcznych predykatów TenantId,
- typed exceptions (NotFoundException / ValidationException / ...),
- brak `any` w TypeScript, `<script setup lang="ts">`,
- brak zmian w niepowiązanych testach/migracjach,
- obecne i sensowne testy dla nowego zachowania,
- brak nowych hardcode'owanych permissions/tenantów/roli.
Werdykt wystawiasz przez `gh pr review` (comment lub request-changes) z konkretnymi
plikami i liniami. NIE edytujesz kodu.
```

## 7. Guardrails

- Max 3 rundy review → fix; potem label `ai:blocked` i eskalacja do człowieka.
- Nigdy push do `main`/`master` — zawsze branch + PR. (Default branch tego repo to `master`.)
- Bramka obiektywna = CI (`ci.yml`: `dotnet build/test` + `npm run build`).
  Reviewer to bramka subiektywna.
- Budżet: limit tokenów/kosztu na issue; monitoring przez `opencode stats`.
- `--auto` (auto-approve) tylko na izolowanym klonie/runnerze, nigdy na
  roboczym repo.
- Reviewer dostaje **tylko diff**, nie historię implementera → niezależność.

## 8. Rollout fazami

1. **Faza 1 — ręcznie:** odpalasz agentów pojedynczo, dopracowujesz prompty.
2. **Faza 2 — półautomat:** `scripts/agent-loop.ps1` na jednym issue, max 1 runda.
3. **Faza 3 — pełna pętla:** 3 rundy + `mes-verifier`, merge za zgodą człowieka.
4. **Faza 4 — autonomicznie:** GitHub Actions event-driven, researcher na cronie,
   auto-merge zielonych.

## 9. Mierzalne KPI eksperymentu

- odsetek issue domkniętych bez interwencji człowieka,
- średnia liczba rund review→fix,
- koszt tokenów na PR (`opencode stats`),
- odsetek PR-ów z zielonym CI za pierwszym razem,
- odsetek zmian odrzuconych przez review (jakość implementera).

## 10. Dashboard (lokalny panel)

Prosty panel do odpalania agentów i podglądu stanu — zero zależności (czysty Node):

```powershell
node scripts/dashboard/server.mjs      # -> http://127.0.0.1:5178
```

- pokazuje: branch, liczbę zmienionych plików, otwarte PR-y `ai/*`, issues
  `ai:implement` / `ai:blocked`, uruchomione procesy, live logi;
- pozwala odpalić dowolnego agenta oraz pętlę (`agent-loop.ps1`) z UI;
- domyślne prompty siedzą w `scripts/dashboard/index.html` (`DEFAULTS`) — edycja
  bez restartu serwera (wystarczy odświeżyć stronę);
- logi runów trafiają do `%TEMP%\opencode\*.log`.

## 11. Stan sesji i gotchas (handoff)

**Stan:** PR #82 (issue #81, reason codes) — review `APPROVED`, gotowy do merge
przez człowieka. Otwarte propozycje: #80 (Production Order), #83 (work-center
calendars), #84 (downtime events), #85 (lot registry). CI naprawione (działa
na `master`).

**Znane pułapki:**

- **Default branch to `master`**, nie `main` — CI i guardraile muszą łapać oba.
- **PowerShell + natywne komendy**: listy pól do `gh` cytuj jako `--json 'a,b'`
  (bez cudzysłowów PS rozbija je na dwa argumenty); `agent-loop.ps1` używa
  `$ErrorActionPreference = 'Continue'`, bo `opencode`/`gh` piszą na stderr.
- **Werdykt review**: `Get-Verdict` bierze **ostatnie** wystąpienie
  `VERDICT: ...` (wcześniej łapał fałszywy `CHANGES_REQUESTED` z rozumowania
  reviewera → zbędna runda).
- **Logowanie dashboardu**: output przechwytujemy pipe-em w Node z `*>&1`
  (samo `*>` do pliku nie łapało outputu `opencode`).
- **Config opencode nie jest hot-reloadowany**: zmiany `opencode.json`/agentów
  wymagają restartu opencode; zmiany promptów w dashboardzie — tylko reload.
- Model `deepseek-v4.1-flash` radzi sobie z CRUD; trudniejsze taski (lifecycle,
  migracje) warto weryfikować.

**Następne kroki:** zmergować #82; oznaczyć #83/#84/#85 labelem `ai:implement`
i odpalić pętlę; odpalić `mes-tracker`; rozważyć auto-merge zielonych PR-ów.

---

## Powiązane dokumenty

- [`AGENT.md`](../AGENT.md) — do/don't dla agentów
- [`.github/copilot-instructions.md`](../.github/copilot-instructions.md) — kontekst projektu
- [`docs/glossary.md`](glossary.md) — słownik domenowy
- [`.github/instructions/`](../.github/instructions/) — instrukcje obszarowe
- [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — bramka CI
- [`scripts/dashboard/server.mjs`](../scripts/dashboard/server.mjs) — lokalny dashboard
