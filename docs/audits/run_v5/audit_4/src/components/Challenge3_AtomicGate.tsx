import ChallengeSection from './ChallengeSection';
import CodeBlock from './CodeBlock';
import { useState, useEffect } from 'react';
import type { SimulationState } from '../hooks/useSimulation';

interface Props {
  state: SimulationState;
}

export default function Challenge3({ state }: Props) {
  return (
    <ChallengeSection
      number="03"
      icon="⚛️"
      title="The Atomic Constant"
      subtitle="Sub-1µs Total Logic-Pass on Intel/AMD L3 Ring-Bus"
      accentColor="#C0A040"
    >
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        {/* Left: Breakdown + Visualizer */}
        <div className="space-y-6">
          <DesignCard
            title="1µs Budget Decomposition"
            content="1µs = 1000ns. At 4GHz, that's 4000 cycles. Every instruction counts. The gate measures from LFENCE (pipe read) to SFENCE (pipe write publish). Between those barriers: deserialize, logic, serialize — all on pre-warmed L1 cache lines."
            accent="#C0A040"
          />
          <DesignCard
            title="L3 Ring-Bus Architecture"
            content="Intel Core i9: L3 is divided into slices, one per core, connected by a bi-directional ring-bus. Worst-case cross-ring latency: ~42 cycles (core 0 → core 7). By pinning producer/consumer to adjacent cores on the ring, we guarantee a 3-hop maximum: ~18 cycles for cache-line transfer."
            accent="#22D3EE"
          />
          <DesignCard
            title="Memory Barrier Mapping"
            content={`LFENCE (Load Fence): Serializes all prior loads. Cost: ~4 cycles. Used at pipe-read to ensure we see the latest write_pos.
SFENCE (Store Fence): Drains the store buffer. Cost: ~8 cycles. Used at pipe-write to publish data before updating write_pos.
MFENCE: BANNED. Cost: ~33 cycles. We never need full serialization because SPSC gives us release-acquire semantics naturally.`}
            accent="#A855F7"
          />

          <LatencyBreakdown state={state} />
        </div>

        {/* Right: Code */}
        <div className="space-y-6">
          <CodeBlock
            title="atomic_gate.rs"
            language="Rust"
            accent="#C0A040"
            code={`/// The 1µs Constant Gate
/// Total budget: 1000ns (4000 cycles at 4GHz)
///
/// LFENCE ──► Deserialize ──► Logic ──► Serialize ──► SFENCE
/// 4cy        80cy            600cy     80cy          8cy
/// = 772 cycles = 193ns ... remaining 807ns is margin

#[repr(C, align(64))]
pub struct SpscPipe {
    // === Cacheline 0: Writer state ===
    write_pos: AtomicU64,
    writer_token: AtomicU64,
    _pad0: [u8; 48],

    // === Cacheline 1: Reader state ===
    read_pos: AtomicU64,
    cached_write: u64,  // Local shadow — no atomics
    _pad1: [u8; 48],

    // === Ring buffer ===
    ring: *mut u8,
    capacity: usize,
    mask: usize,  // capacity - 1 (power of 2)
}

impl SpscPipe {
    /// Producer: publish message (single-writer)
    #[inline(always)]
    pub fn publish<T: Sized>(&self, msg: &T) -> bool {
        let wp = self.write_pos.load(Ordering::Relaxed);
        // Check capacity against cached read_pos
        // Relaxed is fine — we're the only writer
        let rp = self.read_pos.load(Ordering::Acquire);

        let size = core::mem::size_of::<T>();
        if wp + size as u64 > rp + self.capacity as u64 {
            return false; // Full — backpressure
        }

        let offset = (wp as usize) & self.mask;
        let dst = unsafe {
            self.ring.add(offset) as *mut T
        };

        // Direct pointer write — zero copy
        unsafe { dst.write(*msg); }

        // SFENCE: drain store buffer, then publish
        unsafe {
            core::arch::x86_64::_mm_sfence();
        }
        self.write_pos.store(
            wp + size as u64,
            Ordering::Release,
        );
        true
    }

    /// Consumer: spin-poll with PAUSE
    #[inline(always)]
    pub fn try_consume<T: Sized>(&self) -> Option<&T> {
        let rp = self.read_pos.load(Ordering::Relaxed);

        // Fast path: check cached write position
        // Only reload atomic if cache is exhausted
        let wp = self.write_pos.load(Ordering::Acquire);

        // LFENCE: serialize loads before reading data
        unsafe {
            core::arch::x86_64::_mm_lfence();
        }

        let size = core::mem::size_of::<T>();
        if wp < rp + size as u64 {
            // Empty — spin with PAUSE hint
            core::hint::spin_loop();
            return None;
        }

        let offset = (rp as usize) & self.mask;
        let src = unsafe {
            &*(self.ring.add(offset) as *const T)
        };

        Some(src)
    }

    /// Consumer: advance read position
    #[inline(always)]
    pub fn commit_consume<T: Sized>(&self) {
        let rp = self.read_pos.load(Ordering::Relaxed);
        let size = core::mem::size_of::<T>();
        self.read_pos.store(
            rp + size as u64,
            Ordering::Release,
        );
    }
}

/// The Gate Function: LFENCE → Process → SFENCE
#[inline(always)]
pub fn gate_pass(
    input: &SpscPipe,
    output: &SpscPipe,
    logic: &dyn ActorLogic,
) -> bool {
    // LFENCE — read barrier (4 cycles)
    if let Some(msg) = input.try_consume::<TradeMsg>() {
        // Deserialize: already done (zero-copy ptr)
        // Logic: actor processes (budget: 600 cycles)
        let result = logic.process(msg);

        // Serialize + SFENCE — write barrier (8 cycles)
        let ok = output.publish(&result);
        if ok {
            input.commit_consume::<TradeMsg>();
        }
        ok
    } else {
        false
    }
}`}
          />

          <RingBusVisualizer />
        </div>
      </div>
    </ChallengeSection>
  );
}

