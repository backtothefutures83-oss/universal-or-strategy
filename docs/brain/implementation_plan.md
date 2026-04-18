# Implementation Plan: ADR-019 Sovereign Substrate Repair

- **Mission**: V12.15 Platinum Hardening -- surgical repair of 20 Red-Team-adjudicated substrate blockers.
- **Build tag delta**: `Build 1111.002-v28.0` -> `Build 1111.003-v28.0-adr019` (`src/V12_002.Constants.cs:12`).
- **Status**: ARENA ADJUDICATION PENDING. P4 Engineer handoff SUSPENDED per Director directive 2026-04-18.
- **Consensus gate**: 100% (Codex + Gemini + Jules must each independently APPROVE the full plan).
- **Architect**: Claude (P3). **Orchestrator**: Antigravity (P1). **Red Team**: Arena (P5).

---

## Section A -- Executive Summary

The V14.7-CORELANE-ULTRA substrate was adjudicated by a 14-model adversarial fleet at **11/14 (78.6%) readiness** and failed to clear the Sovereign gate. Three structural vulnerability classes were enumerated:

| #   | Class                          | Repairs                                                                                | Scope                                                                         |
| --- | ------------------------------ | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| 1   | Orphan Risk (kernel)           | 17 Red-Team-critical + 15 precautionary convergent = **32 lambda sites**               | `src/V12_002.*.cs` (10 files)                                                 |
| 2   | Infrastructure Gap (substrate) | **3 blockers** (devcontainer, hook gate, label-sync) + **2 portability supplementals** | `.devcontainer/`, `.github/`, `scripts/`, `Linting.csproj`, `deploy-sync.ps1` |
| 3   | Build Tag                      | **1 constant**                                                                         | `src/V12_002.Constants.cs:12`                                                 |

**Gate flow (new)**: P3 draft -> **P5 Arena Red Team audit (100% consensus)** -> P4 Codex implementation -> P5 UltraReview -> Director sign-off.

**Non-goals (explicit)**: no lock introduction, no refactor of lambda bodies, no rename of any public symbol, no change to the actor model, no touching of NinjaTrader DLL hints beyond the one user-profile path, no change to the hard-link `deploy-sync.ps1` contract.

---

## Section B -- Forensic Synthesis

### B.1 Logical proof of failure -- Orphan Risk

The strategy stores its termination state in a single volatile flag:

```csharp
// src/V12_002.cs:127
private volatile bool _isTerminating = false;
```

The flag is set exactly once, on `State.Terminated`:

```csharp
// src/V12_002.Lifecycle.cs:398-400
else if (state == State.Terminated)
{
    _isTerminating = true;
    StopWatchdog();
```

The actor-model discipline (CLAUDE.md: "No Internal Locks") requires all broker-thread callbacks to marshal to the strategy thread via `TriggerCustomEvent` or `InvokeAsync`. Two existing `InvokeAsync` lambdas at `src/V12_002.Lifecycle.cs:380-395` check the flag on entry:

```csharp
// src/V12_002.Lifecycle.cs:380-385 -- MIRROR PATTERN (already correct)
ChartControl.Dispatcher.InvokeAsync(() =>
{
    if (_isTerminating) return;
    AttachHotkeys();
    AttachChartClickHandler();
}, System.Windows.Threading.DispatcherPriority.Normal);
```

All other marshal lambdas lack the guard. Because `TriggerCustomEvent` is queued and can fire after `State.Terminated`, an order-submission, repair, or flatten operation can execute on a strategy that has already released its broker subscriptions -- producing a ghost order (fleet safety violation per CLAUDE.md "Ghost-Order Prevention" and Build 981 protocol).

### B.2 Portability Leak

Two files contain hardcoded Windows-specific paths that block environment-agnostic CI and violate the Sovereign Portability Standard. `deploy-sync.ps1` alone contains **four** hardcoded-path violations, not two as originally characterized:

- `Linting.csproj:37` -- hardcoded `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll`.
- `deploy-sync.ps1` -- full enumeration:

| Line | String                                                                             | Class                                          |
| ---- | ---------------------------------------------------------------------------------- | ---------------------------------------------- |
| 8    | `$RepoRoot = "C:\WSGTA\universal-or-strategy"`                                     | hardcoded repo path                            |
| 9    | `$NtCustomDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"`     | hardcoded user profile                         |
| 89   | `# Fix: run C:\tmp\byte_purge.py, then re-run deploy-sync.ps1`                     | comment points at non-existent tool path       |
| 99   | `Write-Host "  Fix: python C:\tmp\byte_purge.py  then re-run deploy-sync.ps1" ...` | error message points at non-existent tool path |

