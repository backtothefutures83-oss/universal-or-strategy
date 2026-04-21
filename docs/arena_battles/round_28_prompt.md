# Arena Round 28: Antigravity OS -- Sovereign MMIO Ring Buffer

## Prompt (copy-paste to Arena AI)

```
=== ARENA ROUND 28: ANTIGRAVITY OS -- SOVEREIGN MMIO RING BUFFER ===
=== SAL-V12.15 Platinum Standard -- AMAL 6-Test Gate Enforced ===

CONTEXT:
You are building the core IPC primitive for Antigravity OS: a lock-free SPSC
ring buffer designed to operate over a shared memory region (MMIO) between
two independent NativeAOT processes. This is NOT an abstract contract -- it
is a concrete, runnable implementation with an embedded test harness.

The ring carries unmanaged order/fill structs between a Kernel process
(producer) and an L1 Bridge process (consumer). Data integrity is validated
using XOR-Shadow invariants (no CRC -- XOR is cheaper and sufficient for
same-machine shared memory where bit-flip probability is negligible).

REFERENCE PATTERN (proven production code):
The existing V12 Photon Ring uses:
- Power-of-2 capacity with bitmask indexing (cursor & mask)
- Volatile.Read/Write for producer/consumer cursor fencing
- 7-field cache-line padding between producer and consumer cursors
- CRC16 checksums for defense-in-depth
You are EVOLVING this pattern: replace CRC16 with XOR-Shadow, replace
managed T[] array with unsafe pointer arithmetic over a fixed byte region
(simulating MemoryMappedFile), and ensure ALL types are unmanaged.

HARD CONSTRAINTS:
1. .NET 9, NativeAOT (no reflection, no dynamic loading, no GC on hot path)
2. ALL structs must be unmanaged -- zero reference-type fields
3. ALL identifiers are long (not string) -- InstrumentId, StrategyId, OrderId
4. No locks, no Interlocked, no Monitor (ADR-001 permanent)
5. Volatile.Read/Write ONLY for cursor fencing (x86-TSO leveraged)
6. unsafe code allowed and expected for pointer arithmetic over shared region
7. Single self-contained .cs file with a static Main() entry point

DELIVERABLES (single .cs file):

1. OrderSlot struct (unmanaged, fixed-size):
   - long OrderId
   - long InstrumentId
   - long StrategyId
   - int Quantity
   - double Price
   - byte Side (0=Buy, 1=Sell)
   - byte OrderType (0=Market, 1=Limit, 2=StopMarket, 3=StopLimit)
   - long ParentId (for OCO/bracket linking)
   - long TimestampTicks (DateTime.UtcNow.Ticks equivalent)
   - ulong XorShadow (integrity field)
   - Explicit StructLayout with Pack=1 or sequential, no padding surprises

2. FillSlot struct (unmanaged, fixed-size):
   - long OrderId
   - long InstrumentId
   - double FillPrice
   - int FillQuantity
   - long TimestampTicks
   - ulong XorShadow
   - Explicit StructLayout

3. XorShadow static helper:
   - static unsafe ulong Compute(byte* ptr, int lengthExcludingShadow, ulong salt)
     XORs all 8-byte words in the payload region, then XORs with salt.
   - static unsafe bool Validate(byte* ptr, int lengthExcludingShadow, ulong shadow, ulong salt)
     Recomputes and compares.

4. MmioSpscRing<T> where T : unmanaged
   - Constructor takes byte* basePtr and int capacityPowerOf2
   - Layout in the shared region:
     [ProducerCursor:8][Pad:56][ConsumerCursor:8][Pad:56][Slot0:sizeof(T)][Slot1:sizeof(T)]...
   - bool TryEnqueue(in T item) -- writes slot, computes+stamps XorShadow, publishes cursor
   - bool TryDequeue(out T item, out bool xorValid) -- reads slot, validates XorShadow, advances cursor
   - int Count { get; }
   - bool IsEmpty { get; }
   - static int ComputeRegionSize(int capacityPowerOf2) -- returns total bytes needed

5. static void Main(string[] args) -- THE TEST HARNESS (MANDATORY):

   Print "=== AMAL TEST HARNESS: MmioSpscRing ===" at start.
   Allocate a byte[] as simulated shared memory region.
   Run these 8 tests, printing "PASS: <name>" or "FAIL: <name>" for each:

   TEST 1 - "SingleEnqueueDequeue":
     Enqueue one OrderSlot, dequeue it. Verify all fields match exactly.

   TEST 2 - "XorShadowValid":
     Enqueue a slot, dequeue it. Verify xorValid==true on dequeue.

   TEST 3 - "XorShadowCorruptionDetect":
     Enqueue a slot. Before dequeuing, corrupt one byte in the slot region
     (flip a bit via pointer). Dequeue and verify xorValid==false.

   TEST 4 - "RingFullBehavior":
     Fill ring to capacity. Verify next TryEnqueue returns false.
     Dequeue one, verify TryEnqueue now returns true.

   TEST 5 - "RingEmptyBehavior":
     On empty ring, verify TryDequeue returns false.

   TEST 6 - "FullCapacityDrainAndRefill":
     Fill ring completely, drain completely, refill completely.
     Verify all items dequeued match enqueued (field-by-field).

   TEST 7 - "ThroughputBenchmark":
     Enqueue+Dequeue 1,000,000 items sequentially (single-threaded).
     Measure elapsed ticks. Print: "Throughput: X ns/op (Y ops/sec)"
     This is NOT a pass/fail -- it is a measurement for the record.

   TEST 8 - "FillSlotRoundTrip":
     Create MmioSpscRing<FillSlot>. Enqueue a FillSlot, dequeue it.
     Verify all fields including XorShadow validity.

   After all tests, print:
   "=== RESULTS: X/8 PASSED ==="
   "=== STRUCT SIZES: OrderSlot=Xb, FillSlot=Yb ==="

ANTI-PATTERNS (DO NOT):
- No abstract classes or interfaces in this file (concrete only)
- No string fields anywhere (all long/int/double/byte)
- No managed arrays inside structs (use fixed byte[] if needed)
- No System.Collections.Generic usage
- No async/await
- No IDisposable
- No try/catch in the ring operations (caller handles)
- No Console.ReadLine or interactive input
- No external NuGet dependencies

COMPILATION COMMAND:
dotnet run --project . -c Release
-- OR --
dotnet publish -c Release -r win-x64 /p:PublishAot=true && run the native binary

NAMESPACE: AntigravityOS.Kernel.Mmio
FILE: MmioSpscRing.cs

The output must compile, run, and print 8 test results with zero warnings.
```

## Local Verification (Antigravity/Director)

1. Save Arena output to `src/antigravity/MmioSpscRing.cs`
2. Ensure `src/antigravity/AntigravityOS.Kernel.csproj` exists:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>Exe</OutputType>
       <TargetFramework>net9.0</TargetFramework>
       <PublishAot>true</PublishAot>
       <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
       <InvariantGlobalization>true</InvariantGlobalization>
     </PropertyGroup>
   </Project>
   ```
3. Run: `dotnet run --project src/antigravity -c Release`
4. AMAL Gate: Count PASS lines. Minimum 6/8 required for Go Decision.
5. Record throughput (Test 7) and struct sizes for the Arena audit matrix.
6. Report back to Claude with: full console output, pass count, throughput.

## Success Criteria (Round 28)
- 6+ tests PASS (AMAL gate)
- All structs are unmanaged (sizeof() compiles)
- XorShadow detects corruption (Test 3 proves this)
- NativeAOT publish succeeds with zero trimming warnings
- Throughput benchmark provides baseline for future Arena rounds
