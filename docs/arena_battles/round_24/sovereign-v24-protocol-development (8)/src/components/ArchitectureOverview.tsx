export function ArchitectureOverview() {
  const principles = [
    {
      title: 'Hardware-Auto-Detect',
      desc: 'Dynamically identifies L1/L2/L3 cache line widths and NUMA node distances during Initialization. No hardcoded 256B assumptions.',
      icon: '🔍',
      color: 'from-sov-cyan/20 to-sov-cyan/5',
      borderColor: 'border-sov-cyan/30',
    },
    {
      title: 'Zero-Friction Safety',
      desc: 'Non-latency-summing safety checks via bitwise sequence-shadow validation and hardware-level TSO parity proving fence-less model safety.',
      icon: '🛡️',
      color: 'from-sov-green/20 to-sov-green/5',
      borderColor: 'border-sov-green/30',
    },
    {
      title: 'Adaptive Striping',
      desc: 'Friction-less scaling: core adaptively shifts between L1-local and L2-striped modes based on real-time cache contention diagnostics.',
      icon: '🔄',
      color: 'from-sov-amber/20 to-sov-amber/5',
      borderColor: 'border-sov-amber/30',
    },
    {
      title: 'Fence-Less Discipline',
      desc: 'Zero Thread.MemoryBarrier(), Interlocked.*, or lock() calls. Pure hardware sequence-differencing with Marshal-allocated unmanaged buffers.',
      icon: '⚡',
      color: 'from-sov-purple/20 to-sov-purple/5',
      borderColor: 'border-sov-purple/30',
    },
  ];

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
      {principles.map((p) => (
        <div
          key={p.title}
          className={`rounded-xl border bg-sov-panel p-4 bg-gradient-to-br ${p.color} ${p.borderColor} hover:border-sov-border-bright transition-all duration-300`}
        >
          <div className="flex items-start gap-3">
            <span className="text-2xl mt-0.5">{p.icon}</span>
            <div>
              <h4 className="text-sm font-semibold text-sov-text mb-1">{p.title}</h4>
              <p className="text-xs text-sov-text-dim leading-relaxed">{p.desc}</p>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
