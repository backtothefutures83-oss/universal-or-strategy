export default function ArchDiagram() {
  return (
    <section className="max-w-5xl mx-auto px-4 py-8">
      <div className="text-center mb-8">
        <h2 className="text-3xl font-black text-white mb-2">
          Zero-Heap / Open Pipe Topology
        </h2>
        <p className="text-slate-500 text-sm max-w-xl mx-auto">
          Each Engine owns its send-side SPSC rings. The SAB slab is the only shared memory — accessed exclusively through lock-free Atomics.
        </p>
      </div>

      <div className="rounded-2xl border border-slate-700 bg-slate-900/80 p-6 overflow-x-auto">
        <svg
          viewBox="0 0 860 420"
          className="w-full max-w-4xl mx-auto"
          style={{ minWidth: 600 }}
        >
          {/* Background */}
          <rect width={860} height={420} fill="#020617" rx={16} />

          {/* SAB slab in center */}
          <rect x={320} y={160} width={220} height={100} rx={12} fill="#0f172a" stroke="#1e3a5f" strokeWidth={1.5} />
          <text x={430} y={198} textAnchor="middle" fill="#38bdf8" fontSize={11} fontWeight="bold" fontFamily="monospace">
            SharedArrayBuffer
          </text>
          <text x={430} y={214} textAnchor="middle" fill="#1e40af" fontSize={9} fontFamily="monospace">
            Slab Pool · mlockall'd
          </text>
          <text x={430} y={228} textAnchor="middle" fill="#0f3460" fontSize={8} fontFamily="monospace">
            [HEAD][TAIL][...slots...]
          </text>
          <text x={430} y={244} textAnchor="middle" fill="#1e3a5f" fontSize={8} fontFamily="monospace">
            64KB per SPSC ring
          </text>

          {/* Engines */}
          {[
            { id: 0, x: 60,  y: 60,  label: "Engine 0", core: "Core 2", color: "#34d399" },
            { id: 1, x: 660, y: 60,  label: "Engine 1", core: "Core 4", color: "#38bdf8" },
            { id: 2, x: 60,  y: 300, label: "Engine 2", core: "Core 6", color: "#a78bfa" },
            { id: 3, x: 660, y: 300, label: "Engine 3", core: "Core 8", color: "#fb923c" },
          ].map((e) => (
            <g key={e.id}>
              <rect x={e.x} y={e.y} width={140} height={70} rx={10}
                fill="#0f172a" stroke={e.color} strokeWidth={1.5} strokeOpacity={0.6} />
              <circle cx={e.x + 16} cy={e.y + 16} r={5} fill={e.color} opacity={0.8} />
              <text x={e.x + 70} y={e.y + 26} textAnchor="middle"
                fill={e.color} fontSize={11} fontWeight="bold" fontFamily="monospace">
                {e.label}
              </text>
              <text x={e.x + 70} y={e.y + 41} textAnchor="middle"
                fill="#475569" fontSize={9} fontFamily="monospace">
                {e.core} · SCHED_FIFO/99
              </text>
              <text x={e.x + 70} y={e.y + 56} textAnchor="middle"
                fill="#1e293b" fontSize={8} fontFamily="monospace">
                spin-poll · no futex
              </text>
            </g>
          ))}

          {/* SPSC ring labels on arrows */}
          {/* Engine 0 → Engine 1 (top) */}
          <defs>
            <marker id="arr" markerWidth="8" markerHeight="8" refX="4" refY="2" orient="auto">
              <path d="M0,0 L0,4 L6,2 z" fill="#334155" />
            </marker>
            <marker id="arr-g" markerWidth="8" markerHeight="8" refX="4" refY="2" orient="auto">
              <path d="M0,0 L0,4 L6,2 z" fill="#34d399" />
            </marker>
          </defs>

          {/* E0 → SAB */}
          <line x1={200} y1={95} x2={318} y2={200} stroke="#1e3a5f" strokeWidth={1.2} markerEnd="url(#arr)" />
          <text x={248} y={140} textAnchor="middle" fill="#334155" fontSize={8} fontFamily="monospace">
            push HEAD
          </text>

          {/* E1 → SAB */}
          <line x1={660} y1={95} x2={542} y2={200} stroke="#1e3a5f" strokeWidth={1.2} markerEnd="url(#arr)" />
          <text x={610} y={140} textAnchor="middle" fill="#334155" fontSize={8} fontFamily="monospace">
            push HEAD
          </text>

          {/* SAB → E2 */}
          <line x1={322} y1={255} x2={200} y2={315} stroke="#34d399" strokeWidth={1.2} strokeOpacity={0.5} markerEnd="url(#arr-g)" />
          <text x={248} y={298} textAnchor="middle" fill="#1e3a5f" fontSize={8} fontFamily="monospace">
            pop TAIL
          </text>

          {/* SAB → E3 */}
          <line x1={538} y1={255} x2={660} y2={315} stroke="#34d399" strokeWidth={1.2} strokeOpacity={0.5} markerEnd="url(#arr-g)" />
          <text x={612} y={298} textAnchor="middle" fill="#1e3a5f" fontSize={8} fontFamily="monospace">
            pop TAIL
          </text>

          {/* E2 → E3 direct (bottom) */}
          <line x1={200} y1={335} x2={658} y2={335} stroke="#1e293b" strokeWidth={1} strokeDasharray="4 3" />
          <text x={430} y={348} textAnchor="middle" fill="#1e293b" fontSize={8} fontFamily="monospace">
            direct SPSC ring (no SAB hop)
          </text>

          {/* OS Layer */}
          <rect x={20} y={390} width={820} height={22} rx={4} fill="#0a0f1e" stroke="#1e293b" strokeWidth={1} />
          <text x={430} y={404} textAnchor="middle" fill="#334155" fontSize={9} fontFamily="monospace">
            Linux Kernel · isolcpus=2-13 · NO_HZ_FULL · mlockall(MCL_CURRENT|MCL_FUTURE) · zero kernel calls on hot path
          </text>

          {/* Legend */}
          <rect x={20} y={10} width={280} height={50} rx={6} fill="#0f172a" stroke="#1e293b" strokeWidth={1} />
          <text x={30} y={26} fill="#475569" fontSize={8} fontFamily="monospace">■ SPSC ring: one HEAD ptr + one TAIL ptr, never share</text>
          <text x={30} y={38} fill="#475569" fontSize={8} fontFamily="monospace">■ All slots pre-alloc'd in SAB slab at boot — zero malloc</text>
          <text x={30} y={50} fill="#475569" fontSize={8} fontFamily="monospace">■ Consumer spin-polls HEAD — no futex, no kernel mode</text>
        </svg>
      </div>
    </section>
  );
}
