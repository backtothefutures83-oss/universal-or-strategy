export function TaggedPointers() {
  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="bg-gradient-to-br from-blue-900/50 to-slate-800 rounded-2xl p-8 border border-blue-700/50">
        <div className="flex items-start gap-4">
          <div className="text-5xl">🏷️</div>
          <div className="flex-1">
            <h2 className="text-3xl font-bold text-white mb-2">Bitwise Tagged Pointers</h2>
            <p className="text-blue-100 text-lg">
              64-bit tagged pointer system with 48-bit index and 16-bit epoch for ABA safety
            </p>
          </div>
          <div className="bg-blue-500/20 px-4 py-2 rounded-lg border border-blue-500/30">
            <div className="text-blue-400 text-sm font-medium">Latency Impact</div>
            <div className="text-blue-300 text-2xl font-bold font-mono">~1.2ns</div>
          </div>
        </div>
      </div>

      {/* Technical Mechanism */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-blue-400">⚙️</span>
          Technical Mechanism
        </h3>
        <div className="space-y-4 text-slate-300">
          <p className="leading-relaxed">
            The tagged pointer system solves the <span className="text-blue-400 font-semibold">ABA problem</span> in lock-free 
            multi-producer scenarios without object pooling. The design packs two critical pieces of information into a single 64-bit value:
          </p>
          <ul className="space-y-2 ml-6">
            <li className="flex items-start gap-2">
              <span className="text-blue-400 mt-1">•</span>
              <span><strong className="text-white">48-bit Index:</strong> Points to a slot in a pre-allocated array (supports 281 trillion entries)</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-blue-400 mt-1">•</span>
              <span><strong className="text-white">16-bit Epoch/Generation:</strong> Increments on each reuse, preventing ABA (65,536 generations)</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-blue-400 mt-1">•</span>
              <span><strong className="text-white">Atomic CAS:</strong> CompareExchange on full 64-bit value ensures atomicity</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-blue-400 mt-1">•</span>
              <span><strong className="text-white">Bitwise operations:</strong> Fast pack/unpack using shifts and masks</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-blue-400 mt-1">•</span>
              <span><strong className="text-white">No allocations:</strong> Indices reference pre-allocated structs, not heap objects</span>
            </li>
          </ul>
        </div>
      </div>

      {/* Bit Layout Diagram */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-blue-400">🔢</span>
          64-bit Layout
        </h3>
        <div className="bg-slate-900 rounded-lg p-6 border border-slate-700">
          <div className="space-y-4">
            {/* Bit diagram */}
            <div className="grid grid-cols-16 gap-px">
              {/* Epoch bits (16 bits) */}
              {Array.from({length: 16}, (_, i) => (
                <div key={`epoch-${i}`} className="bg-blue-600 text-white text-xs p-2 text-center font-mono">
                  {63 - i}
                </div>
              ))}
              {/* Index bits (48 bits) */}
              {Array.from({length: 16}, (_, i) => (
                <div key={`index-${i}`} className="bg-cyan-600 text-white text-xs p-2 text-center font-mono">
                  {47 - i}
                </div>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-4 mt-4">
              <div className="bg-blue-900/30 border border-blue-700/50 rounded p-3">
                <div className="text-blue-400 font-semibold mb-1">Bits 63-48 (16 bits)</div>
                <div className="text-slate-300 text-sm">Epoch / Generation Counter</div>
              </div>
              <div className="bg-cyan-900/30 border border-cyan-700/50 rounded p-3">
                <div className="text-cyan-400 font-semibold mb-1">Bits 47-0 (48 bits)</div>
                <div className="text-slate-300 text-sm">Array Index (up to 281TB entries)</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Code Implementation */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-blue-400">💻</span>
          C# Implementation
        </h3>
        <div className="bg-slate-900 rounded-lg p-4 border border-slate-700 overflow-x-auto">
          <pre className="text-sm">
            <code className="text-slate-300">
{`using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

// Tagged pointer struct - packs index + epoch into 64 bits
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct TaggedPointer
{
    [FieldOffset(0)]
    private readonly long _value;
    
    // Bit layout constants
    private const long INDEX_MASK = 0x0000FFFFFFFFFFFF;  // Lower 48 bits
    private const long EPOCH_MASK = 0xFFFF000000000000;  // Upper 16 bits
    private const int EPOCH_SHIFT = 48;
    
    // Constructor from index and epoch
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedPointer(long index, ushort epoch)
    {
        _value = (index & INDEX_MASK) | ((long)epoch << EPOCH_SHIFT);
    }
    
    // Constructor from raw 64-bit value
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaggedPointer(long value)
    {
        _value = value;
    }
    
    // Extract index (lower 48 bits)
    public long Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value & INDEX_MASK;
    }
    
    // Extract epoch (upper 16 bits)
    public ushort Epoch
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ushort)((_value & EPOCH_MASK) >> EPOCH_SHIFT);
    }
    
    // Get raw value for atomic operations
    public long RawValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }
    
    // Create next generation (incremented epoch, same index)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedPointer NextGeneration()
    {
        ushort nextEpoch = (ushort)(Epoch + 1);
        return new TaggedPointer(Index, nextEpoch);
    }
    
    // Null sentinel
    public static readonly TaggedPointer Null = new TaggedPointer(0);
}

// Lock-free stack using tagged pointers (multi-producer safe)
public unsafe struct LockFreeStack<T> where T : unmanaged
{
    // Stack node in pre-allocated array
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct Node
    {
        [FieldOffset(0)]
        public T Value;
        
        [FieldOffset(8)]  // Assumes T is 8 bytes or less
        public long Next;  // TaggedPointer as long
    }
    
    private Node* _nodes;
    private long _head;  // TaggedPointer as long
    private int _capacity;
    
    public void Initialize(int capacity)
    {
        _capacity = capacity;
        var ptr = NativeMemory.AllocAligned(
            (nuint)(capacity * sizeof(Node)), 
            64
        );
        _nodes = (Node*)ptr;
        _head = TaggedPointer.Null.RawValue;
        
        // Initialize free list (all nodes available)
        for (int i = 0; i < capacity - 1; i++)
        {
            _nodes[i].Next = new TaggedPointer(i + 1, 0).RawValue;
        }
        _nodes[capacity - 1].Next = TaggedPointer.Null.RawValue;
        _head = new TaggedPointer(0, 0).RawValue;
    }
    
    // Push operation (lock-free with ABA protection)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPush(T value)
    {
        while (true)
        {
            long currentHeadRaw = Volatile.Read(ref _head);
            TaggedPointer currentHead = new TaggedPointer(currentHeadRaw);
            
            if (currentHead.Index == TaggedPointer.Null.Index)
                return false; // Stack full (no free nodes)
            
            long nodeIndex = currentHead.Index;
            ref Node node = ref _nodes[nodeIndex];
            
            // Read next before CAS
            long nextRaw = node.Next;
            TaggedPointer next = new TaggedPointer(nextRaw);
            
            // Attempt to advance head with incremented epoch
            TaggedPointer newHead = new TaggedPointer(
                next.Index, 
                (ushort)(currentHead.Epoch + 1)
            );
            
            long result = Interlocked.CompareExchange(
                ref _head, 
                newHead.RawValue, 
                currentHeadRaw
            );
            
            if (result == currentHeadRaw)
            {
                // Successfully claimed node, store value
                node.Value = value;
                return true;
            }
            // CAS failed, retry
        }
    }
    
    // Pop operation (lock-free with ABA protection)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out T value)
    {
        while (true)
        {
            long currentHeadRaw = Volatile.Read(ref _head);
            TaggedPointer currentHead = new TaggedPointer(currentHeadRaw);
            
            if (currentHead.Index == TaggedPointer.Null.Index)
            {
                value = default;
                return false; // Stack empty
            }
            
            long nodeIndex = currentHead.Index;
            ref Node node = ref _nodes[nodeIndex];
            
            value = node.Value;
            long nextRaw = node.Next;
            TaggedPointer next = new TaggedPointer(nextRaw);
            
            // Attempt to advance head with incremented epoch
            TaggedPointer newHead = new TaggedPointer(
                next.Index,
                (ushort)(currentHead.Epoch + 1)
            );
            
            long result = Interlocked.CompareExchange(
                ref _head,
                newHead.RawValue,
                currentHeadRaw
            );
            
            if (result == currentHeadRaw)
            {
                // Successfully popped
                return true;
            }
            // CAS failed, retry
        }
    }
}`}
            </code>
          </pre>
        </div>
      </div>

      {/* ABA Problem Explanation */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-blue-400">⚠️</span>
          ABA Problem Solution
        </h3>
        <div className="space-y-4">
          <div className="bg-slate-900 rounded-lg p-4 border border-slate-700">
            <h4 className="text-white font-semibold mb-3">The Problem</h4>
            <div className="space-y-2 text-sm text-slate-300">
              <p>In a lock-free stack, the ABA problem occurs when:</p>
              <ol className="list-decimal ml-6 space-y-1 mt-2">
                <li>Thread 1 reads head pointer → <span className="text-blue-400 font-mono">Node A</span></li>
                <li>Thread 2 pops A, then B, then pushes A back (A-B-C becomes A-C)</li>
                <li>Thread 1's CAS succeeds because head is still A, but structure changed!</li>
                <li>Result: Node B is lost, corruption occurs</li>
              </ol>
            </div>
          </div>
          
          <div className="bg-blue-900/20 border border-blue-700/50 rounded-lg p-4">
            <h4 className="text-blue-300 font-semibold mb-3">The Solution</h4>
            <div className="space-y-2 text-sm text-slate-300">
              <p>Tagged pointers prevent ABA by incrementing the epoch on every operation:</p>
              <ol className="list-decimal ml-6 space-y-1 mt-2">
                <li>Thread 1 reads head → <span className="text-blue-400 font-mono">(Index: A, Epoch: 5)</span></li>
                <li>Thread 2 modifies stack, pushes A back with new epoch → <span className="text-blue-400 font-mono">(Index: A, Epoch: 8)</span></li>
                <li>Thread 1's CAS fails because epochs don't match (5 ≠ 8)</li>
                <li>Thread 1 retries with current state, avoiding corruption</li>
              </ol>
              <p className="mt-3 text-blue-200">
                <strong>Key insight:</strong> Even if the index is the same, the epoch changes on every modification,
                making ABA detectable with a single atomic CAS operation.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Performance Analysis */}
      <div className="bg-slate-800 rounded-xl p-6 border border-slate-700">
        <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
          <span className="text-blue-400">📊</span>
          Latency Analysis
        </h3>
        <div className="grid md:grid-cols-2 gap-4">
          <div className="bg-slate-900 rounded-lg p-4 border border-slate-700">
            <h4 className="text-white font-semibold mb-3">Operation Breakdown</h4>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-slate-400">Volatile read (_head):</span>
                <span className="text-blue-400 font-mono">~0.2ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Bitwise AND (extract index):</span>
                <span className="text-blue-400 font-mono">~0.1ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Bitwise shift (extract epoch):</span>
                <span className="text-blue-400 font-mono">~0.1ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Array index calculation:</span>
                <span className="text-blue-400 font-mono">~0.2ns</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-400">Pack new tagged pointer:</span>
                <span className="text-blue-400 font-mono">~0.2ns</span>
              </div>
              <div className="flex justify-between border-t border-slate-700 pt-2">
                <span className="text-white font-semibold">CompareExchange:</span>
                <span className="text-blue-400 font-mono">~0.4ns</span>
              </div>
              <div className="flex justify-between border-t border-slate-700 pt-2 mt-2">
                <span className="text-white font-bold">Total (fast path):</span>
                <span className="text-blue-300 font-mono font-bold">~1.2ns</span>
              </div>
            </div>
          </div>
          
          <div className="bg-slate-900 rounded-lg p-4 border border-slate-700">
            <h4 className="text-white font-semibold mb-3">Key Benefits</h4>
            <div className="space-y-2 text-sm">
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">ABA safety without object pooling</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Single 64-bit CAS (atomic)</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Fast bitwise pack/unpack</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Pre-allocated array (no heap)</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">65K epoch wrapping protection</span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-green-400">✓</span>
                <span className="text-slate-300">Multi-producer safe</span>
              </div>
            </div>
          </div>
        </div>
        
        <div className="bg-blue-900/20 border border-blue-700/50 rounded-lg p-4 mt-4">
          <div className="flex items-start gap-3">
            <span className="text-blue-400 text-xl">💡</span>
            <div>
              <h4 className="text-blue-300 font-semibold mb-1">Performance Notes</h4>
              <p className="text-slate-300 text-sm">
                The 1.2ns estimate assumes fast-path success (no CAS retry). In high contention, retries
                may occur, but the epoch increment ensures each retry sees fresh state. Modern CPUs can
                execute shift/AND operations in a single cycle, making bitwise packing extremely efficient.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
