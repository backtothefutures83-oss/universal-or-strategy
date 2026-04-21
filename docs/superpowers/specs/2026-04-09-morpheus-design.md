# Morpheus -- Architectural Design Spec

**Date**: 2026-04-09
**Author**: Claude (P3 Master Architect)
**Status**: APPROVED
**Approach**: Vertical Slice (Approach A)

---

## Context

We are transitioning from the V12 Universal OR Strategy (a NinjaTrader 8 monolith, 58 files, ~24K LOC) into **Morpheus** -- a unified operating substrate for autonomous multi-strategy trading. This is driven by three forces:

1. **Platform independence**: V12 is locked to NinjaTrader 8 (.NET Framework 4.8, WPF). The Schwab TOS API offers a direct physical bridge that eliminates the NT8 dependency.
2. **N-Producer paradigm shift**: The old Leader/Follower (SIMA) model is retired. The V26.1 MPMC pipeline (3.726ns, XOR-Shadow invariants) was purpose-built for N independent autonomous strategies producing signals simultaneously.
3. **Licensing compliance**: Mixing Apache 2.0/MIT open-source components with proprietary trading logic requires strict process isolation -- not just code separation.

The Arena kernel (26 rounds, SPSC 0.35ns record, MPMC 3.726ns record) provides the proven high-performance foundation. Everything else is greenfield.

---

## Architectural Decisions (Locked)

| Decision             | Choice                        | Rationale                                                                           |
| -------------------- | ----------------------------- | ----------------------------------------------------------------------------------- |
| NT8 relationship     | Parallel targets              | Broker-agnostic kernel; NT8 as one adapter among many                               |
| VS Code relationship | Hybrid Developer Experience   | Zed/Warp for Kernel performance; VS Code for Platform/Svelte architecture           |
| License model        | 3-tier process isolation      | Open-source shell, proprietary kernel, third-party bridges                          |
| IPC model            | Hybrid                        | MMIO hot path (sub-us), named pipes/gRPC cold path (~1-10ms)                        |
| Producer model       | N-Producer (Multiple Leaders) | No followers; every strategy is an independent sovereign producer                   |
| Language strategy    | C# NativeAOT everywhere       | Kernel + MMIO bridge compiled to GC-free native binary; Electron shell in Svelte/TS |

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

Process 3: OS SHELL (Svelte/Electron/TypeScript, open-source)
  +-- Monaco Editor (strategy code viewing/editing)
  +-- OpenAlgo-Chart (Strategy visualization and configuration)
  +-- Extension Host (Plugin isolation)
  +-- Telemetry Dashboard (real-time P&L, fills, fleet status)
  +-- Cold Path Client (reads telemetry stream from kernel)
  +-- Configuration Manager (strategy params, fleet config)

Process 4: THE FORGE (Performance Environment)
  +-- Zed Editor (High-performance, GPU-accelerated code editing)
  +-- Warp Terminal (AI-integrated command center)
