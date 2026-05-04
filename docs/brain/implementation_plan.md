# Implementation Plan: PR #73 Hardening -- Post-Review Compliance Pass
**BUILD_TAG**: Build-983-Phase4-Dispatcher
**BRANCH**: build-983-phase4-dispatcher-final
**PR**: #73
**AUTHORED_BY**: Claude Sonnet 4.6 (P3 Architect)
**DATE**: 2026-05-04T19:58:00Z
**STATUS**: AWAITING DIRECTOR APPROVAL

---

## MISSION

Address all critical and warning-level findings from the automated code review bots
(CodeRabbit, DeepSource, Kilo-Code-Bot) on PR #73. This is a pure compliance/hardening
pass -- no logic mutations, no new features. The goal is a clean merge to `main` with
zero open audit issues.

This plan consolidates with `docs/brain/master_roadmap.md`. When this plan is
executed and PR #73 merges, **M3 is closed** and Phase 4 is production-complete.

---

## DEFECT REGISTRY

| ID | Severity | File | Location | Description |
|:---|:---|:---|:---|:---|
| D1 | CRITICAL | `V12_002.Lifecycle.cs` | L70 | Phantom Block -- empty `catch { }` in actor drain loop |
| D2 | CRITICAL | `V12_002.Lifecycle.cs` | L75 | Phantom Block -- empty `catch { }` outer drain wrapper |
| D3 | WARNING  | `V12_002.Lifecycle.cs` | L481 | Phantom Block -- empty `catch { }` in MMIO Dispose |
| D4 | CRITICAL | `V12_002.Lifecycle.cs` | L68-72 | `cmd.Execute` runs without `_isTerminating` guard |
| D5 | WARNING  | `V12_002.Lifecycle.cs` | L101-102 | `DateTime.Parse` without `CultureInfo.InvariantCulture` |
| D6 | WARNING  | `V12_002.Properties.cs` | L406-412 | `EnablePhotonAffinityBind`/`CpuAffinityMask` never implemented |
| D7 | WARNING  | `V12_002.cs` | L579-581 | Unused dispatch performance counter fields |
| D8 | ADVISORY | `scripts/csharp_hotspots.py` | L47 | Ruff E713 -- non-idiomatic `not in` check |
| D9 | ADVISORY | `docs/brain/master_roadmap.md` | Multiple | Stale Build-982 / old branch references |
| D10 | DEFERRED | `V12_002.Lifecycle.cs` | L542 | Pre-existing phantom block in `ProcessOnConnectionStatusUpdate` -- out of scope (Karpathy rule) |

> [!NOTE]
> **SCOPE BOUNDARY**: D1-D9 are the only defects introduced or explicitly flagged by PR #73 bot review.
> D10 was discovered during P3 validation audit but is **pre-existing code not touched by this PR**.
> Per Karpathy protocol: do NOT fix D10 in this pass. Engineer must leave L542 untouched.
> D10 is logged for the next standalone cleanup pass.

---

## ARCHITECTURAL DECISIONS

### D4 -- Shutdown Guard Design
The guard must be placed **inside** the per-command try block, not as a pre-loop check.
Rationale: We still want to dequeue all items (preventing queue leak), but we must skip
`Execute()` once the strategy is terminating. Pattern: dequeue-always, execute-conditionally.

### D6 -- Photon Property Disposition
`EnablePhotonAffinityBind` and `CpuAffinityMask` are `[NinjaScriptProperty]` decorated,
meaning they are persisted in workspace XML. Hard-removal would generate deserialization
warnings in existing workspaces. The established V12 pattern (see `ReducedRiskPerTrade`
at `Properties.cs:L73-76`) is to retain the stub with `[Browsable(false)]` +
`[XmlIgnore]` + a comment citing the build that deactivated it. **We do NOT remove --
we stub with backward-compat annotation.** The `[NinjaScriptProperty]` attribute
is removed so it stops appearing in the NinjaTrader UI while still deserializing cleanly.

