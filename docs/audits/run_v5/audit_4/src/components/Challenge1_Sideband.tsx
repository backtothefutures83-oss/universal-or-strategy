import ChallengeSection from './ChallengeSection';
import CodeBlock from './CodeBlock';
import { useState, useEffect } from 'react';

export default function Challenge1() {
  return (
    <ChallengeSection
      number="01"
      icon="🎭"
      title="Multimodal Pipes"
      subtitle="L1-Sideband for Heavy Binary Data — Zero L3 Cache Pollution"
      accentColor="#A855F7"
    >
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
        {/* Left: Explanation */}
        <div className="space-y-6">
          <DesignCard
            title="The Problem"
            content="Pipecat audio streams (48kHz × 16-bit × 2ch = 192KB/s) and Vision-Agent screen-captures (~30MB/s at 30fps) cannot share the same cache hierarchy as 64-byte trade messages. A single 1920×1080 RGBA frame (8MB) would evict the entire L3 cache, destroying trade latency."
            accent="#DC2626"
          />
          <DesignCard
            title="The Solution: L1-Sideband Pipe"
            content={`Dedicated SPSC ring on a separate NUMA node's memory region. Uses non-temporal stores (MOVNTDQA) that bypass L1/L2/L3 entirely — writing directly to the memory controller. The trade actors never see sideband data in their cache hierarchy.`}
            accent="#A855F7"
          />
          <DesignCard
            title="Physics Constraint"
            content="Intel's L3 is a ring-bus (or mesh on Xeon). Each core's L2 connects to the L3 slice. By pinning SIDEBAND to Core 12 (separate LLC slice) and using NT-stores, we guarantee zero cross-slice invalidation traffic on the ring-bus for trade cores 1–8."
            accent="#22D3EE"
          />

          <SidebandVisualizer />
        </div>

        {/* Right: Implementation */}
        <div className="space-y-6">
          <CodeBlock
            title="sideband_pipe.rs"
            language="Rust"
            accent="#A855F7"
            code={`/// L1-Sideband: Non-Temporal SPSC for Heavy Binary
/// Bypasses L1/L2/L3 — direct to memory controller
#[repr(C, align(4096))]  // Page-aligned
pub struct SidebandPipe {
    // Producer metadata (own cacheline)
    write_pos: CacheLine<AtomicU64>,
    // Consumer metadata (own cacheline)
    read_pos:  CacheLine<AtomicU64>,
    // Ring buffer — mmap'd from hugepages
    ring: MmapRegion<PageAligned>,
    capacity: usize,
}

impl SidebandPipe {
    pub fn new(capacity: usize) -> Self {
        // Allocate from NUMA node 1 (away from trade cores)
        let ring = MmapRegion::from_hugepage(
            capacity,
            NumaPolicy::BindNode(1),
            MmapFlags::POPULATE | MmapFlags::LOCKED,
        );
        Self {
            write_pos: CacheLine::new(AtomicU64::new(0)),
            read_pos:  CacheLine::new(AtomicU64::new(0)),
            ring,
            capacity,
        }
    }

    /// Write binary frame using non-temporal stores
    /// ZERO cache pollution on trade cores
    #[inline(always)]
    pub unsafe fn push_frame_nt(
        &self,
        src: *const u8,
        len: usize,
    ) -> bool {
        let wp = self.write_pos.load(Ordering::Relaxed);
        let rp = self.read_pos.load(Ordering::Acquire);

        if wp - rp + len > self.capacity {
            return false; // Backpressure — drop frame
        }

        let offset = (wp as usize) % self.capacity;
        let dst = self.ring.as_ptr().add(offset);

        // Non-temporal store: bypasses all cache levels
        // Uses MOVNTDQA — writes go to Write-Combining buffer
        nt_memcpy(dst, src, len);

        // SFENCE: drain WC buffer before publishing
        core::arch::x86_64::_mm_sfence();

        self.write_pos.store(
            wp + len as u64,
            Ordering::Release,
        );
        true
    }

    /// Consumer read (on sideband core only)
    #[inline(always)]
    pub fn pop_frame(
        &self,
        dst: &mut [u8],
    ) -> Option<usize> {
        let rp = self.read_pos.load(Ordering::Relaxed);
        let wp = self.write_pos.load(Ordering::Acquire);
        let avail = (wp - rp) as usize;
        if avail == 0 { return None; }

        let len = avail.min(dst.len());
        let offset = (rp as usize) % self.capacity;
        let src = unsafe {
            self.ring.as_ptr().add(offset)
        };

        // Prefetch with NT hint — don't pollute L1
        unsafe {
            core::arch::x86_64::_mm_prefetch(
                src as *const i8,
                core::arch::x86_64::_MM_HINT_NTA,
            );
            core::ptr::copy_nonoverlapping(
                src, dst.as_mut_ptr(), len
            );
        }

        self.read_pos.store(
            rp + len as u64,
            Ordering::Release,
        );
        Some(len)
    }
}

/// Non-temporal memcpy using SSE streaming stores
#[inline(always)]
unsafe fn nt_memcpy(
    dst: *mut u8, src: *const u8, len: usize
) {
    let chunks = len / 16;
    for i in 0..chunks {
        let s = _mm_loadu_si128(
            src.add(i * 16) as *const __m128i
        );
        _mm_stream_si128(
            dst.add(i * 16) as *mut __m128i, s
        );
    }
    // Handle remainder
    let rem = len % 16;
    if rem > 0 {
        core::ptr::copy_nonoverlapping(
            src.add(chunks * 16),
            dst.add(chunks * 16),
            rem,
        );
    }
}`}
          />
        </div>
      </div>
    </ChallengeSection>
  );
}

