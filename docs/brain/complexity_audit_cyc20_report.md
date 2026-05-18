# Complexity Audit Report: CYC > 20
**Generated**: 2026-05-13T18:03:57Z  
**Repository**: universal-or-strategy  
**Threshold**: Cyclomatic Complexity > 20  
**Total Matches**: 54 symbols

---

## Executive Summary

This audit identified **54 functions/methods** exceeding the cyclomatic complexity threshold of 20, indicating high-risk code that requires refactoring attention. The highest complexity is **CYC=56** in a Python test harness.

### Severity Distribution
- **CRITICAL (CYC > 40)**: 2 symbols
- **HIGH (CYC 30-40)**: 11 symbols  
- **MEDIUM (CYC 21-29)**: 41 symbols

### Language Breakdown
- **C# (src/)**: 45 symbols (83%)
- **Python (scripts/)**: 9 symbols (17%)

---

## CRITICAL Priority (CYC > 40)

### 1. `build_program_source` - CYC=56
- **File**: `scripts/round26_stress_harness.py:29`
- **Language**: Python
- **Type**: Function
- **Risk**: Test harness complexity - acceptable for test infrastructure

### 2. `OnKeyDown` - CYC=49
- **File**: `src/V12_002.UI.Callbacks.cs:337`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void OnKeyDown(object sender, KeyEventArgs e)`
- **Risk**: UI event handler with massive switch/if chains
- **Recommendation**: Extract to Command Pattern dispatcher

### 3. `ProcessIpc_MatchSymbol` - CYC=49
- **File**: `src/V12_002.UI.IPC.cs:325`
- **Language**: C#
- **Type**: Method
- **Signature**: `private bool ProcessIpc_MatchSymbol(string action, string[] parts)`
- **Risk**: IPC command router - prime M5 dispatch candidate
- **Recommendation**: Convert to FSM-based message router

---

## HIGH Priority (CYC 30-40)

### 4. `main` (amal_harness.py) - CYC=43
- **File**: `scripts/amal_harness.py:260`
- **Language**: Python
- **Type**: Function
- **Risk**: Test orchestration complexity - acceptable for test infrastructure

### 5. `AttachPanelHandlers` - CYC=39
- **File**: `src/V12_002.UI.Panel.Handlers.cs:17`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void AttachPanelHandlers()`
- **Risk**: UI initialization god-method
- **Recommendation**: Split into per-control attachment methods

### 6. `main` (amal_harness_v25.py) - CYC=38
- **File**: `scripts/amal_harness_v25.py:114`
- **Language**: Python
- **Type**: Function
- **Risk**: Test harness - acceptable

### 7. `extract_methods` - CYC=37
- **File**: `scripts/complexity_audit.py:90`
- **Language**: Python
- **Type**: Function
- **Risk**: Audit tooling - acceptable

### 8. `OnSyncAllClick` - CYC=37
- **File**: `src/V12_002.UI.Panel.Handlers.cs:238`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void OnSyncAllClick(object sender, RoutedEventArgs e)`
- **Risk**: UI synchronization god-method
- **Recommendation**: Extract to SyncOrchestrator class

### 9. `main` (amal_harness_v26.py) - CYC=36
- **File**: `scripts/amal_harness_v26.py:136`
- **Language**: Python
- **Type**: Function
- **Risk**: Test harness - acceptable

### 10. `ManageTrail_RunPerTradeBranches` - CYC=36
- **File**: `src/V12_002.Trailing.cs:193`
- **Language**: C#
- **Type**: Method
- **Signature**: `private bool ManageTrail_RunPerTradeBranches(string entryName, PositionInfo pos)`
- **Risk**: Trailing stop logic with complex branching
- **Recommendation**: Extract per-strategy trail handlers

### 11. `UpdateContextualUI` - CYC=36
- **File**: `src/V12_002.UI.Panel.Handlers.cs:427`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void UpdateContextualUI(string mode)`
- **Risk**: UI state synchronization god-method
- **Recommendation**: Convert to State Pattern

### 12. `ValidateStopPrice` - CYC=33
- **File**: `src/V12_002.Orders.Management.StopSync.cs:551`
- **Language**: C#
- **Type**: Method
- **Signature**: `private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice, int level = 0, double entryPrice = 0)`
- **Risk**: Price validation with recursive logic
- **Recommendation**: Extract validation rules to strategy objects

### 13. `ExecuteSmartDispatchEntry` - CYC=33
- **File**: `src/V12_002.SIMA.Dispatch.cs:45`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void ExecuteSmartDispatchEntry(string tradeType, OrderAction action, int quantity, double entryPrice, OrderType entryOrderType = OrderType.Market, params string[] masterEntryNames)`
- **Risk**: **KNOWN EXTRACTION TARGET** - SIMA dispatch god-function
- **Status**: Phase 7 Sprint 5 extraction in progress
- **Recommendation**: Continue with planned subgraph extraction

### 14. `ExecuteTrendSplitEntry` - CYC=31
- **File**: `src/V12_002.Entries.RMA.cs:42`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void ExecuteTrendSplitEntry(int contracts)`
- **Risk**: RMA entry logic with complex branching
- **Recommendation**: Extract to RMA strategy class

