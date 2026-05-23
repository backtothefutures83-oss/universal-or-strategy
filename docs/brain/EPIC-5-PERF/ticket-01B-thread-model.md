# EPIC-5-PERF: Ticket 01B - Thread Model Analysis & ThreadStatic Validation

**Ticket ID:** T01B  
**Epic:** EPIC-5-PERF  
**Type:** Validation (No Production Code Changes)  
**Priority:** P2 (Blocks T02)  
**Estimated Duration:** 1 day  
**Dependencies:** T01 (Baseline Instrumentation)

---

## OBJECTIVE

Validate ThreadStatic safety for LogBuffer within NinjaTrader's threading model and Actor pattern context. Provide SAFE/UNSAFE verdict to determine implementation strategy for T02 (String.Format Elimination).

**Success Criteria:**
- NinjaTrader threading model documented
- ThreadStatic safety validated via test harness
- Performance overhead measured (<5% acceptable)
- Actor pattern compatibility confirmed
- **Decision:** ThreadStatic APPROVED or FALLBACK to instance-level buffer

---

## SCOPE

### 1. NinjaTrader Threading Model Investigation

**Goal:** Document which threads execute V12 entry points.

**Investigation Points:**
1. **OnBarUpdate Thread:**
   - Single-threaded per instrument?
   - Thread-pooled?
   - Thread ID consistency across bars?

2. **OnMarketData Thread:**
   - Same thread as OnBarUpdate?
   - Separate tick processing thread?
   - Thread ID consistency across ticks?

3. **OnOrderUpdate Thread:**
   - Same thread as OnBarUpdate?
   - Separate order processing thread?
   - Thread ID consistency across order updates?

4. **Enqueue/Actor Thread:**
   - Dedicated Actor thread per strategy instance?
   - Shared thread pool?
   - Thread ID consistency across Enqueue calls?

5. **UI Thread:**
   - WPF Dispatcher thread?
   - Separate from trading threads?

**Deliverable:** `docs/brain/EPIC-5-PERF/thread-model-report.md`

---

### 2. ThreadStatic Safety Test Harness

**Goal:** Validate ThreadStatic char[] buffer under concurrent access.

**Test Scenarios:**

#### Test 1: Thread Isolation
```csharp
// Verify each thread gets its own buffer
[ThreadStatic]
private static char[] _testBuffer;

[Test]
public void ThreadStatic_ThreadIsolation_NoCorruption()
{
    const int THREAD_COUNT = 10;
    const int ITERATIONS = 1000;
    
    var threads = new Thread[THREAD_COUNT];
    var errors = new ConcurrentBag<string>();
    
    for (int i = 0; i < THREAD_COUNT; i++)
    {
        int threadId = i;
        threads[i] = new Thread(() =>
        {
            for (int j = 0; j < ITERATIONS; j++)
            {
                if (_testBuffer == null)
                    _testBuffer = new char[512];
                
                // Write thread-specific pattern
                for (int k = 0; k < 512; k++)
                    _testBuffer[k] = (char)('A' + threadId);
                
                // Verify no corruption
                for (int k = 0; k < 512; k++)
                {
                    if (_testBuffer[k] != (char)('A' + threadId))
                        errors.Add($"Thread {threadId} corrupted at index {k}");
                }
            }
        });
    }
    
    foreach (var t in threads) t.Start();
    foreach (var t in threads) t.Join();
    
    Assert.IsEmpty(errors, "ThreadStatic buffer corruption detected");
}
```

#### Test 2: Actor Pattern Compatibility
```csharp
// Verify ThreadStatic works with Enqueue pattern
[Test]
public void ThreadStatic_ActorPattern_SafeAccess()
{
    var actorQueue = new ConcurrentQueue<Action>();
    var actorThread = new Thread(() =>
    {
        while (actorQueue.TryDequeue(out var action))
            action();
    });
    
    actorThread.Start();
    
    // Enqueue 1000 operations from multiple threads
    var threads = new Thread[10];
    for (int i = 0; i < 10; i++)
    {
        int threadId = i;
        threads[i] = new Thread(() =>
        {
            for (int j = 0; j < 100; j++)
            {
                actorQueue.Enqueue(() =>
                {
                    if (_testBuffer == null)
                        _testBuffer = new char[512];
                    
                    // Write and verify
                    _testBuffer[0] = (char)('A' + threadId);
                    Assert.AreEqual((char)('A' + threadId), _testBuffer[0]);
                });
            }
        });
    }
    
    foreach (var t in threads) t.Start();
    foreach (var t in threads) t.Join();
    actorThread.Join();
}
```