---

## SURGICAL EDITS

### EDIT 1: `src/V12_002.Lifecycle.cs` -- Fix D1, D2, D4, D5

**Target Region**: Lines 53-76 (DrainQueuesForShutdown) and Lines 100-102 (SetDefaults session defaults)

#### BROKEN (current):
```csharp
private void DrainQueuesForShutdown()
{
    try
    {
        Print("[SHUTDOWN] Draining queues...");
        int ipcDrained = 0;
        if (ipcCommandQueue != null)
        {
            while (ipcDrained < 100 && ipcCommandQueue.TryDequeue(out string _))
            {
                ipcDrained++;
            }
        }

        int actorDrained = 0;
        while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
        {
            try { cmd.Execute(this); } catch { }
            actorDrained++;
        }
        Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds and {1} Actor cmds.", ipcDrained, actorDrained));
    }
    catch { }
}
```

#### FIXED:
```csharp
private void DrainQueuesForShutdown()
{
    try
    {
        Print("[SHUTDOWN] Draining queues...");
        int ipcDrained = 0;
        if (ipcCommandQueue != null)
        {
            while (ipcDrained < 100 && ipcCommandQueue.TryDequeue(out string _))
            {
                ipcDrained++;
            }
        }

        int actorDrained = 0;
        while (actorDrained < 50 && _cmdQueue.TryDequeue(out StrategyCommand cmd))
        {
            // D4: Guard -- discard queued commands during teardown; still dequeue to clear the queue.
            if (!_isTerminating)
            {
                try { cmd.Execute(this); }
                catch (Exception ex) { Print("[SHUTDOWN_ERROR] Actor cmd failed: " + ex.Message); } // D1
            }
            actorDrained++;
        }
        Print(string.Format("[SHUTDOWN] Drained {0} IPC cmds and {1} Actor cmds.", ipcDrained, actorDrained));
    }
    catch (Exception ex) { Print("[SHUTDOWN_ERROR] DrainQueuesForShutdown: " + ex.Message); } // D2
}
```

**Note on D4**: `_isTerminating` is set to `true` at `OnStateChangeTerminated:L440`, which
runs before `DrainQueuesForShutdown` is called at L464. The guard will always be `true`
at drain time, which means actor commands are discarded (correct behavior). The dequeue
still runs so the queue is emptied cleanly.

---

**Target Region**: Lines 100-102 (SessionStart/SessionEnd defaults)

#### BROKEN (current):
```csharp
SessionStart = DateTime.Parse("09:30");
SessionEnd = DateTime.Parse("16:00");
```

#### FIXED (D5):
```csharp
// D5: InvariantCulture prevents locale-dependent parse failures (e.g. European time separators).
SessionStart = DateTime.Parse("09:30", System.Globalization.CultureInfo.InvariantCulture);
SessionEnd   = DateTime.Parse("16:00", System.Globalization.CultureInfo.InvariantCulture);
```

---

### EDIT 2: `src/V12_002.Lifecycle.cs` -- Fix D3

**Target Region**: Lines 479-483 (MMIO Dispose in OnStateChangeTerminated)

#### BROKEN (current):
```csharp
if (_photonMmioMirror != null)
{
    try { _photonMmioMirror.Dispose(); } catch { }
    _photonMmioMirror = null;
}
```

#### FIXED (D3):
```csharp
if (_photonMmioMirror != null)
{
    try { _photonMmioMirror.Dispose(); }
    catch (Exception ex) { Print("[SHUTDOWN_ERROR] MMIO mirror dispose failed: " + ex.Message); } // D3
    _photonMmioMirror = null;
}
```

---

### EDIT 3: `src/V12_002.Properties.cs` -- Fix D6

**Target Region**: Lines 406-412 (Photon Kernel properties block)