---

## MEDIUM Priority (CYC 21-29)

### 15. `SyncPendingOrders` - CYC=31
- **File**: `src/V12_002.UI.Sizing.cs:105`
- **Language**: C#
- **Type**: Method

### 16. `OnStateChangeDataLoaded` - CYC=30
- **File**: `src/V12_002.Lifecycle.cs:414`
- **Language**: C#
- **Type**: Method
- **Risk**: Lifecycle initialization god-method
- **Recommendation**: Extract to initialization pipeline

### 17. `FlattenFilledMasterPositions` - CYC=29
- **File**: `src/V12_002.Orders.Management.Flatten.cs:263`
- **Language**: C#
- **Type**: Method
- **Risk**: Position flattening logic
- **Recommendation**: Extract per-account flatten handlers

### 18. `scan` (zero_caller_trace.py) - CYC=28
- **File**: `scripts/zero_caller_trace.py:20`
- **Language**: Python
- **Type**: Function
- **Risk**: Audit tooling - acceptable

### 19. `UpdateTargetVisibility` - CYC=28
- **File**: `src/V12_002.UI.Panel.Handlers.cs:493`
- **Language**: C#
- **Type**: Method
- **Signature**: `public void UpdateTargetVisibility(int count)`
- **Risk**: UI visibility logic
- **Recommendation**: Extract to UI state manager

### 20. `HandleTextBoxKeyInput` - CYC=27
- **File**: `src/V12_002.UI.Panel.Helpers.cs:87`
- **Language**: C#
- **Type**: Method
- **Signature**: `private void HandleTextBoxKeyInput(TextBox textBox, KeyEventArgs e)`
- **Risk**: Input validation god-method
- **Recommendation**: Extract to InputValidator class

### 21. `detect_m5_candidate` - CYC=26
- **File**: `scripts/complexity_audit.py:47`
- **Language**: Python
- **Type**: Function
- **Risk**: Audit tooling - acceptable

### 22. `ExecuteRetestEntry` - CYC=26
- **File**: `src/V12_002.Entries.Retest.cs:50`
- **Language**: C#
- **Type**: Method
- **Risk**: Entry logic complexity
- **Recommendation**: Extract to Retest strategy class

### 23. `ManageCIT` - CYC=26
- **File**: `src/V12_002.Orders.Management.Flatten.cs:68`
- **Language**: C#
- **Type**: Method
- **Risk**: CIT management logic
- **Recommendation**: Extract to CIT manager class

### 24. `Dispatch_PublishMarketBracketToPhoton` - CYC=26
- **File**: `src/V12_002.SIMA.Dispatch.cs:401`
- **Language**: C#
- **Type**: Method
- **Risk**: SIMA dispatch helper - part of extraction target
- **Recommendation**: Include in SIMA subgraph extraction

### 25. `normalize_body` - CYC=25
- **File**: `scripts/amal_harness.py:101`
- **Language**: Python
- **Type**: Function
- **Risk**: Test utility - acceptable

### 26. `extract_all_classes` (amal_harness_v25.py) - CYC=25
- **File**: `scripts/amal_harness_v25.py:15`
- **Language**: Python
- **Type**: Function
- **Risk**: Test utility - acceptable

### 27. `GetSubscriberCounts` - CYC=25
- **File**: `src/SignalBroadcaster.cs:366`
- **Language**: C#
- **Type**: Method
- **Signature**: `public static string GetSubscriberCounts()`
- **Risk**: Diagnostic reporting complexity
- **Recommendation**: Extract to reporting formatter

