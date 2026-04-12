# Benchmark Baseline — LOCKED (Do Not Edit Without Running Benchmarks)

## Hardware

- **CPU**: AMD Ryzen 5 5625U with Radeon Graphics (1 CPU, 12 logical / 6 physical cores)
- **OS**: Windows 11 (10.0.26200.8037)
- **Runtime**: .NET Framework 4.8
- **BenchmarkDotNet**: v0.13.12
- **Date**: 2026-04-06

## Results

| Benchmark                                | Mean         | Ratio                | Allocated | GC Gen0 |
| ---------------------------------------- | ------------ | -------------------- | --------- | ------- |
| V12: TryEnqueue (managed array)          | **3.598 ns** | 0.46x                | 0 B       | 0       |
| V12: TryDequeue (managed array)          | **4.090 ns** | 0.52x                | 0 B       | 0       |
| **V12: RoundTrip (enqueue+dequeue)**     | **7.792 ns** | **1.00x (BASELINE)** | **0 B**   | **0**   |
| V12: TrailingStop simulation             | **4.778 ns** | 0.61x                | 0 B       | 0       |
| V14.8: TryEnqueue (unmanaged CoreLane\*) | **4.917 ns** | 0.63x                | 0 B       | 0       |
| V14.8: TryDequeue (seq-diff protocol)    | **4.579 ns** | 0.59x                | 0 B       | 0       |
| **V14.8: RoundTrip (enqueue+dequeue)**   | **7.667 ns** | **0.98x**            | **0 B**   | **0**   |
| V14.8: TrailingStop simulation           | **5.210 ns** | 0.67x                | 0 B       | 0       |

## LOCKED_BASELINE_NS

```
LOCKED_BASELINE_NS = 7.792   # V12 RoundTrip (hardware-proven floor)
TARGET_BEAT_NS     = 7.667   # Current V14.8 RoundTrip (0.125ns improvement = 1.6% gain)
NEXT_TARGET_NS     = 7.0     # Next Battle Round target: beat 7.0 ns on this hardware
ALLOCATED          = 0 B     # REQUIRED: any submission allocating > 0B is disqualified
GC_GEN0            = 0       # REQUIRED: GC activity = automatic disqualification
```

## Analysis

### What the numbers tell us

- **Both V12 and V14.8 allocate 0 bytes** — the unmanaged heap isolation is working correctly in both.
- **V14.8 RoundTrip is 0.125ns faster** than V12 (7.667 vs 7.792). Small gain but structurally correct.
- **The 3ns theoretical floor** from Arena estimates is not achieved here because:
  1. These benchmarks run on a laptop (shared CPU, no CPU pinning)
  2. .NET 4.8 JIT is less aggressive than AOT / NativeAOT
  3. The `Volatile.Read/Write` calls have measurable overhead (~1.5ns each on this CPU)

### What the next Battle Round must target

- **Beat 7.0 ns RoundTrip** on this hardware (Ryzen 5 5625U, .NET 4.8)
- Possible improvements: reduce Volatile fence count per op, improve seq-diff branch prediction hint
- **Hard floor** on this hardware estimated at ~4.0 ns (2x Volatile.Read minimum)

## How to Re-run

```powershell
cd c:\WSGTA\universal-or-strategy\benchmarks
dotnet run -c Release
```

Results saved to: `BenchmarkDotNet.Artifacts\results\SpscBench.SpscBenchmarks-report.csv`
