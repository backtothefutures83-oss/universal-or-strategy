// ═══════════════════════════════════════════════════════════════════
//  SIMULATION ENGINE — Deterministic SPSC Metrics Generator
//  Simulates ring buffer state, latency, jitter, core metrics
// ═══════════════════════════════════════════════════════════════════

import type {
  MeshRole,
  SPSCChannel,
  CoreMetrics,
  TimingGate,
  JitterSample,
  RoleId,
} from './types';

// ─── Mesh Topology Definition ────────────────────────────────────
export const MESH_ROLES: MeshRole[] = [
  { id: 'ingress-a',        label: 'Ingress α',       category: 'ingress',   coreId: 0,  numaNode: 0, x: 0,   y: 0.3 },
  { id: 'ingress-b',        label: 'Ingress β',       category: 'ingress',   coreId: 1,  numaNode: 0, x: 0,   y: 0.7 },
  { id: 'router-iso',       label: 'Router ∅',        category: 'router',    coreId: 2,  numaNode: 0, x: 0.2, y: 0.5 },
  { id: 'transform-simd-a', label: 'SIMD Xform α',    category: 'transform', coreId: 3,  numaNode: 0, x: 0.4, y: 0.3 },
  { id: 'transform-simd-b', label: 'SIMD Xform β',    category: 'transform', coreId: 4,  numaNode: 0, x: 0.4, y: 0.7 },
  { id: 'actor-pin-a',      label: 'Actor ⊕ A',       category: 'actor',     coreId: 5,  numaNode: 1, x: 0.6, y: 0.2 },
  { id: 'actor-pin-b',      label: 'Actor ⊕ B',       category: 'actor',     coreId: 6,  numaNode: 1, x: 0.6, y: 0.5 },
  { id: 'actor-pin-c',      label: 'Actor ⊕ C',       category: 'actor',     coreId: 7,  numaNode: 1, x: 0.6, y: 0.8 },
  { id: 'egress-a',         label: 'Egress α',        category: 'egress',    coreId: 8,  numaNode: 1, x: 0.8, y: 0.35 },
  { id: 'egress-b',         label: 'Egress β',        category: 'egress',    coreId: 9,  numaNode: 1, x: 0.8, y: 0.65 },
  { id: 'mirror-a',         label: 'Mirror ◇ A',      category: 'mirror',    coreId: 10, numaNode: 1, x: 1.0, y: 0.3 },
  { id: 'mirror-b',         label: 'Mirror ◇ B',      category: 'mirror',    coreId: 11, numaNode: 1, x: 1.0, y: 0.7 },
];

// ─── SPSC Channel Definitions (1-to-1 only) ─────────────────────
const CHANNEL_DEFS: { from: RoleId; to: RoleId }[] = [
  { from: 'ingress-a',        to: 'router-iso' },
  { from: 'ingress-b',        to: 'router-iso' },
  { from: 'router-iso',       to: 'transform-simd-a' },
  { from: 'router-iso',       to: 'transform-simd-b' },
  { from: 'transform-simd-a', to: 'actor-pin-a' },
  { from: 'transform-simd-a', to: 'actor-pin-b' },
  { from: 'transform-simd-b', to: 'actor-pin-b' },
  { from: 'transform-simd-b', to: 'actor-pin-c' },
  { from: 'actor-pin-a',      to: 'egress-a' },
  { from: 'actor-pin-b',      to: 'egress-a' },
  { from: 'actor-pin-b',      to: 'egress-b' },
  { from: 'actor-pin-c',      to: 'egress-b' },
  { from: 'egress-a',         to: 'mirror-a' },
  { from: 'egress-b',         to: 'mirror-b' },
];

// ─── Simulation State ────────────────────────────────────────────
let tick = 0;
const RING_CAPACITY = 64; // power of 2, cache-line aligned

function jitteredNs(base: number, jitter: number): number {
  return base + (Math.random() - 0.5) * 2 * jitter;
}

export function simulateChannels(): SPSCChannel[] {
  tick++;
  return CHANNEL_DEFS.map((def) => {
    const writeHead = (tick * 3 + Math.floor(Math.random() * 4)) % RING_CAPACITY;
    const readHead = (writeHead - Math.floor(Math.random() * 8) + RING_CAPACITY) % RING_CAPACITY;
    const baseLatency = def.from.includes('mirror') || def.to.includes('mirror') ? 120 : 60;
    return {
      ...def,
      bufferSizeSlots: RING_CAPACITY,
      slotSizeBytes: 64, // cache-line
      latencyNs: jitteredNs(baseLatency, 25),
      jitterNs: jitteredNs(8, 5),
      throughputMps: jitteredNs(14.2, 2),
      writeHead,
      readHead,
      capacity: RING_CAPACITY,
    };
  });
}

export function simulateCoreMetrics(): CoreMetrics[] {
  tick++;
  return MESH_ROLES.map((role) => {
    const l3Miss = Math.random() < 0.03; // 3% L3 miss rate
    return {
      roleId: role.id,
      coreId: role.coreId,
      spinCycles: Math.floor(jitteredNs(180, 40)),
      l3Misses: l3Miss ? Math.floor(Math.random() * 3) + 1 : 0,
      ipc: jitteredNs(3.8, 0.4),
      pollLatencyNs: jitteredNs(l3Miss ? 280 : 45, l3Miss ? 80 : 10),
      cacheHitRate: l3Miss ? jitteredNs(0.94, 0.03) : jitteredNs(0.998, 0.001),
      heapAllocBytes: 0, // ZERO-HEAP invariant
    };
  });
}

export function simulateTimingGate(): TimingGate {
  const ingressNs = jitteredNs(85, 20);
  const routerNs = jitteredNs(110, 30);
  const transformNs = jitteredNs(180, 40);
  const actorNs = jitteredNs(250, 50);
  const egressNs = jitteredNs(140, 30);
  const totalPassNs = ingressNs + routerNs + transformNs + actorNs + egressNs;
  const fail = totalPassNs > 1000;
  const warn = totalPassNs > 850;

  return {
    totalPassNs,
    breakdown: { ingressNs, routerNs, transformNs, actorNs, egressNs },
    csCount: 0,
    interruptCount: 0,
    verdict: fail ? 'FAIL' : warn ? 'WARN' : 'PASS',
  };
}

export function simulateJitterSamples(count: number): JitterSample[] {
  const now = Date.now();
  return Array.from({ length: count }, (_, i) => {
    const l3Miss = Math.random() < 0.03;
    return {
      timestamp: now - (count - i) * 100,
      latencyNs: jitteredNs(l3Miss ? 650 : 420, l3Miss ? 200 : 80),
      l3Miss,
    };
  });
}
