# Copy Trader Engine: Architecture Design Specification

> Designed from first principles for NinjaTrader 8 + Rithmic/Apex fleet trading.
> Date: 2026-03-16 | Architecture: Actor Mailbox | Status: Approved

## Context

**Problem**: A fleet of 1 Master + 18 Follower accounts (Apex/Rithmic on NinjaTrader 8) needs 100% bracket symmetry during high-volatility Opening Range (OR) and RMA entries, while surviving Rithmic connection flickers (500ms-2s) without data loss or ghost orders.

**Solution**: An Actor Mailbox architecture with per-follower unbreakable FSMs, asymmetric audit authority, and skip-and-protect resilience.

**Design Parameters**:
- Bracket-clone: the bracket (entry + stop + target) is the atomic replication unit
- Uniform 1:1 sizing across all 19 accounts
- Clone on ORDER PLACEMENT (not fill) -- let the market fill naturally
- Accept natural fill divergence (no forced entry repair)
- Full lifecycle mirroring: cancels, modifications, trailing stops
- Skip-and-protect on reconnect (no replay of missed commands)

---

## Architecture Overview: Actor Mailbox

NinjaTrader 8's threading model naturally provides an actor: the strategy thread is the single consumer, account event threads are the producers, and a `ConcurrentQueue<AccountEvent>` is the lock-free mailbox.

```
STRATEGY THREAD (Actor)              ACCOUNT THREADS (Producers)
+--------------------------+         +---------------------------+
| OnBarUpdate()            |         | F01: OnAccountOrderUpdate |--+
| OnOrderUpdate()          |         | F02: OnAccountOrderUpdate |--+
| OnExecutionUpdate()      |         | ...                       |  |
|                          |         | F18: OnAccountOrderUpdate |--+
| - Fan-out sync commands  |         +---------------------------+  |
| - Drain mailbox          |                                        |
| - Process FSM transitions|    ConcurrentQueue<AccountEvent>       |
| - Run audit cycles       |  <------(lock-free MPSC)---------------+
+--------------------------+
```

**Zero locks.** The only shared structures are: `ConcurrentQueue` (lock-free) and `volatile bool[]` connection flags. All FSM state, audit state, and bracket tracking live exclusively on the strategy thread.

---

## Pillar 1: Symmetry Sync Logic

### 1.1 Command-Confirm Separation (Unbreakable FSMs)

The fundamental safety principle: **FSM transitions happen ONLY on broker confirmations, never on commands we send.**

When we submit an order to a follower, the FSM does NOT transition. It waits for the broker to confirm the state change. This means:
- `CancelAllOrders` from any source produces a broker `Cancelled` event -- the FSM handles it normally
- Manual interventions at the broker level are visible as broker events -- the FSM adapts
- Connection drops that cancel orders produce broker events on reconnect -- the FSM catches up

The FSM doesn't care WHO caused a state change -- only that the broker confirmed it.

### 1.2 Fan-Out Sequence

When the Master submits a bracket:

```
T+0ms   Master submits bracket (entry + stop + target params captured)
T+1ms   F01.Submit(bracket)  -- FSM stays IDLE (awaiting broker confirm)
T+2ms   F02.Submit(bracket)  -- FSM stays IDLE
...
T+18ms  F18.Submit(bracket)  -- FSM stays IDLE

TOTAL FAN-OUT: ~18ms for 18 accounts (sequential, non-blocking API calls)
```

Each `Submit()` is a non-blocking NT8 API call that queues internally. The strategy thread is not blocked.

### 1.3 Per-Follower Bracket FSM

Each of the 18 followers maintains one FSM per active bracket:

```
States:
  IDLE            -- bracket command sent, awaiting broker acknowledgment
  ENTRY_ACCEPTED  -- entry order acknowledged and working at exchange
  ACTIVE          -- entry filled, stop + target working
  MODIFYING       -- price change requested (trailing), awaiting confirm
  FAILED          -- entry rejected by broker (terminal state)
  CLOSED          -- stop or target filled (terminal state)
  CANCELLED       -- all orders cancelled (terminal state)

Transitions (ALL driven by broker events):
  IDLE           --[broker: Accepted]-->     ENTRY_ACCEPTED
  ENTRY_ACCEPTED --[broker: Filled]-->       ACTIVE (submit stop + target)
  ENTRY_ACCEPTED --[broker: Rejected]-->     FAILED
  ENTRY_ACCEPTED --[broker: Cancelled]-->    CANCELLED
  ACTIVE         --[broker: StopFilled]-->   CLOSED
  ACTIVE         --[broker: TargetFilled]--> CLOSED
  ACTIVE         --[modify command sent]-->  MODIFYING
  MODIFYING      --[broker: ChangeConfirm]-->ACTIVE (updated prices)
  MODIFYING      --[broker: Filled]-->       CLOSED (fill during modify)
  ANY_STATE      --[broker: Cancelled]-->    CANCELLED
```

