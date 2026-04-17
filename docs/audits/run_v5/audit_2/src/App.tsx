const takeoverTimeline = [
  { t: "0ns", event: "Primary shard C5-C8 writes heartbeat seq++ in cacheline-local lane." },
  { t: "250ns", event: "Mirror C11 spin-poll sees stale seq and flags stall window." },
  { t: "650ns", event: "Mirror validates NMI symptom: no ownership handback + no forward progress." },
  { t: "950ns", event: "Mirror commits takeover token using single-writer CAS gate." },
  { t: "1.45us", event: "Mirror replays last committed state snapshot to egress pointer lane." },
  { t: "1.92us", event: "Egress consumer detects monotonic seq continuity; flow is preserved." },
];

const cycleBudget = [
  { stage: "Ingress read + seq check", intel: "60-90 cycles", amd: "65-95 cycles" },
  { stage: "Branchless route classify", intel: "35-45 cycles", amd: "35-50 cycles" },
  { stage: "Store pointer + store_release", intel: "70-85 cycles", amd: "75-95 cycles" },
  { stage: "Fence window (LFENCE/SFENCE)", intel: "25-45 cycles", amd: "30-50 cycles" },
  { stage: "Spin-poll acknowledge", intel: "45-70 cycles", amd: "50-75 cycles" },
  { stage: "Total logic pass", intel: "235-335 cycles", amd: "255-365 cycles" },
];