```

### Process Invariants

- Kernel NEVER imports Electron/Node.js dependencies
- L1 Bridge NEVER imports UI dependencies
- OS Shell NEVER touches shared memory directly -- reads telemetry via cold path only
- Each process can crash independently without taking down the others
- License boundaries enforced by OS-level process isolation

---

## Superpower: Metamorphosis (Full-Stack Adaptability & UI/UX)

**Metamorphosis** is both the foundational architectural principle and the interactive identity of Morpheus. Driven by the Matrix thematic inspiration, the system is designed to dynamically transform across all layers of the stack:

- **Layer Metamorphosis (Architectural Swap)**: The Nexus allows us to hot-swap any component on the fly. Morpheus can seamlessly replace underlying broker APIs, AI models, trading harnesses, and open-source tools without breaking the global state. Just as agents in the Matrix take over any shell, Morpheus can adapt to and operate through any interface subsystem.
- **Flow Transitions (UI/UX)**: The OS completely avoids rigid desktop UI behavior. When switching context (e.g., from TradingView Lightweight charts to the Agent Manager or the IDE layer), the entire app interface physically decomposes into digital smoke, high-fidelity particle effects, or swarms before gracefully reconstructing into the new tab.
- **Autonomous Visual Generation**: The visual nature of the metamorphosis is never static. It will change dynamically every day or every few days, with background agents autonomously designing and deploying new generative visuals (e.g., changing from smoke to liquid metal or digital rain) on a schedule.
- **Agent Presence**: Morpheus, the orchestrator, is visually represented within the OS. When you dismiss the agent or it switches to a background execution state, the UI avatar visually "turns into a swarm of insects/bees" and floats away, reinforcing its autonomy.
- **3D Spatial Computing & UI**: A non-negotiable pillar across the website, OS design, and project identity. Morpheus transcends flat 2D environments—every dashboard, topology, and interactive experience is engineered for an immersive 3D space, utilizing physics-based layout, depth interactions, and spatial event streams.
- **Visual Tech**: Leveraging `GSAP` / `Svelte` / WebGL (Three.js) / `OpenAlgo-Chart` within the Electron shell to achieve fluid transitions and 3D orchestration.

---

## Superpower: Sovereign Transparency ("Clear Pipes")

**Transparent Pipes** is the foundational trust pillar of Morpheus. When you run agents on Morpheus, nothing is a black box — not for you, and not for the agents.

Other platforms (JetBrains + ACP, Cursor, Copilot) decide what their agents can see. The IDE or host chooses which file context, which terminal buffer, which project state to pass — and users cannot inspect exactly what was included. The agent operates inside a curated envelope without knowing what was left out.

**In Morpheus, every agent gets everything:**

- Every running terminal process is explicitly named with its PID, directory, and elapsed runtime
- Any agent can call `read_terminal` and receive the live output of any named process
- All agents read the same `nexus_a2a.json` blackboard: mission phase, BUILD_TAG, plan path, and full A2A handoff history
- When agent A hands off to agent B, B inherits the complete mission state — not a summary, not a truncation

**The result:** agents in Morpheus know what other agents are doing. Agent A knows Agent B is running in terminal 2. Agent A can read Agent B's output. When Agent B finishes, Agent A sees it. No polling, no guessing, no black box.

> Other platforms decide what their agents can see. Morpheus agents see everything — and so do you.

---

## Superpower: Infinite Configurability ("Rubix Cube")

**The Rubix Cube principle:** Morpheus is a substrate, not a product. Like a Rubix Cube, it can be twisted into an endless number of configurations — and every configuration resolves to a valid, governed state.

The fixed pieces are the 6-agent fleet (Claude, Codex, Gemini, Jules, Antigravity, Droid) and the Director's Gate rules. Within those rules, any combination of agents, harnesses, missions, broker adapters, and market surfaces can be composed:

```
TRADING LAYER:  NinjaTrader ↔ Schwab TOS ↔ Binance ↔ XRPL ↔ any IBrokerAdapter
INTELLIGENCE:   Claude ↔ Codex ↔ Gemini ↔ Jules ↔ any ACP-registered agent
MISSION:        Any ADR -- swap in one nexus_a2a.json write
MARKET:         Futures ↔ Equities ↔ Crypto ↔ Prediction markets ↔ DeFi
STRATEGY:       OR ↔ FFMA ↔ RMA ↔ Trend ↔ any IStrategyProducer
```

None of these swaps require touching the kernel. The substrate absorbs them.

> See full pillar spec: [morpheus-sovereign-pillars.md](./morpheus-sovereign-pillars.md)

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

### Migration Map (V12 -> Morpheus)

| V12 Component                                    | Fate     | Notes                                        |
| ------------------------------------------------ | -------- | -------------------------------------------- |
| Entry logic (OR, FFMA, RMA, MOMO, Trend, Retest) | PORTED   | Individual IStrategyProducer implementations |
| SIMA leader/follower dispatch                    | RETIRED  | Replaced by N-producer MPMC model            |
| SIMA fleet account discovery                     | PORTED   | Into Fleet Orchestrator                      |
| REAPER audit + repair                            | PORTED   | Kernel-level safety monitor                  |
| Photon Pool/Ring                                 | EVOLVED  | Into cross-process MMIO MPMC pipeline        |
| BracketFSM                                       | PORTED   | Each producer manages own bracket state      |
| IPC TCP server                                   | REPLACED | Cold path named pipes/gRPC                   |
| WPF Panel UI                                     | RETIRED  | Replaced by Electron OS Shell                |
| StickyState                                      | PORTED   | Config persistence via OS Shell + cold path  |
| Watchdog                                         | PORTED   | Process-level health monitoring              |

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

## Layer 4: Blockchain & DEX Integration (Crypto & Swap)

Morpheus integrates directly with decentralised exchanges (DEX) and crypto networks to enable an "anything to anything" swap capability, bridging traditional futures and on-chain assets.

### Supported Integrations & Protocols

- **Binance USDS Futures**: Direct API integration for high-liquidity crypto futures trading.
- **XRP Ledger (XRPL)**: Integration for high-speed, low-cost cross-border liquidity and arbitrary asset swaps.
- **Polymarket Arbitrage**: Built-in support for event-based binary markets.

### Open-Source vs Proprietary Boundaries

- **Open-Source Crypto Tooling**: Publicly available blockchain connectivity SDKs, `best-of-crypto` curated components, and decentralised routing protocols (Binance, XRP wrappers).
- **Factory-AI Infrastructure**: Utilizing `@factory/eslint-plugin`, `droid-control`, `security-engineer`, and `autoresearch` plugins for maintaining AI agent health and standardising autonomous PRs/linting.
- **Arena AI / Kernel**: **PROPRIETARY**. While the UI shell, blockchain connectors, and deployment tools are open source, the actual multi-agent intelligence (Arena AI) and high-speed C# MPMC execution kernel remain strictly proprietary to maintain our tactical edge.

---

## Vertical Slice Phases

| Phase                  | Arena Rounds | Deliverable                                                                                                    | Verification                                                                           |
| ---------------------- | ------------ | -------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| VS-1: Abstraction      | R27-R28      | IBrokerAdapter, IStrategyProducer, IMarketDataFeed. OR strategy ported. NativeAOT compiles clean.              | Unit tests: interface contracts. NativeAOT publish succeeds with zero warnings.        |
| VS-2: MMIO Bridge      | R29-R30      | Cross-process shared memory ring. One producer writes, one consumer reads. XOR-Shadow across process boundary. | Benchmark: sub-10ns cross-process latency. XOR validation 100% pass rate under stress. |
| VS-3: L1 Bridge        | R31-R32      | Schwab TOS adapter. Paper trading proof: OR -> MMIO -> Schwab -> Fill -> Telemetry.                            | End-to-end paper trade: order submitted, fill received, telemetry logged.              |
| VS-4: OS Shell         | R33-R34      | Electron shell with telemetry dashboard. Cold path wired.                                                      | Visual proof: fills appear in dashboard within 50ms of execution.                      |
| VS-5: N-Producer Scale | R35+         | Remaining strategies ported. MPMC replaces SPSC. Fleet orchestration.                                          | N strategies producing simultaneously. MPMC benchmark matches V26.1 record.            |
| VS-6: NT8 Adapter      | R36+         | NinjaTraderAdapter implements IBrokerAdapter. Legacy bridge.                                                   | Side-by-side: same strategy runs on both NT8 and Schwab adapters.                      |