#### Test 3: Thread Pool Leak Detection
```csharp
// Verify ThreadStatic doesn't leak memory in thread pool
[Test]
public void ThreadStatic_ThreadPool_NoLeak()
{
    var initialMemory = GC.GetTotalMemory(true);
    
    // Simulate thread pool usage
    var tasks = new Task[100];
    for (int i = 0; i < 100; i++)
    {
        tasks[i] = Task.Run(() =>
        {
            if (_testBuffer == null)
                _testBuffer = new char[512];
            
            // Use buffer
            for (int j = 0; j < 512; j++)
                _testBuffer[j] = 'X';
        });
    }
    
    Task.WaitAll(tasks);
    
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    var finalMemory = GC.GetTotalMemory(true);
    var leakBytes = finalMemory - initialMemory;
    
    // Allow 100KB overhead (100 threads × 512 chars × 2 bytes = 102KB)
    Assert.That(leakBytes, Is.LessThan(200_000), 
        $"Memory leak detected: {leakBytes} bytes");
}
```

**Deliverable:** `tests/ThreadStaticSafetyTest.cs`

---

### 3. Thread ID Logging Instrumentation

**Goal:** Log Thread.CurrentThread.ManagedThreadId at all V12 entry points.

**Instrumentation Points:**
1. OnBarUpdate (start of method)
2. OnMarketData (start of method)
3. OnOrderUpdate (start of method)
4. ProcessIpcCommands (start of method)
5. Enqueue callback (inside lambda)
6. PublishUiSnapshot (start of method)

**Implementation:**
```csharp
// Add to each entry point:
private void OnBarUpdate()
{
    int threadId = Thread.CurrentThread.ManagedThreadId;
    Print($"[THREAD-MODEL] OnBarUpdate: ThreadId={threadId}");
    
    // ... existing logic ...
}
```

**Data Collection:**
- Run strategy for 10 minutes under normal load
- Collect all thread ID logs
- Analyze for consistency patterns

**Deliverable:** Thread ID log analysis in `thread-model-report.md`

---

### 4. Performance Comparison

**Goal:** Measure ThreadStatic overhead vs instance-level buffer.

**Benchmark:**
```csharp
[Benchmark]
public string ThreadStatic_Format()
{
    return LogBuffer.Format("[TEST] Value={0}, Price={1:F2}", 123, 45.67);
}

[Benchmark]
public string InstanceLevel_Format()
{
    return _instanceLogBuffer.Format("[TEST] Value={0}, Price={1:F2}", 123, 45.67);
}
```

**Metrics:**
- Mean execution time (ns)
- Allocation (bytes)
- p99 latency (ns)

**Acceptance:** ThreadStatic overhead <5% vs instance-level

**Deliverable:** Benchmark results in `thread-model-report.md`

---

## DELIVERABLES

### 1. Thread Model Report
**File:** `docs/brain/EPIC-5-PERF/thread-model-report.md`

**Structure:**
```markdown
# NinjaTrader Threading Model Analysis

## Executive Summary
- Thread model type: [Single-threaded / Thread-pooled / Hybrid]
- ThreadStatic verdict: [SAFE / UNSAFE]
- Recommendation: [ThreadStatic / Instance-level buffer]

## Thread ID Analysis
| Entry Point | Thread ID Range | Consistency | Notes |
|-------------|----------------|-------------|-------|
| OnBarUpdate | 1234 | 100% same | Single-threaded |
| OnMarketData | 1234 | 100% same | Same as OnBarUpdate |
| OnOrderUpdate | 1234 | 100% same | Same as OnBarUpdate |
| Enqueue | 5678 | 100% same | Dedicated Actor thread |
| PublishUiSnapshot | 1234 | 100% same | Same as OnBarUpdate |

## ThreadStatic Safety Analysis
- Test 1 (Thread Isolation): [PASS / FAIL]
- Test 2 (Actor Pattern): [PASS / FAIL]
- Test 3 (Thread Pool Leak): [PASS / FAIL]

## Performance Comparison
| Implementation | Mean (ns) | Allocation | p99 (ns) | Overhead |
|----------------|-----------|------------|----------|----------|
| ThreadStatic | 150 | 0 bytes | 200 | Baseline |
| Instance-level | 160 | 0 bytes | 210 | +6.7% |

## Actor Pattern Compatibility
- ThreadStatic bypasses Actor queue: [YES / NO]
- Safe for read-only state access: [YES / NO]
- Safe for logging: [YES / NO]

## Decision
**Verdict:** [SAFE / UNSAFE]
**Recommendation:** [Use ThreadStatic / Use instance-level buffer]
**Rationale:** [Explanation]
```