The tool `byte_purge.py` is **not present anywhere in the repo** (verified via filesystem search `**/byte_purge.py` -> 0 matches). The repo-canonical ASCII-purity tool is `check_ascii.py` at the repo root, referenced by CLAUDE.md section "CRITICAL: ASCII-Only in All C# String Literals". Lines 89 and 99 are dead references pointing at a path no portable machine will ever resolve; they must be re-pointed at the canonical tool as part of this repair.

(Note: `deploy-vm-safe.ps1:10` already uses `$env:USERPROFILE` -- that is the substitution model for user-profile paths. `$PSScriptRoot` is the substitution model for repo-anchored paths.)

### B.3 Infrastructure Gaps

- No `.devcontainer/` baseline. `/scripts/*.py`, `check_ascii.py`, and `scripts/install_hooks.ps1` require a reproducible Linux host for CI agents and Director Mode validation.
- No LFS/staged-file gate. `.gitattributes` is absent; `scripts/install_hooks.ps1` enforces lock-ban and ASCII but does not reject non-LFS binaries or files > 5 MB. Risk: a 50-100 MB binary committed accidentally wedges history and burns LFS quota.
- No `.github/workflows/label-sync.yml`. `.github/labeler.yml` maps files to labels reactively but nothing enforces the label manifest. A label deletion via the GitHub UI would silently prune SIMA / REAPER / IPC metadata.

### B.4 14-model consensus posture

| Model                   | Pre-repair verdict                              |
| ----------------------- | ----------------------------------------------- |
| Codex                   | P4-ENGINEER-READY                               |
| Gemini 4.7              | Gemini-4.7-CLI                                  |
| Jules                   | P5-AUDIT-VALIDATED                              |
| 11 others (Arena fleet) | VARIOUS -- 11/14 APPROVED, 3 REQUIRING REVISION |

Target post-repair: **14/14 APPROVED** (100% consensus gate).

---

## Section C -- Kernel Repair: Orphan Guard Injection (32 sites)

### C.1 Full site inventory

All 32 sites receive the same one-line guard. The 17 "Red-Team-critical" order-path sites are bolded; the 15 "precautionary convergent" sites also marshal work that can outlive termination (actor drain, UI compliance queue, IPC TCP callback, pump-chain continuation) and receive the same guard for uniform safety.

