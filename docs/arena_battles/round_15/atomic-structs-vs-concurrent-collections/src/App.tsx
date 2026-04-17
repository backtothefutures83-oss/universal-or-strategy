import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Cpu, Zap, Box, Activity, AlertTriangle, Layers, 
  Lock, Gauge, ShieldAlert, ArrowRight, BarChart3, Database
} from 'lucide-react';
import { 
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, 
  ResponsiveContainer, BarChart, Bar, Legend
} from 'recharts';

// Data for charts
const throughputData = [
  { name: '1 Thread', collections: 1.2, structs: 4.5 },
  { name: '2 Threads', collections: 2.1, structs: 8.2 },
  { name: '4 Threads', collections: 3.5, structs: 15.8 },
  { name: '8 Threads', collections: 4.8, structs: 28.5 },
  { name: '16 Threads', collections: 5.2, structs: 45.1 },
  { name: '32 Threads', collections: 4.9, structs: 62.3 },
];

const latencyData = [
  { metric: 'Avg Latency (ns)', collections: 150, structs: 12 },
  { metric: 'P99 Latency (ns)', collections: 850, structs: 18 },
  { metric: 'GC Pause Impact', collections: 12000, structs: 1 },
];

// Reusable Components
const Card = ({ title, icon: Icon, children, className = '' }: { title: string, icon: any, children: React.ReactNode, className?: string }) => (
  <motion.div 
    initial={{ opacity: 0, y: 20 }}
    whileInView={{ opacity: 1, y: 0 }}
    viewport={{ once: true }}
    transition={{ duration: 0.5 }}
    className={`bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl ${className}`}
  >
    <div className="flex items-center gap-3 mb-4">
      <div className="p-2 bg-blue-500/10 rounded-lg text-blue-400">
        <Icon size={24} />
      </div>
      <h3 className="text-xl font-bold text-white">{title}</h3>
    </div>
    <div className="text-slate-300 leading-relaxed">
      {children}
    </div>
  </motion.div>
);

const ComparisonRow = ({ title, structText, collectionText, structWins }: { title: string, structText: string, collectionText: string, structWins: boolean }) => (
  <div className="grid grid-cols-1 md:grid-cols-3 gap-4 p-4 border-b border-slate-800 last:border-0 hover:bg-slate-800/50 transition-colors">
    <div className="font-semibold text-white flex items-center">{title}</div>
    <div className={`p-3 rounded-lg flex items-start gap-2 ${structWins ? 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-200' : 'bg-slate-800 text-slate-300'}`}>
      {structWins && <Zap size={18} className="text-emerald-400 shrink-0 mt-0.5" />}
      <span>{structText}</span>
    </div>
    <div className={`p-3 rounded-lg flex items-start gap-2 ${!structWins ? 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-200' : 'bg-slate-800 text-slate-300'}`}>
      {!structWins && <Zap size={18} className="text-emerald-400 shrink-0 mt-0.5" />}
      <span>{collectionText}</span>
    </div>
  </div>
);

const CodeBlock = ({ code }: { code: string }) => (
  <div className="relative group">
    <div className="absolute -inset-0.5 bg-gradient-to-r from-blue-500 to-purple-600 rounded-xl opacity-20 group-hover:opacity-40 transition duration-500 blur"></div>
    <pre className="relative bg-slate-950 p-4 rounded-xl overflow-x-auto text-sm font-mono text-slate-300 border border-slate-800">
      <code>{code}</code>
    </pre>
  </div>
);

