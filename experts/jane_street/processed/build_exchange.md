# Distilled Intel: "How to Build an Exchange" (Jane Street / Island ECN)
**Ingestion Architect:** IBM Ingestion Architect
**Source Video:** [How to Build an Exchange](https://www.youtube.com/watch?v=b1e4t2k2KJY)
**Target Domain:** Matching Engine Design / Distributed Systems / State Machine Replication
**V12 DNA Translation Target:** C# / .NET Framework 4.8 (NinjaTrader 8)

---

## 1. Core Exchange Architectural Principles

### Limit Order Book Design
- **Deterministic Priority:** The order book is a deterministic data structure sorted by price-time priority. 
- **Message Types:** Order flow is highly skewed: Add/New Orders (~50%), Cancel Orders (~30-40%), Cancel/Replace, Executions (~1-2%), and the rest is noise.

### State Machine Replication (SMR)
- **Deterministic Core:** The matching engine is structured as a deterministic, single-threaded state machine running on a single x86 machine. It accepts incoming transactions, sequences them globally, and outputs the resulting state transitions.
- **Shot-in-the-Head Resilience:** Because the system is deterministic, any downstream component (client gateways, market data feeds, clearing drops) can crash and recover its exact state in seconds by replaying the sequence from a retransmit server.
- **Active-Passive Failover:** Active-passive replication is achieved by having a passive engine listen to the primary engine's output and run the identical state machine. If the primary dies, the passive engine is promoted.

### Decoupled Sidecar Services
- **Cancel Fairy Pattern:** To keep the matching engine simple and free of timers, temporal operations (e.g., "cancel this order in 2 minutes") are offloaded to external helper processes. These sidecars observe the execution stream, manage timers, and submit cancellations on the client's behalf.
- **Asynchronous Optimization (Auctions):** Complex batching processes (like opening/closing crossing auctions) are run as independent optimization passes outside the continuous matching thread, then injected back.

### Network & Gateway Isolation
- **UDP Multicast for Fairness:** Market data and execution logs are broadcast simultaneously using UDP multicast, ensuring all participants receive data at the same time.
- **Retransmit Buffers:** To handle UDP packet loss, dedicated retransmit servers buffer the message log and fulfill gap-fill requests from downstream components.
- **Gateway Insulators (Ports):** Gateway processes handle TCP client connections, validate requests, and translate client identifiers to array indices (locates) for O(1) matching engine lookups.
- **One-in-Flight Flow Control:** Gateways enforce a limit of one unacknowledged transaction in flight per client. This simplifies rollback state tracking and provides natural backpressure.

### The Memory Bottleneck
- **Memory Latency is the Limit:** Matching engine profiling reveals that ~30% of CPU time is spent on a single instruction: dereferencing pointers/indices to look up order records (cache misses).

---

## 2. V12 C# DNA Mappings (NinjaTrader 8 Context)

NinjaTrader 8 strategies operate as custom scripts in the NT8 runtime. To enforce ECN-level reliability, determinism, and look-up speed in C#, we implement specific patterns.

### A. Deterministic Time & Replayability
- **The Problem:** Using `DateTime.Now` inside strategy calculations makes the system non-deterministic. Backtests will not align with live execution, and state cannot be replayed reliably.
- **V12 Solution:** Rely strictly on the timestamp of the incoming bar or tick (`Time[0]` or market data event time) for all state logic, signal calculations, and temporal controls.

```csharp
// V12 Compliant: Deterministic time tracking for execution signals
public class DeterministicStrategy
{
    private DateTime currentMarketTime = DateTime.MinValue;

    public void OnMarketUpdate(DateTime tickTime)
    {
        // Enforce monotonically increasing time based on market ticks
        if (tickTime > currentMarketTime)
        {
            currentMarketTime = tickTime;
        }
    }

    public bool IsTradingWindowActive()
    {
        // Reference currentMarketTime instead of DateTime.Now
        return currentMarketTime.Hour >= 9 && currentMarketTime.Hour < 16;
    }
}
```

### B. One-in-Flight Order Replacement FSM
- **The Problem:** Cancelling an order and immediately submitting a new one in the same tick creates "ghost orders" and tracking mismatches because the cancel confirmation has not been processed.
- **V12 Solution:** Enforce a strict state machine where order replacement is a two-phase process: wait for confirmation of cancellation before submitting the new order.

```csharp
// V12 Compliant: Two-phase Replacement FSM
public enum OrderReplaceState
{
    Idle,
    PendingCancel,
    Submitting
}

public class OrderReplacementManager
{
    private OrderReplaceState state = OrderReplaceState.Idle;
    private string activeOrderToken = string.Empty;

    public void RequestReplacement(string oldToken)
    {
        if (state != OrderReplaceState.Idle) return;

        state = OrderReplaceState.PendingCancel;
        activeOrderToken = oldToken;
        // Submit cancel command to broker...
    }

    public void OnOrderCancelled(string token)
    {
        if (state == OrderReplaceState.PendingCancel && token == activeOrderToken)
        {
            state = OrderReplaceState.Submitting;
            // Submit new order command...
        }
    }

    public void OnOrderFilledOrRejected(string token)
    {
        state = OrderReplaceState.Idle;
        activeOrderToken = string.Empty;
    }
}
```

### C. O(1) Cache-Friendly Index Lookups
- **The Problem:** Standard C# `Dictionary<TKey, TValue>` lookups involve pointer chasing, hashing overhead, and potential heap allocations.
- **V12 Solution:** Map order slots to a fixed-size, pre-allocated array of structures. Use direct index lookups (locate indices) to access order data in O(1) time without cache misses.

```csharp
// V12 Compliant: Pre-allocated contiguous array lookups
public struct OrderRecord
{
    public int OrderId;
    public double Price;
    public int Quantity;
    public bool IsActive;
}

public class OrderLookupTable
{
    private readonly OrderRecord[] orders = new OrderRecord[1024]; // Contiguous memory block

    public void StoreOrder(int locateIndex, int orderId, double price, int qty)
    {
        orders[locateIndex] = new OrderRecord
        {
            OrderId = orderId,
            Price = price,
            Quantity = qty,
            IsActive = true
        };
    }

    public ref OrderRecord Locate(int locateIndex)
    {
        // Direct array access by index (returns ref to avoid copy)
        return ref orders[locateIndex];
    }
}
```

### D. Separation of Auxiliary Logic (Cancel Fairy in C#)
- Keep the core execution engine focused solely on matching price and submitting orders.
- Move trailing stops, time-in-force cancellations, and telemetry logging to separate lifecycle components that feed orders back to the execution engine.

```csharp
// V12 Compliant: Decoupled Order Lifecycle management
public class TimeInForceWatcher
{
    private readonly double maxLifeSeconds = 60.0;
    
    public void CheckAndTriggerCancels(ref OrderRecord[] activeOrders, DateTime currentMarketTime, Action<int> cancelAction)
    {
        // Sidecar routine: iterates active orders, checks timers, and triggers cancellations
        for (int i = 0; i < activeOrders.Length; i++)
        {
            if (activeOrders[i].IsActive)
            {
                // Check if order has outlived its target lifespan
                // cancelAction(activeOrders[i].OrderId);
            }
        }
    }
}
```

---

## 3. Firestore Sync Template (RAG Metadata)

```json
{
  "document_id": "jane_street_build_exchange_2015",
  "title": "Distilled Intel: How to Build an Exchange",
  "speaker": "Jane Street Technologist",
  "source_url": "https://www.youtube.com/watch?v=b1e4t2k2KJY",
  "ingested_at": "2026-05-19T19:14:15Z",
  "categories": ["Exchange Design", "State Machine Replication", "UDP Multicast", "Flow Control", "Caching"],
  "key_takeaways": [
    "ECN matching engines operate as deterministic single-threaded state machines on commodity x86 hardware.",
    "UDP multicast is utilized for simultaneous, fair distribution of market data to all participants.",
    "State Machine Replication (SMR) allows any component to be rebuilt rapidly by replaying the transaction log.",
    "Decouple core matching logic from timing-based events using helper sidecars (e.g., Cancel Fairy).",
    "Pointers and index dereferencing to locate order records constitute the primary memory/cache bottleneck."
  ],
  "v12_csharp_patterns": {
    "determinism": "Use tick timestamps instead of system clocks to ensure history replayability.",
    "one_in_flight": "Implement a two-phase order replacement FSM to avoid ghost-order states.",
    "cache_optimization": "Use fixed-size struct arrays with direct index lookups to eliminate pointer-chasing.",
    "sidecar_lifecycle": "Segregate lifecycle and temporal order rules from core order book updates."
  }
}
```