| #   | File                                            | Line | Enclosing Method                | Purpose                                               | Red-Team-Critical |
| --- | ----------------------------------------------- | ---- | ------------------------------- | ----------------------------------------------------- | ----------------- |
| 1   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 146  | `OnAccountOrderUpdate`          | `ProcessAccountOrderQueue` (broker callback enqueue)  | **Y**             |
| 2   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 162  | `ProcessAccountOrderQueue`      | reschedule on budget exhaustion                       | **Y**             |
| 3   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 173  | `ProcessAccountOrderQueue`      | re-enqueue on flatten contention                      | **Y**             |
| 4   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 181  | `ProcessAccountOrderQueue`      | drain remaining queue                                 | **Y**             |
| 5   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 369  | `HandleMatchedFollowerOrder`    | `SubmitFollowerReplacement` (FSM two-phase)           | **Y**             |
| 6   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 410  | `HandleMatchedFollowerOrder`    | `SubmitFollowerTargetReplacement`                     | **Y**             |
| 7   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 463  | `HandleMatchedFollowerOrder`    | `RestoreCascadedTargets` (stop-fill restore)          | **Y**             |
| 8   | `src/V12_002.Orders.Callbacks.AccountOrders.cs` | 591  | `ExecuteFollowerCascadeCleanup` | `EmergencyFlattenSingleFleetAccount` (CASCADE-FILLED) | **Y**             |
| 9   | `src/V12_002.Orders.Callbacks.cs`               | 389  | `HandleOrderCancelled`          | `RestoreCascadedTargets` (master-side)                | **Y**             |
| 10  | `src/V12_002.Orders.Callbacks.Execution.cs`     | 235  | `OnAccountExecutionUpdate`      | `UpdateAccountMetricsFromAccount`                     | **Y**             |
| 11  | `src/V12_002.REAPER.Audit.cs`                   | 136  | `AuditAccountState`             | `ProcessReaperRepairQueue` (flat desync repair)       | **Y**             |
| 12  | `src/V12_002.REAPER.Audit.cs`                   | 183  | `AuditAccountState`             | `ProcessReaperFlattenQueue` (critical desync)         | **Y**             |
| 13  | `src/V12_002.REAPER.Audit.cs`                   | 250  | `AuditAccountState`             | `ProcessReaperNakedStopQueue` (naked stop)            | **Y**             |
| 14  | `src/V12_002.REAPER.Audit.cs`                   | 327  | `AuditAccountState`             | `ProcessReaperFlattenQueue` (master flatten)          | **Y**             |
| 15  | `src/V12_002.REAPER.Audit.cs`                   | 372  | `AuditAccountState`             | `ProcessReaperNakedStopQueue` (master naked stop)     | **Y**             |
| 16  | `src/V12_002.SIMA.Dispatch.cs`                  | 60   | `ExecuteSmartDispatchEntry`     | deferred dispatch retry (semaphore contention)        | **Y**             |
| 17  | `src/V12_002.SIMA.Dispatch.cs`                  | 610  | `ExecuteSmartDispatchEntry`     | `PumpFleetDispatch` prime                             | **Y**             |
| 18  | `src/V12_002.SIMA.Flatten.cs`                   | 82   | `InitiateFlattenOps`            | `PumpFlattenOps` kickoff                              | N (precautionary) |
| 19  | `src/V12_002.SIMA.Flatten.cs`                   | 201  | `PumpFlattenOps`                | chain to next account                                 | N (precautionary) |
| 20  | `src/V12_002.SIMA.Flatten.cs`                   | 319  | `FlattenAccountPosition`        | re-kick on completion                                 | N (precautionary) |
| 21  | `src/V12_002.SIMA.Fleet.cs`                     | 174  | `PumpFleetDispatch`             | chain from finally                                    | N (precautionary) |
| 22  | `src/V12_002.SIMA.Fleet.cs`                     | 262  | `PumpFleetDispatch`             | chain after XorShadow CRC fail                        | N (precautionary) |
| 23  | `src/V12_002.SIMA.Lifecycle.cs`                 | 57   | `OnParameterChanged`            | `ProcessApplySimaState` deferred toggle               | N (precautionary) |
| 24  | `src/V12_002.Trailing.StopUpdate.cs`            | 64   | `OnOrderUpdate`                 | `RestoreCascadedTargets` (trailing restore)           | N (precautionary) |
| 25  | `src/V12_002.UI.Compliance.cs`                  | 286  | `OnAccountExecutionUpdate`      | `ProcessAccountExecutionQueue` (marshal)              | N (precautionary) |
| 26  | `src/V12_002.UI.Compliance.cs`                  | 304  | `ProcessAccountExecutionQueue`  | reschedule on budget                                  | N (precautionary) |
| 27  | `src/V12_002.UI.Compliance.cs`                  | 316  | `ProcessAccountExecutionQueue`  | flatten-contention bailout                            | N (precautionary) |
| 28  | `src/V12_002.UI.Compliance.cs`                  | 324  | `ProcessAccountExecutionQueue`  | drain remaining                                       | N (precautionary) |
| 29  | `src/V12_002.UI.IPC.cs`                         | 328  | `ProcessIpcCommands`            | reschedule IPC queue                                  | N (precautionary) |
| 30  | `src/V12_002.UI.IPC.Server.cs`                  | 277  | `OnIpcCommand`                  | TCP server callback -> strategy marshal               | N (precautionary) |
| 31  | `src/V12_002.cs`                                | 373  | `ScheduleActorDrain`            | `TryDrain` (actor mailbox)                            | N (precautionary) |
| 32  | `src/V12_002.REAPER.cs`                         | 132  | `ReaperAuditThread`             | `AuditApexPositions` (bg thread marshal)              | N (precautionary) |

### C.2 Surgical recipe (template)

Every site receives **one line** added as the first statement of the lambda body:

```
if (_isTerminating) return;  // ADR-019 orphan guard
```

No other modifications. No lock, no memory barrier, no refactor, no rename. The volatile field already provides the happens-before relationship on the strategy thread.

### C.3 Worked OLD/NEW blocks (3 representative cases)

#### Case 1 -- `src/V12_002.Orders.Callbacks.AccountOrders.cs:369` (FSM follower resubmit, highest cascade risk)

**OLD** (lines 367-383, verbatim):