function DesignCard({ title, content, accent }: { title: string; content: string; accent: string }) {
  return (
    <div className="p-5 rounded-xl border bg-[#12121A]/80" style={{ borderColor: `${accent}20` }}>
      <h4 className="font-mono text-sm font-semibold mb-2" style={{ color: accent }}>{title}</h4>
      <p className="text-[#B0AFA8] text-sm leading-relaxed whitespace-pre-line">{content}</p>
    </div>
  );
}

function LatencyBreakdown({ state }: { state: SimulationState }) {
  const [, setTick] = useState(0);
  useEffect(() => {
    const i = setInterval(() => setTick(t => t + 1), 100);
    return () => clearInterval(i);
  }, []);

  const breakdown = [
    { label: 'LFENCE', cycles: 4, ns: 1, color: '#A855F7' },
    { label: 'Acquire Load', cycles: 12, ns: 3, color: '#22D3EE' },
    { label: 'L3 Ring Hop (×3)', cycles: 18, ns: 4.5, color: '#22D3EE' },
    { label: 'Deserialize', cycles: 80, ns: 20, color: '#C0A040' },
    { label: 'Actor Logic', cycles: 600, ns: 150, color: '#C0A040' },
    { label: 'Serialize', cycles: 80, ns: 20, color: '#C0A040' },
    { label: 'SFENCE', cycles: 8, ns: 2, color: '#A855F7' },
    { label: 'Release Store', cycles: 8, ns: 2, color: '#22D3EE' },
    { label: 'Pipeline Margin', cycles: 190, ns: 47.5, color: '#555' },
  ];

  const totalCycles = breakdown.reduce((s, b) => s + b.cycles, 0);
  const totalNs = breakdown.reduce((s, b) => s + b.ns, 0);
  const currentGate = state.gateTimeNs;

  return (
    <div className="p-5 rounded-xl border border-[#C0A040]/15 bg-[#0D0D14]">
      <div className="flex items-center justify-between mb-4">
        <span className="font-mono text-xs text-[#C0A040]/60 tracking-wider">CYCLE BUDGET (4GHz)</span>
        <span className={`font-mono text-sm font-bold ${currentGate < 1000 ? 'text-[#10B981]' : 'text-[#DC2626]'}`}>
          LIVE: {currentGate.toFixed(0)}ns {currentGate < 1000 ? '✓' : '✗'}
        </span>
      </div>

      <div className="space-y-1.5">
        {breakdown.map((b, i) => {
          const pct = (b.cycles / 1000) * 100;
          return (
            <div key={i} className="flex items-center gap-2">
              <span className="font-mono text-[10px] text-[#B0AFA8]/60 w-28 text-right flex-shrink-0">{b.label}</span>
              <div className="flex-1 h-4 bg-[#1A1A28] rounded-sm overflow-hidden relative">
                <div
                  className="h-full rounded-sm transition-all duration-300"
                  style={{
                    width: `${pct}%`,
                    backgroundColor: b.color,
                    opacity: 0.6,
                  }}
                />
              </div>
              <span className="font-mono text-[10px] w-16 text-right flex-shrink-0" style={{ color: b.color }}>
                {b.cycles}cy/{b.ns}ns
              </span>
            </div>
          );
        })}
      </div>

      <div className="mt-4 pt-3 border-t border-[#C0A040]/10 flex justify-between">
        <span className="font-mono text-xs text-[#B0AFA8]/60">TOTAL</span>
        <span className="font-mono text-xs text-[#C0A040]">{totalCycles} cycles / {totalNs.toFixed(1)}ns</span>
      </div>
      <div className="flex justify-between mt-1">
        <span className="font-mono text-xs text-[#B0AFA8]/60">REMAINING BUDGET</span>
        <span className="font-mono text-xs text-[#10B981]">{1000 - totalCycles} cycles / {(250 - totalNs).toFixed(1)}ns</span>
      </div>
    </div>
  );
}

