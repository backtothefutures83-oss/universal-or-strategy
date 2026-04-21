export default function BannedPatterns() {
  const banned = [
    {
      pattern: 'Worker Factories',
      replacement: 'Station Static Instances',
      reason: 'Factory pattern causes heap allocation per worker spawn. Static instances are compile-time fixed — zero runtime allocation, deterministic memory layout.',
      icon: '🏭',
    },
    {
      pattern: 'Mutexes',
      replacement: 'Exclusive Thread Ownership',
      reason: 'Mutex = kernel futex syscall in contended path (~2000ns). Exclusive ownership means each data structure has exactly one owning thread. Compile-time enforced. Zero contention possible.',
      icon: '🔒',
    },
    {
      pattern: 'Copying',
      replacement: 'Mmapped Pointer Passing',
      reason: 'memcpy of a 64-byte trade message = ~15ns + L1 pollution. Mmap\'d pointer passing = 0ns. The consumer reads directly from the producer\'s ring buffer slot via shared memory region.',
      icon: '📋',
    },
    {
      pattern: '\'Ultrathink\' Fluff',
      replacement: 'Physics-Grade Logic',
      reason: 'Every design decision maps to a measurable physical constraint: cache-line size (64B), ring-bus hop latency (6cy), store-buffer depth (56 entries), TLB reach (2MB hugepage). No hand-waving.',
      icon: '💭',
    },
  ];

  return (
    <section className="relative py-20 px-4">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-12">
          <span className="font-mono text-xs text-[#DC2626]/60 tracking-[0.3em] uppercase">Discipline</span>
          <h2 className="text-4xl md:text-5xl font-black text-[#E5E4E2] mt-2">
            🚫 Restricted Patterns
          </h2>
          <p className="text-[#B0AFA8] mt-3 max-w-lg mx-auto text-sm">
            Elegance requires constraint. These patterns are permanently banned.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {banned.map((b, i) => (
            <div key={i} className="group relative rounded-xl border border-[#DC2626]/10 bg-[#12121A]/80 overflow-hidden hover:border-[#DC2626]/30 transition-all duration-300">
              {/* Strikethrough bar */}
              <div className="absolute top-0 left-0 right-0 h-0.5 bg-gradient-to-r from-transparent via-[#DC2626]/40 to-transparent" />

              <div className="p-6">
                <div className="flex items-start gap-4">
                  <span className="text-2xl">{b.icon}</span>
                  <div className="flex-1">
                    <div className="flex items-center gap-3 mb-2">
                      <span className="font-mono text-sm text-[#DC2626] line-through opacity-70">{b.pattern}</span>
                      <span className="font-mono text-xs text-[#B0AFA8]/30">→</span>
                      <span className="font-mono text-sm text-[#10B981] font-semibold">{b.replacement}</span>
                    </div>
                    <p className="text-[#B0AFA8]/70 text-xs leading-relaxed">{b.reason}</p>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-[#C0A040]/20 to-transparent" />
    </section>
  );
}
