# Mission Dashboard: Phase 7 Concurrency Hardening
**BUILD_TAG**: 1111.007-phase7-t4
**Repo**: mkalhitti-cloud/universal-or-strategy
**Branch**: main

---

## 🛰️ Current Phase: Pre-Epic Prep (Complexity Extraction Epic)

| Stage  | Role             | Purpose                              | Status              |
| :----- | :--------------- | :----------------------------------- | :------------------ |
| **P0** | **Admin**        | task.md sync, T16 registry, audit    | 🟢 **IN PROGRESS**  |
| **P1** | **Orchestrator** | Central Switchboard (Antigravity)    | 🟢 **ACTIVE**       |
| **P4** | **Engineer**     | Surgical Execution (Bob)             | ⬅ **NEXT**          |

---

## ✅ Sprint 5 — COMPLETE (T2 through T16, T-Q1, T-W1, T-H, T-W2)

| Ticket | Method | CYC Before | CYC After | Status |
| :----- | :----- | :--------: | :-------: | :----- |
| T2   | ExecuteOnExecutionUpdate_CIT_Repair | -- | -- | ✅ COMPLETE |
| T3   | ExecuteSmartDispatchEntry | 29 | 22* | ✅ COMPLETE |
| T4   | SubmitBracketOrders | -- | -- | ✅ COMPLETE |
| T13  | SweepBrokerOrders | 28 | 15 | ✅ COMPLETE |
| T14  | BuildUiLivePositionSnapshot | 20 | 2 | ✅ COMPLETE |
| T15  | ExecuteWatchdogDirectFallback | 20 | 3 | ✅ COMPLETE |
| T16  | CreateNewStopOrder | 21 | 6 | ✅ COMPLETE |
| T-Q1 | Empty-catch logging (4 files) | -- | -- | ✅ COMPLETE |
| T-W1 | ShouldSkipFleetAccount | 25 | 10 | ✅ COMPLETE |
| T-H  | ValidateStopPrice | 33 | 19 | ✅ COMPLETE |
| T-W2 | TryFindOrderInPosition | 25 | 8 | ✅ COMPLETE |

*T3 CYC: T03 doc=22, complexity_audit.py=33. Audit tool is authoritative — T-G Epic ticket reopens.

---

## 🎯 Next Epic: Phase 7 Complexity Extraction (Traycer)

**Epic Brief**: `artifacts/phase7_traycer_epic_brief.md`
**Fresh Audit**: `docs/brain/complexity_audit_cyc20_report.md` (2026-05-13, current)

### Pre-Epic Admin Checklist
- [x] Fresh complexity_audit.py run — 54 symbols, baseline confirmed
- [x] task.md updated to BUILD_TAG t16
- [ ] T16 entry added to Living_Document_Registry.md
- [ ] Traycer Epic created

### Phase 7 Epic Ticket Queue

| Ticket | Method | File | CYC | Status |
| :----- | :----- | :--- | :-: | :----- |
| T-Q1 | Empty catch logging | 4 files | -- | ✅ COMPLETE |
| T-W1 | ShouldSkipFleetAccount | SIMA.Fleet.cs | 25→10 | ✅ COMPLETE |
| T-H  | ValidateStopPrice | Orders.Mgmt.StopSync.cs | 33→19 | ✅ COMPLETE |
| T-W2 | TryFindOrderInPosition | Orders.Callbacks.AccountOrders.cs | 25→8 | ✅ COMPLETE |
| T-Q2 | IPC Server polling comment | IPC.Server.cs | -- | 🟡 READY |
| T-C  | AttachPanelHandlers | UI.Panel.Handlers.cs | 39 | 🟡 READY |
| T-D  | OnSyncAllClick | UI.Panel.Handlers.cs | 37 | 🟡 READY |
| T-E  | ManageTrail_RunPerTradeBranches | Trailing.cs | 36 | 🟡 READY |
| T-F  | UpdateContextualUI | UI.Panel.Handlers.cs | 36 | 🟡 READY |
| T-G  | ExecuteSmartDispatchEntry | SIMA.Dispatch.cs | 33 | 🟡 READY |
| T-A  | OnKeyDown | UI.Callbacks.cs | 49 | 🔵 P3 FIRST |
| T-B  | ProcessIpc_MatchSymbol | UI.IPC.cs | 49 | 🔵 P3 FIRST |

---

## 🅿️ Parked Follow-up: T-W1-Perf

**Function**: `ShouldSkipFleet_RunHealthCheck`
**Current CYC**: 20 (threshold: 18)
**Rationale**: Per-dispatch cadence 1-5 Hz, 2 enumerator allocations per invocation
**Status**: Documented for next Epic, not blocking Phase 7 acceptance
**Context**: Helper function extracted during T-W1 `ShouldSkipFleetAccount` refactoring. Marginal CYC overage (20 vs 18) with low-frequency execution profile does not warrant immediate optimization.
