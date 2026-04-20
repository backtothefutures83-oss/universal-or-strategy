# Implementation Plan: ADR-019-v2 Sovereign Substrate Repair (Full, Zero-Truncation)

- **Mission tag**: `Build 1111.002-v28.0` -> `Build 1111.003-v28.0-adr019` (`src/V12_002.Constants.cs:12`).
- **Tracks**: 4 (Infrastructure, Photon Ring, Path Portability, Kernel Orphan Guards).
- **Sites (Track 4)**: 34 TriggerCustomEvent lambdas = 27 Type 1 + 7 Class A. Zero pre-guarded TCE sites.
- **Status**: P5 REAUDIT CORRECTIONS APPLIED 2026-04-19. Ready for Codex P4 execution.
- **Author (P3)**: Claude Opus 4.7. **Executor (P4)**: Codex.

---

## A. Executive Summary

1. Replace the structurally unsafe prior Transform A2 with TWO recipes: **A1** (simple early-return, 27 Type 1 sites) and **A2-R** (cleanup-preserving early-return, 7 Class A sites). Every TCE lambda must release every reservation it owns even when `_isTerminating` flips between scheduling and lambda execution.
2. Harden the Photon SPSC ring buffer with ONE trailing pad (F-001) and ONE `Thread.MemoryBarrier()` (F-002). DEBUG-only thread-affinity assertions reinforce the pool's single-writer contract (F-003, F-004). AMAL budget preserved.
3. Eliminate every hardcoded `C:\Users\Mohammed Khalid\...` / `C:\Program Files\NinjaTrader 8\...` reference from `deploy-sync.ps1`, `Linting.csproj`, `scripts/install_hooks.ps1`, and runtime scripts. After this track, the repo compiles and deploys on any Windows user account without edits. This also eliminates the bash-quoting hazard previously seen when those paths leaked into verification shell commands.
4. Ship a hardened `.git/hooks/pre-commit` (5 MB size gate + LFS-aware + ASCII purity + lock-ban), canonical `.github/labels.yml` catalog, `label-sync` workflow, and `.devcontainer/` for reproducible dev. Migrate `GCP_PROJECT_ID` to a GitHub secret.
5. Bump the build tag as the final commit: `Build 1111.003-v28.0-adr019`.

---

## B. Track 1 -- Infrastructure

### B.1 `.git/hooks/pre-commit` body (POSIX `/bin/sh`, Git-for-Windows compatible)

Emitted by `scripts/install_hooks.ps1` (see D.3). No bashisms. 5 MB size gate + LFS-awareness + ASCII purity + lock-ban.

```sh
#!/bin/sh
# V12 Pre-Commit Safety Hook (ADR-019-v2)
# Gates: (1) lock() ban, (2) ASCII purity, (3) 5MB file-size gate, (4) LFS-awareness
# POSIX /bin/sh, Git-for-Windows compatible (BusyBox sh). No bashisms.

echo "--- V12 Pre-Commit Gate ---"
REPO_ROOT=$(git rev-parse --show-toplevel)

# Gate 1: Lock-free audit (ADR-002)
LOCK_HITS=$(grep -rl "lock(" "$REPO_ROOT/src/" 2>/dev/null | wc -l)
if [ "$LOCK_HITS" -gt "0" ]; then
    echo "PRE-COMMIT FAIL: lock() found in src/ -- BANNED by Platinum Standard."
    grep -rl "lock(" "$REPO_ROOT/src/"
    exit 1
fi

# Gate 2: ASCII purity on staged .cs files
STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM | grep "\.cs$")
if [ -n "$STAGED_CS" ]; then
    python "$REPO_ROOT/check_ascii.py" $STAGED_CS || {
        echo "PRE-COMMIT FAIL: Non-ASCII detected in staged C# files."
        exit 1
    }
fi

# Gate 3: 5MB file-size gate (LFS-aware)
MAX_BYTES=5242880
STAGED=$(git diff --cached --name-only --diff-filter=ACM)
if [ -z "$STAGED" ]; then
    echo "--- V12 Pre-Commit Gate: PASS (no staged files) ---"
    exit 0
fi

FAIL=0
for f in $STAGED; do
    [ -f "$REPO_ROOT/$f" ] || continue

    # Gate 4: LFS-awareness -- exempt files marked with filter=lfs
    FILTER=$(git check-attr -z -- filter "$f" | tr -d '\000' | sed 's/.*filter//')
    case "$FILTER" in
        *lfs*) continue ;;
    esac

    SIZE=$(wc -c < "$REPO_ROOT/$f")
    if [ "$SIZE" -gt "$MAX_BYTES" ]; then
        echo "PRE-COMMIT FAIL: $f is ${SIZE} bytes (> ${MAX_BYTES}). Use Git LFS or split the asset."
        FAIL=1
    fi
done

if [ "$FAIL" -ne 0 ]; then
    exit 1
fi

echo "--- V12 Pre-Commit Gate: PASS ---"
exit 0
```

### B.2 `.github/workflows/label-sync.yml` (NEW)

```yaml
name: Label Catalog Sync

on:
  push:
    branches: [main, mission-uni-5-full-sync]
    paths:
      - .github/labels.yml
      - .github/workflows/label-sync.yml
  workflow_dispatch:

permissions:
  issues: write
  pull-requests: write
  contents: read

jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: EndBug/label-sync@v2
        with:
          config-file: .github/labels.yml
          delete-other-labels: false
          request-delay: 100
          token: ${{ secrets.GITHUB_TOKEN }}
```

### B.3 `.github/labels.yml` (NEW -- 13-entry canonical catalog)

```yaml
- name: SIMA
  color: 4169E1
  description: SIMA dispatch, fleet, lifecycle
- name: REAPER
  color: 8B0000
  description: REAPER audit, repair, naked-stop
- name: IPC
  color: 2E8B57
  description: IPC server, commands, UI bridge
- name: Orders
  color: FFA500
  description: Order callbacks, management, gateways
- name: Core
  color: 555555
  description: V12_002 core strategy path
- name: UI
  color: 9932CC
  description: UI compliance, panels, handlers
- name: Safety
  color: DC143C
  description: Watchdog, MetadataGuard, Symmetry
- name: Symmetry
  color: 20B2AA
  description: Symmetry FSM, bracket, follower, replace
- name: Photon
  color: 00CED1
  description: Photon ring, pool, MMIO mirror
- name: Trailing
  color: 808000
  description: Trailing, stop update, breakeven
- name: Build
  color: 696969
  description: Build, version bump, constants
- name: Deploy
  color: 4682B4
  description: Deploy scripts, hooks, sync
- name: ADR-019
  color: FF4500
  description: Orphan Guard Injection
```

### B.4 `.devcontainer/devcontainer.json` (NEW)

```json
{
  "name": "Universal OR Strategy V12 (NinjaTrader 8 dev)",
  "build": { "dockerfile": "Dockerfile" },
  "features": {
    "ghcr.io/devcontainers/features/powershell:1": {},
    "ghcr.io/devcontainers/features/python:1": { "version": "3.11" }
  },
  "postCreateCommand": "pwsh -Command \"./scripts/install_hooks.ps1\"",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csharp",
        "ms-dotnettools.csdevkit",
        "ms-vscode.powershell",
        "ms-python.python"
      ]
    }
  }
}
```

### B.5 `.devcontainer/Dockerfile` (NEW)

```dockerfile
# escape=`
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8-windowsservercore-ltsc2022

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

RUN Invoke-WebRequest -Uri https://www.python.org/ftp/python/3.11.8/python-3.11.8-amd64.exe -OutFile py.exe ; `
    Start-Process -FilePath py.exe -ArgumentList '/quiet','InstallAllUsers=1','PrependPath=1' -Wait ; `
    Remove-Item py.exe

WORKDIR C:\workspace
```