#### BROKEN (current):
```csharp
[NinjaScriptProperty]
[Display(Name = "Enable Photon Affinity Bind", GroupName = "Photon Kernel", Order = 1)]
public bool EnablePhotonAffinityBind { get; set; }

[NinjaScriptProperty]
[Display(Name = "CPU Affinity Mask", GroupName = "Photon Kernel", Order = 2)]
public int CpuAffinityMask { get; set; }
```

#### FIXED (D6 -- backward-compat stub, V12 pattern matching ReducedRiskPerTrade):
```csharp
/// <summary>REMOVED (Build-983). Photon CPU affinity deferred to M4.
/// Stub retained for workspace XML backward compatibility.</summary>
[Browsable(false)]
[System.Xml.Serialization.XmlIgnore]
public bool EnablePhotonAffinityBind { get; set; }

/// <summary>REMOVED (Build-983). Photon CPU affinity deferred to M4.
/// Stub retained for workspace XML backward compatibility.</summary>
[Browsable(false)]
[System.Xml.Serialization.XmlIgnore]
public int CpuAffinityMask { get; set; }
```

**Note**: Removing `[NinjaScriptProperty]` and `[Display]`, adding `[Browsable(false)]`
and `[XmlIgnore]` hides these from the NT8 UI while preserving XML deserialization
for existing saved workspaces. This is the identical pattern used by `ReducedRiskPerTrade`
at `Properties.cs:L73-76`.

---

### EDIT 4: `src/V12_002.cs` -- Fix D7

**Target Region**: Lines 579-581 (unused dispatch counter declarations)

#### BROKEN (current):
```csharp
private long _dispatchInvocationCount = 0;
private long _dispatchPeakElapsedTicks = 0;
private long _dispatchTotalElapsedTicks = 0;
```

#### FIXED (D7 -- remove 3 dead fields):
```csharp
// D7: _dispatchInvocationCount / _dispatchPeakElapsedTicks / _dispatchTotalElapsedTicks
// removed (Build-983). Fields were declared but never wired into EmitMetricsSummary.
// Re-introduce if/when FleetDispatch performance telemetry is instrumented (M5).
```

---

### EDIT 5: `scripts/csharp_hotspots.py` -- Fix D8

**Target Region**: Line 47

#### BROKEN (current):
```python
if match and not ';' in line_stripped and not '=' in line_stripped:
```

#### FIXED (D8 -- Ruff E713 idiomatic form):
```python
if match and ';' not in line_stripped and '=' not in line_stripped:
```

---

### EDIT 6: `docs/brain/master_roadmap.md` -- Fix D9

**Lines to update**:

| Line | BROKEN | FIXED |
|:---|:---|:---|
| 2 | `## Build-982-Phase2-RAII Closed \| ADR-020 Phase 4 Next` | `## Build-983-Phase4-Dispatcher \| PR #73 Hardening Pass` |
| 5 | `` `feature/phase-4-event-lifecycle` `` | `` `build-983-phase4-dispatcher-final` `` |
| 43 | `Phase 4 \| Event Lifecycle Dispatcher (ADR-020) \| NEXT` | `Phase 4 \| Event Lifecycle Dispatcher (ADR-020) \| IN PROGRESS -- PR #73` |
| 53 | `Phase 4 Event Lifecycle Dispatcher \| IN PROGRESS` | `Phase 4 Event Lifecycle Dispatcher \| PR #73 -- Hardening Pass` |
| 75 | `- [ ] Push feature/phase-4-event-lifecycle to GitHub` | `- [x] PR #73 open on build-983-phase4-dispatcher-final` |
| 78 | Step 2 header | Add note: P6 validation re-confirmed post-Build-983 |
| 150 | `[PASS] Zero empty try { } in src/*.cs` | `[PENDING] 3 phantom blocks identified -- see PR #73 D1/D2/D3` |
| 158 | `[PENDING] Push needed before Arena AI step` | `[OPEN] PR #73 -- hardening pass in progress` |

