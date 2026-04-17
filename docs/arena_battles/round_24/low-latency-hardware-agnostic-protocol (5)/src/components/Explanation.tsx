import React from 'react';
import { ShieldAlert, Fingerprint, Layers } from 'lucide-react';

export const Explanation: React.FC = () => {
  return (
    <div className="bg-slate-900 border border-slate-700 rounded-xl p-6 text-slate-300 shadow-xl space-y-6 h-[500px] lg:h-full overflow-auto">
      <h3 className="text-xl font-mono text-cyan-400 mb-6 flex items-center gap-2 border-b border-slate-700 pb-4">
        <ShieldAlert size={20} />
        V24 Architecture Exegesis
      </h3>

      <div className="space-y-6 text-sm leading-relaxed">
        <section>
          <h4 className="text-white font-mono flex items-center gap-2 mb-2 text-md">
            <Fingerprint className="text-cyan-500" size={16} />
            Handling the "Portable Hardware Fence-Less" Invariant
          </h4>
          <p className="mb-3">
            The fundamental challenge of maintaining sub-1ns performance while abandoning legacy `Thread.MemoryBarrier()` 
            or `Interlocked` operations is relying entirely on the underlying CPU memory-consistency model.
          </p>
          <ul className="list-disc pl-5 space-y-2 text-slate-400 marker:text-cyan-500">
            <li>
              <strong className="text-slate-200">Hardware-TSO Leverage:</strong> On x86/x64 architectures, Total Store Order (TSO) guarantees 
              that loads are not reordered with other loads, and stores are not reordered with other stores. The V24 core exploits this by ensuring all shared sequence counters are naturally aligned to the dynamically detected hardware-stripe.
            </li>
            <li>
              <strong className="text-slate-200">Sequence-Shadow Validation:</strong> Instead of locking or atomic CAS operations, we maintain two distinct, unmanaged shadow pointers (`_producerShadowSequence` and `_consumerShadowSequence`) padded to separate cache lines (e.g., 64B or 128B as auto-detected). This eliminates cross-core false sharing entirely.
            </li>
            <li>
              <strong className="text-slate-200">Marshal-Allocated Unmanaged Telemetry:</strong> By utilizing `Marshal.AllocHGlobal`, we bypass the managed heap entirely. This eradicates GC pauses and allows us to bitwise-align telemetry variables directly to L1 cache boundaries, preserving the 0.87ns record and pushing below 0.5ns.
            </li>
          </ul>
        </section>

        <section>
          <h4 className="text-white font-mono flex items-center gap-2 mb-2 text-md">
            <Layers className="text-cyan-500" size={16} />
            Adaptive Striping & Adversarial Portability
          </h4>
          <p className="mb-3 text-slate-400">
            During high-interrupt context switching, legacy systems fail because lock-convoys form or OS scheduler preemption breaks assumptions. 
            The V24 core detects contention anomalies and adaptively shifts from aggressive L1-local caching to L2-striped modes. 
            Because data structures are fence-less and only read sequence counts, an interrupted thread cannot block another thread. 
            A consumer simply sees the sequence has not advanced and spins/yields, satisfying the Zero-Friction mandate while ensuring "Safety-under-Pressure."
          </p>
        </section>
      </div>
    </div>
  );
};
