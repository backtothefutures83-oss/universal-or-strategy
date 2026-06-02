# Jane Street Deviations

This document tracks intentional deviations from Jane Street principles with documented rationale and improvement paths.

---

## CodeScene Low Cohesion (EPIC-CCN-11 Extraction)

**File**: `src/V12_002.Orders.Management.Flatten.cs`  
**Date**: 2026-06-02  
**Deviation**: Low cohesion score after ManageCIT extraction  

**Metrics**:
- Code Health: 5.45 → 4.88 (degradation)
- Cyclomatic Complexity: 26 → 9 (65% reduction) ✅
- Cohesion: Degraded (functions less related) ⚠️

**Rationale**:
- **Complexity Reduction Priority**: CYC 26 → 9 is MORE critical than cohesion score
- **Extraction Phase Natural**: Cohesion degrades when splitting monolithic functions
- **Jane Street Alignment**: "Cognitive simplicity" (low CYC) > perfect cohesion
- **Future Improvement**: EPIC-CCN-10 will address cohesion without sacrificing complexity gains

**Jane Street Compliance**:
- ✅ **Cognitive Simplicity**: CYC 9 aligns with threshold (≤15)
- ⚠️ **Cohesion**: Acceptable trade-off for extraction phase
- ⚠️ **Primitive Obsession**: Acceptable (value objects would increase complexity)

**Improvement Path**: EPIC-CCN-10 (cohesion refactoring)  
**Status**: ACCEPTED (temporary, tracked in backlog)  
**Review Date**: 2026-09-01 (3 months post-extraction)

---

## Future Deviations

Document any future Jane Street deviations here with the same structure:
- File/Component
- Date
- Deviation description
- Rationale
- Jane Street compliance analysis
- Improvement path
- Status and review date