---

## EXECUTION ORDER FOR ENGINEER (P5)

The following order minimizes context switches:

```
Step 1: Edit src/V12_002.Lifecycle.cs
  1a. DrainQueuesForShutdown -- apply D1, D2, D4 (lines 53-76)
  1b. OnStateChangeSetDefaults -- apply D5 (lines 101-102)
  1c. OnStateChangeTerminated -- apply D3 (lines 479-483)

Step 2: Edit src/V12_002.Properties.cs
  2a. Photon Kernel block -- apply D6 (lines 406-412)

Step 3: Edit src/V12_002.cs
  3a. Remove dispatch counter fields -- apply D7 (lines 579-581)

Step 4: Edit scripts/csharp_hotspots.py
  4a. Fix E713 lint -- apply D8 (line 47)

Step 5: Edit docs/brain/master_roadmap.md
  5a. Apply D9 metadata corrections (multiple lines)

Step 6: SELF-AUDIT (mandatory before handoff)
  6a. grep -rn "catch { }" src/     --> must return ZERO hits
  6b. grep -rn "lock(" src/         --> must return ZERO hits (existing clean state)
  6c. python scripts/check_ascii.py --> must return all PASS

Step 7: deploy-sync.ps1
  7a. powershell -File .\deploy-sync.ps1
  7b. ASCII Gate must PASS
  7c. Instruct Director to press F5 in NinjaTrader
  7d. Verify BUILD_TAG banner shows 1111.004-v28.0-pr56 (or next increment)

Step 8: Commit and push to build-983-phase4-dispatcher-final
  8a. Commit message: "fix(pr73): resolve CodeRabbit/DeepSource findings -- phantom blocks, shutdown guard, culture parse, unused fields"
  8b. Push -- PR #73 will auto-update
```

---

## VERIFICATION CRITERIA

| Check | Tool | Pass Condition |
|:---|:---|:---|
| Zero phantom blocks | `grep -rn "catch { }" src/` | 0 matches |
| Zero lock usage | `grep -rn "lock(" src/` | 0 matches |
| ASCII compliance | `python scripts/check_ascii.py` | All PASS |
| Compilation | F5 in NinjaTrader | BUILD_TAG banner visible |
| Python lint | `ruff check scripts/csharp_hotspots.py` | 0 E713 violations |
| PR audit bots | Push to branch | CodeRabbit / DeepSource show 0 new issues |

---

## ROADMAP CONSOLIDATION

This plan directly serves `master_roadmap.md` Step 6 (P7 Sentinel / Close M3):

- When PR #73 merges after this hardening pass, **Phase 4 is production-complete**.
- **M3 closes** when: this plan is executed (P5) + validated (P6) + PR #73 merges to main (P7).
- No additional architectural work is required for M3 closure.

Post-merge, the next session should:
1. Update `nexus_a2a.json` to reflect `P7 COMPLETE`.
2. Update `master_roadmap.md` Phase 4 status to `DONE`.
3. Update M3 status to `COMPLETE`.

---

## DNA COMPLIANCE CHECKLIST

- [x] Zero `lock(stateLock)` introduced
- [x] All new `catch` blocks log via `Print()` (ASCII-safe messages only)
- [x] No Unicode, emoji, or curly quotes in any C# string literal
- [x] No `Thread.Sleep` or blocking calls introduced
- [x] `_isTerminating` guard uses existing field (no new state introduced)
- [x] `CultureInfo.InvariantCulture` from existing `using System.Globalization;` import
- [x] Photon property stub follows established `[Browsable(false)][XmlIgnore]` pattern
- [x] Dispatch counter comment documents deferral reason and milestone (M5)
- [x] Python fix is purely idiomatic -- no logic change

---

Plan saved to docs/brain/implementation_plan.md. Awaiting Director approval.