```csharp
                    bool replacementScheduled = false;
                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            // ATR tick absorption may have updated PendingPrice/PendingQty after the
                            // lambda was scheduled -- using stale captures would submit wrong values.
                            SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                            _followerReplaceSpecs.TryRemove(sigName, out _);
                        }, null);
                        replacementScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }
```

**NEW**:

```csharp
                    bool replacementScheduled = false;
                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            if (_isTerminating) return;  // ADR-019 orphan guard
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            // ATR tick absorption may have updated PendingPrice/PendingQty after the
                            // lambda was scheduled -- using stale captures would submit wrong values.
                            SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                            _followerReplaceSpecs.TryRemove(sigName, out _);
                        }, null);
                        replacementScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }
```

#### Case 2 -- `src/V12_002.REAPER.Audit.cs:136` (REAPER repair queue from background audit thread)

**OLD** (lines 134-141, verbatim):

```csharp
                            _reaperRepairQueue.Enqueue(acct.Name);
                            // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                            try { TriggerCustomEvent(o => ProcessReaperRepairQueue(), null); }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }
```

**NEW**:

```csharp
                            _reaperRepairQueue.Enqueue(acct.Name);
                            // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                            try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessReaperRepairQueue(); }, null); }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }
```

Note: single-expression lambdas are expanded to a statement block to host the guard. No semantic change.

**Sister sites (same Transform B, identical surgical recipe, no further worked block needed):**

- `src/V12_002.REAPER.Audit.cs:183` -- `try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); }` --> `try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessReaperFlattenQueue(); }, null); }`
- `src/V12_002.REAPER.Audit.cs:250` -- `try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }` --> `try { TriggerCustomEvent(e => { if (_isTerminating) return; ProcessReaperNakedStopQueue(); }, null); }`
- `src/V12_002.REAPER.Audit.cs:327` -- `try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); }` --> `try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessReaperFlattenQueue(); }, null); }`
- `src/V12_002.REAPER.Audit.cs:372` -- `try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }` --> `try { TriggerCustomEvent(e => { if (_isTerminating) return; ProcessReaperNakedStopQueue(); }, null); }`

All four mirror Case 2 exactly. The lambda parameter name (`o` vs `e`) is preserved verbatim in each site -- do not normalize.

#### Case 3 -- `src/V12_002.UI.IPC.Server.cs:277` (TCP server callback to strategy marshal)

**OLD** (lines 272-280, verbatim):

```csharp
            Print(string.Format("V12.1 IPC ENQUEUE [client={0}] {1}", clientId, message));

            // Trigger processing
            try
            {
                TriggerCustomEvent(o => ProcessIpcCommands(), null);
            }
            catch { }
```

**NEW**:

```csharp
            Print(string.Format("V12.1 IPC ENQUEUE [client={0}] {1}", clientId, message));

            // Trigger processing
            try
            {
                TriggerCustomEvent(o => { if (_isTerminating) return; ProcessIpcCommands(); }, null);
            }
            catch { }
```

### C.4 Coverage pattern for the remaining 29 sites

For each remaining site, the engineer applies one of two transforms:

- **Transform A (statement-body lambda)**: add `if (_isTerminating) return;  // ADR-019 orphan guard` as the first line inside the `{ ... }` block. (Sites: 1, 2, 3, 4, 6, 7, 8, 9, 10, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 31, 32. -- 25 sites.)
- **Transform B (expression-body lambda `o => Method()`)**: rewrite to `o => { if (_isTerminating) return; Method(); }`. (Sites: 11, 12, 13, 14, 15, 30. Case 2 and sister sites above; Case 3. -- 6 sites.)

Plus Case 1 (site 5) = 1 Transform A already shown above. Total: **26 Transform A + 6 Transform B = 32 sites**, matching the C.1 inventory.

No existing comment, no existing `catch`, no outer block is touched. Each edit is strictly additive.

### C.5 Kernel verification (post-P4)

```
grep -n 'if (_isTerminating) return;  // ADR-019 orphan guard' src/   -- expect 26 hits (Transform A)
grep -cE 'o => \{ if \(_isTerminating\) return; [A-Za-z]+\(\); \}' src/  -- expect 6 hits (Transform B, includes 5 REAPER.Audit.cs sites)
grep -c 'if (_isTerminating) return;' src/V12_002.REAPER.Audit.cs  -- expect 5 hits (per-file REAPER coverage)
grep -n 'lock(stateLock)' src/   -- expect 0 hits (invariant)
```

