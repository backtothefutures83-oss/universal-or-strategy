# Antigravity OS -- Architectural Design Spec

**Date**: 2026-04-09
**Author**: Claude (P3 Master Architect)
**Status**: APPROVED
**Approach**: Vertical Slice (Approach A)

---

## Context

We are transitioning from the V12 Universal OR Strategy (a NinjaTrader 8 monolith, 58 files, ~24K LOC) into **Antigravity OS** -- a unified operating substrate for autonomous multi-strategy trading. This is driven by three forces:

1. **Platform independence**: V12 is locked to NinjaTrader 8 (.NET Framework 4.8, WPF). The Schwab TOS API offers a direct physical bridge that eliminates the NT8 dependency.
2. **N-Producer paradigm shift**: The old Leader/Follower (SIMA) model is retired. The V26.1 MPMC pipeline (3.726ns, XOR-Shadow invariants) was purpose-built for N independent autonomous strategies producing signals simultaneously.
3. **Licensing compliance**: Mixing Apache 2.0/MIT open-source components with proprietary trading logic requires strict process isolation -- not just code separation.

The Arena kernel (26 rounds, SPSC 0.35ns record, MPMC 3.726ns record) provides the proven high-performance foundation. Everything else is greenfield.

---

## Architectural Decisions (Locked)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| NT8 relationship | Parallel targets | Broker-agnostic kernel; NT8 as one adapter among many |
| VS Code relationship | Cherry-pick components | Electron + Monaco + Extension Host; custom shell, no full fork |
| License model | 3-tier process isolation | Open-source shell, proprietary kernel, third-party bridges |
| IPC model | Hybrid | MMIO hot path (sub-us), named pipes/gRPC cold path (~1-10ms) |
| Producer model | N-Producer (Multiple Leaders) | No followers; every strategy is an independent sovereign producer |
| Language strategy | C# NativeAOT everywhere | Kernel + MMIO bridge compiled to GC-free native binary; Electron shell in TypeScript |

---

## System Architecture: Three Sovereign Processes

```
Process 1: KERNEL (C# NativeAOT, proprietary)
  +-- Strategy Engine (N independent producers)
  |   +-- Strategy A (e.g., OR entry logic)
  |   +-- Strategy B (e.g., FFMA entry logic)
  |   +-- Strategy N (e.g., Trend logic)
  +-- MPMC Pipeline (V26.1 XOR-Shadow, 3.726ns)
  |   +-- N producers -> M consumer lanes
  +-- Fleet Orchestrator (account management, replaces SIMA leader/follower)
  +-- REAPER Safety Audit (position reconciliation)
  +-- Watchdog (process health, heartbeat monitoring)

      |                              |
      | HOT PATH (MMIO)             | COLD PATH (named pipes/gRPC)
      | Shared memory region         | Protobuf messages
      | Sub-microsecond              | ~1-10ms acceptable
      |                              |
      v                              v

Process 2: L1 BRIDGE (C# NativeAOT, third-party licensed)
  +-- IBrokerAdapter implementation
  |   +-- SchwabTosAdapter (primary)
  |   +-- NinjaTraderAdapter (legacy)
  +-- MMIO Consumer (reads from kernel pipeline)
  +-- Order Router (submits to broker API)
  +-- Fill Reporter (writes fills back to MMIO)

Process 3: OS SHELL (Electron/TypeScript, open-source)
  +-- Monaco Editor (strategy code viewing/editing)
  +-- Extension Host (VS Code extension isolation)
  +-- Telemetry Dashboard (real-time P&L, fills, fleet status)
  +-- Cold Path Client (reads telemetry stream from kernel)
  +-- Configuration Manager (strategy params, fleet config)
```

### Process Invariants
- Kernel NEVER imports Electron/Node.js dependencies
- L1 Bridge NEVER imports UI dependencies
- OS Shell NEVER touches shared memory directly -- reads telemetry via cold path only
- Each process can crash independently without taking down the others
- License boundaries enforced by OS-level process isolation

---

## Layer 1: Kernel -- Broker Abstraction & N-Producer Model

### IBrokerAdapter Interface

```csharp
public interface IBrokerAdapter
{
    long SubmitOrder(OrderSpec spec);
    void CancelOrder(long orderId);
    void ReplaceOrder(long orderId, OrderSpec newSpec);
    int GetPosition(long instrumentId);
    event Action<FillEvent> OnFill;
    event Action<OrderStateEvent> OnOrderStateChange;
    event Action<ConnectionEvent> OnConnectionChange;
}

public readonly struct OrderSpec
{
    public readonly long InstrumentId;
    public readonly int Quantity;
    public readonly double Price;
    public readonly OrderSide Side;
    public readonly OrderType Type;
    public readonly long ParentId;
}
```