---

## C. Track 2 -- Photon Ring Buffer

### C.1 F-001 -- Trailing pad after `_consumerCursor`

**File**: `src/V12_002.Photon.Ring.cs`, lines 22-28.

**OLD** (verbatim, lines 22-28):
```csharp
            // Cache-line isolation: 7 long pads between cursors. False-sharing hurts
            // only throughput, not correctness. Both cursors are Volatile-fenced.
            private long _producerCursor;
#pragma warning disable 0169
            private long _pad1, _pad2, _pad3, _pad4, _pad5, _pad6, _pad7;
#pragma warning restore 0169
            private long _consumerCursor;
```

**NEW**:
```csharp
            // ADR-019-v2 [F-001]: cache-line isolation on BOTH sides of each cursor.
            // Leading _padA1..7 separate _producerCursor from the CLR object header.
            // Trailing _padB1..7 separate _consumerCursor from any heap neighbour.
            // x64 cache line = 64 B. 7 longs + 1 cursor = 64 B exactly.
            private long _producerCursor;
#pragma warning disable 0169
            private long _padA1, _padA2, _padA3, _padA4, _padA5, _padA6, _padA7;
#pragma warning restore 0169
            private long _consumerCursor;
#pragma warning disable 0169
            private long _padB1, _padB2, _padB3, _padB4, _padB5, _padB6, _padB7;
#pragma warning restore 0169
```

Note: `_pad1..7` rename to `_padA1..7`. Grep confirms the names are not referenced outside this file; rename is safe.

### C.2 F-002 -- `Thread.MemoryBarrier()` in `TryDequeue`

**File**: `src/V12_002.Photon.Ring.cs`, lines 64-77.

**OLD** (verbatim):
```csharp
            public bool TryDequeue(out T item)
            {
                long cons = Volatile.Read(ref _consumerCursor);
                long prod = Volatile.Read(ref _producerCursor);
                if (cons >= prod)
                {
                    item = default(T);
                    return false; // ring empty
                }
                int idx = (int)(cons & _mask);
                item = _buffer[idx];
                Volatile.Write(ref _consumerCursor, cons + 1); // consume barrier
                return true;
            }
```

**NEW**:
```csharp
            public bool TryDequeue(out T item)
            {
                long cons = Volatile.Read(ref _consumerCursor);
                long prod = Volatile.Read(ref _producerCursor);
                if (cons >= prod)
                {
                    item = default(T);
                    return false; // ring empty
                }
                int idx = (int)(cons & _mask);
                // ADR-019-v2 [F-002]: full fence between producer-cursor read and slot read.
                Thread.MemoryBarrier();
                item = _buffer[idx];
                Volatile.Write(ref _consumerCursor, cons + 1); // consume barrier
                return true;
            }
```

### C.3 F-003 -- DEBUG thread-affinity assertion on `ExecutionIdRing.ContainsOrAdd`

**File**: `src/V12_002.Photon.Pool.cs`, line 216 (top of method body).

**OLD** (verbatim, lines 216-220):
```csharp
            public bool ContainsOrAdd(long hash)
            {
                if (hash == EMPTY_KEY) hash = 1L;

                int bucket = (int)(hash & _tableMask);
```

**NEW**:
```csharp
            public bool ContainsOrAdd(long hash)
            {
#if DEBUG
                // ADR-019-v2 [F-003]: reinforce single-writer contract (Pool.cs:70-73).
                int _tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (_probeOwnerTid != 0 && _probeOwnerTid != _tid)
                    throw new InvalidOperationException("ExecutionIdRing cross-thread access: owner=" + _probeOwnerTid + " caller=" + _tid);
                _probeOwnerTid = _tid;
#endif
                if (hash == EMPTY_KEY) hash = 1L;

                int bucket = (int)(hash & _tableMask);
```

