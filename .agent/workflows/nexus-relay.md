---
description: P5 Mission Control Handoff -- Hand off implementation to the Engineer (Codex/Jules) with mandatory pre/post test gates before routing to the P6 Validation Worker.
---

# Nexus Relay: P5 Engineer Mission Control Handoff

**You are the P5 ENGINEER.** You received an approved `implementation_plan.md`. Your job is
surgical implementation with mandatory test verification at each gate. You do NOT improvise.
You do NOT expand scope. Every changed line must trace to the plan.

---

## Phase 0: Pre-Edit Baseline Capture (MANDATORY)

Before touching any `src/` file, establish a clean baseline. This proves any regression
you find in Phase 3 was caused by YOUR edit, not pre-existing.

```powershell
# Baseline: Logic Tests
dotnet test Testing.csproj --logger "console;verbosity=detailed" | Tee-Object -FilePath "tmp/p5_baseline_test.txt"

# Baseline: AMAL Performance
python scripts/amal_harness.py | Tee-Object -FilePath "tmp/p5_baseline_amal.txt"
```

**STOP** if the baseline `dotnet test` fails. Report to Director -- do not implement over a broken baseline.

---

## Phase 1: Context Load

1. `docs/brain/implementation_plan.md` -- The approved plan. Read it fully.
2. `docs/brain/nexus_a2a.json` -- Current mission state.
3. Each `src/` file listed in the plan -- Read before editing (required by agent harness).

**Testing Decision Tree -- Which tool do I use?**

| Error Type | Tool |
|------------|------|
| Compile error | `dotnet build Linting.csproj` |
| Logic / math error (pure C#) | `dotnet test Testing.csproj` |
| Runtime exception (NinjaTrader, IPC) | Run and Debug tab -- set breakpoint at crash site |
| Performance regression | `python scripts/amal_harness.py` |
| Race condition / concurrency | `powershell -File scripts/test_stress.ps1` |

---

## Phase 2: Surgical Implementation

Apply ONLY the changes in `implementation_plan.md`. Touch ONLY the files listed.

**Hard Rules:**
- Zero `lock(stateLock)` introductions.
- Zero non-ASCII characters in any C# string literal.
- Zero managed exit methods (`ExitLong`, `ExitShort`) in unmanaged strategy files.
- Every new catch block MUST log -- never silent swallow.

---

## Phase 3: Post-Edit Verification (MANDATORY -- cannot skip)

Run ALL of the following. Each must pass before handoff.

```powershell
# 1. Logic regression test
dotnet test Testing.csproj --logger "console;verbosity=detailed"

# 2. Compile and ASCII gate
powershell -File scripts/build_readiness.ps1

# 3. AMAL post-edit (compare to tmp/p5_baseline_amal.txt)
python scripts/amal_harness.py

# 4. DNA grep audit
grep -rn "lock\s*(\s*stateLock\s*)" src/
grep -rn "ExitLong\|ExitShort" src/
```

**STOP** if any check fails. Fix the issue. Re-run from Phase 2. Do NOT hand off a failing state.

---

## Phase 4: Deploy and Sync

```powershell
# Re-establish hard links after file edits
powershell -File .\deploy-sync.ps1
```

Then tell the Director: **"Press F5 in NinjaTrader to compile. Verify BUILD_TAG banner."**

---

## Phase 5: Write P5 Handoff Note

Update `docs/brain/nexus_a2a.json`:
- `"phase"`: `"P6_VALIDATION_PENDING"`
- `"last_relay"`: `{ "agent": "Codex", "time": "<now UTC>", "status": "P5_COMPLETE" }`

Write the P5 Handoff Note in `docs/brain/nexus_a2a.json` under `"p5_handoff"`:
```json
{
  "files_modified": ["src/file1.cs", "src/file2.cs"],
  "baseline_tests": "9/9 PASS",
  "post_edit_tests": "9/9 PASS",
  "baseline_amal_alloc": "0 B",
  "post_edit_amal_alloc": "0 B",
  "deploy_sync": "PASS",
  "ascii_gate": "PASS"
}
```

---

## Phase 6: Trigger P6 Validation Worker

Direct the Director to launch a **NEW, FRESH Gemini CLI session** with this exact prompt:

```
You are the P6 Validation Worker for the V12 Universal OR Strategy project.
Read docs/brain/nexus_a2a.json for the P5 Handoff Note.
Then execute the /mission-validate workflow exactly as written.
Do not write any code. Run tests and return a verdict.
Workspace: C:\WSGTA\universal-or-strategy
```

**P6 is BANNED from running in this same session.**

---

## Phase 7: Mandatory Post-Use Self-Improvement Audit

1. Was any test gate unclear?
2. Did any Phase 2 constraint cause an unexpected issue?
3. Was the baseline-vs-post-edit comparison protocol clear?

**If no gap found:** `workflow(nexus-relay): no gaps identified.`
**Commit format:** `workflow(nexus-relay): [what was fixed and why]`