### 1.4 Trailing Stop Synchronization

**Pattern**: Master computes new stop price --> Engine fans out `ModifyStop(newPrice)` to all ACTIVE followers.

**Absorption rule**: Only one modify can be in-flight per follower at a time. If a new trail tick arrives while a modify is pending, the newer price overwrites the pending modification spec. This prevents a cascade of modify requests.

**Safety**: If the stop fills DURING a pending modify, the FSM transitions to CLOSED. Any subsequent modify confirmation is a no-op on a CLOSED FSM.

### 1.5 Partial Fill & Rejection Handling

- **Partial fills**: Each follower fills independently. If F05 fills 1 of 2 contracts, its stop and target are sized to protect 1 contract. No forced repair.
- **Rejections**: FSM transitions to FAILED. Logged with reason code. No automatic retry (avoids order loops). The audit layer flags the divergence for human review.

---

## Pillar 2: The Audit Layer (Safety Hub)

### 2.1 Asymmetric Authority (Anti-Loop Guarantee)

The audit layer and sync engine have **non-overlapping authorities**. This is the fundamental guarantee against recursive order loops:

| Authority | Sync Engine (Pillar 1) | Audit Layer (Pillar 2) |
|-----------|----------------------|----------------------|
| Entry orders | YES (from Master) | NEVER |
| Initial stop/target | YES | NEVER |
| Price modifications | YES (trailing) | NEVER |
| Cancel mirroring | YES | NEVER |
| Ghost order cleanup | NEVER | YES (cancel untracked) |
| Protective order repair | NEVER | YES (missing stop only) |
| FSM staleness fix | NEVER | YES (force-sync) |

Because they operate on non-overlapping order types, they cannot fight each other.

### 2.2 Audit Cycle (Every 5s, Configurable)

```
Snapshot Collector          FSM State Aggregator
(query each account's      (collect current FSM
 positions + orders)        states for all 18)
        |                          |
        +--------+   +------------+
                 |   |
                 v   v
          Divergence Detector
          (compare reality vs FSM)
                 |
       +---------+---------+
       |         |         |
  Ghost Order  Naked     Stale FSM
  (untracked)  Position  (stuck >10s)
       |         |         |
  Cancel it   Submit    Force-sync
             emergency   to broker
             ATR stop    reality
                 |
          Circuit Breaker
          (max 3 corrections/cycle)
                 |
          Persistent? (3+ cycles)
                 |
           Human Alert
```

### 2.3 Divergence Types

1. **Ghost Order** (order at broker, no FSM tracking it)
   - Cause: order submitted before crash, FSM state lost
   - Fix: cancel the ghost. If already filled, route to Naked Position handler.

2. **Naked Position** (MOST DANGEROUS -- position with no protective stop)
   - Cause: protective orders cancelled/rejected, or ghost fill
   - Fix: submit emergency protective stop at worst-case ATR distance. Stop is mandatory; target is optional.

3. **Stale FSM** (stuck in transitional state > 10s)
   - Cause: broker confirmation dropped or delayed
   - Fix: query broker for actual order state, force-transition FSM to match reality.

4. **Quantity Mismatch** (FSM says 2 contracts, broker says 1)
   - Cause: partial fill event dropped, or manual intervention
   - Fix: adjust protective order quantity to match broker reality. FSM adopts broker qty as truth.

### 2.4 Circuit Breaker Rules

| Rule | Value |
|------|-------|
| Max corrective actions | 3 per account per audit cycle |
| Persistent divergence alert | 3 consecutive cycles with same divergence --> human alert |
| Cooldown | No corrective action on same order within 2 audit cycles |
| Priority | Naked position fix always takes priority over other corrections |

---

## Pillar 3: Concurrency & Thread-Safety

### 3.1 The AccountEvent Message

```csharp
struct AccountEvent  // struct = value type, no GC pressure
{
    // Identity
    string     AccountAlias;    // "F01", "F02", etc.
    int        FollowerIndex;   // 0-17, direct array indexing

    // Order info
    string     OrderId;         // Broker-assigned
    OrderState NewState;        // Accepted, Working, Filled, Cancelled, Rejected
    int        FilledQty;       // For partial fill tracking
    double     FillPrice;       // Actual fill price

    // Timing
    long       TimestampTicks;  // DateTime.UtcNow.Ticks at enqueue
}
```

### 3.2 Queue Drain Protocol

```
DrainMailbox() -- called from OnBarUpdate/OnOrderUpdate (STRATEGY THREAD ONLY)

  processed = 0
  MAX_PER_DRAIN = 100  // bound to prevent tick starvation

  while processed < MAX_PER_DRAIN && _mailbox.TryDequeue(out evt):
      _followerFSMs[evt.FollowerIndex].ProcessEvent(evt)
      processed++

  if processed == MAX_PER_DRAIN && !_mailbox.IsEmpty:
      Log("V12 MAILBOX BACKPRESSURE: {count} events pending")
```