---

## Section D -- Infrastructure Repair

### D.1 `.devcontainer/` (NEW) -- Zero-Standard Environment baseline

Create two files under `.devcontainer/` at repo root:

**`.devcontainer/devcontainer.json`** (full content):

```json
{
  "name": "V12 Sovereign Substrate",
  "build": { "dockerfile": "Dockerfile" },
  "features": {
    "ghcr.io/devcontainers/features/git-lfs:1": {},
    "ghcr.io/devcontainers/features/github-cli:1": {},
    "ghcr.io/devcontainers/features/powershell:1": { "version": "7.4" }
  },
  "remoteUser": "vscode",
  "postCreateCommand": "pwsh -NoProfile -File scripts/install_hooks.ps1",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csharp",
        "ms-vscode.powershell",
        "ms-python.python",
        "DavidAnson.vscode-markdownlint"
      ],
      "settings": { "editor.tabSize": 4, "files.eol": "\n" }
    }
  }
}
```

**`.devcontainer/Dockerfile`** (full content):

```dockerfile
FROM mcr.microsoft.com/devcontainers/dotnet:6.0-bookworm

RUN apt-get update && apt-get install -y --no-install-recommends \
        python3.11 python3-pip git-lfs \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

RUN git lfs install --system
```

Non-goals: no NinjaTrader DLL layer (proprietary, host-only). CI builds that require NinjaTrader linkage must run on the Windows runner (`dotnet-build.yml`); the devcontainer is for scripts, documentation, and P3/P5 tooling.

### D.2 `.github/workflows/label-sync.yml` + `.github/labels.yml` (NEW) -- label-pruning protection

**`.github/labels.yml`** (canonical manifest):

```yaml
- name: "SIMA / Fleet"
  color: "1d76db"
  description: "SIMA dispatch, flatten, or fleet logic"
- name: "Orders / Callbacks"
  color: "0e8a16"
  description: "Broker callbacks, FSM, follower replace"
- name: "REAPER"
  color: "b60205"
  description: "REAPER audit, repair, or flatten queue"
- name: "IPC"
  color: "5319e7"
  description: "UI.IPC server, client, or command pipeline"
- name: "UI / Compliance"
  color: "fbca04"
  description: "UI panel, compliance queue, or marshalling"
- name: "Core Strategy"
  color: "333333"
  description: "Lifecycle, OnBarUpdate, entry logic"
- name: "Deploy / Scripts"
  color: "c5def5"
  description: "deploy-sync, install_hooks, or scripts/*"
- name: "Workflows / CI"
  color: "ededed"
  description: "GitHub Actions or CI configuration"
- name: "Agent / Manifesto"
  color: "d4c5f9"
  description: ".agent/, _agents/, or standards_manifesto"
- name: "adr-019"
  color: "000000"
  description: "ADR-019 Sovereign Substrate Repair scope"
- name: "orphan-guard"
  color: "b60205"
  description: "Orphan-order guard injection"
- name: "substrate-repair"
  color: "1d76db"
  description: "Devcontainer, hooks, label-sync, portability"
```

**`.github/workflows/label-sync.yml`** (workflow):

```yaml
name: Label Sync
on:
  push:
    branches: [main]
    paths: [".github/labels.yml"]
  workflow_dispatch: {}
permissions:
  issues: write
jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: EndBug/label-sync@v2
        with:
          config-file: .github/labels.yml
          delete-other-labels: false
```

`delete-other-labels: false` preserves any label the Director added by hand (e.g., `priority:high`, `wontfix`). The sync is additive only; it will never prune. Pruning is an explicit Director action through a manual `workflow_dispatch` with a flag flip (future ADR if needed).

### D.3 `scripts/install_hooks.ps1` (AMEND) + `.gitattributes` (NEW) -- Hook gate

The current hook enforces (a) `lock(stateLock)` ban, (b) ASCII on staged `.cs`. Amendment adds two new gates:

**`.gitattributes`** (NEW, full content):

```
*.dll filter=lfs diff=lfs merge=lfs -text
*.exe filter=lfs diff=lfs merge=lfs -text
*.bin filter=lfs diff=lfs merge=lfs -text
*.onnx filter=lfs diff=lfs merge=lfs -text
*.pt filter=lfs diff=lfs merge=lfs -text
*.pdb filter=lfs diff=lfs merge=lfs -text
```

