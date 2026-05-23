# Ticket 11: Benchmark Methodology Fixes (EPIC-6 Phase 2)

## Status
- [ ] TODO

## Priority
P0 CRITICAL (deferred from PR #6)

## Scope
Fix 7 remaining benchmark methodology issues identified in PR #6 forensics but not addressed due to file access limitations.

## Issues to Fix

### 1. SIMADispatchBenchmark.cs
- **Issue**: Benchmark exercises mock properties instead of production logic
- **Fix**: Return computed `shouldFlatten` and `shouldCancel` values
- **Bot**: cubic-dev-ai, codacy-production

### 2. BarUpdateBenchmark.cs
- **Issue**: Dead code elimination risk - computed values not returned
- **Fix**: Return `hasPnL` and `isWorking` to prevent JIT optimization
- **Bot**: cubic-dev-ai

### 3. OrderCallbacksBenchmark.cs
- **Issue**: Computed `accountPnL` not returned
- **Fix**: Return value to prevent constant folding
- **Bot**: cubic-dev-ai

### 4. All Benchmarks - RunStrategy
- **Issue**: Using `RunStrategy.Monitoring` instead of `Throughput`
- **Fix**: Change to `[SimpleJob(RunStrategy.Throughput)]`
- **Bot**: codacy-production

### 5. All Benchmarks - Constant Folding
- **Issue**: `messageType` is constant, allows JIT optimization
- **Fix**: Use `[Params(OrderEventType.Fill, OrderEventType.Cancel)]`
- **Bot**: cubic-dev-ai

## V12 DNA Constraints
- Maintain 0B allocation target
- Keep latency < 300μs
- No locks (already compliant)
- ASCII-only (already compliant)

## Verification
1. Run `dotnet run -c Release --project benchmarks/V12_Performance.Benchmarks.csproj`
2. Verify 0B allocation in BenchmarkDotNet output
3. Verify Mean < 300μs for all benchmarks
4. Run `powershell -File .\deploy-sync.ps1`
5. F5 verification in NinjaTrader

## Files to Modify
- `benchmarks/SIMADispatchBenchmark.cs`
- `benchmarks/BarUpdateBenchmark.cs`
- `benchmarks/OrderCallbacksBenchmark.cs`

## Success Criteria
- [ ] All 7 benchmark issues fixed
- [ ] BenchmarkDotNet reports 0B allocation
- [ ] Mean latency < 300μs
- [ ] StyleCop pipeline passes
- [ ] PHS 100/100 on follow-up PR

## Notes
This ticket completes EPIC-6 Phase 1 deferred work. PR #6 (tests) can merge independently since core test infrastructure is solid.