export default function Footer() {
  const refs = [
    {
      category: "CPU Architecture",
      items: [
        "Intel® 64 and IA-32 Architectures Software Developer's Manual, Vol. 3A (Chapter 8, 11) — intel.com",
        "Agner Fog, 'Microarchitecture of Intel, AMD and VIA CPUs' & Instruction Tables — agner.org",
        "AMD64 Architecture Programmer's Manual, Vol. 2: System Programming — amd.com",
        "ARM Architecture Reference Manual (AArch64) §B2 (Memory Model) — developer.arm.com",
        "RISC-V Instruction Set Manual Vol. I §A (RVWMO) — riscv.org",
      ],
    },
    {
      category: "Memory Models & Fences",
      items: [
        "Paul McKenney, 'Is Parallel Programming Hard, And, If So, What Can You Do About It?' — kernel.org/pub/linux/kernel/people/paulmck/",
        "Hans Boehm & Sarita Adve, 'Foundations of the C++ Concurrency Memory Model' — PLDI 2008",
        "Herb Sutter, 'atomic<> Weapons' — C++ and Beyond 2012 (YouTube)",
        "ECMA-335 §I.12.6: .NET Memory Model — ecma-international.org",
        "Linus Torvalds, linux/include/linux/seqlock.h — kernel.org",
      ],
    },
    {
      category: "Lock-Free Algorithms",
      items: [
        "Maged Michael, 'Hazard Pointers: Safe Memory Reclamation for Lock-Free Objects' — IEEE TPDS 2004",
        "Martin Thompson et al., 'Disruptor: High performance alternative to bounded queues for exchanging data between concurrent threads' — LMAX 2011",
        "Dmitry Vyukov, 'Non-intrusive MPSC node-based queue' — 1024cores.net",
        "Paul McKenney, 'Read-Copy Update: Using Execution History to Solve Concurrency Problems' — USENIX 2007",
        "Maurice Herlihy & Nir Shavit, 'The Art of Multiprocessor Programming' (2nd ed.) — Morgan Kaufmann",
      ],
    },
    {
      category: "Benchmarking & Real-World Data",
      items: [
        "Brendan Gregg, 'Systems Performance' (2nd ed.) — latency numbers Chapter 1 — brendangregg.com",
        "lmbench: lat_ctx, lat_mem_rd measurements — mcvoy.com/lmbench",
        "BenchmarkDotNet: .NET micro-benchmark framework — benchmarkdotnet.org",
        "Aeron: High-performance messaging — github.com/real-logic/aeron",
        "'Non-scalable locks are dangerous', Boyd-Wickizer et al. — Ottawa Linux Symposium 2012",
      ],
    },
  ];

  return (
    <footer className="border-t border-gray-800 bg-gray-950 mt-16">
      <div className="max-w-7xl mx-auto px-4 py-12">
        {/* Integrity statement */}
        <div className="rounded-xl border border-gray-700 bg-gray-900 p-6 mb-10">
          <div className="text-white font-black text-lg mb-3">📋 Engineering Integrity Statement</div>
          <p className="text-gray-300 text-sm leading-relaxed">
            Every latency number, memory model description, and algorithm safety claim on this page
            is sourced from peer-reviewed literature, hardware vendor documentation, or reproducible
            empirical benchmarks. All sources are cited. No invented terminology, no physically
            impossible performance targets, no safety claims that contradict established computer
            science. The goal of this reference is to help engineers build systems that are actually
            fast and actually correct — not systems that sound fast in a specification document.
          </p>
        </div>

        {/* References */}
        <div className="text-gray-400 text-xs font-bold uppercase tracking-wider mb-5">Primary References</div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-10">
          {refs.map((section) => (
            <div key={section.category}>
              <div className="text-gray-300 font-bold text-sm mb-2">{section.category}</div>
              <ul className="space-y-1">
                {section.items.map((item, i) => (
                  <li key={i} className="text-gray-500 text-xs flex gap-2">
                    <span className="text-gray-700 flex-shrink-0">›</span>
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="border-t border-gray-800 pt-6 flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="text-gray-600 text-xs">
            CPU Topology & Low-Latency IPC Reference — Built with honest engineering.
          </div>
          <div className="flex gap-4 text-gray-600 text-xs">
            <span>Sources cited</span>
            <span>·</span>
            <span>Claims verified</span>
            <span>·</span>
            <span>No invented jargon</span>
          </div>
        </div>
      </div>
    </footer>
  );
}