function RingBusVisualizer() {
  const [activeCoreIdx, setActiveCoreIdx] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setActiveCoreIdx(i => (i + 1) % 12);
    }, 300);
    return () => clearInterval(interval);
  }, []);

  const cores = Array.from({ length: 12 }, (_, i) => {
    const angle = (i / 12) * Math.PI * 2 - Math.PI / 2;
    const r = 80;
    return {
      x: 150 + Math.cos(angle) * r,
      y: 100 + Math.sin(angle) * r,
      label: `C${i + 1}`,
      active: i === activeCoreIdx,
    };
  });

  return (
    <div className="p-4 rounded-xl border border-[#C0A040]/15 bg-[#0D0D14]">
      <span className="font-mono text-xs text-[#C0A040]/60 tracking-wider">L3 RING-BUS TOPOLOGY</span>
      <svg viewBox="0 0 300 200" className="w-full mt-2">
        {/* Ring */}
        <circle cx={150} cy={100} r={80} fill="none" stroke="#C0A040" strokeWidth={1.5} opacity={0.15} />
        <circle cx={150} cy={100} r={80} fill="none" stroke="#C0A040" strokeWidth={1.5} opacity={0.3}
          strokeDasharray="8 8">
          <animateTransform attributeName="transform" type="rotate" from="0 150 100" to="360 150 100" dur="10s" repeatCount="indefinite" />
        </circle>

        {/* Data packet on ring */}
        <circle r={4} fill="#C0A040">
          <animateMotion dur="3s" repeatCount="indefinite" path="M230,100 A80,80 0 1,1 229.9,100" />
        </circle>

        {/* Cores */}
        {cores.map((c, i) => (
          <g key={i}>
            <circle cx={c.x} cy={c.y} r={14} fill="#12121A" stroke={c.active ? '#C0A040' : '#333'} strokeWidth={c.active ? 2 : 1} />
            {c.active && (
              <circle cx={c.x} cy={c.y} r={18} fill="none" stroke="#C0A040" strokeWidth={1} opacity={0.4}>
                <animate attributeName="r" from="14" to="24" dur="0.6s" repeatCount="indefinite" />
                <animate attributeName="opacity" from="0.4" to="0" dur="0.6s" repeatCount="indefinite" />
              </circle>
            )}
            <text x={c.x} y={c.y + 3.5} textAnchor="middle" fill={c.active ? '#C0A040' : '#888'} fontSize={7} fontFamily="'JetBrains Mono'">
              {c.label}
            </text>
          </g>
        ))}

        {/* Center label */}
        <text x={150} y={98} textAnchor="middle" fill="#C0A040" fontSize={8} fontFamily="'JetBrains Mono'" opacity={0.5}>L3 LLC</text>
        <text x={150} y={108} textAnchor="middle" fill="#888" fontSize={6} fontFamily="'JetBrains Mono'" opacity={0.4}>RING BUS</text>
      </svg>
    </div>
  );
}