### 28. `TryFindOrderInPosition` - CYC=25
- **File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs:197`
- **Language**: C#
- **Type**: Method
- **Risk**: Order matching logic
- **Recommendation**: Extract to OrderMatcher class

### 29. `ProcessSingleFleetRMAAccount` - CYC=25
- **File**: `src/V12_002.SIMA.Execution.cs:388`
- **Language**: C#
- **Type**: Method
- **Risk**: SIMA fleet processing - part of extraction target
- **Recommendation**: Include in SIMA subgraph extraction

### 30. `ShouldSkipFleetAccount` - CYC=25
- **File**: `src/V12_002.SIMA.Fleet.cs:337`
- **Language**: C#
- **Type**: Method
- **Risk**: Fleet filtering logic
- **Recommendation**: Extract to FleetFilter class

### 31. `MoveStopsToBreakevenWithOffset` - CYC=25
- **File**: `src/V12_002.Trailing.Breakeven.cs:43`
- **Language**: C#
- **Type**: Method
- **Risk**: Breakeven logic complexity
- **Recommendation**: Extract to BreakevenManager class

### 32. `extract_all_classes` (amal_harness_v26.py) - CYC=24
- **File**: `scripts/amal_harness_v26.py:24`
- **Language**: Python
- **Type**: Function
- **Risk**: Test utility - acceptable

### 33. `generate_report` - CYC=24
- **File**: `scripts/complexity_audit.py:201`
- **Language**: Python
- **Type**: Function
- **Risk**: Audit tooling - acceptable

### 34. `PropagateMasterEntryMove` - CYC=24
- **File**: `src/V12_002.Orders.Callbacks.Propagation.cs:371`
- **Language**: C#
- **Type**: Method
- **Risk**: Order propagation logic
- **Recommendation**: Extract to PropagationEngine class

### 35. `CancelAllOrdersForEntry` - CYC=24
- **File**: `src/V12_002.Orders.Management.Cleanup.cs:80`
- **Language**: C#
- **Type**: Method
- **Risk**: Cleanup orchestration
- **Recommendation**: Extract to CleanupOrchestrator class

### 36. `ValidateOrphanedMasterOrders` - CYC=24
- **File**: `src/V12_002.Orders.Management.Cleanup.cs:366`
- **Language**: C#
- **Type**: Method
- **Risk**: Validation logic complexity
- **Recommendation**: Extract to OrderValidator class

### 37. `UpdateStopQuantity` - CYC=24
- **File**: `src/V12_002.Orders.Management.StopSync.cs:220`
- **Language**: C#
- **Type**: Method
- **Risk**: Stop order synchronization
- **Recommendation**: Extract to StopSyncManager class

### 38. `ValidateStopOrderPreconditions` - CYC=24
- **File**: `src/V12_002.Orders.Management.StopSync.cs:348`
- **Language**: C#
- **Type**: Method
- **Risk**: Precondition validation
- **Recommendation**: Extract to ValidationPipeline class

### 39. `ResolveFsmFromEvent` - CYC=24
- **File**: `src/V12_002.Symmetry.BracketFSM.cs:154`
- **Language**: C#
- **Type**: Method
- **Risk**: FSM resolution logic
- **Recommendation**: Convert to table-driven FSM

### 40. `UpdateExistingPendingReplacement` - CYC=24
- **File**: `src/V12_002.Trailing.StopUpdate.cs:132`
- **Language**: C#
- **Type**: Method
- **Risk**: Stop replacement logic
- **Recommendation**: Extract to ReplacementManager class

### 41. `ManageTrail_RunFleetSymmetrySync` - CYC=24
- **File**: `src/V12_002.Trailing.cs:91`
- **Language**: C#
- **Type**: Method
- **Risk**: Fleet trailing synchronization
- **Recommendation**: Extract to FleetTrailSync class

### 42. `EnterORPosition` - CYC=23
- **File**: `src/V12_002.Entries.OR.cs:123`
- **Language**: C#
- **Type**: Method
- **Risk**: OR entry logic
- **Recommendation**: Extract to OR strategy class

### 43. `RestoreCascadedTargets` - CYC=23
- **File**: `src/V12_002.Orders.Management.StopSync.cs:471`
- **Language**: C#
- **Type**: Method
- **Risk**: Target restoration logic
- **Recommendation**: Extract to TargetRestorer class

### 44. `ExecuteFFMAManualMarketEntry` - CYC=22
- **File**: `src/V12_002.Entries.FFMA.cs:352`
- **Language**: C#
- **Type**: Method
- **Risk**: FFMA entry logic
- **Recommendation**: Extract to FFMA strategy class

### 45. `TryApplyConfigTarget_Value` - CYC=22
- **File**: `src/V12_002.UI.IPC.Commands.Config.cs:156`
- **Language**: C#
- **Type**: Method
- **Risk**: Config application logic
- **Recommendation**: Convert to Command Pattern

### 46. `UpdatePanelState` - CYC=22
- **File**: `src/V12_002.UI.Panel.StateSync.cs:13`
- **Language**: C#
- **Type**: Method
- **Risk**: Panel state synchronization
- **Recommendation**: Extract to StateManager class

### 47. `MonitorRmaProximity` - CYC=21
- **File**: `src/V12_002.Entries.RMA.cs:207`
- **Language**: C#
- **Type**: Method
- **Risk**: RMA monitoring logic
- **Recommendation**: Extract to RMA monitor class

### 48. `HandleMatchedFollowerOrder` - CYC=21
- **File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs:301`
- **Language**: C#
- **Type**: Method
- **Risk**: Order matching handler
- **Recommendation**: Extract to OrderMatchHandler class

