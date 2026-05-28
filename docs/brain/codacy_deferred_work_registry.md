# Codacy Deferred Work Registry

**Date**: 2026-05-28  
**Total Deferred**: 375 issues  
**Actionable**: 17 issues (PR #9)  
**False Positives**: 353 issues (PR #8)  
**Non-Existent**: 5 issues (PR #14)  
**Status**: Documented and tracked

---

## Overview

This registry tracks all Codacy issues that were deferred during Phase 1 PR analysis. Issues are categorized by actionability, priority, and effort required.

**Key Principle**: Not all Codacy issues require action. Some are false positives, some are threshold mismatches with V12 standards, and some are legitimate but require breaking changes.

---

## Category 1: Breaking Changes (17 issues)

### Metadata
- **Origin**: PR #9 (MERGED)
- **Priority**: MEDIUM
- **Effort**: 2-3 days
- **Status**: DEFERRED - Requires separate epic
- **Recommended Epic**: EPIC-SIGNALS-REFACTOR
- **Target Sprint**: TBD (after Phase 2 baseline cleanup)

### Issues Breakdown

#### Subcategory A: Event Handler Signatures (8 HIGH)

**File**: `src/SignalBroadcaster.cs`  
**Issue**: Codacy wants `EventHandler<T>` instead of `Action<T>`  
**Impact**: BREAKING CHANGE - all subscribers must update signatures  
**Severity**: HIGH

**Affected Events**:

1. **Line 182**: `public static event Action<TradeSignal> OnTradeSignal;`
2. **Line 188**: `public static event Action<TrailUpdateSignal> OnTrailUpdate;`
3. **Line 200**: `public static event Action<FlattenSignal> OnFlattenAll;`
4. **Line 206**: `public static event Action<BreakevenSignal> OnBreakevenRequest;`
5. **Line 212**: `public static event Action<StopUpdateSignal> OnStopUpdate;`
6. **Line 218**: `public static event Action<EntryUpdateSignal> OnEntryUpdate;`
7. **Line 224**: `public static event Action<OrderCancelSignal> OnOrderCancel;`
8. **Line 230**: `public static event Action<ExternalCommandSignal> OnExternalCommand;`

**Migration Pattern**:
```csharp
// Before
public static event Action<TradeSignal> OnTradeSignal;
SignalBroadcaster.OnTradeSignal += (signal) => { /* handle */ };

// After
public static event EventHandler<TradeSignal> OnTradeSignal;
SignalBroadcaster.OnTradeSignal += (sender, signal) => { /* handle */ };
```

**Deferral Rationale**:
- Requires updating ALL subscribers across codebase
- Breaking change needs proper impact analysis
- Should include migration guide for external consumers
- Needs comprehensive testing of all signal paths

#### Subcategory B: Missing IEquatable<T> (9 MEDIUM)

**File**: `src/SignalBroadcaster.cs`  
**Issue**: Structs should implement `IEquatable<T>` for performance  
**Impact**: Performance optimization (eliminates reflection in equality checks)  
**Severity**: MEDIUM

**Affected Structs**:

1. **Line 20**: `public struct TradeSignal`
2. **Line 56**: `public struct TrailUpdateSignal`
3. **Line 71**: `public struct StopUpdateSignal`
4. **Line 86**: `public struct EntryUpdateSignal`
5. **Line 100**: `public struct OrderCancelSignal`
6. **Line 113**: `public struct TargetActionSignal`
7. **Line 142**: `public struct FlattenSignal`
8. **Line 154**: `public struct BreakevenSignal`
9. **Line 167**: `public struct ExternalCommandSignal`

**Implementation Pattern**:
```csharp
public struct TradeSignal : IEquatable<TradeSignal>
{
    public string Symbol;
    public int Quantity;
    
    public bool Equals(TradeSignal other)
    {
        return Symbol == other.Symbol && Quantity == other.Quantity;
    }
    
    public override bool Equals(object obj)
    {
        return obj is TradeSignal other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(Symbol, Quantity);
    }
    
    public static bool operator ==(TradeSignal left, TradeSignal right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(TradeSignal left, TradeSignal right)
    {
        return !left.Equals(right);
    }
}
```

**Deferral Rationale**:
- Performance optimization, not critical bug
- Should be done alongside event handler migration
- Part of broader signal refactoring effort

### Epic Specification: EPIC-SIGNALS-REFACTOR

**Scope**:
1. Migrate all 8 events to `EventHandler<T>` pattern
2. Implement `IEquatable<T>` for all 9 signal structs
3. Update all subscribers across codebase
4. Create migration guide for external consumers
5. Add comprehensive signal tests

**Effort**: 2-3 days  
**Priority**: MEDIUM  
**Dependencies**: None  
**Blocking**: None

---

## Category 2: Threshold Mismatch (29 issues)

### Metadata
- **Origin**: PR #8 (MERGED)
- **Priority**: LOW
- **Effort**: 0 (documentation only)
- **Status**: DEFERRED - Won't Fix
- **Recommended Action**: Document in `.codacy.yml` comments

### Issues Breakdown

#### CYC Violations (13 methods)

**Threshold Mismatch**:
- **Codacy Limit**: CYC ≤ 8 (SonarQube default)
- **V12 Limit**: CYC ≤ 15 (Jane Street aligned)

| Method | CYC | V12 Status |
|--------|-----|------------|
| SyncLimitTarget | 18 | ⚠️ ABOVE (tracked in EPIC-CCN-10) |
| SymmetryGuardTryResolveFollower | 14 | ✅ BELOW |
| SymmetryGuardOnMasterFill | 14 | ✅ BELOW |
| InitiateStopReplacement | 13 | ✅ BELOW |
| CancelAllOrdersForEntry | 12 | ✅ BELOW |
| ManageTrail_ApplyPointBasedCascade | 11 | ✅ BELOW |
| SubmitTargetOrdersLoop | 10 | ✅ BELOW |
| ValidateStopPrice | 10 | ✅ BELOW |
| CreateDirectStopOrder | 10 | ✅ BELOW |
| ClassifyAndRouteFleetOrder | 10 | ✅ BELOW |
| ProcessOnExecution_HandleTargetFill | 9 | ✅ BELOW |
| AuditStopQuantityAndPrint | 9 | ✅ BELOW |
| ScanAndRemoveGhostReferences | 15 | ✅ BELOW |

#### LOC Violations (16 methods)

All methods exceed Codacy's 50-line limit but are acceptable under V12 standards for construction/dispatch patterns.

### Rationale for Won't Fix

**Jane Street Alignment**:
- V12 uses CYC ≤ 15 for HFT systems (cognitive simplicity)
- Construction/dispatch methods naturally have higher LOC
- Co-location improves hot-path performance

**Recommended Action**: Add comments to `.codacy.yml` explaining V12 threshold

---

## Category 3: False Positives (324 issues)

### Metadata
- **Origin**: PR #8 CSharpier formatting (MERGED)
- **Priority**: NONE
- **Effort**: 0
- **Status**: DEFERRED - Won't Fix

### Breakdown

- **~250 Curly Brace Violations**: Already have braces, Codacy misdetection
- **~50 Unused Parameter Violations**: NinjaTrader API required
- **~20 Empty Catch Violations**: Documented spurious exceptions with logging
- **~4 Redundant Initialization Violations**: V12 DNA explicit clarity preference

**Conclusion**: All "violations" are V12 DNA compliant. No action needed.

---

## Category 4: Non-Existent (5 issues)

### Metadata
- **Origin**: PR #14 (CLOSED, not merged)
- **Status**: RESOLVED
- **Action**: None

All 5 reported issues were verified as non-existent through agent search.

---

## Category 5: Style Violations (NEW)

### EPIC-STYLE-INLINE-COMMENTS

**Metadata**:
- **Pattern**: Code + inline comment on same line
- **Count**: TBD (need scan)
- **Priority**: P2
- **Effort**: 2-3 hours
- **Status**: DEFERRED - Fix after P0 ErrorProne issues complete

**Examples**:
- `src/V12_002.Entries.Trend.cs:911`: `UpdateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded`
- `src/V12_002.REAPER.Audit.cs:65`: `int threshold = 1600; // 80% of 2000 capacity`

**Fix Pattern**:
```csharp
// Before
UpdateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded

// After
// V12.30: Ceiling-rounded
UpdateATRStopDistance(RMAStopATRMultiplier);
```

**Scan Command**:
```powershell
Select-String -Path "src/*.cs" -Pattern ";\s*//" | Measure-Object
```

**Rationale**:
- V12 DNA mandates clean separation of code and comments
- Inline comments reduce readability in dense HFT code
- CSharpier does not enforce this (style-only rule)
- Manual fix required across entire codebase

**Documentation**: See `docs/brain/codacy_inline_comment_issue.md`

---

## Summary Table

| Category | Count | Priority | Effort | Action |
|----------|-------|----------|--------|--------|
| Breaking Changes (PR #9) | 17 | MEDIUM | 2-3 days | EPIC-SIGNALS-REFACTOR |
| Threshold Mismatch (PR #8) | 29 | LOW | 0 | Document in `.codacy.yml` |
| False Positives (PR #8) | 324 | NONE | 0 | No action |
| Non-Existent (PR #14) | 5 | NONE | 0 | No action |
| Style Violations (NEW) | TBD | P2 | 2-3 hours | EPIC-STYLE-INLINE-COMMENTS |
| **TOTAL** | **375+** | - | **2-3 days + 2-3 hours** | 2 epics + 1 doc task |

---

## Action Items

### Immediate (Next Sprint)
1. [ ] Document Codacy threshold mismatch in `.codacy.yml`
2. [ ] Update Jane Street deviation documentation

### Short-Term (After Phase 2)
1. [ ] Create EPIC-SIGNALS-REFACTOR
2. [ ] Plan event handler migration
3. [ ] Design IEquatable implementation

---

## References

- **PR #8**: https://github.com/malhitticrypto-debug/universal-or-strategy/pull/8
- **PR #9**: https://github.com/malhitticrypto-debug/universal-or-strategy/pull/9
- **PR #14**: https://github.com/malhitticrypto-debug/universal-or-strategy/pull/14
- **PR #16**: https://github.com/malhitticrypto-debug/universal-or-strategy/pull/16
- **Codacy Dashboard**: https://app.codacy.com/gh/malhitticrypto-debug/universal-or-strategy/dashboard
- **Complete Analysis**: `docs/brain/codacy_pr_analysis_complete.md`

---

**Document Version**: 1.0  
**Last Updated**: 2026-05-28  
**Author**: Antigravity (Gemini CLI)