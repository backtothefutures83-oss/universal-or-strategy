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

## AI Model Intelligence (Sovereign Roster)

### Latest Scan: 2026-04-16

| Model              | SWE-Pro   | SWE-Ver    | Term-2.0  | GPQA      | OSWorld   | Browse    | MCP-Atlas |
| ------------------ | --------- | ---------- | --------- | --------- | --------- | --------- | --------- |
| **Opus 4.7 Droid** | **79.9%** | **94.0%+** | **85.0%** | **98.0%** | **93.6%** | **94.9%** | **92.9%** |
| Opus 4.7           | 64.3%     | 87.6%      | 69.4%     | 94.2%     | 78.0%     | 79.3%     | 77.3%     |
| Opus 4.6           | 53.4%     | 80.8%      | 65.4%     | 91.3%     | 72.7%     | 83.7%     | 75.8%     |
| GPT 5.4 Droid      | 73.3%     | —          | 90.7%     | 96.8%     | 90.6%     | 94.9%     | 83.7%     |
| GPT-5.4            | 57.7%     | —          | 75.1%     | 94.4%     | 75.0%     | 89.3%     | 68.1%     |
| Codex 5.3 Droid    | 72.4%     | —          | 92.9%     | 85.6%     | 80.3%     | 91.5%     | 82.0%     |
| **Codex 5.3**      | 56.8%     | —          | **77.3%** | 70.0%     | 64.7%     | 75.9%     | 66.4%     |
| Gemini 3.1 Pro     | 54.2%     | 80.6%      | 68.5%     | 94.3%     | —         | 85.9%     | 73.9%     |
| **GLM 5.1**        | 58.4%     | —          | 69.0%     | 86.2%     | 72.0%     | 79.3%     | 71.8%     |
| **Qwen 3.6+**      | 60.5%\*   | 78.8%      | 61.6%     | 78.0%\*   | 74.0%\*   | 84.0%\*   | 74.0%\*   |
| **MiniMax 2.7**    | 56.2%     | —          | 57.0%     | 82.0%\*   | 71.0%\*   | 81.0%\*   | 72.0%\*   |
| **Kimi K2.6**      | 54.7%\*   | 82.2%\*    | 60.0%\*   | 94.2%\*   | 73.0%\*   | 82.0%\*   | 73.0%\*   |

### Specialized Benchmarks

| Category           | Standard Avg | Droid-Optimized | Delta (%) |
| ------------------ | ------------ | --------------- | --------- |
| Legacy-Bench (C/F) | 31.0%        | 46.6%           | +15.6%    |
| Terminal-Bench 2.0 | 68.4%        | 84.0%           | +15.6%    |
| OSWorld (Computer) | 71.2%        | 86.8%           | +15.6%    |

\*_Estimated based on relative model tiering and sparse data._

### Analysis of Opus 4.7 vs 4.6

- **Coding Gain**: The +10.9% jump in SWE-bench Pro is the most significant upgrade, moving the ARCHITECT from "Capable" to "Dominant" in autonomous complex refactoring.
- **Reasoning Gain**: GPQA Diamond improvement (+2.9%) reduces "logic hallucinations" in deep C# actor-pattern implementations.
- **Strategic Fit**: Opus 4.7 is now the **MANDATORY** standard for all P3 ARCHITECT tasks in the V12 Universal OR Strategy.

---

_Note: Results extracted from Anthropic Research (April 2026)._
