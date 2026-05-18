# Cluster Bug Hunt
## V12 Photon Kernel -- Single-Cluster Bug Hunt (Any CLI)

> **Agent-agnostic** -- run this on ONE cluster per agent session.
> **Bob native**: `/cluster-bug-hunt` (`.bob/commands/cluster-bug-hunt.md`)
> **All CLIs**: paste the header prompt below with your cluster assignment.
> **Consolidation**: feed all 7 reports to Bob via `/bug-bounty-consolidate`
> **Full spec**: `docs/brain/bug_bounty_workflow.md`

---

## Trigger

Use this when an agent is assigned a single cluster to audit for bugs.
Each agent runs this independently with focused single-cluster context.
Output feeds Bob's consolidation phase (`/bug-bounty-consolidate`).

---

## How to Invoke (Any CLI)

Paste this header with your cluster assignment filled in:

```
MISSION: V12 Bug Hunt -- Single Cluster Audit
SPEC REF: docs/brain/bug_bounty_workflow.md
CLUSTER: [S1 / S2 / S3 / S4 / S5 / S6 / S7]
CLUSTER NAME: [SIMA Core / Execution Engine / UI & Photon IO / REAPER Defense / Kernel State / Signals & Entries / Kernel Infrastructure]
OUTPUT FILE: docs/brain/bug_report_s[N].md
MODE: READ-ONLY FORENSIC AUDIT -- NO src/ edits allowed.

Scope -- audit ONLY these files:
[paste file list for your assigned cluster]

Hunt for the following bug patterns in your cluster files ONLY:
  1. Race conditions -- shared state without atomic guards
  2. Use-after-free windows -- resource released before all references cleared
  3. Re-entrancy floods -- callbacks triggered inside critical sections
  4. Ghost order windows -- async ID registered before submission completes
  5. FSM state leaks -- incomplete reset during cancel/error
  6. Null ref hot paths -- property access before null check
  7. O(N^2) nested loops -- fleet/account list iterations
  8. Semaphore leaks -- missing finally blocks on release
  9. lock() remnants -- any remaining banned patterns
  10. Non-ASCII string literals -- compiler safety violations

Use jCodemunch tools for navigation:
  - get_file_outline -> map all symbols in your cluster files
  - get_blast_radius -> find cross-file impact of high-risk methods
  - find_references  -> trace shared state mutations
  - search_ast       -> pattern-match banned constructs

Report each finding in this EXACT format:
  BUG-[S#]-[NNN]
  Title: [short description]
  Severity: Critical / High / Med / Low
  Location: [file].[method] (line range if known)
  Root Cause: [exact mechanism -- be specific]
  Evidence: [exact code pattern or line reference]
  Test Impact: [which test type would catch this]

Write all findings to: docs/brain/bug_report_s[N].md

At the end of your report, include:
  AUDIT SUMMARY
  Files scanned: [N]
  Total findings: [N]
  Critical: [N] | High: [N] | Med: [N] | Low: [N]
  Confidence note: [any patterns you are uncertain about -- flag for consolidator]
```

---

## Cluster File Lists (copy the one matching your assignment)

### S1: SIMA Core (7 files)
```
src/V12_002.SIMA.cs
src/V12_002.SIMA.Dispatch.cs
src/V12_002.SIMA.Execution.cs
src/V12_002.SIMA.Flatten.cs
src/V12_002.SIMA.Fleet.cs
src/V12_002.SIMA.Lifecycle.cs
src/V12_002.SIMA.Shadow.cs
```

### S2: Execution Engine (16 files)
```
src/V12_002.Orders.Callbacks.cs
src/V12_002.Orders.Callbacks.AccountOrders.cs
src/V12_002.Orders.Callbacks.Execution.cs
src/V12_002.Orders.Callbacks.Propagation.cs
src/V12_002.Orders.CancelGateway.cs
src/V12_002.Orders.Management.cs
src/V12_002.Orders.Management.Cleanup.cs
src/V12_002.Orders.Management.Flatten.cs
src/V12_002.Orders.Management.StopSync.cs
src/V12_002.Symmetry.cs
src/V12_002.Symmetry.BracketFSM.cs
src/V12_002.Symmetry.Follower.cs
src/V12_002.Symmetry.Replace.cs
src/V12_002.Trailing.cs
src/V12_002.Trailing.Breakeven.cs
src/V12_002.Trailing.StopUpdate.cs
```

### S3: UI & Photon IO (16 files)
```
src/V12_002.UI.Callbacks.cs
src/V12_002.UI.Compliance.cs
src/V12_002.UI.IPC.cs
src/V12_002.UI.IPC.Commands.Config.cs
src/V12_002.UI.IPC.Commands.Fleet.cs
src/V12_002.UI.IPC.Commands.Misc.cs
src/V12_002.UI.IPC.Commands.Mode.cs
src/V12_002.UI.IPC.Server.cs
src/V12_002.UI.Panel.Brushes.cs
src/V12_002.UI.Panel.Construction.cs
src/V12_002.UI.Panel.Handlers.cs
src/V12_002.UI.Panel.Helpers.cs
src/V12_002.UI.Panel.Lifecycle.cs
src/V12_002.UI.Panel.StateSync.cs
src/V12_002.UI.Sizing.cs
src/V12_002.UI.Snapshot.cs
```

### S4: REAPER Defense (5 files)
```
src/V12_002.REAPER.cs
src/V12_002.REAPER.Audit.cs
src/V12_002.REAPER.NakedStop.cs
src/V12_002.REAPER.Repair.cs
src/V12_002.Safety.Watchdog.cs
```

### S5: Kernel State (5 files)
```
src/V12_002.Lifecycle.cs
src/V12_002.Properties.cs
src/V12_002.StickyState.cs
src/V12_002.StructuredLog.cs
src/V12_002.Telemetry.cs
```

### S6: Signals & Entries (7 files)
```
src/V12_002.Entries.cs
src/V12_002.Entries.FFMA.cs
src/V12_002.Entries.MOMO.cs
src/V12_002.Entries.OR.cs
src/V12_002.Entries.Retest.cs
src/V12_002.Entries.RMA.cs
src/V12_002.Entries.Trend.cs
```

### S7: Kernel Infrastructure (11 files)
```
src/SignalBroadcaster.cs
src/V12_002.cs
src/V12_002.AccountUpdate.cs
src/V12_002.Atm.cs
src/V12_002.BarUpdate.cs
src/V12_002.Constants.cs
src/V12_002.Data.cs
src/V12_002.DrawingHelpers.cs
src/V12_002.LogicAudit.cs
src/V12_002.PositionInfo.cs
src/V12_002.PureLogic.cs
```

---

## Full Spec Reference

See `docs/brain/bug_bounty_workflow.md` for the complete bug pattern list,
report format requirements, and how findings feed into `/bug-bounty-consolidate`.