### 2. Test Harness
**File:** `tests/ThreadStaticSafetyTest.cs`

**Requirements:**
- All 3 test scenarios implemented
- Tests pass with zero errors
- Tests run in <10 seconds

### 3. Thread ID Logs
**File:** `docs/brain/EPIC-5-PERF/thread-id-logs.txt`

**Format:**
```
[2026-05-23 10:15:23.456] [THREAD-MODEL] OnBarUpdate: ThreadId=1234
[2026-05-23 10:15:23.457] [THREAD-MODEL] OnMarketData: ThreadId=1234
[2026-05-23 10:15:23.458] [THREAD-MODEL] Enqueue: ThreadId=5678
...
```

---

## ACCEPTANCE CRITERIA

### Must-Have (Blocking T02)
- [ ] Thread model documented in `thread-model-report.md`
- [ ] ThreadStatic safety verdict: SAFE or UNSAFE
- [ ] If SAFE: All 3 tests pass with zero errors
- [ ] If UNSAFE: Fallback strategy documented
- [ ] Performance overhead measured (<5% acceptable)
- [ ] Actor pattern compatibility confirmed

### Nice-to-Have
- [ ] Benchmark comparison chart (ThreadStatic vs instance-level)
- [ ] Thread lifecycle diagram (visual)
- [ ] NinjaTrader API documentation references

---

## RISKS & MITIGATIONS

### Risk 1: ThreadStatic Unsafe
**Probability:** LOW  
**Impact:** HIGH (blocks T02 ThreadStatic implementation)  
**Mitigation:** Fallback to instance-level buffer with lock protection

### Risk 2: Thread Pool Leak
**Probability:** MEDIUM  
**Impact:** MEDIUM (memory leak over time)  
**Mitigation:** Document leak, recommend instance-level buffer

### Risk 3: Actor Pattern Incompatibility
**Probability:** LOW  
**Impact:** HIGH (violates V12 DNA)  
**Mitigation:** Document incompatibility, recommend instance-level buffer

---

## V12 DNA COMPLIANCE

- **Lock-Free Actor Pattern:** ✅ No locks introduced (validation only)
- **ASCII-Only:** ✅ No string literals (validation only)
- **Correctness by Construction:** ✅ Test harness validates safety
- **Bounded Latency:** ✅ No unbounded loops (validation only)
- **Thread Safety:** ✅ PRIMARY FOCUS OF THIS TICKET

---

## EXECUTION PROTOCOL

### Step 1: Thread ID Instrumentation
1. Add thread ID logging to 6 entry points
2. Run strategy for 10 minutes
3. Collect logs to `thread-id-logs.txt`
4. Analyze for consistency patterns

### Step 2: Test Harness Implementation
1. Create `tests/ThreadStaticSafetyTest.cs`
2. Implement 3 test scenarios
3. Run tests, verify all pass
4. Document results in `thread-model-report.md`

### Step 3: Performance Benchmark
1. Implement ThreadStatic and instance-level LogBuffer prototypes
2. Run BenchmarkDotNet comparison
3. Document results in `thread-model-report.md`

### Step 4: Decision & Documentation
1. Analyze all data (thread IDs, tests, benchmarks)
2. Make SAFE/UNSAFE verdict
3. Document recommendation in `thread-model-report.md`
4. Update T02 ticket with implementation strategy

---

## HANDOFF TO T02

**If ThreadStatic SAFE:**
- T02 implements LogBuffer with ThreadStatic char[] buffer
- No lock required
- Zero allocation guaranteed

**If ThreadStatic UNSAFE:**
- T02 implements LogBuffer with instance-level char[] buffer
- Add `_logBuffer` field to V12_002 class
- Protect with lock (acceptable for logging, not hot path)
- Document performance trade-off

---

## NOTES

- This is a **validation-only** ticket (no production code changes)
- All test code goes in `tests/` directory
- All documentation goes in `docs/brain/EPIC-5-PERF/`
- Thread ID logging is temporary (remove after analysis)
- Decision must be made before T02 can proceed

---

**[TICKET-READY]** T01B ready for execution. Awaiting Director approval.