export default function App() {
  return (
    <main className="min-h-screen bg-[#030712] text-slate-100">
      <style>{`
        @keyframes pulseLane {
          0% { transform: translateX(0%); opacity: 0.2; }
          35% { opacity: 1; }
          100% { transform: translateX(140%); opacity: 0.2; }
        }
        @keyframes scanline {
          0% { transform: translateY(-10%); }
          100% { transform: translateY(110%); }
        }
        @keyframes glow {
          0%, 100% { opacity: 0.45; }
          50% { opacity: 0.9; }
        }
      `}</style>

      <section className="relative overflow-hidden border-b border-slate-800">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_18%_25%,rgba(56,189,248,0.2),transparent_45%),radial-gradient(circle_at_82%_20%,rgba(99,102,241,0.2),transparent_42%),linear-gradient(180deg,#020617_0%,#030712_100%)]" />
        <div className="absolute inset-0 opacity-30" style={{ animation: "scanline 9s linear infinite" }}>
          <div className="h-24 w-full bg-gradient-to-b from-transparent via-cyan-300/20 to-transparent" />
        </div>

        <div className="relative mx-auto max-w-6xl px-6 pb-20 pt-16 lg:px-8 lg:pb-28 lg:pt-24">
          <p className="text-sm tracking-[0.24em] text-cyan-300">ADAPTIVE SOVEREIGN MESH v5</p>
          <h1 className="mt-4 max-w-4xl text-4xl font-semibold leading-tight text-white md:text-6xl">
            The Platinum Battle: 1us Constant Gate for Infinite and Immortal Transport
          </h1>
          <p className="mt-5 max-w-2xl text-base text-slate-300 md:text-lg">
            A physics-aligned IPC design for multimodal heavy flow, mirror-node hot injection, and
            sub-microsecond deterministic logic on Intel and AMD ring-bus topologies.
          </p>

          <div className="mt-10 h-52 overflow-hidden border border-cyan-400/30 bg-slate-950/70 p-6">
            <p className="mb-4 text-xs uppercase tracking-[0.2em] text-cyan-200">Transport Plane Composition</p>
            <div className="space-y-5 font-mono text-xs text-slate-300">
              {[
                "L0 Trade Lane    : 64B thin packet, cache-resident, deterministic actor path",
                "L1 Sideband Lane : mmap ptr + lease token for audio/frame blobs",
                "L2 Recovery Lane : mirror takeover token + monotonic replay index",
              ].map((line, index) => (
                <div key={line} className="relative overflow-hidden border border-slate-700/80 px-3 py-2">
                  <span>{line}</span>
                  <span
                    className="pointer-events-none absolute inset-y-0 -left-12 w-12 bg-gradient-to-r from-cyan-400/0 via-cyan-300/70 to-cyan-400/0"
                    style={{ animation: `pulseLane ${3 + index * 0.7}s linear infinite` }}
                  />
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      <section id="sideband" className="mx-auto max-w-6xl border-b border-slate-800 px-6 py-16 lg:px-8">
        <p className="text-xs tracking-[0.22em] text-slate-400">SOVEREIGN HURDLE 1</p>
        <h2 className="mt-3 text-3xl font-semibold text-white">L1-Sideband Multimodal Pipes Without L3 Pollution</h2>
        <p className="mt-4 max-w-4xl text-slate-300">
          Heavy payloads never enter trade-actor cachelines. The trade lane carries only 64-byte descriptors:
          pointer, byte length, media type, sequence, and lease id. Audio and frame blobs live in mmap slabs pinned
          to sideband NUMA pages with write-combined allocation. Trade actors read descriptors only; sideband workers
          dereference payload pointers on isolated cores.
        </p>
        <div className="mt-8 grid gap-4 text-sm md:grid-cols-3">
          <div className="border border-slate-700 px-4 py-4">
            <p className="font-mono text-cyan-300">Descriptor (64B)</p>
            <p className="mt-2 text-slate-300">[ptr48 | len16 | media8 | seq64 | lease32 | crc16 | pad]</p>
          </div>
          <div className="border border-slate-700 px-4 py-4">
            <p className="font-mono text-cyan-300">Isolation Rule</p>
            <p className="mt-2 text-slate-300">L0/L1 use separate cacheline colors and separate prefetch streams.</p>
          </div>
          <div className="border border-slate-700 px-4 py-4">
            <p className="font-mono text-cyan-300">No Copy Path</p>
            <p className="mt-2 text-slate-300">Producer writes blob once, publishes pointer once, consumer retires lease.</p>
          </div>
        </div>
      </section>

      <section id="mirror" className="mx-auto max-w-6xl border-b border-slate-800 px-6 py-16 lg:px-8">
        <p className="text-xs tracking-[0.22em] text-slate-400">SOVEREIGN HURDLE 2</p>
        <h2 className="mt-3 text-3xl font-semibold text-white">Jitter-Free Mirror Takeover in 2.00us</h2>
        <p className="mt-4 max-w-4xl text-slate-300">
          Primary and mirror run station-static instances. The mirror is always warm with replay-ready snapshots,
          but it is read-only until takeover. Stall detection combines heartbeat age, outstanding reservation age,
          and NMI guard bit. Ownership flips through one atomic token, preserving strict SPSC single-writer semantics.
        </p>
        <div className="mt-8 space-y-3">
          {takeoverTimeline.map((item) => (
            <div key={item.t} className="flex gap-4 border-b border-slate-800 py-3 text-sm md:text-base">
              <div className="w-24 shrink-0 font-mono text-indigo-300">{item.t}</div>
              <div className="text-slate-300">{item.event}</div>
            </div>
          ))}
        </div>
      </section>

      <section id="atomic" className="mx-auto max-w-6xl px-6 py-16 lg:px-8">
        <p className="text-xs tracking-[0.22em] text-slate-400">SOVEREIGN HURDLE 3</p>
        <h2 className="mt-3 text-3xl font-semibold text-white">Atomic Constant: Mapping Fences to Ring-Bus Reality</h2>
        <p className="mt-4 max-w-4xl text-slate-300">
          Fence placement is minimal and explicit. Use store-release on descriptor publish. Use LFENCE only after
          polling sequence transitions to serialize dependent reads. Use SFENCE when sideband DMA writes must be
          globally visible before publish. Spin-poll loops use PAUSE with bounded backoff, then immediate forward progress.
        </p>

        <div className="mt-8 overflow-x-auto border border-slate-700">
          <table className="w-full min-w-[620px] border-collapse text-left text-sm">
            <thead className="bg-slate-900/70 text-slate-200">
              <tr>
                <th className="border-b border-slate-700 px-4 py-3">Stage</th>
                <th className="border-b border-slate-700 px-4 py-3">Intel L3 Ring</th>
                <th className="border-b border-slate-700 px-4 py-3">AMD Fabric/Ring</th>
              </tr>
            </thead>
            <tbody>
              {cycleBudget.map((row) => (
                <tr key={row.stage} className="odd:bg-slate-950/40">
                  <td className="border-b border-slate-800 px-4 py-3 text-slate-300">{row.stage}</td>
                  <td className="border-b border-slate-800 px-4 py-3 font-mono text-cyan-200">{row.intel}</td>
                  <td className="border-b border-slate-800 px-4 py-3 font-mono text-indigo-200">{row.amd}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="mt-8 border border-cyan-500/40 bg-cyan-950/20 px-5 py-4 font-mono text-xs text-cyan-100" style={{ animation: "glow 4.6s ease-in-out infinite" }}>
          Gate Equation: T_total = T_seq_check + T_route + T_publish + T_fence + T_ack &lt;= 1.00us @ &gt;=3.6GHz bin,
          with sideband payload excluded from critical path and no heap, no mutex, no copy.
        </div>
      </section>
    </main>
  );
}