**`scripts/install_hooks.ps1`** amendment -- append the following two gates to the generated `.git/hooks/pre-commit` body, before the final `exit 0`:

```bash
# ADR-019: LFS pointer gate -- reject non-LFS binary file types
for staged in $(git diff --cached --name-only --diff-filter=ACM); do
    case "$staged" in
        *.dll|*.exe|*.bin|*.onnx|*.pt|*.pdb)
            head -c 50 "$staged" | grep -q "version https://git-lfs.github.com/spec/v1" || {
                echo "[HOOK] ADR-019 REJECT: $staged is a tracked binary type but is not an LFS pointer."
                echo "       Run: git lfs track '$staged' ; git add .gitattributes ; git add '$staged'"
                exit 1
            }
            ;;
    esac
done

# ADR-019: 5MB size gate -- reject any non-LFS staged file > 5 MiB
FIVE_MB=5242880
for staged in $(git diff --cached --name-only --diff-filter=ACM); do
    if [ -f "$staged" ]; then
        head -c 50 "$staged" | grep -q "version https://git-lfs.github.com/spec/v1" && continue
        size=$(wc -c < "$staged")
        if [ "$size" -gt "$FIVE_MB" ]; then
            echo "[HOOK] ADR-019 REJECT: $staged is $size bytes (> 5 MiB) and not tracked via LFS."
            exit 1
        fi
    fi
done
```

Preserves existing lock-ban + ASCII gates. `head -c 50` matches the LFS pointer prefix `version https://git-lfs.github.com/spec/v1` without dereferencing the file. No change to hard-link behavior or the Post-Edit Deployment Protocol (CLAUDE.md).

### D.4 Portability bundle (supplementary)

#### D.4.a `Linting.csproj:37`

**OLD**:

```xml
      <HintPath>C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll</HintPath>
```

**NEW**:

```xml
      <HintPath>$(UserProfile)\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll</HintPath>
```

MSBuild resolves `$(UserProfile)` identically to `%USERPROFILE%` on Windows and to `$HOME` on Linux/macOS when .NET is cross-running. No other line in `Linting.csproj` is touched (the `C:\Program Files\NinjaTrader 8\bin\...` entries on lines 33, 41, 45, 49, 53, 57 stay as-is; they reference the system install and are portable for this mission).

#### D.4.b `deploy-sync.ps1` (four-path portability repair)

**Sub-block 1 -- lines 8-9** (variable declarations):

**OLD** (verbatim from the live file):

```powershell
$RepoRoot = "C:\WSGTA\universal-or-strategy"
$NtCustomDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"
```

**NEW**:

```powershell
$RepoRoot = $PSScriptRoot
$NtCustomDir = Join-Path $env:USERPROFILE "Documents\NinjaTrader 8\bin\Custom"
```

`$PSScriptRoot` resolves to the directory containing `deploy-sync.ps1`, which is the repo root. This matches the model already used at `deploy-vm-safe.ps1:10` and lets any Director Mode machine run the script without editing. Note: the actual file uses `$RepoRoot` and `$NtCustomDir`, not `$Source`/`$Target` -- earlier drafts of this plan cited the wrong variable names; the OLD/NEW above is verified against the live source.

**Sub-block 2 -- lines 89, 99** (dead-reference error messages pointing at non-existent `C:\tmp\byte_purge.py`):

**OLD** (verbatim from the live file):

```powershell
# Fix: run C:\tmp\byte_purge.py, then re-run deploy-sync.ps1
```

```powershell
        Write-Host "  Fix: python C:\tmp\byte_purge.py  then re-run deploy-sync.ps1" -ForegroundColor Red
```

**NEW**:

```powershell
# Fix: run `python (Join-Path $PSScriptRoot 'check_ascii.py') src/`, then re-run deploy-sync.ps1
```

```powershell
        Write-Host "  Fix: python $(Join-Path $PSScriptRoot 'check_ascii.py') src/  then re-run deploy-sync.ps1" -ForegroundColor Red
```

`byte_purge.py` does not exist anywhere in the repository (verified via filesystem search). `check_ascii.py` is the repo-canonical ASCII-purity tool per CLAUDE.md section "CRITICAL: ASCII-Only in All C# String Literals". Anchoring the path at `$PSScriptRoot` (the same anchor used for `$RepoRoot` in sub-block 1) guarantees the error message resolves on any machine where the repo is cloned, regardless of drive letter, username, or install location. No other error-message strings in the file reference `C:\` paths.

---

## Section E -- Build Tag Bump

**File**: `src/V12_002.Constants.cs:12`.

**OLD**:

```csharp
        public const string BuildTag = "Build 1111.002-v28.0";
