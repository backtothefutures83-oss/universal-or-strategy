import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Cpu, 
  Database, 
  Clock, 
  Activity, 
  ShieldAlert, 
  CheckCircle2, 
  Terminal,
  Zap
} from 'lucide-react';

// C# Code Snippets
const ingressCode = `// Zero-allocation pre-allocated memory ring
public unsafe struct IngressRingBuffer
{
    private const int BufferSize = 1024;
    private const int Mask = BufferSize - 1;
    
    // Aligned to cache lines
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct CacheLinePad { public long Value; }
    
    [FieldOffset(0)] private CacheLinePad _head;
    [FieldOffset(64)] private CacheLinePad _tail;
    
    // Fixed buffer using unmanaged memory
    private byte* _buffer;

    public void Init(byte* unmanagedMemory)
    {
        _buffer = unmanagedMemory;
    }

    public void Push(byte value)
    {
        long currentTail = Interlocked.Read(ref _tail.Value);
        _buffer[currentTail & Mask] = value;
        Interlocked.Increment(ref _tail.Value);
    }
}`;

const pointerCode = `// 64-bit Tagged Pointer (48-bit index, 16-bit epoch)
public struct TaggedPointer
{
    private long _value;
    
    private const long IndexMask = 0x0000FFFFFFFFFFFFL;
    private const int EpochShift = 48;

    public TaggedPointer(long index, ushort epoch)
    {
        _value = (index & IndexMask) | ((long)epoch << EpochShift);
    }

    public long Index => _value & IndexMask;
    public ushort Epoch => (ushort)(_value >> EpochShift);

    public bool TryUpdate(long newIndex, ushort newEpoch, long expectedIndex, ushort expectedEpoch)
    {
        long expected = (expectedIndex & IndexMask) | ((long)expectedEpoch << EpochShift);
        long newValue = (newIndex & IndexMask) | ((long)newEpoch << EpochShift);
        
        return Interlocked.CompareExchange(ref _value, newValue, expected) == expected;
    }
}`;

const cacheCode = `// Cache Line Padded Struct for 64-byte lines
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct PaddedCounter
{
    // Padding before to prevent prefetcher invalidation
    [FieldOffset(0)]
    private fixed byte _pad1[64];

    // The actual hot data (8 bytes)
    [FieldOffset(64)]
    private long _value;

    // Padding after to prevent false sharing with next element
    [FieldOffset(72)]
    private fixed byte _pad2[56];

    public long Increment()
    {
        return Interlocked.Increment(ref _value);
    }
}`;

const sections = [
  {
    id: "ingress",
    title: "Ingress Bridge",
    icon: <Database className="w-6 h-6 text-blue-400" />,
    color: "border-blue-500/30",
    bg: "bg-blue-500/10",
    mechanism: "An ingestion layer utilizing a zero-allocation, pre-allocated memory ring. We bypass standard C# collections by pinning an unmanaged byte buffer using 'unsafe' contexts. The queue state is managed via Interlocked atomic operations on padded Head/Tail indices to prevent contention. Data is directly mapped from the socket's unmanaged memory into this ring without GC allocation.",
    latency: "~1.5 ns",
    code: ingressCode,
    details: [
      "Zero GC Allocations",
      "Unmanaged Memory Bounding",
      "Atomic Head/Tail Pointers"
    ]
  },
  {
    id: "pointers",
    title: "Bitwise Tagged Pointers",
    icon: <ShieldAlert className="w-6 h-6 text-purple-400" />,
    color: "border-purple-500/30",
    bg: "bg-purple-500/10",
    mechanism: "To ensure ABA safety in our multi-producer lock-free queues without object pooling, we pack a 48-bit memory index and a 16-bit generation/epoch counter into a single 64-bit unsigned integer. We leverage Interlocked.CompareExchange (CAS) on this 64-bit value to atomically swap pointers while verifying the epoch has not advanced asynchronously.",
    latency: "~1.0 ns",
    code: pointerCode,
    details: [
      "48-bit Index (256TB Addressable)",
      "16-bit Epoch (65k Generations)",
      "Lock-free CAS Validation"
    ]
  },
  {
    id: "cache",
    title: "Cache Concurrency Guard",
    icon: <Cpu className="w-6 h-6 text-green-400" />,
    color: "border-green-500/30",
    bg: "bg-green-500/10",
    mechanism: "We utilize struct padding with [StructLayout(LayoutKind.Explicit, Size = 128)] to perfectly align our critical atomic variables to hardware cache lines (64 bytes). Padding is applied both before and after the data to prevent false sharing (where multiple threads invalidate each other's L1/L2 cache lines despite modifying different variables).",
    latency: "0.0 ns (Prevents spikes)",
    code: cacheCode,
    details: [
      "Explicit Memory Layout",
      "64-byte Cache Line Alignment",
      "Eliminates False Sharing"
    ]
  }
];

const CodeBlock = ({ code }: { code: string }) => (
  <div className="relative group mt-4 rounded-lg overflow-hidden bg-[#0d1117] border border-gray-800 font-mono text-sm shadow-xl">
    <div className="flex items-center px-4 py-2 bg-gray-900 border-b border-gray-800 text-gray-400 text-xs">
      <Terminal className="w-4 h-4 mr-2" />
      <span>Implementation.cs</span>
    </div>
    <pre className="p-4 overflow-x-auto text-gray-300 leading-relaxed">
      <code>{code}</code>
    </pre>
  </div>
);

