import { useState, useEffect } from 'react';
import { Server, Database, ArrowLeftRight, Monitor } from 'lucide-react';

export function ArchitectureSection() {
  const [activeTab, setActiveTab] = useState<'topology' | 'striping'>('topology');

  return (
    <section className="relative py-20 px-4">
      <div className="max-w-6xl mx-auto">
        {/* Section header */}
        <div className="text-center mb-16">
          <div className="font-mono text-[10px] text-cyan-dim tracking-[0.3em] mb-3">ARCHITECTURAL SPECIFICATION</div>
          <h2 className="font-mono text-3xl md:text-4xl font-bold text-white mb-4">
            V24 <span className="text-cyan-neon">Design Pillars</span>
          </h2>
          <p className="text-slate-400 max-w-2xl mx-auto text-sm">
            Four mandates define the Sovereign V24 protocol. Each pillar eliminates a class of latency 
            while preserving absolute safety under adversarial hardware conditions.
          </p>
        </div>

        {/* Spec cards */}
        <div className="grid md:grid-cols-2 gap-4 mb-16">
          <SpecCard
            number="01"
            title="Hardware-Auto-Detect Topology"
            desc="Dynamically identifies L1/L2/L3 cache line widths and NUMA node distances during Initialization. Hardcoded 256B assumptions are BANNED."
            icon={Server}
            gradient="from-cyan-500/20 to-blue-500/20"
            borderColor="border-cyan-500/30"
            details={[
              'CPUID-based L1/L2/L3 detection',
              'NUMA distance matrix computation',
              'Auto-alignment to detected stripe width',
              'Heterogeneous core topology mapping',
            ]}
          />
          <SpecCard
            number="02"
            title="Zero-Friction Safety Invariants"
            desc="Non-latency-summing safety check via bitwise sequence-shadow validation proving fence-less model is 100% safe across multiple sockets."
            icon={Monitor}
            gradient="from-green-500/20 to-emerald-500/20"
            borderColor="border-green-500/30"
            details={[
              'Bitwise sequence-shadow validation',
              'Hardware-level TSO parity checks',
              'Zero-copy data integrity proofs',
              'Cross-socket coherence guarantees',
            ]}
          />
          <SpecCard
            number="03"
            title="Adaptive Striping Engine"
            desc="Friction-less scaling: core adaptively shifts between L1-local and L2-striped modes based on real-time cache contention diagnostics."
            icon={ArrowLeftRight}
            gradient="from-purple-500/20 to-violet-500/20"
            borderColor="border-purple-500/30"
            details={[
              'Real-time contention monitoring',
              'L1↔L2 mode auto-transition',
              'Contention-aware stripe width',
              'Zero-overhead mode switching',
            ]}
          />
          <SpecCard
            number="04"
            title="ADR-015: Fence-Less Discipline"
            desc="Pure hardware sequence-differencing and Marshal-allocated unmanaged telemetry. All legacy barriers are strictly prohibited."
            icon={Database}
            gradient="from-orange-500/20 to-red-500/20"
            borderColor="border-orange-500/30"
            details={[
              'No Thread.MemoryBarrier()',
              'No Interlocked.* operations',
              'No lock() primitives',
              'No legacy volatile-barriers',
            ]}
          />
        </div>

        {/* Interactive tabs */}
        <div className="neon-box rounded-xl overflow-hidden">
          {/* Tab headers */}
          <div className="flex border-b border-slate-700/50">
            <button
              onClick={() => setActiveTab('topology')}
              className={`flex-1 px-6 py-3 font-mono text-xs tracking-wider transition-all ${
                activeTab === 'topology'
                  ? 'text-cyan-neon bg-cyan-neon/5 border-b-2 border-cyan-neon'
                  : 'text-slate-500 hover:text-slate-300'
              }`}
            >
              TOPOLOGY MAP
            </button>
            <button
              onClick={() => setActiveTab('striping')}
              className={`flex-1 px-6 py-3 font-mono text-xs tracking-wider transition-all ${
                activeTab === 'striping'
                  ? 'text-green-neon bg-green-neon/5 border-b-2 border-green-neon'
                  : 'text-slate-500 hover:text-slate-300'
              }`}
            >
              STRIPING ENGINE
            </button>
          </div>

          <div className="p-6">
            {activeTab === 'topology' ? <TopologyMap /> : <StripingEngine />}
          </div>
        </div>
      </div>
    </section>
  );
}