```

**NEW**:

```csharp
        public const string BuildTag = "Build 1111.003-v28.0-adr019";
```

Per CLAUDE.md "Naming Conventions", build-tag increment is MANDATORY for every production delivery. The `-adr019` suffix lets the Arena reliably identify the post-repair binary.

---

## Section F -- Verification Matrix (must pass before Arena APPROVED)

| #   | Gate                                    | Check                                                                         | Expected                                   |
| --- | --------------------------------------- | ----------------------------------------------------------------------------- | ------------------------------------------ |
| 1   | ASCII purity (C#)                       | `python check_ascii.py src/`                                                  | zero findings                              |
| 2   | Lock-ban                                | `grep -n 'lock(stateLock)' src/`                                              | zero hits                                  |
| 3   | Guard coverage (Transform A)            | `grep -c 'if (_isTerminating) return;  // ADR-019 orphan guard' src/`         | 26 hits                                    |
| 3b  | REAPER.Audit.cs per-file coverage       | `grep -c 'if (_isTerminating) return;' src/V12_002.REAPER.Audit.cs`           | 5 hits                                     |
| 4   | Guard coverage (Transform B)            | `grep -cE 'o => \{ if \(_isTerminating\) return; [A-Za-z]+\(\); \}' src/`     | 6 hits                                     |
| 5   | Devcontainer presence                   | `test -f .devcontainer/devcontainer.json && test -f .devcontainer/Dockerfile` | exit 0                                     |
| 6   | Label-sync presence                     | `test -f .github/workflows/label-sync.yml && test -f .github/labels.yml`      | exit 0                                     |
| 7   | LFS config presence                     | `test -f .gitattributes`                                                      | exit 0                                     |
| 8   | Hook amendment                          | `grep -q "ADR-019: LFS pointer gate" .git/hooks/pre-commit`                   | exit 0                                     |
| 9   | Hook live test (LFS)                    | stage a non-LFS `*.dll`                                                       | hook rejects with ADR-019 message          |
| 10  | Hook live test (size)                   | stage a non-LFS file > 5 MiB                                                  | hook rejects with ADR-019 message          |
| 11  | Portability (Linting)                   | `grep -c 'C:\\Users\\Mohammed' Linting.csproj`                                | zero hits                                  |
| 12  | Portability (deploy) -- user profile    | `grep -nE 'C:\\\\Users\\\\' deploy-sync.ps1`                                  | zero hits                                  |
| 12b | Portability (deploy) -- repo/tool paths | `grep -nE 'C:\\\\(WSGTA\|tmp)\\\\' deploy-sync.ps1`                           | zero hits                                  |
| 12c | Portability (deploy) -- positive check  | `grep -cE '\$PSScriptRoot\|\$env:USERPROFILE\|GetFolderPath' deploy-sync.ps1` | >= 3 hits                                  |
| 13  | Build tag                               | `grep -n '1111.003-v28.0-adr019' src/V12_002.Constants.cs`                    | one hit on line 12                         |
| 14  | Deploy sync round-trip                  | `pwsh -File ./deploy-sync.ps1` then F5 in NT                                  | ASCII gate PASS, banner shows new BuildTag |

---

## Section G -- Handoff Block -> P5 ARENA (Red Team Audit)

**P4 Codex is SUSPENDED. The block below is for the Director to paste into Antigravity/Arena to run the Red Team audit. No implementation begins until every agent returns APPROVED on every target.**

```
# =======================================================================
# P5 ARENA RED TEAM AUDIT -- ADR-019 Sovereign Substrate Repair
# =======================================================================
# MISSION: Adversarial audit of the ADR-019 structural plan.
# TARGET: docs/brain/implementation_plan.md @ branch mission-uni-5-full-sync
# HANDOFF STATUS: P4 ENGINEER SUSPENDED -- 100% CONSENSUS GATE IN EFFECT
# SIMULATION BAN: CLAUDE.md NO SIMULATION -- every verdict must cite an
#        authentic local log, file write, or tool output.
# GITHUB-LINK PROTOCOL: all citations MUST reference GitHub URLs to the
#        branch file (not raw inline code) per CLAUDE.md.
# =======================================================================

RED TEAM TASKS (each agent runs ALL six):

