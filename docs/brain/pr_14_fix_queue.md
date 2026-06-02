# PR #14 Fix Queue (Jane Street Audited)
Generated: 2026-06-01 18:51:03  
**Updated**: 2026-06-01 18:52:00 (Jane Street Audit Complete)

## Instructions for v12-engineer

Process these issues in priority order. Mark each as FIXED after applying the fix.

---

## VALID-FIX Issues (3 total)

### Fix #1 - [P1] Array Allocation in GetTargetContracts
[x] **Bot:** coderabbitai, gitar-bot (consensus)
[x] **File:** `src/V12_002.PositionInfo.cs:280-288`
[x] **Severity:** P1 (High) - Performance regression
[x] **Category:** VALID-FIX - **FIXED**

**Issue**: `new int[]` allocation on every call breaks zero-allocation requirement.

**Jane Street Rationale**: 
- Violates V12 DNA Principle #2 (Zero-Allocation Hot Paths)
- AMAL harness gate requires `Allocated = 0 B`
- Switch statements are allocation-free and equally readable

**Current Code** (lines 280-288):
```csharp
int[] contracts = new int[]
{
    pos.T1Contracts,
    pos.T2Contracts,
    pos.T3Contracts,
    pos.T4Contracts,
    pos.T5Contracts,
};
return contracts[targetNumber - 1];
```

**Required Fix**: Revert to switch-based accessor:
```csharp
switch (targetNumber)
{
    case 1: return pos.T1Contracts;
    case 2: return pos.T2Contracts;
    case 3: return pos.T3Contracts;
    case 4: return pos.T4Contracts;
    case 5: return pos.T5Contracts;
    default: return 0;
}
```

**Action Required:**
1. Replace array allocation with switch statement
2. Verify AMAL harness: `Allocated = 0 B`
3. Mark as [x] FIXED

---

### Fix #2 - [P2] Array Allocation in GetTargetPrice
[x] **Bot:** coderabbitai, gitar-bot (consensus)
[x] **File:** `src/V12_002.PositionInfo.cs:295-303`
[x] **Severity:** P2 (Medium) - Performance regression
[x] **Category:** VALID-FIX - **FIXED**

**Issue**: `new double[]` allocation on every call.

**Jane Street Rationale**: Same as Fix #1.

**Current Code** (lines 295-303):
```csharp
double[] prices = new double[]
{
    pos.Target1Price,
    pos.Target2Price,
    pos.Target3Price,
    pos.Target4Price,
    pos.Target5Price,
};
return prices[targetNumber - 1];
```

**Required Fix**: Revert to switch-based accessor:
```csharp
switch (targetNumber)
{
    case 1: return pos.Target1Price;
    case 2: return pos.Target2Price;
    case 3: return pos.Target3Price;
    case 4: return pos.Target4Price;
    case 5: return pos.Target5Price;
    default: return 0.0;
}
```

**Action Required:**
1. Replace array allocation with switch statement
2. Verify AMAL harness: `Allocated = 0 B`
3. Mark as [x] FIXED

---

### Fix #3 - [P2] Array Allocation in GetTargetFilledQuantity
[x] **Bot:** coderabbitai, gitar-bot (consensus)
[x] **File:** `src/V12_002.PositionInfo.cs:342-350`
[x] **Severity:** P2 (Medium) - Performance regression
[x] **Category:** VALID-FIX - **FIXED**

**Issue**: `new int[]` allocation on every call.

**Jane Street Rationale**: Same as Fix #1.

**Current Code** (lines 342-350):
```csharp
int[] filledQty = new int[]
{
    pos.T1FilledQuantity,
    pos.T2FilledQuantity,
    pos.T3FilledQuantity,
    pos.T4FilledQuantity,
    pos.T5FilledQuantity,
};
return filledQty[targetNumber - 1];
```

**Required Fix**: Revert to switch-based accessor:
```csharp
switch (targetNumber)
{
    case 1: return pos.T1FilledQuantity;
    case 2: return pos.T2FilledQuantity;
    case 3: return pos.T3FilledQuantity;
    case 4: return pos.T4FilledQuantity;
    case 5: return pos.T5FilledQuantity;
    default: return 0;
}
```

**Action Required:**
1. Replace array allocation with switch statement
2. Verify AMAL harness: `Allocated = 0 B`
3. Mark as [x] FIXED

---

### Fix #4 - [P2] Array Allocation in IsTargetFilled (Not flagged by bots, but same issue)
[x] **Bot:** N/A (consistency fix)
[x] **File:** `src/V12_002.PositionInfo.cs:310-311`
[x] **Severity:** P2 (Medium) - Performance regression
[x] **Category:** VALID-FIX - **FIXED**

**Issue**: `new bool[]` allocation on every call (same pattern as other accessors).

**Jane Street Rationale**: Same as Fix #1.

**Current Code** (line 310):
```csharp
bool[] filled = new bool[] { pos.T1Filled, pos.T2Filled, pos.T3Filled, pos.T4Filled, pos.T5Filled };
return filled[targetNumber - 1];
```

**Required Fix**: Revert to switch-based accessor:
```csharp
switch (targetNumber)
{
    case 1: return pos.T1Filled;
    case 2: return pos.T2Filled;
    case 3: return pos.T3Filled;
    case 4: return pos.T4Filled;
    case 5: return pos.T5Filled;
    default: return false;
}
```

**Action Required:**
1. Replace array allocation with switch statement
2. Verify AMAL harness: `Allocated = 0 B`
3. Mark as [x] FIXED

---

## Complexity Trade-off Analysis

**Original Code** (CYC 11):
- ✅ Zero allocation
- ✅ AMAL harness compliant
- ❌ Higher cyclomatic complexity

**Refactored Code** (CYC 8):
- ✅ Lower cyclomatic complexity
- ❌ Heap allocation on every call
- ❌ AMAL harness violation

**Jane Street Verdict**: **Revert to switch-based accessors.**

**Rationale**:
1. **Performance > Complexity**: Jane Street prioritizes zero-allocation hot paths over cyclomatic complexity metrics
2. **CYC 11 is acceptable**: V12 threshold is CYC ≤ 15 (Jane Street aligned)
3. **Switch statements are readable**: 5-case switches are cognitively simple
4. **AMAL gate is non-negotiable**: `Allocated = 0 B` is a hard requirement

---

## Verification Checklist

After applying all fixes:
- [x] All 4 fixes applied (2026-06-01 18:54 PST)
- [ ] Run AMAL harness: `python scripts/amal_harness.py`
- [ ] Verify: `Allocated = 0 B`
- [ ] Run build: `powershell -File .\scripts\build_readiness.ps1`
- [ ] Verify: Zero compilation errors
- [ ] Update PR description with Jane Street rationale

---

## References

- **Jane Street Audit**: `docs/brain/pr_14_jane_street_audit.md`
- **Jane Street Deviations**: `docs/standards/JANE_STREET_DEVIATIONS.md`
- **V12 DNA**: AGENTS.md Section 2 (Architectural Mandates)
- **AMAL Harness**: `scripts/amal_harness.py`