Add private field at the top of class `ExecutionIdRing` (before line 176's `private readonly long[] _ringHashes;`):
```csharp
#if DEBUG
            private int _probeOwnerTid;
#endif
```

### C.4 F-004 -- DEBUG thread-affinity assertion on `PhotonOrderPool.Claim` and `ReleaseByIndex`

**File**: `src/V12_002.Photon.Pool.cs`.

Insert as FIRST statement of `public PoolClaimResult Claim()` (after line 101's brace):
```csharp
#if DEBUG
                // ADR-019-v2 [F-004]: reinforce single-writer contract (Pool.cs:70-73).
                int _tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (_poolOwnerTid != 0 && _poolOwnerTid != _tid)
                    throw new InvalidOperationException("PhotonOrderPool cross-thread access: owner=" + _poolOwnerTid + " caller=" + _tid);
                _poolOwnerTid = _tid;
#endif
```

Repeat verbatim as FIRST statement of `public void ReleaseByIndex(int slotIndex)` (after line 130's brace).

Add private field at top of class `PhotonOrderPool` (before line 77's `private readonly Order[][] _orderArrays;`):
```csharp
#if DEBUG
            private int _poolOwnerTid;
#endif
```

**Rationale (F-003/F-004)**: the pool is documented single-threaded at `V12_002.Photon.Pool.cs:70-73`. Real Interlocked CAS / ABA tags would blow the AMAL budget (allocation stays 0 but cycle count triples). A DEBUG-only assertion traps the first cross-thread call; RELEASE strips it.

---

## D. Track 3 -- Path Portability (Bash-Quoting Hazard Eliminated)

**Root cause**: `C:\Users\Mohammed Khalid\` contains a space. Any downstream script, shell command, or log message that embeds this path without double-quoting fails in bash (interpreted as two tokens). The fix is to **never emit that literal** -- resolve the user home dynamically via `$env:USERPROFILE` (PowerShell) or `$(UserProfile)` (MSBuild), both of which contain a space-agnostic expansion.

### D.1 `deploy-sync.ps1`

**Four** distinct edits; lines match verified file content (version committed at HEAD).

**Edit D.1.a -- Line 8 (`$RepoRoot`)**:

**OLD** (verbatim):
```powershell
$RepoRoot = "C:\WSGTA\universal-or-strategy"
```

**NEW**:
```powershell
$RepoRoot = $PSScriptRoot
```

**Edit D.1.b -- Line 9 (`$NtCustomDir`)**:

**OLD** (verbatim):
```powershell
$NtCustomDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"
```

**NEW**:
```powershell
$NtCustomDir = Join-Path $env:USERPROFILE "Documents\NinjaTrader 8\bin\Custom"
```

**Edit D.1.c -- Line 89 (byte_purge comment, inside ASCII-gate header block)**:

**OLD** (verbatim):
```powershell
# Fix: run C:\tmp\byte_purge.py, then re-run deploy-sync.ps1
```

**NEW**:
```powershell
# Fix: inline ASCII gate below blocked the deploy. Run: python "$RepoRoot\check_ascii.py" <files> for file-level detail, then re-run deploy-sync.ps1.
```

**Edit D.1.d -- Line 99 (error output)**:

**OLD** (verbatim):
```powershell
        Write-Host "  Fix: python C:\tmp\byte_purge.py  then re-run deploy-sync.ps1" -ForegroundColor Red
```

**NEW**:
```powershell
        Write-Host ("  Fix: python `"" + (Join-Path $RepoRoot "check_ascii.py") + "`" <files>  then re-run deploy-sync.ps1") -ForegroundColor Red
```

Note the BACKTICK-ESCAPED `` `" `` pairs around the path. If `$RepoRoot` ever resolves to a path containing a space, the emitted command copies to a shell ready-to-run instead of splitting.

### D.2 `Linting.csproj` -- 7 HintPath replacements

Lines match verified file content. Only the `<HintPath>...</HintPath>` inner text changes.

| # | Line | OLD HintPath | NEW HintPath |
|---|---|---|---|
| 1 | 33 | `C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll` | `$(ProgramFiles)\NinjaTrader 8\bin\NinjaTrader.Core.dll` |
| 2 | 37 | `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll` | `$(UserProfile)\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll` |
| 3 | 41 | `C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Gui.dll` | `$(ProgramFiles)\NinjaTrader 8\bin\NinjaTrader.Gui.dll` |
| 4 | 45 | `C:\Program Files\NinjaTrader 8\bin\SharpDX.dll` | `$(ProgramFiles)\NinjaTrader 8\bin\SharpDX.dll` |
| 5 | 49 | `C:\Program Files\NinjaTrader 8\bin\SharpDX.Direct2D1.dll` | `$(ProgramFiles)\NinjaTrader 8\bin\SharpDX.Direct2D1.dll` |
| 6 | 53 | `C:\Program Files\NinjaTrader 8\bin\SharpDX.Direct3D10.dll` | `$(ProgramFiles)\NinjaTrader 8\bin\SharpDX.Direct3D10.dll` |
| 7 | 57 | `C:\Program Files\NinjaTrader 8\bin\SharpDX.DXGI.dll` | `$(ProgramFiles)\NinjaTrader 8\bin\SharpDX.DXGI.dll` |

### D.3 `scripts/install_hooks.ps1` -- Hook-body array + icacls tail

Verified current file: 58 lines. Inline `$lines = @(...)` array occupies lines 22-50. Line 52 writes the array to `$hookTarget` via `Set-Content -Path $hookTarget -Encoding UTF8`.

**Edit D.3.a -- Replace the `$lines` array (lines 22-50 of current file) with the new array below (emits the hardened hook body from B.1)**:

**OLD (verbatim, lines 22-50)**:
```powershell
$lines = @(
    "#!/bin/sh",
    "# V12 Pre-Commit Safety Hook -- installed by scripts/install_hooks.ps1",
    "# Gates: (1) No lock() in src/, (2) ASCII-only in staged .cs files",
    "",
    'echo "--- V12 Pre-Commit Gate ---"',
    "",
    "# Gate 1: Lock-free audit",
    'REPO_ROOT=$(git rev-parse --show-toplevel)',
    'LOCK_HITS=$(grep -rl "lock(" "$REPO_ROOT/src/" 2>/dev/null | wc -l)',
    'if [ "$LOCK_HITS" -gt "0" ]; then',
    '    echo "PRE-COMMIT FAIL: lock() found in src/ -- BANNED by Platinum Standard."',
    '    grep -rl "lock(" "$REPO_ROOT/src/"',
    '    exit 1',
    "fi",
    "",
    "# Gate 2: ASCII purity on staged .cs files",
    'STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM | grep "\.cs$")',
    'if [ -n "$STAGED_CS" ]; then',
    '    python "$REPO_ROOT/check_ascii.py" $STAGED_CS',
    '    if [ $? -ne 0 ]; then',
    '        echo "PRE-COMMIT FAIL: Non-ASCII detected in staged C# files."',
    '        exit 1',
    '    fi',
    "fi",
    "",
    'echo "--- V12 Pre-Commit Gate: PASS ---"',
    "exit 0"
)
```

**NEW (verbatim)**:
```powershell
$lines = @(
    "#!/bin/sh",
    "# V12 Pre-Commit Safety Hook (ADR-019-v2) -- installed by scripts/install_hooks.ps1",
    "# Gates: (1) lock() ban, (2) ASCII purity, (3) 5MB file-size gate, (4) LFS-awareness",
    "# POSIX /bin/sh, Git-for-Windows compatible (BusyBox sh). No bashisms.",
    "",
    'echo "--- V12 Pre-Commit Gate ---"',
    'REPO_ROOT=$(git rev-parse --show-toplevel)',
    "",
    "# Gate 1: Lock-free audit (ADR-002)",
    'LOCK_HITS=$(grep -rl "lock(" "$REPO_ROOT/src/" 2>/dev/null | wc -l)',
    'if [ "$LOCK_HITS" -gt "0" ]; then',
    '    echo "PRE-COMMIT FAIL: lock() found in src/ -- BANNED by Platinum Standard."',
    '    grep -rl "lock(" "$REPO_ROOT/src/"',
    '    exit 1',
    "fi",
    "",
    "# Gate 2: ASCII purity on staged .cs files",
    'STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM | grep "\.cs$")',
    'if [ -n "$STAGED_CS" ]; then',
    '    python "$REPO_ROOT/check_ascii.py" $STAGED_CS || {',
    '        echo "PRE-COMMIT FAIL: Non-ASCII detected in staged C# files."',
    '        exit 1',
    '    }',
    "fi",
    "",
    "# Gate 3: 5MB file-size gate (LFS-aware)",
    'MAX_BYTES=5242880',
    'STAGED=$(git diff --cached --name-only --diff-filter=ACM)',
    'if [ -z "$STAGED" ]; then',
    '    echo "--- V12 Pre-Commit Gate: PASS (no staged files) ---"',
    '    exit 0',
    "fi",
    "",
    'FAIL=0',
    'for f in $STAGED; do',
    '    [ -f "$REPO_ROOT/$f" ] || continue',
    '    # Gate 4: LFS-awareness -- exempt files marked with filter=lfs',
    '    FILTER=$(git check-attr -z -- filter "$f" | tr -d ''\000'' | sed ''s/.*filter//'')',
    '    case "$FILTER" in',
    '        *lfs*) continue ;;',
    '    esac',
    '    SIZE=$(wc -c < "$REPO_ROOT/$f")',
    '    if [ "$SIZE" -gt "$MAX_BYTES" ]; then',
    '        echo "PRE-COMMIT FAIL: $f is ${SIZE} bytes (> ${MAX_BYTES}). Use Git LFS or split the asset."',
    '        FAIL=1',
    '    fi',
    'done',
    "",
    'if [ "$FAIL" -ne 0 ]; then exit 1; fi',
    "",
    'echo "--- V12 Pre-Commit Gate: PASS ---"',
    "exit 0"
)
```

**Edit D.3.b -- append the icacls block after the existing Write-Host ("To bypass...") line at line 57**:

**OLD (verbatim, lines 54-57)**:
```powershell
Write-Host ""
Write-Host "HOOK INSTALLED : $hookTarget"
Write-Host "Active gates   : [1] lock() ban  [2] ASCII purity"
Write-Host "To bypass (rare): git commit --no-verify"
```

**NEW**:
```powershell
Write-Host ""
Write-Host "HOOK INSTALLED : $hookTarget"
Write-Host "Active gates   : [1] lock() ban  [2] ASCII purity  [3] 5MB size  [4] LFS-aware"
Write-Host "To bypass (rare): git commit --no-verify"

# ADR-019-v2: grant execute permissions on Windows (Git-for-Windows ignores POSIX chmod).
& icacls.exe "$hookTarget" /grant Everyone:RX | Out-Null
Write-Host "pre-commit hook granted RX via icacls" -ForegroundColor Green
```

Note the DOUBLE-QUOTED `"$hookTarget"` in the icacls call. `$hookTarget` is built via `Join-Path` (line 9) from `$hooksDir` which derives from `$PSScriptRoot`. If the repo lives under a path with spaces, double-quoting ensures the icacls token parses correctly.

### D.4 `.github/workflows/gemini-pr-audit.yml` (SEC-002: `GCP_PROJECT_ID` secret migration)

The repo currently hardcodes the GCP project id in two places: the env block (line 43) and a user-facing error message (line 140). Both must migrate to `${{ secrets.GCP_PROJECT_ID }}`.

**Edit D.4.a -- Line 43**:

**OLD** (verbatim):
```yaml
          GCP_PROJECT_ID: "project-263b6139-6893-4788-995"
```

**NEW**:
```yaml
          GCP_PROJECT_ID: ${{ secrets.GCP_PROJECT_ID }}
```

**Edit D.4.b -- Insert preflight step IMMEDIATELY BEFORE the "Run Gemini Standards Audit" step (before line 40 `- name: Run Gemini Standards Audit`)**:

```yaml
      - name: Verify GCP_PROJECT_ID secret
        env:
          GCP_PROJECT_ID: ${{ secrets.GCP_PROJECT_ID }}
        run: |
          if [ -z "${GCP_PROJECT_ID}" ]; then
            echo "GCP_PROJECT_ID secret is not configured -- aborting audit."
            exit 1
          fi
```

**Edit D.4.c -- Line 140 (error-message template)**:

**OLD** (verbatim):
```javascript
                  "\n\n**Recommendation**: Check GCP Console for Project 'project-263b6139-6893-4788-995'. Ensure 'Vertex AI API' is enabled and models are accessible in your region."
```

**NEW**:
```javascript
                  "\n\n**Recommendation**: Check GCP Console for Project '" + (process.env.GCP_PROJECT_ID || 'unset') + "'. Ensure 'Vertex AI API' is enabled and models are accessible in your region."
```

**Director action (out-of-band, required before merge)**: add `GCP_PROJECT_ID` to repo secrets. The preflight step (D.4.b) fails the job loudly if the secret is absent.

---

## E. Track 4 -- Kernel Orphan Guards (34 sites, individually enumerated)

### E.0 Recipes

**Transform A1 (Type 1 -- simple early-return, 27 sites)**:
```csharp
TriggerCustomEvent(o =>
{
    if (_isTerminating) return;
    <OriginalBody>;
}, null);
```

**Transform A2-R (Class A -- cleanup-preserving early-return, 7 sites)**:
```csharp
TriggerCustomEvent(o =>
{
    if (_isTerminating)
    {
        <CleanupOnly>;
        return;
    }
    <OriginalBody>;
}, null);
```

**Closure discipline (MANDATORY for every site that captures locals)**:
- Promote captures to dedicated locals BEFORE the lambda: `var keyCapture = key;`.
- Do NOT close over `foreach` loop variables.
- Do NOT wrap the lambda body in `try/finally` -- NinjaTrader's dispatcher already wraps the delegate in `catch`.

---

### E.1 -- Class A Site #1: `src/V12_002.Orders.Callbacks.AccountOrders.cs:369` (Follower Replace FSM)

**OLD** (verbatim, lines 366-383):
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
                    string sigNameCap = sigName;
                    string acctNameCap = acctNameCapture;
                    FollowerReplaceFsm fsmCap = fsmCapture;
                    bool replacementScheduled = false;
                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            if (_isTerminating)
                            {
                                // ADR-019-v2 [A2-R]: release FSM reservation on shutdown to prevent
                                // hot-restart seeing a stale in-flight entry.
                                _followerReplaceSpecs.TryRemove(sigNameCap, out _);
                                return;
                            }
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            SubmitFollowerReplacement(sigNameCap, acctNameCap, fsmCap.PendingPrice, fsmCap.PendingQty, fsmCap);
                            _followerReplaceSpecs.TryRemove(sigNameCap, out _);
                        }, null);
                        replacementScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }
```

---

### E.2 -- Class A Site #2: `src/V12_002.REAPER.Audit.cs:136` (Repair queue)

**OLD** (verbatim, lines 132-141):
```csharp
                            // A3-2: Mark in-flight BEFORE TriggerCustomEvent to block double-enqueue in next audit cycle (Build 960 audit fix)
                            _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
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
                            // A3-2: Mark in-flight BEFORE TriggerCustomEvent to block double-enqueue in next audit cycle (Build 960 audit fix)
                            _repairInFlight.TryAdd(repairKey, 0); // [Build 968]
                            _reaperRepairQueue.Enqueue(acct.Name);
                            string repairKeyCap = repairKey;
                            // B957/E1: Clear in-flight guard if TriggerCustomEvent fails, preventing permanent lockout.
                            try
                            {
                                TriggerCustomEvent(o =>
                                {
                                    if (_isTerminating)
                                    {
                                        // ADR-019-v2 [A2-R]: release repair reservation + drain queue on shutdown.
                                        _repairInFlight.TryRemove(repairKeyCap, out _);
                                        string _drained;
                                        while (_reaperRepairQueue.TryDequeue(out _drained)) { }
                                        return;
                                    }
                                    ProcessReaperRepairQueue();
                                }, null);
                            }
                            catch (Exception repairTriggerEx)
                            {
                                _repairInFlight.TryRemove(repairKey, out _); // [Build 968]
                                Print("[REAPER] TriggerCustomEvent failed for " + repairKey + ": " + repairTriggerEx.Message + " -- in-flight cleared.");
                            }
```

---

### E.3 -- Class A Site #3: `src/V12_002.REAPER.Audit.cs:183` (Fleet flatten queue)

**OLD** (verbatim, lines 179-192):
```csharp
                    if (AutoFlattenDesync)
                    {
                        if (shouldLog) Print($"[REAPER] * QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                        _reaperFlattenQueue.Enqueue(acct.Name);
                        try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); }
                        catch (Exception _flatTriggerEx)
                        {
                            string _discarded;
                            _reaperFlattenQueue.TryDequeue(out _discarded);
                            Print("[REAPER] TriggerCustomEvent failed for flatten of "
                                + acct.Name + ": " + _flatTriggerEx.Message
                                + " -- dequeued, will re-detect next cycle");
                        }
                    }
```

**NEW**:
```csharp
                    if (AutoFlattenDesync)
                    {
                        if (shouldLog) Print($"[REAPER] * QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                        _reaperFlattenQueue.Enqueue(acct.Name);
                        try
                        {
                            TriggerCustomEvent(o =>
                            {
                                if (_isTerminating)
                                {
                                    // ADR-019-v2 [A2-R]: drain flatten queue on shutdown.
                                    string _drained;
                                    while (_reaperFlattenQueue.TryDequeue(out _drained)) { }
                                    return;
                                }
                                ProcessReaperFlattenQueue();
                            }, null);
                        }
                        catch (Exception _flatTriggerEx)
                        {
                            string _discarded;
                            _reaperFlattenQueue.TryDequeue(out _discarded);
                            Print("[REAPER] TriggerCustomEvent failed for flatten of "
                                + acct.Name + ": " + _flatTriggerEx.Message
                                + " -- dequeued, will re-detect next cycle");
                        }
                    }
```

---

### E.4 -- Class A Site #4: `src/V12_002.REAPER.Audit.cs:250` (Fleet naked-stop)

**OLD** (verbatim, lines 246-255):
```csharp
                                _reaperNakedStopInFlight.TryAdd(ExpKey(acct.Name), 0); // [Build 968]
                                Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                    acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                                _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                                try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                                catch (Exception tcEx)
                                {
                                    _reaperNakedStopInFlight.TryRemove(ExpKey(acct.Name), out _); // [Build 969]
                                    Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.", acct.Name, tcEx.Message));
                                }
```

**NEW**:
```csharp
                                string nakedKeyCap = ExpKey(acct.Name);
                                _reaperNakedStopInFlight.TryAdd(nakedKeyCap, 0); // [Build 968]
                                Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                    acct.Name, actualQty, (DateTime.UtcNow - firstSeen).TotalSeconds));
                                _reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
                                try
                                {
                                    TriggerCustomEvent(e =>
                                    {
                                        if (_isTerminating)
                                        {
                                            // ADR-019-v2 [A2-R]: release reservation + drain queue on shutdown.
                                            _reaperNakedStopInFlight.TryRemove(nakedKeyCap, out _);
                                            // Tuple element names erased at runtime; positional match to declared type.
                                            (string, MarketPosition, int) _drained;
                                            while (_reaperNakedStopQueue.TryDequeue(out _drained)) { }
                                            return;
                                        }
                                        ProcessReaperNakedStopQueue();
                                    }, null);
                                }
                                catch (Exception tcEx)
                                {
                                    _reaperNakedStopInFlight.TryRemove(nakedKeyCap, out _); // [Build 969]
                                    Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.", acct.Name, tcEx.Message));
                                }
```

Tuple type verified at `V12_002.REAPER.cs:27-28`: `ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)>`.

---

### E.5 -- Class A Site #5: `src/V12_002.REAPER.Audit.cs:327` (Master flatten)

**OLD** (verbatim, lines 323-335):
```csharp
                        if (AutoFlattenDesync)
                        {
                            if (shouldLog) Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                            _reaperFlattenQueue.Enqueue(Account.Name);
                            try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); }
                            catch (Exception _mFlatTriggerEx)
                            {
                                string _mDiscarded;
                                _reaperFlattenQueue.TryDequeue(out _mDiscarded);
                                Print("[REAPER] TriggerCustomEvent failed for master flatten: "
                                    + _mFlatTriggerEx.Message + " -- dequeued, will re-detect next cycle");
                            }
                        }
```

**NEW**:
```csharp
                        if (AutoFlattenDesync)
                        {
                            if (shouldLog) Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                            _reaperFlattenQueue.Enqueue(Account.Name);
                            try
                            {
                                TriggerCustomEvent(o =>
                                {
                                    if (_isTerminating)
                                    {
                                        // ADR-019-v2 [A2-R]: drain flatten queue on shutdown.
                                        string _mDrained;
                                        while (_reaperFlattenQueue.TryDequeue(out _mDrained)) { }
                                        return;
                                    }
                                    ProcessReaperFlattenQueue();
                                }, null);
                            }
                            catch (Exception _mFlatTriggerEx)
                            {
                                string _mDiscarded;
                                _reaperFlattenQueue.TryDequeue(out _mDiscarded);
                                Print("[REAPER] TriggerCustomEvent failed for master flatten: "
                                    + _mFlatTriggerEx.Message + " -- dequeued, will re-detect next cycle");
                            }
                        }
```

---

### E.6 -- Class A Site #6: `src/V12_002.REAPER.Audit.cs:372` (Master naked-stop)

**OLD** (verbatim, lines 368-378):
```csharp
                            _reaperNakedStopInFlight.TryAdd(ExpKey(Account.Name), 0);
                            Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
                            _reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
                            try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
                            catch (Exception tcEx)
                            {
                                _reaperNakedStopInFlight.TryRemove(ExpKey(Account.Name), out _);
                                Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0} (Master): {1} -- in-flight cleared.",
                                    Account.Name, tcEx.Message));
                            }
```

**NEW**:
```csharp
                            string masterNakedKeyCap = ExpKey(Account.Name);
                            _reaperNakedStopInFlight.TryAdd(masterNakedKeyCap, 0);
                            Print(string.Format("[REAPER][NAKED_POSITION] {0} (Master): {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                                Account.Name, masterActualQty, (DateTime.UtcNow - masterFirstSeen).TotalSeconds));
                            _reaperNakedStopQueue.Enqueue((Account.Name, masterPos.MarketPosition, Math.Abs(masterActualQty)));
                            try
                            {
                                TriggerCustomEvent(e =>
                                {
                                    if (_isTerminating)
                                    {
                                        // ADR-019-v2 [A2-R]: release reservation + drain queue on shutdown.
                                        _reaperNakedStopInFlight.TryRemove(masterNakedKeyCap, out _);
                                        (string, MarketPosition, int) _mDrained;
                                        while (_reaperNakedStopQueue.TryDequeue(out _mDrained)) { }
                                        return;
                                    }
                                    ProcessReaperNakedStopQueue();
                                }, null);
                            }
                            catch (Exception tcEx)
                            {
                                _reaperNakedStopInFlight.TryRemove(masterNakedKeyCap, out _);
                                Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0} (Master): {1} -- in-flight cleared.",
                                    Account.Name, tcEx.Message));
                            }
```

---

### E.7 -- Class A Site #7: `src/V12_002.cs:373` (ScheduleActorDrain)

**CRITICAL**: this site's cleanup (`Interlocked.Exchange(ref _actorWakeScheduled, 0)`) MUST run BEFORE the terminate check -- otherwise no future drain can schedule after a mid-flight shutdown.

**OLD** (verbatim, lines 370-382):
```csharp
        private void ScheduleActorDrain() {
            if (Interlocked.CompareExchange(ref _actorWakeScheduled, 1, 0) != 0) return;
            try {
                TriggerCustomEvent(o => {
                    Interlocked.Exchange(ref _actorWakeScheduled, 0);
                    TryDrain();
                }, null);
            }
            catch (Exception ex) {
                Interlocked.Exchange(ref _actorWakeScheduled, 0);
                Print("[V12_INLINE_ACTOR] schedule failed: " + ex.Message);
            }
        }
```

**NEW**:
```csharp
        private void ScheduleActorDrain() {
            if (Interlocked.CompareExchange(ref _actorWakeScheduled, 1, 0) != 0) return;
            try {
                TriggerCustomEvent(o => {
                    // ADR-019-v2 [A2-R]: ALWAYS clear the wake token FIRST -- the interlocked exchange IS
                    // the cleanup. If we returned before clearing, no future drain could be scheduled.
                    Interlocked.Exchange(ref _actorWakeScheduled, 0);
                    if (_isTerminating) return;
                    TryDrain();
                }, null);
            }
            catch (Exception ex) {
                Interlocked.Exchange(ref _actorWakeScheduled, 0);
                Print("[V12_INLINE_ACTOR] schedule failed: " + ex.Message);
            }
        }
```

---

### E.8 through E.34 -- Type 1 Sites (27, Transform A1 verbatim)

Each site below contains the current single-line or inline TCE lambda and its Transform A1 replacement. All captures are value-type or stable reference; no closure discipline edits needed beyond the guard insertion.

---

### E.8 -- Type 1 Site T01: `src/V12_002.Orders.Callbacks.AccountOrders.cs:146`

**OLD** (verbatim):
```csharp
            try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
```

**NEW**:
```csharp
            try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountOrderQueue(); }, null); } catch { }
```

---

### E.9 -- Type 1 Site T02: `src/V12_002.Orders.Callbacks.AccountOrders.cs:162`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountOrderQueue(); }, null); } catch { }
```

---

### E.10 -- Type 1 Site T03: `src/V12_002.Orders.Callbacks.AccountOrders.cs:173`

**OLD** (verbatim):
```csharp
                    try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
```

**NEW**:
```csharp
                    try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountOrderQueue(); }, null); } catch { }
```

---

### E.11 -- Type 1 Site T04: `src/V12_002.Orders.Callbacks.AccountOrders.cs:181`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountOrderQueue(); }, null); } catch { }
```

Codex note: sites T01-T04 all contain the identical statement. Use a ranged edit or confirm line numbers individually -- DO NOT `replace_all` on the fragment alone, as the OLD text also matches T13 at V12_002.cs:701 and a grep hit inside comments. Use the surrounding line context to disambiguate.

---

### E.12 -- Type 1 Site T05: `src/V12_002.Orders.Callbacks.AccountOrders.cs:410`

**OLD** (verbatim, lines 408-415):
```csharp
                        try
                        {
                            TriggerCustomEvent(o => SubmitFollowerTargetReplacement(capturedKey, captured), null);
                        }
                        catch (Exception tFsmEx)
                        {
                            Print("[FSM_TGT] TriggerCustomEvent failed for " + capturedKey + ": " + tFsmEx.Message);
                        }
```

**NEW**:
```csharp
                        try
                        {
                            TriggerCustomEvent(o =>
                            {
                                if (_isTerminating) return;
                                SubmitFollowerTargetReplacement(capturedKey, captured);
                            }, null);
                        }
                        catch (Exception tFsmEx)
                        {
                            Print("[FSM_TGT] TriggerCustomEvent failed for " + capturedKey + ": " + tFsmEx.Message);
                        }
```

---

### E.13 -- Type 1 Site T06: `src/V12_002.Orders.Callbacks.AccountOrders.cs:463`

**OLD** (verbatim):
```csharp
                                        TriggerCustomEvent(o => RestoreCascadedTargets(_rKey, _snap), null);
```

**NEW**:
```csharp
                                        TriggerCustomEvent(o =>
                                        {
                                            if (_isTerminating) return;
                                            RestoreCascadedTargets(_rKey, _snap);
                                        }, null);
```

---

### E.14 -- Type 1 Site T07: `src/V12_002.Orders.Callbacks.AccountOrders.cs:591`

**OLD** (verbatim):
```csharp
                            TriggerCustomEvent(o => EmergencyFlattenSingleFleetAccount(filledFollowerAcct), null);
```

**NEW**:
```csharp
                            TriggerCustomEvent(o =>
                            {
                                if (_isTerminating) return;
                                EmergencyFlattenSingleFleetAccount(filledFollowerAcct);
                            }, null);
```

---

### E.15 -- Type 1 Site T08: `src/V12_002.Orders.Callbacks.Execution.cs:235`

**OLD** (verbatim):
```csharp
                    TriggerCustomEvent(o => UpdateAccountMetricsFromAccount(Account), null);
```

**NEW**:
```csharp
                    TriggerCustomEvent(o =>
                    {
                        if (_isTerminating) return;
                        UpdateAccountMetricsFromAccount(Account);
                    }, null);
```

---

### E.16 -- Type 1 Site T09: `src/V12_002.SIMA.Dispatch.cs:60`

**OLD** (verbatim, lines 58-64):
```csharp
                try
                {
                    TriggerCustomEvent(o => ExecuteSmartDispatchEntry(
                        _defTradeType, _defAction, _defQty, _defPrice,
                        _defOrderType, _defMasterNames), null);
                }
                catch { Print("[DISPATCH] Deferred retry scheduling failed"); }
```

**NEW**:
```csharp
                try
                {
                    TriggerCustomEvent(o =>
                    {
                        if (_isTerminating) return;
                        ExecuteSmartDispatchEntry(
                            _defTradeType, _defAction, _defQty, _defPrice,
                            _defOrderType, _defMasterNames);
                    }, null);
                }
                catch { Print("[DISPATCH] Deferred retry scheduling failed"); }
```

---

### E.17 -- Type 1 Site T10: `src/V12_002.SIMA.Dispatch.cs:610`

**OLD** (verbatim):
```csharp
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
```

**NEW**:
```csharp
                    try { TriggerCustomEvent(o => { if (_isTerminating) return; PumpFleetDispatch(); }, null); } catch { }
```

---

### E.18 -- Type 1 Site T11: `src/V12_002.REAPER.cs:132`

**OLD** (verbatim, lines 129-137):
```csharp
            try
            {
                // Marshal to strategy thread via TriggerCustomEvent
                TriggerCustomEvent(o => AuditApexPositions(), null);
            }
            catch (Exception ex)
            {
                Print("[REAPER] Timer Marshalling Error: " + ex.Message);
            }
```

**NEW**:
```csharp
            try
            {
                // Marshal to strategy thread via TriggerCustomEvent
                TriggerCustomEvent(o =>
                {
                    if (_isTerminating) return;
                    AuditApexPositions();
                }, null);
            }
            catch (Exception ex)
            {
                Print("[REAPER] Timer Marshalling Error: " + ex.Message);
            }
```

---

### E.19 -- Type 1 Site T12: `src/V12_002.Orders.Callbacks.cs:389`

**OLD** (verbatim):
```csharp
                                TriggerCustomEvent(o => RestoreCascadedTargets(_mKey, _mSnap), null);
```

**NEW**:
```csharp
                                TriggerCustomEvent(o =>
                                {
                                    if (_isTerminating) return;
                                    RestoreCascadedTargets(_mKey, _mSnap);
                                }, null);
```

---

### E.20 -- Type 1 Site T13: `src/V12_002.cs:701`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountOrderQueue(); }, null); } catch { }
```

---

### E.21 -- Type 1 Site T14: `src/V12_002.cs:707`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountExecutionQueue(); }, null); } catch { }
```

---

### E.22 -- Type 1 Site T15: `src/V12_002.SIMA.Flatten.cs:82`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => PumpFlattenOps(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; PumpFlattenOps(); }, null); } catch { }
```

---

### E.23 -- Type 1 Site T16: `src/V12_002.SIMA.Flatten.cs:201`

**OLD** (verbatim):
```csharp
                    try { TriggerCustomEvent(o => PumpFlattenOps(), null); } catch { }
```

**NEW**:
```csharp
                    try { TriggerCustomEvent(o => { if (_isTerminating) return; PumpFlattenOps(); }, null); } catch { }
```

---

### E.24 -- Type 1 Site T17: `src/V12_002.SIMA.Flatten.cs:319`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => PumpFlattenOps(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; PumpFlattenOps(); }, null); } catch { }
```

Codex note: T15, T16, T17 are line-distinct (82, 201, 319) but share identical text. Edit by line range or re-read surrounding context after each edit.

---

### E.25 -- Type 1 Site T18: `src/V12_002.SIMA.Lifecycle.cs:57`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessApplySimaState(_defEnabled), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessApplySimaState(_defEnabled); }, null); } catch { }
```

---

### E.26 -- Type 1 Site T19: `src/V12_002.SIMA.Fleet.cs:174`

**OLD** (verbatim):
```csharp
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
```

**NEW**:
```csharp
                    try { TriggerCustomEvent(o => { if (_isTerminating) return; PumpFleetDispatch(); }, null); } catch { }
```

---

### E.27 -- Type 1 Site T20: `src/V12_002.SIMA.Fleet.cs:262`

**OLD** (verbatim):
```csharp
                        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
```

**NEW**:
```csharp
                        try { TriggerCustomEvent(o => { if (_isTerminating) return; PumpFleetDispatch(); }, null); } catch { }
```

---

### E.28 -- Type 1 Site T21: `src/V12_002.Trailing.StopUpdate.cs:64`

**OLD** (verbatim):
```csharp
                                TriggerCustomEvent(o => RestoreCascadedTargets(_tKey, _tSnap), null);
```

**NEW**:
```csharp
                                TriggerCustomEvent(o =>
                                {
                                    if (_isTerminating) return;
                                    RestoreCascadedTargets(_tKey, _tSnap);
                                }, null);
```

---

### E.29 -- Type 1 Site T22: `src/V12_002.UI.Compliance.cs:286`

**OLD** (verbatim):
```csharp
            try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); } catch { }