1. Kernel guard surface (32 sites)
   - Challenge: Is any of the 32 sites mis-scoped? Any lambda body where
     `if (_isTerminating) return;` on the first line skips critical
     cleanup (e.g., releasing a reservation, clearing an in-flight flag)
     that must still run after State.Terminated?
   - Specifically audit sites #5 (AccountOrders.cs:369) and #11
     (REAPER.Audit.cs:136) for reservation-leak after guard short-circuit.
   - Challenge Transform B: does the rewritten expression body preserve
     exception semantics exactly?

2. _isTerminating race
   - Challenge: volatile read on strategy thread vs volatile write on
     lifecycle transition -- is there any call site where the marshal
     lambda can read the flag BEFORE the Lifecycle.cs:400 write but
     EXECUTE after the broker subscription is released? Name the site
     and the window.

3. Devcontainer spec
   - Challenge: dotnet:6.0 matches NinjaTrader 8; confirm PowerShell 7.4
     runs install_hooks.ps1 on the Linux image without Windows-only
     cmdlets. Is any Python dependency in scripts/*.py missing from the
     Dockerfile?

4. Hook amendment
   - Challenge: Can the 5 MiB gate false-positive on legitimate binaries
     (e.g., a graphify-out/graph.json > 5 MiB)? The gate only skips
     already-LFS-pointer files; is that the correct behavior given that
     graphify-out/ is not LFS-tracked?
   - Challenge: LFS pointer detection uses `head -c 50` and a grep
     pattern. Is that the canonical byte prefix or can a legitimate LFS
     pointer evade it?

5. Label-sync manifest
   - Challenge: Is any label currently in use on an open issue or PR
     missing from .github/labels.yml? If `delete-other-labels: false` is
     set, is there any drift risk we are missing?

6. Portability substitutions
   - Challenge: Does `$(UserProfile)` in MSBuild resolve identically to
     `%USERPROFILE%` on Windows AND on the Linux devcontainer (where
     NinjaTrader linkage is not attempted)? Does `$PSScriptRoot` behave
     identically whether deploy-sync.ps1 is invoked from the repo root
     or via an absolute path?

7. Adversarial scenario (open-ended)
   - Design ONE corner case per category (kernel / devcontainer / hook /
     label-sync / portability) where the plan fails. If ANY model
     produces a VALID failure mode, CONSENSUS = FAIL and the plan
     returns to P3 for revision.

RETURN FORMAT (per agent, separate message):

  AGENT: <codex|gemini|jules>
  TARGET-1 GUARD-SURFACE: APPROVED | REVISION REQUIRED
    <citations: GitHub URLs with line anchors>
  TARGET-2 ISTERMINATING-RACE: APPROVED | REVISION REQUIRED
    <citations>
  TARGET-3 DEVCONTAINER: APPROVED | REVISION REQUIRED
    <citations>
  TARGET-4 HOOK: APPROVED | REVISION REQUIRED
    <citations>
  TARGET-5 LABEL-SYNC: APPROVED | REVISION REQUIRED
    <citations>
  TARGET-6 PORTABILITY: APPROVED | REVISION REQUIRED
    <citations>
  TARGET-7 ADVERSARIAL SCENARIO:
    <one failure mode per category>
  OVERALL: APPROVED | REVISION REQUIRED

CONSENSUS GATE:
  - All 3 agents must return APPROVED on targets 1-6.
  - Target 7 produces failure modes; if any are VALID, plan returns to P3.
  - Task-splitting during audit is STRICTLY FORBIDDEN.
  - If any agent is unreachable, the Director is notified; audit pauses.

NEXT STEPS (post-APPROVED):
  - P3 updates docs/brain/nexus_a2a.json status to P4_EXECUTION_AUTHORIZED.
  - P4 Codex receives handoff with the exact 32-site list + infra designs.
  - P5 UltraReview re-audits after Codex finishes implementation.
  - Director signs off after Build 1111.003-v28.0-adr019 clears regression.
# =======================================================================
```

---

## Section H -- Architect's sign-off

- **P3 (Claude)**: plan drafted per Director directive 2026-04-18. Handoff target is P5 Arena (NOT P4 Engineer). No `src/` edits performed this session.
- **Next action**: Director pastes Section G block into Antigravity to trigger the 14-model adversarial fleet audit.
- **Rollback**: if the Arena rejects, the plan returns to P3 for revision; no implementation has been performed, so rollback is trivial.

End of plan.