function SpecCard({ number, title, desc, icon: Icon, gradient, borderColor, details }: {
  number: string;
  title: string;
  desc: string;
  icon: React.ElementType;
  gradient: string;
  borderColor: string;
  details: string[];
}) {
  return (
    <div className={`neon-box rounded-xl p-6 hover:${borderColor} transition-all group`}>
      <div className="flex items-start gap-4 mb-4">
        <div className={`w-10 h-10 rounded-lg bg-gradient-to-br ${gradient} flex items-center justify-center border ${borderColor}`}>
          <Icon className="w-5 h-5 text-slate-200" />
        </div>
        <div>
          <div className="font-mono text-[10px] text-slate-500 mb-1">MANDATE {number}</div>
          <h3 className="font-mono text-sm font-bold text-white">{title}</h3>
        </div>
      </div>
      <p className="text-xs text-slate-400 mb-4 leading-relaxed">{desc}</p>
      <div className="space-y-1.5">
        {details.map((d, i) => (
          <div key={i} className="flex items-center gap-2">
            <div className="w-1 h-1 rounded-full bg-cyan-neon/50" />
            <span className="font-mono text-[10px] text-slate-500">{d}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

// Topology Map Component
function TopologyMap() {
  const [selectedNode, setSelectedNode] = useState<number | null>(null);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const i = setInterval(() => setTick(t => t + 1), 1000);
    return () => clearInterval(i);
  }, []);

  const nodes = [
    { id: 0, type: 'NUMA 0', cores: [0, 1, 2, 3], l1: 32, l2: 1024, l3: 32768, latency: 0.42 },
    { id: 1, type: 'NUMA 1', cores: [4, 5, 6, 7], l1: 32, l2: 1024, l3: 32768, latency: 0.51 },
  ];

  const nodeLabels = ['CCX-0', 'CCX-1', 'CCX-2', 'CCX-3', 'CCX-4', 'CCX-5', 'CCX-6', 'CCX-7'];

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <div>
          <h3 className="font-mono text-sm text-cyan-neon font-bold">Cache Topology Map — Auto-Detected</h3>
          <p className="font-mono text-[10px] text-slate-500 mt-1">Dual-socket AMD Zen 4 — 2 NUMA nodes, 8 core complexes</p>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-green-neon animate-glow-pulse" />
          <span className="font-mono text-[10px] text-green-neon">AUTO-DETECTED</span>
        </div>
      </div>

      {/* Topology visualization */}
      <div className="grid gap-4">
        {nodes.map((node, ni) => (
          <div key={node.id} className={`rounded-lg border ${selectedNode === ni ? 'border-cyan-neon/50 bg-cyan-neon/5' : 'border-slate-700/30 bg-sovereign-900/50'} p-4 transition-all`}>
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <Server className="w-4 h-4 text-cyan-neon" />
                <span className="font-mono text-xs font-bold text-slate-200">{node.type}</span>
              </div>
              <span className="font-mono text-[10px] text-slate-500">
                Latency: <span className="text-cyan-neon">{(node.latency + Math.sin(tick + ni) * 0.03).toFixed(2)}ns</span>
              </span>
            </div>

            {/* Core complexes */}
            <div className="grid grid-cols-4 gap-2 mb-3">
              {node.cores.map((core, ci) => (
                <div
                  key={core}
                  onClick={() => setSelectedNode(ni * 4 + ci)}
                  className={`rounded-md p-2 text-center cursor-pointer transition-all border ${
                    selectedNode === ni * 4 + ci
                      ? 'border-cyan-neon bg-cyan-neon/10'
                      : 'border-slate-700/50 bg-sovereign-800/50 hover:border-slate-600'
                  }`}
                >
                  <div className="font-mono text-[10px] text-slate-400">{nodeLabels[ni * 4 + ci]}</div>
                  <div className="font-mono text-[10px] text-green-neon mt-1">
                    {selectedNode === ni * 4 + ci ? '●' : '○'}
                  </div>
                </div>
              ))}
            </div>

            {/* Cache info */}
            <div className="grid grid-cols-3 gap-2">
              <div className="bg-sovereign-800/50 rounded p-2 text-center">
                <div className="font-mono text-[10px] text-slate-500">L1</div>
                <div className="font-mono text-xs text-cyan-neon">{node.l1}B</div>
              </div>
              <div className="bg-sovereign-800/50 rounded p-2 text-center">
                <div className="font-mono text-[10px] text-slate-500">L2</div>
                <div className="font-mono text-xs text-purple-neon">{node.l2}KB</div>
              </div>
              <div className="bg-sovereign-800/50 rounded p-2 text-center">
                <div className="font-mono text-[10px] text-slate-500">L3</div>
                <div className="font-mono text-xs text-orange-neon">{(node.l3 / 1024).toFixed(0)}MB</div>
              </div>
            </div>
          </div>
        ))}

        {/* NUMA distance */}
        <div className="bg-sovereign-900/50 rounded-lg border border-slate-700/30 p-4">
          <h4 className="font-mono text-[10px] text-slate-500 mb-3">NUMA DISTANCE MATRIX</h4>
          <div className="grid grid-cols-3 gap-1 max-w-xs mx-auto">
            <div className="font-mono text-[10px] text-slate-500" />
            <div className="font-mono text-[10px] text-slate-500 text-center">N0</div>
            <div className="font-mono text-[10px] text-slate-500 text-center">N1</div>
            <div className="font-mono text-[10px] text-slate-500 text-center">N0</div>
            <div className="font-mono text-xs text-green-neon text-center bg-green-neon/10 rounded py-1">10</div>
            <div className="font-mono text-xs text-yellow-neon text-center bg-yellow-neon/10 rounded py-1">21</div>
            <div className="font-mono text-[10px] text-slate-500 text-center">N1</div>
            <div className="font-mono text-xs text-yellow-neon text-center bg-yellow-neon/10 rounded py-1">21</div>
            <div className="font-mono text-xs text-green-neon text-center bg-green-neon/10 rounded py-1">10</div>
          </div>
        </div>
      </div>
    </div>
  );
}

// Striping Engine Component
function StripingEngine() {
  const [mode, setMode] = useState<'L1' | 'L2'>('L1');
  const [contention, setContention] = useState(23);

  useEffect(() => {
    const i = setInterval(() => {
      setContention(prev => {
        const next = prev + (Math.random() - 0.5) * 15;
        const clamped = Math.max(5, Math.min(95, next));
        setMode(clamped > 60 ? 'L2' : 'L1');
        return Math.round(clamped);
      });
    }, 800);
    return () => clearInterval(i);
  }, []);

  const stripeWidth = mode === 'L1' ? 32 : 64;
  const latency = mode === 'L1' ? 0.43 : 0.48;

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <div>
          <h3 className="font-mono text-sm text-green-neon font-bold">Adaptive Striping Engine</h3>
          <p className="font-mono text-[10px] text-slate-500 mt-1">Real-time mode switching based on cache contention</p>
        </div>
        <div className={`px-3 py-1 rounded-full font-mono text-xs font-bold ${
          mode === 'L1' ? 'bg-cyan-neon/10 text-cyan-neon border border-cyan-neon/30' : 'bg-purple-neon/10 text-purple-neon border border-purple-neon/30'
        }`}>
          {mode === 'L1' ? 'L1-LOCAL' : 'L2-STRIPED'} MODE
        </div>
      </div>

      {/* Contention gauge */}
      <div className="mb-6">
        <div className="flex justify-between items-center mb-2">
          <span className="font-mono text-[10px] text-slate-500">CACHE CONTENTION</span>
          <span className={`font-mono text-xs font-bold ${contention > 60 ? 'text-orange-neon' : contention > 40 ? 'text-yellow-neon' : 'text-green-neon'}`}>
            {contention}%
          </span>
        </div>
        <div className="h-3 bg-sovereign-800 rounded-full overflow-hidden border border-slate-700/30">
          <div
            className={`h-full rounded-full transition-all duration-700 ${
              contention > 60 ? 'bg-gradient-to-r from-orange-500 to-red-500' :
              contention > 40 ? 'bg-gradient-to-r from-yellow-500 to-orange-500' :
              'bg-gradient-to-r from-green-500 to-emerald-400'
            }`}
            style={{ width: `${contention}%` }}
          />
        </div>
        <div className="flex justify-between mt-1">
          <span className="font-mono text-[9px] text-slate-600">0% — Idle</span>
          <span className="font-mono text-[9px] text-slate-600">60% — Threshold</span>
          <span className="font-mono text-[9px] text-slate-600">100% — Saturated</span>
        </div>
      </div>

      {/* Mode transition visual */}
      <div className="grid grid-cols-2 gap-4 mb-6">
        <div className={`rounded-lg p-4 border transition-all ${mode === 'L1' ? 'border-cyan-neon/40 bg-cyan-neon/5' : 'border-slate-700/30 bg-sovereign-900/50 opacity-50'}`}>
          <div className="font-mono text-[10px] text-slate-500 mb-2">L1-LOCAL MODE</div>
          <div className="flex gap-1 mb-3">
            {[...Array(8)].map((_, i) => (
              <div key={i} className={`flex-1 h-8 rounded-sm ${mode === 'L1' ? 'bg-cyan-neon/60' : 'bg-slate-700/30'} transition-all`} />
            ))}
          </div>
          <div className="font-mono text-[10px] text-cyan-neon">Stripe: {stripeWidth}B | Latency: {latency}ns</div>
        </div>
        <div className={`rounded-lg p-4 border transition-all ${mode === 'L2' ? 'border-purple-neon/40 bg-purple-neon/5' : 'border-slate-700/30 bg-sovereign-900/50 opacity-50'}`}>
          <div className="font-mono text-[10px] text-slate-500 mb-2">L2-STRIPED MODE</div>
          <div className="flex gap-1 mb-3">
            {[...Array(8)].map((_, i) => (
              <div key={i} className={`flex-1 h-8 rounded-sm transition-all ${mode === 'L2' ? (i % 2 === 0 ? 'bg-purple-neon/60' : 'bg-purple-neon/30') : 'bg-slate-700/30'}`} />
            ))}
          </div>
          <div className="font-mono text-[10px] text-purple-neon">Stripe: {mode === 'L2' ? '64' : '32'}B | Latency: {mode === 'L2' ? '0.48' : '0.43'}ns</div>
        </div>
      </div>

      {/* Performance metrics */}
      <div className="grid grid-cols-3 gap-3">
        <Metric label="MODE SWITCHES" value={`${Math.floor(contention * 0.3)}`} unit="events" />
        <Metric label="STRIPE WIDTH" value={mode === 'L1' ? '32' : '64'} unit="bytes" />
        <Metric label="P99 LATENCY" value={mode === 'L1' ? '0.44' : '0.49'} unit="ns" />
      </div>
    </div>
  );
}

function Metric({ label, value, unit }: { label: string; value: string | number; unit: string }) {
  return (
    <div className="bg-sovereign-800/50 rounded-lg p-3 text-center border border-slate-700/30">
      <div className="font-mono text-[9px] text-slate-500 mb-1">{label}</div>
      <div className="font-mono text-lg font-bold text-white">{value}</div>
      <div className="font-mono text-[9px] text-slate-600">{unit}</div>
    </div>
  );
}