export default function App() {
  const [activeTab, setActiveTab] = useState('overview');

  return (
    <div className="min-h-screen bg-slate-950 text-slate-200 font-sans selection:bg-blue-500/30 selection:text-blue-200 pb-20">
      {/* Navigation */}
      <nav className="sticky top-0 z-50 bg-slate-950/80 backdrop-blur-md border-b border-slate-800">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            <div className="flex items-center gap-2">
              <Zap className="text-blue-500" />
              <span className="text-lg font-bold bg-clip-text text-transparent bg-gradient-to-r from-blue-400 to-purple-500">
                NetPerf.Dev
              </span>
            </div>
            <div className="hidden md:flex space-x-1">
              {['overview', 'deep-dive', 'benchmarks', 'code'].map((tab) => (
                <button
                  key={tab}
                  onClick={() => setActiveTab(tab)}
                  className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                    activeTab === tab 
                      ? 'bg-slate-800 text-white' 
                      : 'text-slate-400 hover:text-white hover:bg-slate-800/50'
                  }`}
                >
                  {tab.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')}
                </button>
              ))}
            </div>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <header className="relative overflow-hidden pt-20 pb-32">
        <div className="absolute inset-0 bg-[url('https://images.unsplash.com/photo-1550751827-4bd374c3f58b?auto=format&fit=crop&q=80')] bg-cover bg-center opacity-5"></div>
        <div className="absolute inset-0 bg-gradient-to-b from-transparent to-slate-950"></div>
        
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 relative z-10 text-center">
          <motion.div
            initial={{ opacity: 0, y: 30 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.7 }}
          >
            <span className="inline-block py-1 px-3 rounded-full bg-blue-500/10 border border-blue-500/20 text-blue-400 text-sm font-semibold tracking-wider mb-6">
              HIGH-PERFORMANCE C#
            </span>
            <h1 className="text-5xl md:text-7xl font-extrabold text-white tracking-tight mb-8 leading-tight">
              Atomic Structs <span className="text-slate-500 font-light">vs</span> <br />
              <span className="bg-clip-text text-transparent bg-gradient-to-r from-blue-400 via-indigo-400 to-purple-400">
                Concurrent Collections
              </span>
            </h1>
            <p className="mt-4 max-w-2xl text-xl text-slate-400 mx-auto">
              Why standard collections fail under the extreme pressure of packet processing engines, and how custom lock-free atomic structs unlock millions of packets per second.
            </p>
          </motion.div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 -mt-16 relative z-20">
        
        <AnimatePresence mode="wait">
          {activeTab === 'overview' && (
            <motion.div
              key="overview"
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: 20 }}
              transition={{ duration: 0.3 }}
              className="space-y-8"
            >
              {/* Executive Summary */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <Card title="The Standard: Concurrent Collections" icon={Layers}>
                  <p className="mb-4">
                    Generic collections like <code className="text-blue-300">ConcurrentQueue&lt;T&gt;</code> and <code className="text-blue-300">ConcurrentDictionary&lt;TKey, TValue&gt;</code> are robust, thread-safe, and perfect for 95% of enterprise applications.
                  </p>
                  <ul className="space-y-2 mt-4 text-sm">
                    <li className="flex gap-2"><Lock className="text-yellow-500 w-4 h-4" /> Fine-grained locking or spinlocks</li>
                    <li className="flex gap-2"><Database className="text-red-500 w-4 h-4" /> Heap allocations for nodes/segments</li>
                    <li className="flex gap-2"><Activity className="text-orange-500 w-4 h-4" /> Unpredictable GC pauses</li>
                  </ul>
                </Card>
                
                <Card title="The Challenger: Custom Atomic Structs" icon={Cpu}>
                  <p className="mb-4">
                    In extreme throughput scenarios (like DPDK-style packet processing), every nanosecond counts. Custom structs use hardware-level atomic instructions.
                  </p>
                  <ul className="space-y-2 mt-4 text-sm">
                    <li className="flex gap-2"><Zap className="text-emerald-500 w-4 h-4" /> Lock-free (<code className="text-emerald-300">Interlocked</code>) operations</li>
                    <li className="flex gap-2"><Box className="text-emerald-500 w-4 h-4" /> Zero-allocation (Stack or pre-allocated unmanaged memory)</li>
                    <li className="flex gap-2"><Gauge className="text-emerald-500 w-4 h-4" /> Predictable, ultra-low latency</li>
                  </ul>
                </Card>
              </div>

              {/* The Requirement */}
              <div className="bg-gradient-to-r from-blue-900/20 to-purple-900/20 border border-blue-500/20 rounded-2xl p-8">
                <div className="flex items-center gap-3 mb-4">
                  <ShieldAlert className="text-blue-400" size={28} />
                  <h2 className="text-2xl font-bold text-white">Why Packet Processing is Different</h2>
                </div>
                <p className="text-lg text-slate-300 mb-6">
                  A packet processing engine (e.g., routing 10Gbps+ traffic) needs to handle millions of packets per second. At 10Gbps with 64-byte packets, you have just <strong>~67 nanoseconds</strong> to process each packet. 
                </p>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div className="bg-slate-900/50 p-4 rounded-xl border border-slate-700/50">
                    <div className="text-3xl font-bold text-white mb-1">0</div>
                    <div className="text-sm text-slate-400 uppercase tracking-wider">Allocations Allowed</div>
                  </div>
                  <div className="bg-slate-900/50 p-4 rounded-xl border border-slate-700/50">
                    <div className="text-3xl font-bold text-white mb-1">&lt; 10ns</div>
                    <div className="text-sm text-slate-400 uppercase tracking-wider">Queue Latency Target</div>
                  </div>
                  <div className="bg-slate-900/50 p-4 rounded-xl border border-slate-700/50">
                    <div className="text-3xl font-bold text-white mb-1">100%</div>
                    <div className="text-sm text-slate-400 uppercase tracking-wider">Cache Hit Requirement</div>
                  </div>
                </div>
              </div>
            </motion.div>
          )}

          {activeTab === 'deep-dive' && (
            <motion.div
              key="deep-dive"
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: 20 }}
              transition={{ duration: 0.3 }}
              className="space-y-6"
            >
              <h2 className="text-3xl font-bold text-white mb-8">Architectural Differences</h2>
              
              <div className="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
                <div className="grid grid-cols-1 md:grid-cols-3 bg-slate-950 p-4 border-b border-slate-800">
                  <div className="font-bold text-slate-400 uppercase tracking-wider text-sm">Vector</div>
                  <div className="font-bold text-slate-400 uppercase tracking-wider text-sm">Custom Atomic Structs</div>
                  <div className="font-bold text-slate-400 uppercase tracking-wider text-sm">Generic Concurrent Collections</div>
                </div>
                
                <ComparisonRow 
                  title="Memory Allocation" 
                  structText="Zero-allocation. Structs are value types, often stored in pre-allocated unmanaged arrays or stack memory."
                  collectionText="Heap allocates internal nodes (e.g., linked list nodes in ConcurrentQueue) or array resizing, triggering GC."
                  structWins={true}
                />
                <ComparisonRow 
                  title="Garbage Collection" 
                  structText="Invisible to the GC if using unmanaged memory. No pauses, ensuring stable P99 latency."
                  collectionText="Frequent allocations create Gen0/Gen1 pressure. GC pauses will drop packets at 10Gbps speeds."
                  structWins={true}
                />
                <ComparisonRow 
                  title="Cache Locality" 
                  structText="Contiguous memory layout. Highly cache-friendly (L1/L2 hits). Padding avoids false sharing."
                  collectionText="Pointer chasing. Nodes scattered across the heap lead to frequent cache misses."
                  structWins={true}
                />
                <ComparisonRow 
                  title="Synchronization" 
                  structText="Lock-free using CAS (Compare-and-Swap). Extremely fast but requires complex, bug-prone manual implementation."
                  collectionText="Uses spinlocks or fine-grained locking. Highly optimized by Microsoft, easier to use, but adds overhead."
                  structWins={false}
                />
                <ComparisonRow 
                  title="Flexibility & Safety" 
                  structText="Rigid, highly specialized for one specific use case. Extremely hard to debug and unsafe."
                  collectionText="Generic, type-safe, handles dynamic sizing, extremely reliable and well-tested."
                  structWins={false}
                />
              </div>

              <div className="mt-8 grid grid-cols-1 md:grid-cols-2 gap-6">
                <Card title="The False Sharing Trap" icon={AlertTriangle}>
                  In multi-core packet processing, if Thread A and Thread B update variables that reside on the same 64-byte cache line, the CPU invalidates the cache line for both, destroying performance. Custom structs allow explicit <code className="text-blue-300">[StructLayout(LayoutKind.Explicit)]</code> padding to prevent this. Standard collections don't always guarantee this isolation at the item level.
                </Card>
                <Card title="Pointer Chasing Costs" icon={ArrowRight}>
                  When processing a packet, fetching memory from DRAM takes ~100ns. Fetching from L1 cache takes ~1ns. Concurrent collections rely on reference types (pointers), meaning the CPU constantly stalls waiting for DRAM. Struct-based ring buffers pre-allocate flat arrays, allowing the CPU hardware prefetcher to load packets into L1 cache before you even process them.
                </Card>
              </div>
            </motion.div>
          )}

          {activeTab === 'benchmarks' && (
            <motion.div
              key="benchmarks"
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: 20 }}
              transition={{ duration: 0.3 }}
              className="space-y-8"
            >
              <h2 className="text-3xl font-bold text-white mb-2">Performance Metrics</h2>
              <p className="text-slate-400 mb-8">Simulated benchmarks for a Multi-Producer Single-Consumer (MPSC) queue processing 64-byte payloads.</p>

              <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                {/* Throughput Chart */}
                <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl">
                  <h3 className="text-xl font-bold text-white mb-6 flex items-center gap-2">
                    <Activity className="text-blue-500" /> Throughput Scaling (Millions Ops/sec)
                  </h3>
                  <div className="h-80">
                    <ResponsiveContainer width="100%" height="100%">
                      <AreaChart data={throughputData} margin={{ top: 10, right: 30, left: 0, bottom: 0 }}>
                        <defs>
                          <linearGradient id="colorStructs" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#10b981" stopOpacity={0.8}/>
                            <stop offset="95%" stopColor="#10b981" stopOpacity={0}/>
                          </linearGradient>
                          <linearGradient id="colorColl" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.8}/>
                            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0}/>
                          </linearGradient>
                        </defs>
                        <XAxis dataKey="name" stroke="#475569" tick={{fill: '#94a3b8'}} />
                        <YAxis stroke="#475569" tick={{fill: '#94a3b8'}} />
                        <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
                        <Tooltip 
                          contentStyle={{ backgroundColor: '#0f172a', borderColor: '#1e293b', color: '#f1f5f9' }}
                          itemStyle={{ color: '#f1f5f9' }}
                        />
                        <Legend />
                        <Area type="monotone" dataKey="structs" name="Custom Atomic Structs" stroke="#10b981" fillOpacity={1} fill="url(#colorStructs)" />
                        <Area type="monotone" dataKey="collections" name="Concurrent Collections" stroke="#3b82f6" fillOpacity={1} fill="url(#colorColl)" />
                      </AreaChart>
                    </ResponsiveContainer>
                  </div>
                  <p className="mt-4 text-sm text-slate-400 text-center">
                    Notice how concurrent collections plateau due to lock contention and GC overhead, while atomic structs scale linearly with core count.
                  </p>
                </div>

                {/* Latency Chart */}
                <div className="bg-slate-900 border border-slate-800 p-6 rounded-xl">
                  <h3 className="text-xl font-bold text-white mb-6 flex items-center gap-2">
                    <BarChart3 className="text-purple-500" /> Latency & GC Impact
                  </h3>
                  <div className="h-80">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={latencyData} layout="vertical" margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" horizontal={false} />
                        <XAxis type="number" stroke="#475569" tick={{fill: '#94a3b8'}} scale="log" domain={['auto', 'auto']} />
                        <YAxis dataKey="metric" type="category" stroke="#475569" tick={{fill: '#94a3b8'}} width={120} />
                        <Tooltip 
                          contentStyle={{ backgroundColor: '#0f172a', borderColor: '#1e293b', color: '#f1f5f9' }}
                          cursor={{fill: '#1e293b'}}
                        />
                        <Legend />
                        <Bar dataKey="collections" name="Concurrent Collections" fill="#3b82f6" radius={[0, 4, 4, 0]} />
                        <Bar dataKey="structs" name="Custom Atomic Structs" fill="#10b981" radius={[0, 4, 4, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                  <p className="mt-4 text-sm text-slate-400 text-center">
                    (Logarithmic Scale). Custom structs maintain ultra-low latency. Concurrent collections suffer massive latency spikes during Garbage Collection.
                  </p>
                </div>
              </div>
            </motion.div>
          )}

          {activeTab === 'code' && (
            <motion.div
              key="code"
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: 20 }}
              transition={{ duration: 0.3 }}
              className="space-y-8"
            >
              <h2 className="text-3xl font-bold text-white mb-6">Code Comparison</h2>
              
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                <div>
                  <div className="flex items-center gap-2 mb-4">
                    <Database className="text-blue-500" />
                    <h3 className="text-xl font-bold text-white">The Easy Way (Allocates)</h3>
                  </div>
                  <CodeBlock code={`// Standard C# ConcurrentQueue
public class PacketProcessor
{
    private ConcurrentQueue<Packet> _queue = new();

    public void Enqueue(Packet p)
    {
        // Behind the scenes:
        // 1. May allocate new Segments (GC pressure)
        // 2. Uses Interlocked but with complex segment logic
        // 3. Packet is copied (if struct) or referenced (if class)
        _queue.Enqueue(p);
    }

    public void Process()
    {
        while (_queue.TryDequeue(out var p))
        {
            // Process packet
            // Cache locality is poor due to segment hopping
        }
    }
}`} />
                  <div className="mt-4 p-4 bg-blue-900/20 border border-blue-500/20 rounded-lg text-blue-200 text-sm">
                    <strong>Pros:</strong> Safe, infinite capacity, easy to use.<br/>
                    <strong>Cons:</strong> Unpredictable latency, GC allocations, poor cache locality.
                  </div>
                </div>

                <div>
                  <div className="flex items-center gap-2 mb-4">
                    <Cpu className="text-emerald-500" />
                    <h3 className="text-xl font-bold text-white">The Fast Way (Zero Allocation)</h3>
                  </div>
                  <CodeBlock code={`// Lock-free Ring Buffer using Atomic Structs
[StructLayout(LayoutKind.Sequential, Pack = 64)] // Prevent False Sharing
public unsafe struct RingBuffer
{
    private Packet* _buffer;
    private int _capacity;
    private int _capacityMask;
    
    // Aligned to 64-byte cache lines
    private long _head; // Written by producers
    private fixed byte _pad1[56]; 
    private long _tail; // Written by consumers
    
    public bool TryEnqueue(ref Packet p)
    {
        long currentHead;
        long nextHead;
        do {
            currentHead = Volatile.Read(ref _head);
            if (currentHead - Volatile.Read(ref _tail) >= _capacity)
                return false; // Full
                
            nextHead = currentHead + 1;
        // Compare-And-Swap (CAS) hardware instruction
        } while (Interlocked.CompareExchange(ref _head, nextHead, currentHead) != currentHead);
        
        _buffer[currentHead & _capacityMask] = p;
        return true;
    }
}`} />
                  <div className="mt-4 p-4 bg-emerald-900/20 border border-emerald-500/20 rounded-lg text-emerald-200 text-sm">
                    <strong>Pros:</strong> Zero allocations, L1 cache friendly, extremely high throughput.<br/>
                    <strong>Cons:</strong> Unsafe code, fixed size, very hard to write correctly (ABA problem, memory ordering).
                  </div>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </main>
      
      {/* Footer */}
      <footer className="mt-20 border-t border-slate-800 py-8 text-center text-slate-500">
        <p>Built for illustrating high-performance C# concepts.</p>
      </footer>
    </div>
  );
}