```

**NEW**:
```csharp
            try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountExecutionQueue(); }, null); } catch { }
```

---

### E.30 -- Type 1 Site T23: `src/V12_002.UI.Compliance.cs:304`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountExecutionQueue(); }, null); } catch { }
```

---

### E.31 -- Type 1 Site T24: `src/V12_002.UI.Compliance.cs:316`

**OLD** (verbatim):
```csharp
                    try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); } catch { }
```

**NEW**:
```csharp
                    try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountExecutionQueue(); }, null); } catch { }
```

---

### E.32 -- Type 1 Site T25: `src/V12_002.UI.Compliance.cs:324`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessAccountExecutionQueue(); }, null); } catch { }
```

---

### E.33 -- Type 1 Site T26: `src/V12_002.UI.IPC.cs:328`

**OLD** (verbatim):
```csharp
                try { TriggerCustomEvent(o => ProcessIpcCommands(), null); } catch { }
```

**NEW**:
```csharp
                try { TriggerCustomEvent(o => { if (_isTerminating) return; ProcessIpcCommands(); }, null); } catch { }
```

---

### E.34 -- Type 1 Site T27: `src/V12_002.UI.IPC.Server.cs:277`

**OLD** (verbatim, lines 274-279):
```csharp
            try
            {
                TriggerCustomEvent(o => ProcessIpcCommands(), null);
            }
            catch { }