### IStrategyProducer Interface

```csharp
public interface IStrategyProducer
{
    string Id { get; }
    void OnMarketData(MarketTick tick);
    void OnFill(FillEvent fill);
    void Shutdown();
}
```

### Migration Map (V12 -> Antigravity OS)

| V12 Component | Fate | Notes |
|---|---|---|
| Entry logic (OR, FFMA, RMA, MOMO, Trend, Retest) | PORTED | Individual IStrategyProducer implementations |
| SIMA leader/follower dispatch | RETIRED | Replaced by N-producer MPMC model |
| SIMA fleet account discovery | PORTED | Into Fleet Orchestrator |
| REAPER audit + repair | PORTED | Kernel-level safety monitor |
| Photon Pool/Ring | EVOLVED | Into cross-process MMIO MPMC pipeline |
| BracketFSM | PORTED | Each producer manages own bracket state |
| IPC TCP server | REPLACED | Cold path named pipes/gRPC |
| WPF Panel UI | RETIRED | Replaced by Electron OS Shell |
| StickyState | PORTED | Config persistence via OS Shell + cold path |
| Watchdog | PORTED | Process-level health monitoring |

---

## Layer 2: MMIO Hot Path

### Shared Memory Layout

```
KERNEL PROCESS                          L1 BRIDGE PROCESS
+------------------+                   +------------------+
|  MPMC Pipeline   |                   |  MMIO Consumer   |
|  (V26.1 lanes)   |                   |                  |
|                  |    Named Shared    |  Reads slots,    |
|  TrySend() ------+--> Memory Region --+-> validates XOR  |
|                  |    (OS MMIO)       |  shadow, routes  |
|  FillRing <------+--< Fill Return <---+-- to broker API  |
|                  |    Channel         |                  |
+------------------+                   +------------------+
```

**Implementation details:**
- `MemoryMappedFile.CreateNew()` shared between two NativeAOT processes
- Two rings in shared region: Order Ring (Kernel -> Bridge), Fill Ring (Bridge -> Kernel)
- XOR-Shadow invariants (ADR-016) validate data integrity across process boundaries
- Cache-line padding (64-byte aligned) prevents false sharing
- Heartbeat slot: Bridge writes monotonic timestamp every 100ms; Kernel Watchdog monitors staleness

---

## Layer 3: Cold Path & OS Shell

### Cold Path Protocol (named pipes, protobuf)

Messages: TelemetryUpdate, StrategyState, ConfigChange, HealthStatus

### OS Shell Panels (Electron + Monaco)

- Fleet Dashboard: Real-time position/P&L for all N producers
- Strategy Editor: Monaco with C# syntax, read-only by default
- Telemetry Stream: Fill-by-fill event log
- Config Panel: Strategy parameters, risk limits (replaces WPF panel)

---

## Vertical Slice Phases

| Phase | Arena Rounds | Deliverable | Verification |
|-------|-------------|-------------|--------------|
| VS-1: Abstraction | R27-R28 | IBrokerAdapter, IStrategyProducer, IMarketDataFeed. OR strategy ported. NativeAOT compiles clean. | Unit tests: interface contracts. NativeAOT publish succeeds with zero warnings. |
| VS-2: MMIO Bridge | R29-R30 | Cross-process shared memory ring. One producer writes, one consumer reads. XOR-Shadow across process boundary. | Benchmark: sub-10ns cross-process latency. XOR validation 100% pass rate under stress. |
| VS-3: L1 Bridge | R31-R32 | Schwab TOS adapter. Paper trading proof: OR -> MMIO -> Schwab -> Fill -> Telemetry. | End-to-end paper trade: order submitted, fill received, telemetry logged. |
| VS-4: OS Shell | R33-R34 | Electron shell with telemetry dashboard. Cold path wired. | Visual proof: fills appear in dashboard within 50ms of execution. |
| VS-5: N-Producer Scale | R35+ | Remaining strategies ported. MPMC replaces SPSC. Fleet orchestration. | N strategies producing simultaneously. MPMC benchmark matches V26.1 record. |
| VS-6: NT8 Adapter | R36+ | NinjaTraderAdapter implements IBrokerAdapter. Legacy bridge. | Side-by-side: same strategy runs on both NT8 and Schwab adapters. |
