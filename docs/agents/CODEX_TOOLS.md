# Codex CLI Tools & Capabilities

## Overview

**Agent**: Codex CLI (`codex-rescue` mode)  
**Role**: Secondary engineer for surgical logic hardening and lock-free kernel updates  
**Strengths**: Logic hardening, atomic operations, lock-free patterns, kernel-level updates  
**Primary Use Cases**: Logic hardening when Bob delegates, lock-free conversions, atomic operation audits

## Core Capabilities

### 1. Logic Hardening Specialist

Codex CLI excels at **surgical logic hardening**:

- **Atomic Operations**: Convert lock-based code to lock-free patterns
- **Race Condition Fixes**: Identify and eliminate race conditions
- **Memory Safety**: Ensure thread-safe memory access
- **CAS Operations**: Implement Compare-And-Swap patterns

### 2. Lock-Free Kernel Updates

**Expertise Areas**:
- FSM/Actor pattern implementation
- Lock-free queue operations
- Atomic state transitions
- Memory ordering guarantees

### 3. V12 DNA Enforcement

Codex CLI is **trained on V12 DNA principles**:

- ✅ Lock-free Actor pattern (MANDATORY)
- ✅ ASCII-only compliance
- ✅ Correctness by construction
- ✅ Jane Street alignment

## Tool Access Matrix

| Tool Category | Access Level | Notes |
|---------------|--------------|-------|
| **Code Navigation** | ✅ Full | jCodemunch MCP |
| **Architecture** | ✅ Full | Routa CLI, graphify |
| **Knowledge Base** | ✅ Full | Jane Street KB (HFT patterns) |
| **Testing** | ✅ Full | All test harnesses |
| **Build & Deploy** | ✅ Full | Local PowerShell scripts |
| **PR Workflow** | ✅ Full | Via gh CLI |
| **GitHub Apps** | ⚠️ Limited | Read-only access |
| **MCP Servers** | ✅ Full | All configured servers |

## When to Use Codex vs Bob

### Use Codex CLI When:

1. **Bob Delegates Logic Hardening**:
   - Bob identifies logic that needs hardening
   - Bob hands off to Codex for surgical fixes
   - Codex applies atomic operations
   - Bob reviews and integrates

2. **Lock-Free Conversions**:
   - Converting `lock(stateLock)` to FSM/Actor
   - Implementing atomic primitives
   - Ensuring memory ordering
   - Eliminating race conditions

3. **Atomic Operation Audits**:
   - Reviewing CAS usage
   - Verifying memory barriers
   - Checking volatile semantics
   - Ensuring thread safety

4. **Kernel-Level Updates**:
   - FSM state machine modifications
   - Actor queue operations
   - Lock-free data structures
   - Memory-mapped I/O (MMIO)

### Use Bob CLI When:

1. **Full Feature Implementation**:
   - End-to-end feature development
   - Architecture design
   - Multi-file refactoring
   - God-function splitting

2. **Design Gates**:
   - Architecture planning
   - Implementation specs
   - Mermaid diagrams
   - Mini-spec creation

3. **Extraction Tasks**:
   - SIMA subgraph extraction
   - Module separation
   - Interface definition
   - Dependency management

## Workflow Integration

### 1. Bob → Codex Handoff

**Typical Flow**:

```markdown
## Bob's Analysis (Stage 1)

"I've identified a lock-based pattern in V12_002.Orders.Management.cs 
that needs hardening. The `ProcessOrder` method uses `lock(stateLock)` 
which violates V12 DNA."

**Handoff to Codex:**
- File: src/V12_002.Orders.Management.cs
- Method: ProcessOrder (lines 145-178)
- Issue: lock(stateLock) usage
- Goal: Convert to FSM/Actor pattern
```

**Codex's Response**:

```csharp
// BEFORE (lock-based)
lock (stateLock)
{
    if (orderState == OrderState.Pending)
    {
        orderState = OrderState.Processing;
        ProcessOrderInternal(order);
    }
}

// AFTER (lock-free)
private void ProcessOrder(Order order)
{
    // Enqueue to FSM Actor
    _fsmActor.Enqueue(new ProcessOrderCommand(order));
}

private void HandleProcessOrderCommand(ProcessOrderCommand cmd)
{
    // Atomic state transition
    var expected = OrderState.Pending;
    var desired = OrderState.Processing;
    
    if (Interlocked.CompareExchange(
        ref _orderState, 
        (int)desired, 
        (int)expected) == (int)expected)
    {
        ProcessOrderInternal(cmd.Order);
    }
}
```

### 2. Atomic Operation Patterns

**CAS (Compare-And-Swap)**:

```csharp
// Pattern: Atomic state transition
var expected = CurrentState;
var desired = NextState;

if (Interlocked.CompareExchange(
    ref _state, 
    (int)desired, 
    (int)expected) == (int)expected)
{
    // Transition succeeded
    OnStateChanged(expected, desired);
}
else
{
    // Transition failed (retry or handle)
    HandleTransitionFailure();
}
```

**Atomic Increment**:

```csharp
// Pattern: Thread-safe counter
var newValue = Interlocked.Increment(ref _counter);
```

**Atomic Exchange**:

```csharp
// Pattern: Atomic swap
var oldValue = Interlocked.Exchange(ref _value, newValue);
```

### 3. FSM/Actor Pattern

**Actor Enqueue**:

```csharp
// Pattern: Lock-free command queue
public void Enqueue(ICommand command)
{
    _commandQueue.Enqueue(command);
    _semaphore.Release(); // Signal worker thread
}
```

**Actor Processing Loop**:

```csharp
// Pattern: Single-threaded command processor
private void ProcessCommands()
{
    while (!_cancellationToken.IsCancellationRequested)
    {
        _semaphore.Wait(_cancellationToken);
        
        if (_commandQueue.TryDequeue(out var command))
        {
            command.Execute(this);
        }
    }
}
```

## V12 DNA Enforcement

### 1. Lock Detection

**Banned Patterns**:
```csharp
// ❌ BANNED: lock statement
lock (stateLock) { ... }

// ❌ BANNED: Monitor
Monitor.Enter(obj);
Monitor.Exit(obj);

// ❌ BANNED: Mutex
mutex.WaitOne();
mutex.ReleaseMutex();
```

**Enforcement**:
```bash
# Forensic scan (must return zero matches)
grep -r "lock(" src/

# Semgrep rule
semgrep --config .semgrep.yml src/
```

### 2. Atomic Primitives

**Approved Patterns**:
```csharp
// ✅ APPROVED: Interlocked operations
Interlocked.CompareExchange(ref _state, desired, expected);
Interlocked.Increment(ref _counter);
Interlocked.Exchange(ref _value, newValue);
Interlocked.Add(ref _sum, delta);

// ✅ APPROVED: Volatile reads/writes
var value = Volatile.Read(ref _field);
Volatile.Write(ref _field, value);

// ✅ APPROVED: Memory barriers
Thread.MemoryBarrier();
```

### 3. ASCII-Only Compliance

**Enforcement**:
```bash
# Check for Unicode violations
python check_ascii.py src/

# Must return: "All files are ASCII-compliant"
```

## Jane Street Alignment

Codex CLI is **trained on Jane Street patterns**:

### 1. Microsecond-Latency Patterns

**Query KB Before Implementation**:
```bash
python scripts/query_kb.py "lock-free queue implementation"
python scripts/query_kb.py "atomic state transitions"
python scripts/query_kb.py "memory ordering guarantees"
```

### 2. Correctness by Construction

**Design Principle**:
- Make illegal states unrepresentable
- Use type system to enforce invariants
- Eliminate runtime checks via design

**Example**:
```csharp
// ❌ BAD: Runtime validation
if (state != OrderState.Pending)
    throw new InvalidOperationException();

// ✅ GOOD: Type-level enforcement
public sealed class PendingOrder
{
    // Can only be created in Pending state
    // Transition returns ProcessingOrder type
    public ProcessingOrder Process() { ... }
}
```

### 3. Testing Standards

**Mandatory Tests**:
- Unit tests for atomic operations
- Race condition stress tests
- Memory ordering verification
- Concurrency benchmarks

## Configuration

### Environment Variables

```bash
# Required
CODEX_MODE=codex-rescue

# Optional
CODEX_STRICT_MODE=true  # Enforce V12 DNA
CODEX_AUTO_TEST=true    # Run tests after changes
```

### .codex/settings.json

```json
{
  "mode": "codex-rescue",
  "strict_dna": true,
  "auto_test": true,
  "lock_detection": {
    "enabled": true,
    "fail_on_violation": true
  },
  "atomic_patterns": {
    "prefer_cas": true,
    "require_memory_barriers": true
  }
}
```

## Best Practices

### 1. Always Query Jane Street KB

**Before any logic hardening**:
```bash
python scripts/query_kb.py "lock-free <pattern>"
```

### 2. Test Atomic Operations

**Mandatory test coverage**:
```csharp
[Fact]
public void AtomicStateTransition_ShouldBeThreadSafe()
{
    // Arrange
    var actor = new OrderActor();
    var tasks = new Task[100];
    
    // Act: Concurrent state transitions
    for (int i = 0; i < 100; i++)
    {
        tasks[i] = Task.Run(() => actor.ProcessOrder(order));
    }
    Task.WaitAll(tasks);
    
    // Assert: No race conditions
    Assert.Equal(OrderState.Completed, actor.State);
}
```

### 3. Verify Memory Ordering

**Use memory barriers**:
```csharp
// Write barrier before publishing
Volatile.Write(ref _isReady, true);

// Read barrier before consuming
if (Volatile.Read(ref _isReady))
{
    ProcessData();
}
```

## Common Commands

### Lock Detection

```bash
# Scan for lock violations
grep -r "lock(" src/

# Semgrep audit
powershell -File .\scripts\run_semgrep.ps1

# Expected: Zero matches
```

### Atomic Operation Audit

```bash
# Find all Interlocked usage
grep -r "Interlocked\." src/

# Verify CAS patterns
grep -r "CompareExchange" src/
```

### Testing

```bash
# Run concurrency tests
dotnet test tests/V12_Performance.Tests/ --filter "Category=Concurrency"

# Run stress tests
powershell -File .\scripts\test_stress.ps1
```

## Troubleshooting

### Race Conditions

```bash
# Symptoms:
- Intermittent test failures
- State corruption
- Deadlocks

# Diagnosis:
1. Add logging around atomic operations
2. Run stress tests with ThreadSanitizer
3. Review memory ordering

# Fix:
1. Add memory barriers
2. Use stronger CAS patterns
3. Increase retry logic
```

### Memory Ordering Issues

```bash
# Symptoms:
- Stale reads
- Reordering bugs
- Cache coherency issues

# Diagnosis:
1. Check volatile usage
2. Verify memory barriers
3. Review CPU architecture docs

# Fix:
1. Add Thread.MemoryBarrier()
2. Use Volatile.Read/Write
3. Strengthen memory ordering
```

## References

- [Jane Street Knowledge Base](scripts/query_kb.py)
- [Lock-Free Patterns](docs/intel/jane-street/lock-free-patterns.md)
- [Atomic Operations Guide](docs/intel/jane-street/atomic-operations.md)
- [V12 DNA Protocol](AGENTS.md#architectural-mandates)
- [Universal Agent Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)