// ═══════════════════════════════════════════════════════════════════
//  ARCHITECTURE SPEC — Code-aligned reference implementation
//  SPSC ring buffer pseudocode, banned constructs, invariants
// ═══════════════════════════════════════════════════════════════════

export default function ArchitectureSpec() {
  return (
    <div className="p-3 h-full flex flex-col font-mono overflow-y-auto text-[10px] leading-relaxed">
      {/* Ring buffer implementation */}
      <div className="mb-3">
        <div className="text-[9px] text-cyan-600 uppercase tracking-wider mb-1.5">
          ▸ SPSC Ring Buffer — SharedArrayBuffer
        </div>
        <pre className="bg-slate-950 rounded border border-slate-800 p-2 text-[9px] overflow-x-auto">
          <code>{`// Cache-line aligned slots: 64B each
// Power-of-2 capacity for bitwise modulo
const CAPACITY = 64;          // slots
const SLOT_SIZE = 64;         // bytes (1 cache line)
const MASK = CAPACITY - 1;    // bitwise mod

// SharedArrayBuffer layout:
// [0]       = write_head  (Atomics)
// [1]       = read_head   (Atomics)
// [64..end] = slot_data[] (zero-copy)

function spin_publish(sab, data, slot_size) {
  const wh = Atomics.load(head, 0);
  const rh = Atomics.load(head, 1);
  // FULL check: (wh + 1) & MASK === rh
  if (((wh + 1) & MASK) === rh) return false;
  // Zero-copy write into slot
  const offset = 64 + (wh & MASK) * slot_size;
  new Uint8Array(sab, offset, slot_size).set(data);
  // Release-store write head
  Atomics.store(head, 0, (wh + 1) & MASK);
  return true;
}

function spin_consume(sab, slot_size) {
  const wh = Atomics.load(head, 0);
  const rh = Atomics.load(head, 1);
  // EMPTY check
  if (wh === rh) return null;
  // Zero-copy read from slot
  const offset = 64 + (rh & MASK) * slot_size;
  const view = new Uint8Array(sab, offset, slot_size);
  // Advance read head
  Atomics.store(head, 1, (rh + 1) & MASK);
  return view; // zero-copy reference
}`}</code>
        </pre>
      </div>

      {/* Spin-poll loop */}
      <div className="mb-3">
        <div className="text-[9px] text-amber-600 uppercase tracking-wider mb-1.5">
          ▸ Spin-Poll Loop — Zero Context-Switch
        </div>
        <pre className="bg-slate-950 rounded border border-slate-800 p-2 text-[9px] overflow-x-auto">
          <code>{`// Each role runs a dedicated spin loop
// No yield, no sleep, no setTimeout
function sovereign_loop(role, channel_in, channel_out) {
  // BOOT PHASE: allocate all memory here
  const scratch = new ArrayBuffer(4096);
  // ─── ZERO-HEAP BARRIER ───
  // No allocations past this point
  
  while (true) {
    const msg = spin_consume(channel_in, 64);
    if (msg === null) continue; // spin-wait
    
    // Process in-place (zero-copy)
    const result = role.process(msg, scratch);
    
    // Spin-publish to next stage
    while (!spin_publish(channel_out, result, 64)) {
      // back-pressure: spin until slot available
    }
  }
}`}</code>
        </pre>
      </div>

      {/* Banned constructs */}
      <div className="mb-3">
        <div className="text-[9px] text-red-600 uppercase tracking-wider mb-1.5">
          ▸ Banned Constructs
        </div>
        <div className="grid grid-cols-2 gap-1">
          {[
            { construct: 'Worker.postMessage()', reason: 'Structured clone' },
            { construct: 'Mutex / Lock',          reason: 'Blocking primitive' },
            { construct: 'structuredClone()',      reason: 'Heap allocation' },
            { construct: 'new Object() in loop',   reason: 'GC pressure' },
            { construct: 'setTimeout / setInterval', reason: 'OS scheduler' },
            { construct: 'Promise.resolve()',      reason: 'Microtask queue' },
          ].map((b) => (
            <div key={b.construct}
              className="flex items-start gap-1 p-1 rounded bg-red-950/20 border border-red-900/30">
              <span className="text-red-500 mt-0.5">✕</span>
              <div>
                <div className="text-red-400 text-[8px]">{b.construct}</div>
                <div className="text-red-700 text-[7px]">{b.reason}</div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Invariants */}
      <div>
        <div className="text-[9px] text-emerald-600 uppercase tracking-wider mb-1.5">
          ▸ Enforced Invariants
        </div>
        <div className="space-y-1">
          {[
            'Total logic-pass ≤ 1000ns (1µs)',
            'Context switches = 0 (spin-poll only)',
            'OS interrupts = 0 (isolcpus + nohz_full)',
            'Heap allocations = 0 (post-boot)',
            'All channels are strict 1-to-1 SPSC',
            'All data transfer via SharedArrayBuffer',
            'Slot size = cache line (64 bytes)',
            'Ring capacity = power of 2 (bitwise mod)',
          ].map((inv) => (
            <div key={inv}
              className="flex items-center gap-1.5 text-[8px] text-emerald-500/80">
              <span className="text-emerald-600">✓</span>
              {inv}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