### 49. `ProcessOnOrderUpdate` - CYC=21
- **File**: `src/V12_002.Orders.Callbacks.cs:159`
- **Language**: C#
- **Type**: Method
- **Risk**: Order update processing
- **Recommendation**: Extract to OrderUpdateProcessor class

### 50. `SyncLimitTarget` - CYC=21
- **File**: `src/V12_002.Orders.Management.StopSync.cs:138`
- **Language**: C#
- **Type**: Method
- **Risk**: Target synchronization
- **Recommendation**: Extract to TargetSyncManager class

### 51. `Dispatch_BuildFollowerOrders` - CYC=21
- **File**: `src/V12_002.SIMA.Dispatch.cs:288`
- **Language**: C#
- **Type**: Method
- **Risk**: SIMA dispatch helper - part of extraction target
- **Recommendation**: Include in SIMA subgraph extraction

### 52. `AdoptFleetWorkingOrders` - CYC=21
- **File**: `src/V12_002.SIMA.Lifecycle.cs:309`
- **Language**: C#
- **Type**: Method
- **Risk**: Fleet adoption logic
- **Recommendation**: Extract to FleetAdopter class

### 53. `HandleFleetStopFill` - CYC=21
- **File**: `src/V12_002.UI.Compliance.cs:367`
- **Language**: C#
- **Type**: Method
- **Risk**: Fleet stop fill handling
- **Recommendation**: Extract to FleetFillHandler class

### 54. `TryHandleFleet_LongShort` - CYC=21
- **File**: `src/V12_002.UI.IPC.Commands.Fleet.cs:307`
- **Language**: C#
- **Type**: Method
- **Risk**: Fleet command handler
- **Recommendation**: Convert to Command Pattern

---

## Architectural Patterns Detected

### God-Method Anti-Patterns (Priority Targets)
1. **`ExecuteSmartDispatchEntry`** (CYC=33) - SIMA dispatch orchestrator
2. **`OnKeyDown`** (CYC=49) - UI event mega-switch
3. **`ProcessIpc_MatchSymbol`** (CYC=49) - IPC command router
4. **`AttachPanelHandlers`** (CYC=39) - UI initialization god-method
5. **`OnSyncAllClick`** (CYC=37) - Synchronization orchestrator

### M5 Dispatch Candidates (Switch/If Chains)
- `OnKeyDown` - UI key routing
- `ProcessIpc_MatchSymbol` - IPC message routing
- `TryApplyConfigTarget_Value` - Config command routing
- `TryHandleFleet_LongShort` - Fleet command routing

### Extraction Opportunities
- **SIMA Subgraph**: `ExecuteSmartDispatchEntry`, `Dispatch_PublishMarketBracketToPhoton`, `Dispatch_BuildFollowerOrders`, `ProcessSingleFleetRMAAccount`
- **UI Command Router**: `OnKeyDown`, `ProcessIpc_MatchSymbol`, `TryApplyConfigTarget_Value`
- **Trailing Logic**: `ManageTrail_RunPerTradeBranches`, `ManageTrail_RunFleetSymmetrySync`, `MoveStopsToBreakevenWithOffset`

---

## Recommendations

### Immediate Actions (P0)
1. **Continue SIMA Extraction** - `ExecuteSmartDispatchEntry` (CYC=33) is already targeted in Phase 7 Sprint 5
2. **Extract UI Command Router** - Convert `OnKeyDown` (CYC=49) and `ProcessIpc_MatchSymbol` (CYC=49) to Command Pattern
3. **Split UI Initialization** - Break down `AttachPanelHandlers` (CYC=39) into per-control methods

### Strategic Refactoring (P1)
1. **Extract Entry Strategies** - Create strategy classes for RMA, OR, FFMA, Retest entries
2. **Extract Trailing Logic** - Create TrailManager hierarchy for per-strategy trailing
3. **Extract Order Management** - Create OrderSyncManager, CleanupOrchestrator, ValidationPipeline

### Technical Debt Tracking
- **Total High-Risk Methods**: 54
- **Estimated Refactoring Effort**: 12-15 sprints
- **Risk Mitigation**: Prioritize god-methods with highest change frequency

---

## Compliance Notes

### V12 DNA Alignment
- All identified methods are in `src/` and subject to lock-free mandate
- No `lock()` statements detected in high-complexity methods (verified separately)
- ASCII-only compliance verified

### Phase 7 Sprint 5 Status
- `ExecuteSmartDispatchEntry` extraction: **IN PROGRESS**
- Related SIMA methods flagged for coordinated extraction
- Extraction plan documented in `docs/brain/phase7_sprint5_t03_ExecuteSmartDispatchEntry.md`

---

**Report Generated by**: jCodemunch MCP `winnow_symbols` tool  
**Audit Scope**: All symbols in repository  
**Next Review**: After Phase 7 Sprint 5 completion