function DesignCard({ title, content, accent }: { title: string; content: string; accent: string }) {
  return (
    <div className="p-5 rounded-xl border bg-[#12121A]/80" style={{ borderColor: `${accent}20` }}>
      <h4 className="font-mono text-sm font-semibold mb-2" style={{ color: accent }}>{title}</h4>
      <p className="text-[#B0AFA8] text-sm leading-relaxed">{content}</p>
    </div>
  );
}

function SidebandVisualizer() {
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => setTick(t => t + 1), 80);
    return () => clearInterval(interval);
  }, []);

  const tradePackets = Array.from({ length: 8 }, (_, i) => ({
    x: 20 + ((tick * 3 + i * 50) % 360),
    y: 50,
    size: 4,
  }));

  const binaryFrames = Array.from({ length: 3 }, (_, i) => ({
    x: 20 + ((tick * 2 + i * 120) % 360),
    y: 130,
    size: 12 + Math.sin(tick * 0.1 + i) * 3,
  }));

  return (
    <div className="p-4 rounded-xl border border-[#A855F7]/15 bg-[#0D0D14]">
      <div className="font-mono text-xs text-[#A855F7]/60 mb-3 tracking-wider">CACHE HIERARCHY ISOLATION</div>
      <svg viewBox="0 0 400 180" className="w-full">
        {/* Trade pipe lane */}
        <rect x={10} y={30} width={380} height={40} rx={6} fill="#C0A040" fillOpacity={0.05} stroke="#C0A040" strokeOpacity={0.2} strokeWidth={1} />
        <text x={20} y={25} fill="#C0A040" fontSize={9} fontFamily="'JetBrains Mono'" opacity={0.6}>TRADE PIPE — L1/L2/L3 HOT PATH</text>
        {tradePackets.map((p, i) => (
          <rect key={i} x={p.x} y={p.y - p.size/2} width={p.size * 4} height={p.size} rx={1} fill="#C0A040" opacity={0.7} />
        ))}

        {/* Divider */}
        <line x1={10} y1={90} x2={390} y2={90} stroke="#555" strokeWidth={0.5} strokeDasharray="4 4" />
        <text x={200} y={95} fill="#DC2626" fontSize={7} fontFamily="'JetBrains Mono'" textAnchor="middle" opacity={0.5}>
          ━━━ CACHE BOUNDARY (NT-STORE) ━━━
        </text>

        {/* Sideband lane */}
        <rect x={10} y={110} width={380} height={50} rx={6} fill="#A855F7" fillOpacity={0.05} stroke="#A855F7" strokeOpacity={0.2} strokeWidth={1} />
        <text x={20} y={105} fill="#A855F7" fontSize={9} fontFamily="'JetBrains Mono'" opacity={0.6}>L1-SIDEBAND — BYPASS TO MEMORY CONTROLLER</text>
        {binaryFrames.map((p, i) => (
          <rect key={i} x={p.x} y={p.y - p.size/2} width={p.size * 3} height={p.size} rx={2} fill="#A855F7" opacity={0.5}>
            <animate attributeName="opacity" values="0.3;0.7;0.3" dur={`${1.5 + i * 0.5}s`} repeatCount="indefinite" />
          </rect>
        ))}

        {/* Labels */}
        <text x={20} y={175} fill="#10B981" fontSize={8} fontFamily="'JetBrains Mono'" opacity={0.5}>
          ✓ Zero L3 Cache Pollution · MOVNTDQA · Hugepage-backed
        </text>
      </svg>
    </div>
  );
}
