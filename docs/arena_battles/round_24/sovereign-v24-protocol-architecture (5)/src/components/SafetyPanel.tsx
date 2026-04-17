import React from 'react';
import { Shield, ShieldCheck, Eye, Lock, CheckCircle, XCircle } from 'lucide-react';

const SafetyPanel: React.FC = () => {
  const invariants = [
    {
      id: 'SI-001',
      title: 'Bitwise Sequence-Shadow Validation',
      description: 'Pure hardware sequence-differencing validates ordering without memory barriers. Each write carries a monotonically increasing sequence tag validated via atomic compare at the read site.',
      status: 'verified',
      icon: <ShieldCheck className="w-5 h-5 text-emerald-400" />,
    },
    {
      id: 'SI-002',
      title: 'Hardware TSO Parity',
      description: 'Leverages x86 Total Store Order hardware property to guarantee zero-copy data integrity across multiple sockets without software fences.',
      status: 'verified',
      icon: <Eye className="w-5 h-5 text-cyan-400" />,
    },
    {
      id: 'SI-003',
      title: 'Marshal-Allocated Unmanaged Telemetry',
      description: 'All telemetry data flows through unmanaged memory allocated via Marshal, avoiding GC pauses and managed runtime overhead entirely.',
      status: 'verified',
      icon: <Lock className="w-5 h-5 text-purple-400" />,
    },
    {
      id: 'SI-004',
      title: 'Fence-Less Discipline (ADR-015)',
      description: 'Zero use of Thread.MemoryBarrier(), Interlocked.*, lock(), or legacy volatile-barriers. Pure hardware sequence-differencing only.',
      status: 'verified',
      icon: <Shield className="w-5 h-5 text-amber-400" />,
    },
  ];

  const bannedOps = [
    { name: 'Thread.MemoryBarrier()', reason: 'Introduces 50-100ns fence latency' },
    { name: 'Interlocked.*', reason: 'CPU pipeline flush on every call' },
    { name: 'lock() / Monitor', reason: 'OS scheduler involvement, µs-scale' },
    { name: 'volatile barriers', reason: 'Legacy x86 fence overhead' },
  ];

  const safetyChecks = [
    { check: 'Cross-socket data integrity', result: 'PASS' },
    { check: 'Zero-copy validation', result: 'PASS' },
    { check: 'Context-switch stability', result: 'PASS' },
    { check: 'High-interrupt resilience', result: 'PASS' },
    { check: 'NUMA topology coverage', result: 'PASS' },
    { check: 'Fence-free guarantee', result: 'PASS' },
    { check: 'GC-pause independence', result: 'PASS' },
    { check: 'Pipeline-friendly access', result: 'PASS' },
  ];

  return (
    <section className="py-20 px-4" id="safety">
      <div className="max-w-6xl mx-auto">
        {/* Section header */}
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-500/10 border border-emerald-500/20 mb-4">
            <ShieldCheck className="w-3 h-3 text-emerald-400" />
            <span className="text-xs font-mono text-emerald-300 tracking-wider">SAFETY INVARIANTS</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold mb-3">
            <span className="text-shimmer">Safety-Under-Pressure</span>
          </h2>
          <p className="text-slate-400 max-w-xl mx-auto">
            Non-latency-summing safety checks prove the fence-less model is 100% safe across multiple sockets.
          </p>
        </div>

        {/* Invariant cards */}
        <div className="grid md:grid-cols-2 gap-4 mb-12">
          {invariants.map((inv) => (
            <div key={inv.id} className="glass-panel rounded-xl p-5 glass-panel-hover transition-all">
              <div className="flex items-start gap-3 mb-3">
                <div className="mt-0.5">{inv.icon}</div>
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-[10px] font-mono text-slate-500">{inv.id}</span>
                    <span className="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/20 text-emerald-300">
                      {inv.status.toUpperCase()}
                    </span>
                  </div>
                  <h4 className="text-sm font-semibold text-slate-200">{inv.title}</h4>
                </div>
              </div>
              <p className="text-xs text-slate-400 leading-relaxed">{inv.description}</p>
            </div>
          ))}
        </div>

        {/* Banned operations */}
        <div className="glass-panel rounded-xl p-6 mb-8">
          <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <XCircle className="w-4 h-4 text-red-400" />
            <span>Banned Operations (ADR-015)</span>
          </h3>
          <div className="grid md:grid-cols-2 gap-3">
            {bannedOps.map((op) => (
              <div key={op.name} className="flex items-center gap-3 p-3 rounded-lg bg-red-500/5 border border-red-500/10">
                <XCircle className="w-4 h-4 text-red-400 shrink-0" />
                <div>
                  <div className="text-xs font-mono text-red-300">{op.name}</div>
                  <div className="text-[10px] text-slate-500">{op.reason}</div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Safety checklist */}
        <div className="glass-panel rounded-xl p-6">
          <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <CheckCircle className="w-4 h-4 text-emerald-400" />
            <span>Safety Verification Matrix</span>
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
            {safetyChecks.map((sc) => (
              <div key={sc.check} className="flex items-center justify-between p-2 rounded bg-slate-900/50 border border-slate-800">
                <span className="text-xs text-slate-300">{sc.check}</span>
                <span className="text-xs font-mono text-emerald-400 flex items-center gap-1">
                  <CheckCircle className="w-3 h-3" />
                  {sc.result}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
};

export default SafetyPanel;