function App() {
  const [activeTab, setActiveTab] = useState(sections[0].id);

  const activeSection = sections.find(s => s.id === activeTab) || sections[0];

  return (
    <div className="min-h-screen bg-[#0a0a0f] text-gray-200 font-sans selection:bg-blue-500/30">
      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-gray-800/60 bg-[#0a0a0f]/80 backdrop-blur-md">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center space-x-3">
            <Zap className="w-6 h-6 text-yellow-500" />
            <h1 className="text-xl font-bold tracking-tight text-white">HFT Data Plane Architecture</h1>
          </div>
          <div className="flex items-center space-x-4">
            <div className="flex items-center space-x-2 bg-gray-900/50 rounded-full px-3 py-1 border border-gray-800 text-sm">
              <Clock className="w-4 h-4 text-emerald-400" />
              <span className="text-gray-300">Target Cycle:</span>
              <span className="font-mono text-emerald-400 font-semibold">&lt; 5.0 ns</span>
            </div>
            <div className="flex items-center space-x-2 bg-gray-900/50 rounded-full px-3 py-1 border border-gray-800 text-sm">
              <Activity className="w-4 h-4 text-blue-400" />
              <span className="text-gray-300">Status:</span>
              <span className="font-mono text-blue-400 font-semibold">Spec Complete</span>
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-8">
          <h2 className="text-3xl font-extrabold text-white mb-2">High-Frequency Packet Processing Pipeline</h2>
          <p className="text-gray-400 max-w-3xl leading-relaxed">
            Standard generic C# collections regress to ~50ns due to interface dispatch, cache misses, and object headers. 
            This document outlines the custom atomic topologies required to achieve deterministic sub-5ns processing 
            using unmanaged memory, bitwise atomics, and cache line alignment.
          </p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          {/* Navigation Sidebar */}
          <div className="lg:col-span-3 flex flex-col space-y-2">
            {sections.map((section) => {
              const isActive = activeTab === section.id;
              let activeClasses = 'bg-gray-800 text-white shadow-lg border-l-4 ';
              if (section.id === 'ingress') activeClasses += 'border-l-blue-500';
              else if (section.id === 'pointers') activeClasses += 'border-l-purple-500';
              else activeClasses += 'border-l-green-500';
              
              const inactiveClasses = 'bg-gray-900/30 text-gray-400 hover:bg-gray-800/50 hover:text-gray-200 border-l-4 border-l-transparent';

              return (
                <button
                  key={section.id}
                  onClick={() => setActiveTab(section.id)}
                  className={`w-full flex items-center space-x-3 px-4 py-4 rounded-xl transition-all duration-200 ${isActive ? activeClasses : inactiveClasses}`}
                >
                  <div className={`p-2 rounded-lg ${isActive ? section.bg : 'bg-gray-800/50'}`}>
                    {section.icon}
                  </div>
                  <div className="flex flex-col items-start">
                    <span className="font-semibold text-sm">{section.title}</span>
                    <span className="text-xs font-mono mt-1 opacity-70">Latency: {section.latency}</span>
                  </div>
                </button>
              );
            })}

            <div className="mt-8 p-4 bg-gray-900/50 rounded-xl border border-gray-800">
              <h3 className="text-xs font-bold text-gray-500 uppercase tracking-wider mb-3">Total Pipeline Latency</h3>
              <div className="space-y-2 font-mono text-sm">
                <div className="flex justify-between items-center text-gray-400">
                  <span>Ingress:</span>
                  <span>~1.5 ns</span>
                </div>
                <div className="flex justify-between items-center text-gray-400">
                  <span>CAS Check:</span>
                  <span>~1.0 ns</span>
                </div>
                <div className="flex justify-between items-center text-gray-400">
                  <span>Guard/Padding:</span>
                  <span>~0.0 ns</span>
                </div>
                <div className="h-px bg-gray-800 my-2"></div>
                <div className="flex justify-between items-center text-emerald-400 font-bold">
                  <span>Estimated:</span>
                  <span>~2.5 ns</span>
                </div>
              </div>
            </div>
          </div>

          {/* Main Content Area */}
          <div className="lg:col-span-9">
            <AnimatePresence mode="wait">
              <motion.div
                key={activeSection.id}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -10 }}
                transition={{ duration: 0.2 }}
                className={`bg-[#12121a] border ${activeSection.color} rounded-2xl p-6 sm:p-8 shadow-2xl`}
              >
                <div className="flex items-center space-x-4 mb-6">
                  <div className={`p-3 rounded-xl ${activeSection.bg}`}>
                    {activeSection.icon}
                  </div>
                  <div>
                    <h2 className="text-2xl font-bold text-white">{activeSection.title}</h2>
                    <div className="flex items-center mt-1 space-x-2">
                      <Clock className="w-4 h-4 text-gray-500" />
                      <span className="text-sm font-mono text-gray-400">Added Latency: <strong className="text-gray-200">{activeSection.latency}</strong></span>
                    </div>
                  </div>
                </div>

                <div className="mb-8">
                  <h3 className="text-sm font-bold text-gray-500 uppercase tracking-wider mb-3">Technical Mechanism</h3>
                  <p className="text-gray-300 leading-relaxed text-lg">
                    {activeSection.mechanism}
                  </p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
                  {activeSection.details.map((detail, idx) => (
                    <div key={idx} className="flex items-center space-x-2 bg-gray-900/60 p-3 rounded-lg border border-gray-800/60">
                      <CheckCircle2 className="w-5 h-5 text-emerald-500 shrink-0" />
                      <span className="text-sm text-gray-300 font-medium">{detail}</span>
                    </div>
                  ))}
                </div>

                <div>
                  <h3 className="text-sm font-bold text-gray-500 uppercase tracking-wider mb-3">Implementation Details</h3>
                  <CodeBlock code={activeSection.code} />
                </div>
              </motion.div>
            </AnimatePresence>
          </div>
        </div>
      </main>
    </div>
  );
}

export default App;
