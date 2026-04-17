export default function ArchDiagram() {
  return (
    <div className="bg-gray-900/80 backdrop-blur-sm border border-gray-700/50 rounded-2xl p-6">
      <h2 className="text-lg font-semibold text-gray-200 mb-1">
        V10 Phantom Gate — Architecture
      </h2>
      <p className="text-xs text-gray-500 mb-5">
        Layered design composing all V7–V9 breakthroughs
      </p>

      <div className="space-y-2">
        {/* Layer 5 - Application */}
        <Layer
          label="L5"
          name="Monitoring UI"
          detail="pretext zero-reflow · 60fps numeric streaming"
          color="fuchsia"
          width="100%"
        />

        {/* Layer 4 - Dispatch */}
        <Layer
          label="L4"
          name="Phantom Gate Dispatch"
          detail="Userspace ring · Branchless gate · FPGA-Parity validation"
          color="cyan"
          width="92%"
        />

        {/* Layer 3 - Worker Mesh */}
        <Layer
          label="L3"
          name="Worker Mesh (×12)"
          detail="Neighbor-Watch CAS · Ring-Bus SPSC · Self-repair matrix"
          color="pink"
          width="84%"
        />

        {/* Layer 2 - Memory */}
        <Layer
          label="L2"
          name="Memory Substrate"
          detail="Uint32 Arena · NUMA-local · L1-D Pre-Touch"
          color="emerald"
          width="76%"
        />

        {/* Layer 1 - Hardware */}
        <Layer
          label="L1"
          name="Hardware Isolation"
          detail="CPU pinning · IRQ affinity · Cache-line alignment"
          color="amber"
          width="68%"
        />
      </div>

      {/* Data flow arrows */}
      <div className="mt-4 flex items-center justify-center gap-1 text-gray-600">
        <span className="text-[10px]">L1</span>
        <svg className="w-4 h-4" viewBox="0 0 16 16" fill="currentColor">
          <path d="M8 3l5 5-5 5V9H3V7h5V3z" />
        </svg>
        <span className="text-[10px]">L2</span>
        <svg className="w-4 h-4" viewBox="0 0 16 16" fill="currentColor">
          <path d="M8 3l5 5-5 5V9H3V7h5V3z" />
        </svg>
        <span className="text-[10px]">L3</span>
        <svg className="w-4 h-4" viewBox="0 0 16 16" fill="currentColor">
          <path d="M8 3l5 5-5 5V9H3V7h5V3z" />
        </svg>
        <span className="text-[10px]">L4</span>
        <svg className="w-4 h-4" viewBox="0 0 16 16" fill="currentColor">
          <path d="M8 3l5 5-5 5V9H3V7h5V3z" />
        </svg>
        <span className="text-[10px]">L5</span>
        <span className="text-[10px] ml-2 text-gray-500">
          180ns total critical path
        </span>
      </div>
    </div>
  );
}

function Layer({
  label,
  name,
  detail,
  color,
  width,
}: {
  label: string;
  name: string;
  detail: string;
  color: string;
  width: string;
}) {
  const colorMap: Record<string, string> = {
    cyan: 'border-cyan-500/30 bg-cyan-500/5 text-cyan-300',
    pink: 'border-pink-500/30 bg-pink-500/5 text-pink-300',
    emerald: 'border-emerald-500/30 bg-emerald-500/5 text-emerald-300',
    amber: 'border-amber-500/30 bg-amber-500/5 text-amber-300',
    fuchsia: 'border-fuchsia-500/30 bg-fuchsia-500/5 text-fuchsia-300',
  };
  const labelColorMap: Record<string, string> = {
    cyan: 'bg-cyan-500/20 text-cyan-400',
    pink: 'bg-pink-500/20 text-pink-400',
    emerald: 'bg-emerald-500/20 text-emerald-400',
    amber: 'bg-amber-500/20 text-amber-400',
    fuchsia: 'bg-fuchsia-500/20 text-fuchsia-400',
  };

  return (
    <div className="flex justify-center">
      <div
        className={`border rounded-lg px-4 py-2.5 ${colorMap[color]} flex items-center gap-3`}
        style={{ width }}
      >
        <span
          className={`text-[9px] font-mono font-bold px-1.5 py-0.5 rounded ${labelColorMap[color]}`}
        >
          {label}
        </span>
        <div>
          <div className="text-xs font-medium">{name}</div>
          <div className="text-[10px] opacity-60">{detail}</div>
        </div>
      </div>
    </div>
  );
}