Drain triggers: `OnBarUpdate()` (every tick), `OnOrderUpdate()` (Master events), audit timer callback.

### 3.3 Thread Ownership Map

| Data Structure | Owner Thread | Access Pattern |
|---------------|-------------|----------------|
| `_followerFSMs[18]` | Strategy only | Read + write from strategy thread only |
| `_mailbox` (ConcurrentQueue) | Shared (lock-free) | Account threads Enqueue, strategy TryDequeue |
| `_masterBracketState` | Strategy only | Updated from OnOrderUpdate/OnExecutionUpdate |
| `_auditSnapshots[18]` | Strategy only | Written during audit cycle |
| `_connectionState[18]` | Atomic (volatile) | Account threads write, strategy reads |

### 3.4 Critical Invariant

> **No code path outside the strategy thread may read or write FSM state.**
> Account event handlers contain exactly ONE line: `_mailbox.Enqueue(evt);`

Enforced by code review and grep scan (per P4 audit protocol). Any access to `_followerFSMs` outside of strategy-thread methods is a build-blocking defect.

---

## Pillar 4: Resilience Pattern

### 4.1 Per-Account Connection FSM

```
CONNECTED --> (ConnectionLost) --> DISCONNECTED --> (ConnectionRestored) --> RECONNECTING --> (Audit passes) --> CONNECTED
```

- **CONNECTED**: Normal operation. Receives sync commands.
- **DISCONNECTED**: FSMs frozen. Skipped in fan-out. Orders at exchange still live.
- **RECONNECTING**: Online but not yet synced. Single-account audit runs.

### 4.2 Disconnection Protocol (Instant)

On connection loss:
1. `_connectionState[i] = Disconnected` (volatile write from account thread)
2. `_mailbox.Enqueue(DISCONNECTED event)`
3. Strategy thread: freeze FSMs, skip in fan-out, log with timestamp
4. **Do NOT cancel or modify any orders** -- they're still live at the exchange protecting positions

### 4.3 Reconnection Protocol (Skip & Protect)

On connection restore:
1. **Mark online**: `_connectionState[i] = Reconnecting`
2. **Single-account audit**: Query broker for actual positions and working orders
3. **Protect naked positions**: If position exists without protective stop, submit emergency ATR-distance stop
4. **Sync FSM to reality**: Force-update FSM to match broker state (don't reconstruct outage history)
5. **Rejoin fleet**: `_connectionState[i] = Connected`. Unfreeze FSM. New sync commands flow normally.

**Missed entries are NOT replayed** -- submitting stale entries into a moved market is more dangerous than missing them.

### 4.4 Degraded Mode Thresholds

| Disconnected Count | Severity | Action |
|-------------------|----------|--------|
| 1-3 accounts | Normal (expected flickers) | Per-account handling. Fleet continues. Log only. |
| 4-9 accounts | Warning (infrastructure issue) | Fleet continues but UI alert. Consider pausing new entries. |
| 10+ accounts | Critical (systemic failure) | HALT new entries. Existing positions protected by exchange-side orders. IPC alert to operator. |

### 4.5 Self-Healing Gaps

If a follower missed a trailing stop move during a flicker (e.g., F07 at 4500 while fleet is at 4505), the NEXT Master trailing action naturally syncs it (to 4510). No special repair logic required -- the gap self-heals through normal sync operations.

### 4.6 What We Deliberately Do NOT Do

- **No fleet pause** for single disconnects -- 17 healthy accounts should not wait for 1
- **No entry replay** on reconnect -- stale entries in a moved market are dangerous
- **No automatic connection retry** -- Rithmic handles its own reconnection; we react to state changes
- **No order cancellation on disconnect** -- working orders protect positions; cancelling them is destructive

---

## Implementation Approach

1. **Define the `AccountEvent` struct and `ConcurrentQueue` mailbox** as the concurrency backbone
2. **Build the per-follower Bracket FSM** with the state machine defined in Section 1.3
3. **Implement the fan-out dispatcher** on the strategy thread (sequential, non-blocking)
4. **Wire account event handlers** to the single-line `_mailbox.Enqueue(evt)` pattern
5. **Build the audit cycle** with asymmetric authority and circuit breaker
6. **Implement the connection FSM** with skip-and-protect reconnection
7. **Add degraded mode monitoring** with the threshold table

## Verification Checklist

- [ ] **Thread safety**: Grep scan for any access to `_followerFSMs` outside strategy-thread methods
- [ ] **No locks**: Grep scan for `lock(` in all strategy files
- [ ] **ASCII compliance**: No emoji or Unicode in string literals (per CLAUDE.md)
- [ ] **FSM completeness**: Every broker event type has a defined transition from every non-terminal state
- [ ] **Audit authority**: Verify audit layer never submits entry orders or modifies prices
- [ ] **Circuit breaker**: Verify max corrections per cycle is bounded
- [ ] **Connection FSM**: Verify no orders are cancelled on disconnect