```

**NEW**:
```csharp
            try
            {
                TriggerCustomEvent(o =>
                {
                    if (_isTerminating) return;
                    ProcessIpcCommands();
                }, null);
            }
            catch { }
```

---

### E.35 -- Build tag bump (final commit)

**File**: `src/V12_002.Constants.cs`, line 12.

**OLD** (verbatim):
```csharp
            public const string Version = "Build 1111.002-v28.0";
```

**NEW**:
```csharp
            public const string Version = "Build 1111.003-v28.0-adr019";
```

---

### E.36 -- Out-of-scope (NOT a TCE site): `src/V12_002.UI.Panel.Construction.cs:237`

Line 237 (`if (_isTerminating || rootContainer == null) return;`) lives inside `DispatcherTimer.Tick += (s, e) =>` at line 234, NOT a `TriggerCustomEvent` lambda. Grep of this file returns ZERO `TriggerCustomEvent` matches. It is therefore OUT of Track 4 scope. No edit required. Its existing guard is harmless but unrelated.

---

## F. Verification Matrix (18 checks, paired POSIX + PowerShell)

Run after each commit per the order in Section G. `REPO_ROOT` assumed to be the cwd; no user-home paths embedded in any check, so bash-quoting is never an issue.

| # | Check | POSIX shell | PowerShell |
|---|---|---|---|
| F1 | `lock()` ban holds (ADR-002) | `! grep -r "lock(" src/ \| grep -v stateLock \| grep .` | `(Get-ChildItem -Recurse src -Filter *.cs \| Select-String 'lock\(' \| Where-Object { $_ -notmatch 'stateLock' } \| Measure-Object).Count -eq 0` |
| F2 | ASCII purity of every edited .cs | `python check_ascii.py src/*.cs` | `python check_ascii.py (Get-ChildItem src\*.cs).FullName` |
| F3 | Build tag bumped to `1111.003-v28.0-adr019` | `grep -n '1111.003-v28.0-adr019' src/V12_002.Constants.cs` | `Select-String -Path src/V12_002.Constants.cs -Pattern '1111\.003-v28\.0-adr019'` |
| F4 | 34 TCE sites present | `[ $(grep -rE 'TriggerCustomEvent\(.*=>' src/ \| wc -l) -eq 34 ]` | `(Select-String -Path src\*.cs -Pattern 'TriggerCustomEvent\(.*=>').Count -eq 34` |
| F5 | >= 34 `if (_isTerminating)` hits (one per TCE site; substring matches both Type 1 single-line and Class A multi-line forms) | `[ $(grep -rE 'if \(_isTerminating\)' src/ \| wc -l) -ge 34 ]` | `(Select-String -Path src\*.cs -Pattern 'if \(_isTerminating\)').Count -ge 34` |
| F6 | Class A cleanup present in REAPER.Audit.cs (5 sites: 136, 183, 250, 327, 372) | `[ $(grep -cE 'if \(_isTerminating\)' src/V12_002.REAPER.Audit.cs) -ge 5 ]` | `(Select-String -Path src\V12_002.REAPER.Audit.cs -Pattern 'if \(_isTerminating\)').Count -ge 5` |
| F7 | Ring trailing pad present | `grep -n '_padB7' src/V12_002.Photon.Ring.cs` | `Select-String -Path src\V12_002.Photon.Ring.cs -Pattern '_padB7'` |
| F8 | MemoryBarrier in TryDequeue | `grep -A 20 'public bool TryDequeue' src/V12_002.Photon.Ring.cs \| grep MemoryBarrier` | `(Get-Content src\V12_002.Photon.Ring.cs -Raw) -match '(?s)TryDequeue[^}]+?Thread\.MemoryBarrier'` |
| F9 | deploy-sync.ps1 has no hardcoded user / repo roots | `! grep -nE 'C:\\\\WSGTA\|Mohammed Khalid' deploy-sync.ps1` | `(Select-String -Path deploy-sync.ps1 -Pattern 'C:\\WSGTA\|Mohammed Khalid').Count -eq 0` |
| F10 | byte_purge.py references removed | `! grep -n byte_purge deploy-sync.ps1` | `(Select-String -Path deploy-sync.ps1 -Pattern 'byte_purge').Count -eq 0` |
| F11 | Linting.csproj uses MSBuild properties (>= 7 hits) | `[ $(grep -cE '\$\(UserProfile\)\|\$\(ProgramFiles\)' Linting.csproj) -ge 7 ]` | `(Select-String -Path Linting.csproj -Pattern '\$\(UserProfile\)\|\$\(ProgramFiles\)').Count -ge 7` |
| F12 | Pre-commit has 5 MB gate | `grep -n 5242880 .git/hooks/pre-commit` | `Select-String -Path .git\hooks\pre-commit -Pattern '5242880'` |
| F13 | Pre-commit has LFS check | `grep -n 'check-attr' .git/hooks/pre-commit` | `Select-String -Path .git\hooks\pre-commit -Pattern 'check-attr'` |
| F14 | label-sync workflow present, delete-other-labels false | `grep 'delete-other-labels: false' .github/workflows/label-sync.yml` | `Select-String -Path .github\workflows\label-sync.yml -Pattern 'delete-other-labels:\s*false'` |
| F15 | labels.yml catalog present (>= 13 entries) | `[ $(grep -c '^- name:' .github/labels.yml) -ge 13 ]` | `(Select-String -Path .github\labels.yml -Pattern '^- name:').Count -ge 13` |
| F16 | devcontainer present | `test -f .devcontainer/devcontainer.json && test -f .devcontainer/Dockerfile` | `(Test-Path .devcontainer\devcontainer.json) -and (Test-Path .devcontainer\Dockerfile)` |
| F17 | SEC-002 gemini secret wired | `grep 'GCP_PROJECT_ID: \${{ secrets\.GCP_PROJECT_ID }}' .github/workflows/gemini-pr-audit.yml` | `Select-String -Path .github\workflows\gemini-pr-audit.yml -Pattern 'GCP_PROJECT_ID:\s*\$\{\{\s*secrets\.GCP_PROJECT_ID\s*\}\}'` |
| F18 | No hardcoded user-home path in Linting.csproj | `! grep -nE 'C:\\\\Users\\\\Mohammed Khalid' Linting.csproj` | `(Select-String -Path Linting.csproj -Pattern 'Mohammed Khalid').Count -eq 0` |

**F19 -- Director live test (MANUAL, not automatable)**:
1. Run `powershell -File .\deploy-sync.ps1` -- ASCII gate must PASS, all hard-links recreated, no hardcoded-path errors on alternate user accounts.
2. Press **F5** in NinjaTrader 8 -- compile must succeed with zero errors.
3. Enable V12_002 on a fleet.
4. Control Center log banner must show `Build 1111.003-v28.0-adr019`.
5. Toggle SIMA ON/OFF 5 times -- no `(!) Stop replace in flight` stalls.
6. Force a REAPER desync (manual broker cancel) -- verify repair fires and `_repairInFlight` drains (grep log for `in-flight cleared`).
7. Disable strategy mid-drain -- verify no ghost orders and no stuck `_followerReplaceSpecs` entries (next start must be clean).
8. AMAL harness (if present): mean latency within +/- 5% of last baseline; Gen0 = 0; Allocated = 0 B.

Attach the verification log to the PR.

---

## G. Commit Ordering (file-disjoint across tracks)

Each commit independently passes the pre-commit gate and compiles. `git bisect` isolates any regression to a single track step.

1. **commit 1 -- Track 3 path portability**: `deploy-sync.ps1` (D.1.a-d) + `Linting.csproj` (D.2). Verify F9, F10, F11, F18.
   - Message: `fix(infra): path portability for deploy-sync and Linting [ADR-019-v2 T3]`
2. **commit 2 -- Track 1 + D.3 + D.4**: update `scripts/install_hooks.ps1` per D.3 and execute it to reinstall `.git/hooks/pre-commit`; create `.github/workflows/label-sync.yml` (B.2), `.github/labels.yml` (B.3), `.devcontainer/devcontainer.json` (B.4), `.devcontainer/Dockerfile` (B.5); edit `.github/workflows/gemini-pr-audit.yml` per D.4. Verify F12, F13, F14, F15, F16, F17.
   - Message: `feat(infra): pre-commit 5MB+LFS gate, label-sync, devcontainer, GCP secret [ADR-019-v2 T1]`
3. **commit 3 -- Track 2 F-001**: Ring trailing pad (C.1). Verify F7.
   - Message: `fix(photon): trailing cache-line pad after _consumerCursor [ADR-019-v2 F-001]`
4. **commit 4 -- Track 2 F-002**: TryDequeue MemoryBarrier (C.2). Verify F8.
   - Message: `fix(photon): MemoryBarrier before slot read in TryDequeue [ADR-019-v2 F-002]`
5. **commit 5 -- Track 2 F-003/F-004**: DEBUG thread-affinity asserts (C.3, C.4). Verify Debug+Release compile.
   - Message: `chore(photon): DEBUG thread-affinity asserts for pool + execId probe [ADR-019-v2 F-003/F-004]`
6. **commit 6 -- Track 4a Type 1 (27 sites)**: E.8 through E.34 (sites T01-T27). Verify F4 (=34), F5 (>=34-7=27 minimum; full check >=34 only after commit 7).
   - Message: `feat(kernel): Type 1 orphan-guard sweep across 13 files [ADR-019-v2 T4a]`
7. **commit 7 -- Track 4b Class A (7 sites)**: E.1 through E.7. Verify F5 (>=34), F6 (>=5 in REAPER.Audit.cs).
   - Message: `feat(kernel): Class A cleanup-preserving guards for 7 sites [ADR-019-v2 T4b]`
8. **commit 8 -- build tag bump**: E.35 Constants.cs:12. Verify F3.
   - Message: `chore(build): bump tag to 1111.003-v28.0-adr019 [ADR-019-v2 FINAL]`

---

## H. Risk Ledger

| Risk | Severity | Mitigation |
|---|---|---|
| Closure captures loop variables in REAPER `foreach acct` sites | High | Explicit captures promoted to locals in each Class A site (E.2, E.4) |
| Double-guard a TCE lambda that already contains `if (_isTerminating) return;` | Low | Grep-before-insertion is safety net; verified zero pre-guarded TCE sites |
| `_isTerminating` flips between guard and cleanup | Low | Lifecycle sets flag once and never clears; no race window |
| Ring trailing pad changes object size | Low | NT8 x64 only; no 32-bit build |
| GCP secret absent before merge | Med | Preflight step (D.4.b) fails job loudly; Director adds secret before PR merge |
| AMAL regresses > 5% from fence + DEBUG asserts | Low | DEBUG asserts stripped in RELEASE; fence is ~5 ns |
| Naked-stop tuple-type mismatch | Resolved | `V12_002.REAPER.cs:27-28` declaration verified: `(string, MarketPosition, int)` |
| Unquoted `C:\Users\Mohammed Khalid\` in downstream shell command | Resolved | All scripts now use `$env:USERPROFILE` / `$(UserProfile)` / `$PSScriptRoot`; no literal embedded anywhere |
| Line-number drift during commit sequence | Med | All OLD blocks re-grep-verifiable by symbol name; SYMBOL > line if drift |

---

## I. Critical Files Summary

**Modified (src/)**:
- `src/V12_002.Constants.cs` (build tag, E.35)
- `src/V12_002.Photon.Ring.cs` (F-001, F-002)
- `src/V12_002.Photon.Pool.cs` (F-003, F-004 DEBUG asserts)
- `src/V12_002.Orders.Callbacks.AccountOrders.cs` (1 A2-R at E.1 + 7 A1 at E.8-E.14)
- `src/V12_002.REAPER.Audit.cs` (5 A2-R at E.2-E.6)
- `src/V12_002.REAPER.cs` (1 A1 at E.18)
- `src/V12_002.SIMA.Dispatch.cs` (2 A1 at E.16, E.17)
- `src/V12_002.SIMA.Lifecycle.cs` (1 A1 at E.25)
- `src/V12_002.SIMA.Flatten.cs` (3 A1 at E.22-E.24)
- `src/V12_002.SIMA.Fleet.cs` (2 A1 at E.26, E.27)
- `src/V12_002.Orders.Callbacks.cs` (1 A1 at E.19)
- `src/V12_002.Orders.Callbacks.Execution.cs` (1 A1 at E.15)
- `src/V12_002.Trailing.StopUpdate.cs` (1 A1 at E.28)
- `src/V12_002.UI.Compliance.cs` (4 A1 at E.29-E.32)
- `src/V12_002.UI.IPC.cs` (1 A1 at E.33)
- `src/V12_002.UI.IPC.Server.cs` (1 A1 at E.34)
- `src/V12_002.cs` (1 A2-R at E.7 + 2 A1 at E.20, E.21)

**Modified (infra/scripts)**:
- `deploy-sync.ps1`
- `Linting.csproj`
- `scripts/install_hooks.ps1`
- `.git/hooks/pre-commit` (emitted by install_hooks)
- `.github/workflows/gemini-pr-audit.yml`

**Created**:
- `.github/workflows/label-sync.yml`
- `.github/labels.yml`
- `.devcontainer/devcontainer.json`
- `.devcontainer/Dockerfile`

**Deleted**: none.

**TOTAL TCE guard injections: 34 (27 Type 1 + 7 Class A). Zero sites skipped.**

---

## J. Post-Edit Deployment Protocol (MANDATORY)

After all 8 commits land:
1. `powershell -File .\deploy-sync.ps1` -- ASCII gate must PASS; all hard-links recreated.
2. Director: Press **F5** in NinjaTrader 8 to compile; verify banner shows `Build 1111.003-v28.0-adr019`.
3. Director runs F19 live-test checklist.
4. Attach verification log (F1-F18 + F19) to the PR.

**If any verification step fails**: stop; do NOT continue to the next commit. Report the failing step number + exact command output to Director. DO NOT use `--no-verify` to bypass the pre-commit hook -- diagnose the underlying violation.

**If any OLD block does not match verbatim**: pause; re-grep by symbol name; quote observed state; request architect amendment. Do NOT invent a fix.

**End of implementation plan.**
