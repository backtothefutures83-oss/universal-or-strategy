// ═══════════════════════════════════════════════════════════════════
//  RING BUFFER VISUALIZER — Circular SPSC buffer state
//  Shows write head, read head, slot occupancy, cache-line alignment
// ═══════════════════════════════════════════════════════════════════

import type { SPSCChannel } from '../types';

interface Props {
  channel: SPSCChannel | null;
}

export default function RingBufferViz({ channel }: Props) {
  if (!channel) {
    return (
      <div className="flex items-center justify-center h-full text-slate-600 font-mono text-sm">
        <div className="text-center">
          <div className="text-2xl mb-2 opacity-40">⊘</div>
          <div>Select a channel edge<br/>to inspect ring buffer</div>
        </div>
      </div>
    );
  }

  const slots = channel.capacity;
  const writeH = channel.writeHead;
  const readH = channel.readHead;

  // Calculate occupancy
  const occupied = (writeH - readH + slots) % slots;
  const occupancyPct = (occupied / slots) * 100;

  // Generate slot states
  const slotStates: ('empty' | 'data' | 'write' | 'read')[] = [];
  for (let i = 0; i < slots; i++) {
    if (i === writeH) slotStates.push('write');
    else if (i === readH) slotStates.push('read');
    else {
      const inRange =
        writeH > readH
          ? i >= readH && i < writeH
          : i >= readH || i < writeH;
      slotStates.push(inRange ? 'data' : 'empty');
    }
  }

  const slotColors: Record<string, string> = {
    empty: 'bg-slate-800/50 border-slate-700',
    data:  'bg-emerald-900/60 border-emerald-600',
    write: 'bg-amber-900/80 border-amber-400 shadow-amber-400/30 shadow-sm',
    read:  'bg-sky-900/80 border-sky-400 shadow-sky-400/30 shadow-sm',
  };

  return (
    <div className="p-3 h-full flex flex-col">
      {/* Header */}
      <div className="flex items-center justify-between mb-3">
        <div className="font-mono text-xs text-slate-400">
          <span className="text-emerald-400">{channel.from}</span>
          <span className="text-slate-600 mx-1">→</span>
          <span className="text-sky-400">{channel.to}</span>
        </div>
        <div className="font-mono text-[10px] text-slate-500">
          {channel.slotSizeBytes}B × {slots} slots
        </div>
      </div>

      {/* Ring grid */}
      <div className="grid grid-cols-8 gap-1 mb-3">
        {slotStates.map((state, i) => (
          <div
            key={i}
            className={`h-6 rounded border text-[8px] font-mono flex items-center justify-center ${slotColors[state]}`}
          >
            {state === 'write' ? 'W' : state === 'read' ? 'R' : i}
          </div>
        ))}
      </div>

      {/* Metrics */}
      <div className="space-y-2 text-xs font-mono flex-1">
        <div className="flex justify-between">
          <span className="text-slate-500">Write Head</span>
          <span className="text-amber-400">{writeH}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-slate-500">Read Head</span>
          <span className="text-sky-400">{readH}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-slate-500">Occupancy</span>
          <span className={occupied > slots * 0.8 ? 'text-red-400' : 'text-emerald-400'}>
            {occupied}/{slots} ({occupancyPct.toFixed(0)}%)
          </span>
        </div>

        {/* Occupancy bar */}
        <div className="w-full h-2 bg-slate-800 rounded-full overflow-hidden">
          <div
            className={`h-full rounded-full transition-all duration-300 ${
              occupancyPct > 80 ? 'bg-red-500' : occupancyPct > 50 ? 'bg-amber-500' : 'bg-emerald-500'
            }`}
            style={{ width: `${occupancyPct}%` }}
          />
        </div>

        <div className="border-t border-slate-800 pt-2 mt-1 space-y-1">
          <div className="flex justify-between">
            <span className="text-slate-500">Latency</span>
            <span className="text-cyan-400">{channel.latencyNs.toFixed(1)} ns</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-500">Jitter (p99−p50)</span>
            <span className="text-purple-400">{channel.jitterNs.toFixed(1)} ns</span>
          </div>
          <div className="flex justify-between">
            <span className="text-slate-500">Throughput</span>
            <span className="text-emerald-400">{channel.throughputMps.toFixed(1)} Mpkt/s</span>
          </div>
        </div>
      </div>

      {/* Legend */}
      <div className="flex gap-3 mt-2 pt-2 border-t border-slate-800">
        {[
          { label: 'Empty', cls: 'bg-slate-800 border-slate-700' },
          { label: 'Data',  cls: 'bg-emerald-900 border-emerald-600' },
          { label: 'Write', cls: 'bg-amber-900 border-amber-400' },
          { label: 'Read',  cls: 'bg-sky-900 border-sky-400' },
        ].map((l) => (
          <div key={l.label} className="flex items-center gap-1">
            <div className={`w-3 h-3 rounded border ${l.cls}`} />
            <span className="text-[9px] text-slate-500 font-mono">{l.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
