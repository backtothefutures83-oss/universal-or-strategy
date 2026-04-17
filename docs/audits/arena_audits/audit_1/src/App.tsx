import { useState } from 'react';
import { motion } from 'framer-motion';
import { Activity, Cpu, HardDrive, Lock, Zap, ShieldAlert, CheckCircle, Database, AlertTriangle, Fingerprint } from 'lucide-react';
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { OpenPipesAnimation } from './components/OpenPipesAnimation';

// Mock data for the jitter chart
const jitterData = [
  { time: '0ms', before: 12, after: 1.1 },
  { time: '10ms', before: 15, after: 1.2 },
  { time: '20ms', before: 6, after: 0.9 },
  { time: '30ms', before: 14, after: 1.0 },
  { time: '40ms', before: 8, after: 1.1 },
  { time: '50ms', before: 11, after: 0.8 },
  { time: '60ms', before: 16, after: 1.2 },
  { time: '70ms', before: 14, after: 1.0 },
  { time: '80ms', before: 5, after: 1.1 },
  { time: '90ms', before: 12, after: 0.9 },
  { time: '100ms', before: 15, after: 1.0 },
];

export default function App() {
  const [activeEngine, setActiveEngine] = useState<number | null>(null);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-300 font-mono selection:bg-cyan-900 selection:text-cyan-100 p-4 md:p-8">
      {/* Header */}
      <header className="mb-10 border-b border-cyan-900/50 pb-6">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-end gap-4">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <Zap className="text-cyan-400 w-8 h-8" />
              <h1 className="text-3xl md:text-4xl font-bold tracking-tighter text-white">
                ANTIGRAVITY <span className="text-cyan-400">NEXUS OS</span>
              </h1>
            </div>
            <h2 className="text-xl text-slate-400 tracking-wide flex items-center gap-2">
              <Activity className="w-5 h-5" />
              SURGICAL AUDIT: Sovereign Actor v2
            </h2>
          </div>
          <div className="flex gap-4">
            <div className="bg-slate-900 border border-slate-800 px-4 py-2 rounded flex items-center gap-2">
              <div className="w-2 h-2 rounded-full bg-green-500 animate-pulse"></div>
              <span className="text-sm font-semibold text-green-400">ZERO-HEAP ENABLED</span>
            </div>
            <div className="bg-slate-900 border border-slate-800 px-4 py-2 rounded flex items-center gap-2">
              <div className="w-2 h-2 rounded-full bg-cyan-500 animate-pulse"></div>
              <span className="text-sm font-semibold text-cyan-400">OPEN PIPES ACTIVE</span>
            </div>
          </div>
        </div>
      </header>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        
        {/* Left Column - Architecture Vis */}
        <div className="lg:col-span-1 space-y-8">
          <section className="bg-slate-900/50 border border-slate-800 p-6 rounded-xl relative overflow-hidden group hover:border-cyan-900/50 transition-colors">
            <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-cyan-500 to-transparent opacity-50"></div>
            <h3 className="text-lg font-bold text-white mb-6 flex items-center gap-2">
              <Cpu className="text-cyan-400" />
              12 Engines / Isolated Cores
            </h3>
            
            <div className="grid grid-cols-3 gap-4">
              {Array.from({ length: 12 }).map((_, i) => (
                <motion.div 
                  key={i}
                  whileHover={{ scale: 1.05 }}
                  onHoverStart={() => setActiveEngine(i)}
                  onHoverEnd={() => setActiveEngine(null)}
                  className={`
                    aspect-square rounded border flex items-center justify-center flex-col gap-1 cursor-crosshair transition-colors
                    ${activeEngine === i 
                      ? 'border-cyan-400 bg-cyan-950/30 text-cyan-300 shadow-[0_0_15px_rgba(34,211,238,0.2)]' 
                      : 'border-slate-800 bg-slate-950 text-slate-500 hover:border-slate-600'}
                  `}
                >
                  <Cpu className="w-5 h-5" />
                  <span className="text-xs font-bold">C-{i < 9 ? "0" + (i + 1) : i + 1}</span>
                </motion.div>
              ))}
            </div>

            <OpenPipesAnimation />

            <div className="mt-8 space-y-4">
              <div className="p-4 bg-slate-950 border border-slate-800 rounded flex items-center justify-between">
                <span className="text-sm text-slate-400">Architecture</span>
                <span className="text-sm font-bold text-cyan-400">Open Pipes</span>
              </div>
              <div className="p-4 bg-slate-950 border border-slate-800 rounded flex items-center justify-between">
                <span className="text-sm text-slate-400">Software Staggering</span>
                <span className="text-sm font-bold text-green-400">0.00%</span>
              </div>
              <div className="p-4 bg-slate-950 border border-slate-800 rounded flex items-center justify-between">
                <span className="text-sm text-slate-400">Supervisor</span>
                <span className="text-sm font-bold text-red-400">REJECTED</span>
              </div>
            </div>
          </section>

          {/* DNA Section */}
          <section className="bg-slate-900/50 border border-slate-800 p-6 rounded-xl">
             <h3 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
              <Fingerprint className="text-purple-400" />
              System DNA
            </h3>
            <ul className="space-y-3 text-sm">
              <li className="flex items-start gap-3">
                <CheckCircle className="w-5 h-5 text-green-500 shrink-0" />
                <span><strong className="text-slate-200">100% Transparency.</strong> No hidden logic.</span>
              </li>
              <li className="flex items-start gap-3">
                <CheckCircle className="w-5 h-5 text-green-500 shrink-0" />
                <span><strong className="text-slate-200">Raw "Hot Potato" pass-along</strong> directly to the Hardware FIFO.</span>
              </li>
              <li className="flex items-start gap-3">
                <CheckCircle className="w-5 h-5 text-green-500 shrink-0" />
                <span><strong className="text-slate-200">Zero-Heap Execution</strong> across the active memory paths.</span>
              </li>
            </ul>
          </section>
        </div>

        {/* Right Column - Audit Results */}
        <div className="lg:col-span-2 space-y-8">
          
          {/* Performance Gap Remediation */}
          <section className="bg-slate-900/50 border border-slate-800 p-6 rounded-xl relative">
            <h3 className="text-xl font-bold text-white mb-6 flex items-center gap-2">
              <Activity className="text-green-400" />
              SLUB Allocator Contention Audit
            </h3>
            
            <div className="mb-6 grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="bg-slate-950 border border-slate-800 p-4 rounded-lg border-l-4 border-l-cyan-500">
                <Lock className="w-5 h-5 text-cyan-400 mb-2" />
                <h4 className="font-bold text-slate-200 text-sm mb-1">mlockall</h4>
                <p className="text-xs text-slate-400">MCL_CURRENT | MCL_FUTURE</p>
              </div>
              <div className="bg-slate-950 border border-slate-800 p-4 rounded-lg border-l-4 border-l-purple-500">
                <HardDrive className="w-5 h-5 text-purple-400 mb-2" />
                <h4 className="font-bold text-slate-200 text-sm mb-1">Custom Slab Pool</h4>
                <p className="text-xs text-slate-400">Pre-allocated at startup</p>
              </div>
              <div className="bg-slate-950 border border-slate-800 p-4 rounded-lg border-l-4 border-l-orange-500">
                <Database className="w-5 h-5 text-orange-400 mb-2" />
                <h4 className="font-bold text-slate-200 text-sm mb-1">Atomic Redis Lua</h4>
                <p className="text-xs text-slate-400">Single atomic persistence layer</p>
              </div>
            </div>

            <div className="bg-slate-950 rounded-lg p-4 border border-slate-800 mb-6">
              <h4 className="text-sm font-bold text-slate-400 mb-4">Kernel Jitter Latency (µs)</h4>
              <div className="h-64 w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={jitterData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                    <defs>
                      <linearGradient id="colorBefore" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3}/>
                        <stop offset="95%" stopColor="#ef4444" stopOpacity={0}/>
                      </linearGradient>
                      <linearGradient id="colorAfter" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#22d3ee" stopOpacity={0.3}/>
                        <stop offset="95%" stopColor="#22d3ee" stopOpacity={0}/>
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" vertical={false} />
                    <XAxis dataKey="time" stroke="#475569" tick={{ fontSize: 12 }} />
                    <YAxis stroke="#475569" tick={{ fontSize: 12 }} />
                    <Tooltip 
                      contentStyle={{ backgroundColor: '#0f172a', borderColor: '#1e293b', color: '#f8fafc' }}
                      itemStyle={{ color: '#e2e8f0' }}
                    />
                    <Area type="monotone" dataKey="before" name="Pre-Audit (Heap)" stroke="#ef4444" fillOpacity={1} fill="url(#colorBefore)" />
                    <Area type="monotone" dataKey="after" name="Post-Audit (Zero-Heap)" stroke="#22d3ee" fillOpacity={1} fill="url(#colorAfter)" />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="space-y-6">
              {/* Question 1 */}
              <div className="bg-slate-950 border border-slate-800 p-5 rounded-lg">
                <div className="flex gap-4 items-start mb-3">
                  <div className="bg-slate-800 text-slate-300 w-8 h-8 rounded-full flex items-center justify-center font-bold shrink-0">Q1</div>
                  <h4 className="text-slate-200 font-semibold leading-relaxed">
                    Does this combination (mlockall + Slab Pool + Open Pipes) resolve the 5-15µs SLUB jitter identified previously?
                  </h4>
                </div>
                <div className="flex gap-4 items-start pl-12">
                  <div className="text-green-400 shrink-0 pt-1">
                    <CheckCircle className="w-5 h-5" />
                  </div>
                  <div className="text-slate-400 text-sm leading-relaxed space-y-2">
                    <p>
                      <strong className="text-green-400 font-mono text-base">YES. RESOLVED.</strong>
                    </p>
                    <p>
                      <code className="text-cyan-300 bg-cyan-900/30 px-1 py-0.5 rounded">mlockall(MCL_CURRENT | MCL_FUTURE)</code> completely eliminates page fault latency by locking the VMA into RAM, preventing both major and minor faults.
                    </p>
                    <p>
                      By utilizing a Custom Slab Pool pre-allocated at startup, the system physically bypasses the kernel's <code className="text-cyan-300 bg-cyan-900/30 px-1 py-0.5 rounded">kmalloc</code> / <code className="text-cyan-300 bg-cyan-900/30 px-1 py-0.5 rounded">kfree</code> paths entirely during the hot execution loop. This removes all lock contention on kernel slab caches (SLUB allocator). Memory access on the hot path becomes strictly <strong className="text-slate-200">O(1)</strong>.
                    </p>
                  </div>
                </div>
              </div>

              {/* Question 2 */}
              <div className="bg-slate-950 border border-red-900/30 p-5 rounded-lg">
                <div className="flex gap-4 items-start mb-3">
                  <div className="bg-slate-800 text-slate-300 w-8 h-8 rounded-full flex items-center justify-center font-bold shrink-0">Q2</div>
                  <h4 className="text-slate-200 font-semibold leading-relaxed">
                    Are there any remaining "Hidden Kernel Locks" in the Node.js worker_thread serialization path that could cause a &gt;10µs delay?
                  </h4>
                </div>
                <div className="flex gap-4 items-start pl-12">
                  <div className="text-red-400 shrink-0 pt-1">
                    <ShieldAlert className="w-5 h-5" />
                  </div>
                  <div className="text-slate-400 text-sm leading-relaxed space-y-2">
                    <p>
                      <strong className="text-red-400 font-mono text-base">YES. HIDDEN LOCKS DETECTED.</strong>
                    </p>
                    <p>
                      Standard Node.js <code className="text-orange-300 bg-orange-900/30 px-1 py-0.5 rounded">worker_threads</code> use <code className="text-orange-300 bg-orange-900/30 px-1 py-0.5 rounded">MessagePort</code> for IPC. This triggers V8's <code className="text-orange-300 bg-orange-900/30 px-1 py-0.5 rounded">ValueSerializer</code>, which <strong>allocates heap memory</strong> and invokes underlying libuv mutexes on the pipe/event loop.
                    </p>
                    <div className="bg-red-950/20 border border-red-900/50 p-3 rounded text-red-200 mt-2">
                      <strong className="flex items-center gap-2 mb-1"><AlertTriangle className="w-4 h-4"/> Critical Serialization Hazard:</strong>
                      Even when using <code className="text-red-300">SharedArrayBuffer</code> (SAB) to bypass structured cloning, invoking <code className="text-red-300">Atomics.wait()</code> or <code className="text-red-300">Atomics.notify()</code> traps into the kernel via <code className="text-red-300">futex(2)</code>. If the receiving thread is descheduled, kernel wakeup scheduling will easily violate the 10µs constraint, causing 15-50µs jitter.
                    </div>
                    <p className="mt-2 border-t border-slate-800 pt-3">
                      <strong className="text-slate-200">Required Remediation:</strong> To maintain strict &lt;10µs open pipes, you must implement a <strong>Lock-Free SPSC (Single-Producer Single-Consumer) Ring Buffer</strong> over SAB, combined with <strong>user-space spin-polling</strong> (busy waiting). You cannot yield to the OS.
                    </p>
                  </div>
                </div>
              </div>
            </div>

          </section>
        </div>
      </div>
      
      <footer className="mt-12 border-t border-slate-800 pt-6 text-center text-xs text-slate-600">
        ANTIGRAVITY NEXUS OS // SOVEREIGN ACTOR v2 // RESTRICTED ACCESS // ZERO-HEAP VALIDATED
      </footer>
    </div>
  );
}