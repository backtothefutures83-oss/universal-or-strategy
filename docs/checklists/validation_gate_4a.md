# Mandatory Step 4a Validation Gate Checklist

**THIS IS A HARD GATE. You MAY NOT proceed to P4 Adjudication until ALL checks below exit 0.**

1. **Draft Fix**: Write your proposed C# refactor to `src/_draft_fix.cs`.
2. **Build Gate**: Run `dotnet build Linting.csproj` (Target .NET 4.8). 
   - **Requirement**: Zero errors, zero warnings in the affected module.
3. **AMAL Gate**: Run `python scripts/amal_harness.py --gate adr019`.
   - **Requirement**: `Allocated = 0 B` and `Mean Latency < Baseline`.
4. **The Adversary Check (Probe 1-4)**:
   - Probe 1 (Torn Read): Verify `Interlocked.Read` is used for all 64-bit doubles on x86.
   - Probe 2 (Visibility): Verify `SymmetryDispatchContext` is `private sealed`.
   - Probe 3 (Concurrency): Verify `FollowerEntries` uses `ConcurrentDictionary<string, byte>`.
   - Probe 4 (Safety): Verify `CancelOrderSafe` is called BEFORE state removal.

**VERDICT: [PENDING]**
