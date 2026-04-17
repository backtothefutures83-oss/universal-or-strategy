export function CacheConcurrency() {
  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="bg-gradient-to-br from-green-900/50 to-slate-800 rounded-2xl p-8 border border-green-700/50">
        <div className="flex items-start gap-4">
          <div className="text-5xl">⚡</div>
          <div className="flex-1">
            <h2 className="text-3xl font-bold text-white mb-2">Cache Concurrency Guard</h2>
            <p className="text-green-100 text-lg">
              Memory alignment and padding strategy to prevent cache-line invalidation across 12 threads
            </p>
          </div>
          <div className="bg-green-500/20 px-4 py-2 rounded-lg border border-green-500/30">
            <div className="text-green-400 text-sm font-medium">Latency Impact</div>
            <div className="text-green-300 text-2xl font-bold font-mono">~0.5ns</div>
          </div>
        </div>
      </div>

      {/* Technical Mechanism */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">⚙️</span>
          Technical Mechanism
        </h3>
        <div className="space-y-4 text-slate-300">
          <p className="leading-relaxed">
            The cache concurrency guard prevents <span className="text-green-400 font-semibold">false sharing</span> - 
            the silent performance killer where threads writing to different variables on the same cache line 
            trigger unnecessary cache invalidations across cores.
          </p>
          <ul className="space-y-2 ml-6">
            <li className="flex items-start gap-2">
              <span className="text-green-400 mt-1">•</span>
              <span><strong className="text-white">64-byte cache line alignment:</strong> Match hardware cache line size (typical on x86-64)</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-green-400 mt-1">•</span>
              <span><strong className="text-white">Explicit padding:</strong> Fixed-size byte arrays to fill cache lines</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-green-400 mt-1">•</span>
              <span><strong className="text-white">StructLayout control:</strong> Explicit field offsets for guaranteed layout</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-green-400 mt-1">•</span>
              <span><strong className="text-white">Per-thread ownership:</strong> Each thread owns exclusive cache lines</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-green-400 mt-1">•</span>
              <span><strong className="text-white">Read-only sharing:</strong> Immutable data can be shared without invalidation</span>
            </li>
          </ul>
        </div>
      </div>

      {/* False Sharing Problem */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">⚠️</span>
          The False Sharing Problem
        </h3>
        <div className="grid md:grid-cols-2 gap-6">
          <div>
            <h4 className="text-red-400 font-semibold mb-3 flex items-center gap-2">
              <span>❌</span>
              <span>Bad: Shared Cache Line</span>
            </h4>
            <div className="bg-slate-900 rounded-lg p-4 border border-red-700/50">
              <div className="font-mono text-xs space-y-2 text-slate-300">
                <div className="text-red-400 mb-2">// 64-byte cache line</div>
                <div className="bg-red-900/30 p-2 rounded border border-red-700/50">
                  <div>Byte 0-7: Thread 0 counter</div>
                  <div>Byte 8-15: Thread 1 counter</div>
                  <div>Byte 16-23: Thread 2 counter</div>
                  <div className="text-slate-500">...</div>
                </div>
                <div className="text-red-400 mt-3">
                  ⚠️ When Thread 0 writes, cache line invalidates for ALL threads!
                </div>
              </div>
            </div>
            <div className="mt-3 text-sm text-slate-400">
              <strong className="text-red-400">Result:</strong> Cache coherency protocol triggers expensive 
              MESI state transitions. Each write causes 50-200ns stalls on other cores.
            </div>
          </div>
          
          <div>
            <h4 className="text-green-400 font-semibold mb-3 flex items-center gap-2">
              <span>✓</span>
              <span>Good: Isolated Cache Lines</span>
            </h4>
            <div className="bg-slate-900 rounded-lg p-4 border border-green-700/50">
              <div className="font-mono text-xs space-y-2 text-slate-300">
                <div className="text-green-400 mb-2">// Separate 64-byte lines</div>
                <div className="bg-green-900/30 p-2 rounded border border-green-700/50 mb-2">
                  <div>Cache Line 0:</div>
                  <div className="ml-2">Thread 0 counter + padding</div>
                </div>
                <div className="bg-green-900/30 p-2 rounded border border-green-700/50 mb-2">
                  <div>Cache Line 1:</div>
                  <div className="ml-2">Thread 1 counter + padding</div>
                </div>
                <div className="bg-green-900/30 p-2 rounded border border-green-700/50">
                  <div>Cache Line 2:</div>
                  <div className="ml-2">Thread 2 counter + padding</div>
                </div>
              </div>
            </div>
            <div className="mt-3 text-sm text-slate-400">
              <strong className="text-green-400">Result:</strong> Each thread owns its cache line. 
              Writes don't trigger invalidations on other cores. Near-zero contention.
            </div>
          </div>
        </div>
      </div>

      {/* Code Implementation */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">💻</span>
          C# Implementation
        </h3>
        <div className="bg-slate-900 rounded-lg p-4 border border-slate-700 overflow-x-auto">
          <pre className="text-sm">
            <code className="text-slate-300">
{`using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Cache-line aligned structure for per-thread data
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct CacheAlignedCounter
{
    // Actual data (8 bytes)
    [FieldOffset(0)]
    public long Value;
    
    // Padding to fill 64-byte cache line (56 bytes)
    [FieldOffset(8)]
    private fixed byte _padding[56];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment()
    {
        Value++;
    }
}

// Per-thread packet processing state
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct ThreadLocalState
{
    // --- Cache Line 0: Hot write path (0-63) ---
    
    // Packet counters (written frequently)
    [FieldOffset(0)]
    public long PacketsProcessed;
    
    [FieldOffset(8)]
    public long BytesProcessed;
    
    [FieldOffset(16)]
    public long CurrentTimestamp;
    
    // Padding to fill first cache line
    [FieldOffset(24)]
    private fixed byte _hotPadding[40];
    
    // --- Cache Line 1: Configuration (64-127) ---
    
    // Read-only config (rarely changes, safe to share)
    [FieldOffset(64)]
    public int ThreadId;
    
    [FieldOffset(68)]
    public int ProcessorAffinity;
    
    [FieldOffset(72)]
    private fixed byte _configPadding[56];
}

// Main data plane structure for 12 parallel threads
public unsafe struct DataPlane
{
    // Each thread gets its own cache-aligned state
    private ThreadLocalState* _threadStates;
    private const int MAX_THREADS = 12;
    
    public void Initialize()
    {
        // Allocate 128 bytes per thread (2 cache lines each)
        var size = (nuint)(MAX_THREADS * sizeof(ThreadLocalState));
        
        // Align to 64-byte boundary
        var ptr = NativeMemory.AllocAligned(size, 64);
        _threadStates = (ThreadLocalState*)ptr;
        
        // Initialize each thread state
        for (int i = 0; i < MAX_THREADS; i++)
        {
            _threadStates[i].ThreadId = i;
            _threadStates[i].PacketsProcessed = 0;
            _threadStates[i].BytesProcessed = 0;
        }
    }
    
    // Get thread-local state (no contention)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref ThreadLocalState GetThreadState(int threadId)
    {
        return ref _threadStates[threadId];
    }
    
    // Process packet on specific thread
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ProcessPacket(int threadId, int packetSize)
    {
        ref ThreadLocalState state = ref _threadStates[threadId];
        
        // These writes only affect this thread's cache lines
        state.PacketsProcessed++;
        state.BytesProcessed += packetSize;
        state.CurrentTimestamp = GetTimestamp();
        
        // No cache invalidation on other threads!
    }
    
    // Read-only aggregation (can be done periodically)
    public long GetTotalPackets()
    {
        long total = 0;
        for (int i = 0; i < MAX_THREADS; i++)
        {
            // Reading is safe - no write contention
            total += _threadStates[i].PacketsProcessed;
        }
        return total;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetTimestamp()
    {
        return DateTime.UtcNow.Ticks;
    }
}

// Additional pattern: Striped array for lock-free operations
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct StripedSlot<T> where T : unmanaged
{
    [FieldOffset(0)]
    public T Value;
    
    [FieldOffset(8)]  // Assumes T is <= 8 bytes
    private fixed byte _padding[56];
}

// Use striped slots to distribute atomic operations
public unsafe struct StripedCounter
{
    private StripedSlot<long>* _slots;
    private int _stripeCount;
    
    public void Initialize(int stripeCount)
    {
        _stripeCount = stripeCount;
        var size = (nuint)(stripeCount * sizeof(StripedSlot<long>));
        var ptr = NativeMemory.AllocAligned(size, 64);
        _slots = (StripedSlot<long>*)ptr;
    }
    
    // Hash thread to a stripe (reduces contention)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(int threadId)
    {
        int stripe = threadId % _stripeCount;
        Interlocked.Increment(ref _slots[stripe].Value);
    }
    
    public long GetTotal()
    {
        long sum = 0;
        for (int i = 0; i < _stripeCount; i++)
        {
            sum += Volatile.Read(ref _slots[i].Value);
        }
        return sum;
    }
}`}
            </code>
          </pre>
        </div>
      </div>

      {/* Memory Layout Visualization */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">🗺️</span>
          Memory Layout for 12 Threads
        </h3>
        <div className="bg-slate-900 rounded-lg p-6 border border-slate-700">
          <div className="space-y-3">
            {Array.from({length: 12}, (_, i) => (
              <div key={i} className="space-y-1">
                <div className="text-green-400 text-xs font-mono">Thread {i}</div>
                <div className="grid grid-cols-2 gap-2">
                  <div className="bg-green-900/30 border border-green-700/50 rounded p-2">
                    <div className="text-xs text-slate-400">Cache Line {i * 2}</div>
                    <div className="text-xs text-white font-mono">
                      Offset {(i * 128).toString().padStart(4, '0')}: Hot data (64 bytes)
                    </div>
                  </div>
                  <div className="bg-green-900/20 border border-green-700/30 rounded p-2">
                    <div className="text-xs text-slate-400">Cache Line {i * 2 + 1}</div>
                    <div className="text-xs text-slate-300 font-mono">
                      Offset {(i * 128 + 64).toString().padStart(4, '0')}: Config (64 bytes)
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
          <div className="mt-4 text-xs text-slate-400">
            Total allocation: 1,536 bytes (24 cache lines × 64 bytes)
          </div>
        </div>
      </div>

      {/* Performance Analysis */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">📊</span>
          Latency Analysis
        </h3>
        <div className="grid md:grid-cols-2 gap-4">
          <div className="bg-slate-900 rounded-lg p-4 border border-slate-700">
            <h4 className="text-white font-semibold mb-3">Cache Hit Scenario</h4>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-slate-400">L1 cache hit (aligned):</span>
                <span className="text-green-400 font-mono">~0.5ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">No coherency overhead:</span>
                <span className="text-green-400 font-mono">0ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">No false sharing stall:</span>
                <span className="text-green-400 font-mono">0ns</span>
              </div>
              <div className="flex justify-between border-t border-slate-700 pt-2 mt-2">
                <span className="text-white font-bold">Total (hot path):</span>
                <span className="text-green-300 font-mono font-bold">~0.5ns</span>
              </div>
            </div>
          </div>
          
          <div className="bg-slate-900 rounded-lg p-4 border border-slate-700">
            <h4 className="text-white font-semibold mb-3">Without Padding (Comparison)</h4>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-slate-400">L1 cache hit:</span>
                <span className="text-red-400 font-mono">~0.5ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Cache invalidation (MESI):</span>
                <span className="text-red-400 font-mono">~50ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Memory barrier stall:</span>
                <span className="text-red-400 font-mono">~20ns</span>
              </div>
              <div className="flex justify-between border-t border-slate-700 pt-2 mt-2">
                <span className="text-white font-bold">Total (worst case):</span>
                <span className="text-red-300 font-mono font-bold">~70ns</span>
              </div>
            </div>
          </div>
        </div>
        
        <div className="mt-4 bg-green-900/20 border border-green-700/50 rounded-lg p-4">
          <div className="flex items-start gap-3">
            <span className="text-green-400 text-xl">💡</span>
            <div>
              <h4 className="text-green-300 font-semibold mb-1">Performance Impact</h4>
              <p className="text-slate-300 text-sm">
                Proper cache alignment reduces latency from ~70ns to ~0.5ns - a <strong className="text-green-400">140× improvement</strong>.
                The 0.5ns estimate represents the raw L1 cache access time when there's no contention or coherency overhead.
                Memory cost is minimal: 1.5KB for 12 threads vs potential 70ns stalls on every operation.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Best Practices */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">📋</span>
          Cache Alignment Best Practices
        </h3>
        <div className="grid md:grid-cols-2 gap-4">
          <div className="space-y-3">
            <h4 className="text-white font-semibold">Do:</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Align to 64 bytes (x86-64 cache line)</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Pad structs to fill complete cache lines</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Group hot fields together</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Separate read-only from writable data</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Use per-thread ownership patterns</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Profile with perf/VTune for verification</span>
              </div>
            </div>
          </div>
          
          <div className="space-y-3">
            <h4 className="text-white font-semibold">Don't:</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-start gap-2">
                <span className="text-red-400">✗</span>
                <span className="text-slate-300">Pack multiple thread counters together</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-red-400">✗</span>
                <span className="text-slate-300">Use shared writeable state without padding</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-red-400">✗</span>
                <span className="text-slate-300">Assume .NET handles alignment automatically</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-red-400">✗</span>
                <span className="text-slate-300">Mix hot and cold data in same cache line</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-red-400">✗</span>
                <span className="text-slate-300">Ignore hardware cache line size variations</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-red-400">✗</span>
                <span className="text-slate-300">Over-optimize cold paths</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Hardware Considerations */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-green-400">🔧</span>
          Hardware Considerations
        </h3>
        <div className="bg-slate-900 rounded-lg p-4 border border-slate-700">
          <div className="grid md:grid-cols-3 gap-4 text-sm">
            <div>
              <div className="text-white font-semibold mb-2">x86-64 (Intel/AMD)</div>
              <div className="text-slate-400 space-y-1">
                <div>L1: 64 bytes</div>
                <div>L2: 64 bytes</div>
                <div>L3: 64 bytes</div>
              </div>
            </div>
            <div>
              <div className="text-white font-semibold mb-2">ARM (Apple Silicon)</div>
              <div className="text-slate-400 space-y-1">
                <div>L1: 64-128 bytes</div>
                <div>L2: 128 bytes</div>
                <div>Use 128-byte alignment</div>
              </div>
            </div>
            <div>
              <div className="text-white font-semibold mb-2">Power/RISC</div>
              <div className="text-slate-400 space-y-1">
                <div>L1: 128 bytes</div>
                <div>L2: 128 bytes</div>
                <div>Use 128-byte alignment</div>
              </div>
            </div>
          </div>
          <div className="mt-4 pt-4 border-t border-slate-700">
            <p className="text-slate-300 text-sm">
              <strong className="text-green-400">Recommendation:</strong> For maximum portability, use 128-byte 
              alignment on systems where performance is critical. The memory overhead is negligible compared 
              to the performance gains from eliminating false sharing.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
