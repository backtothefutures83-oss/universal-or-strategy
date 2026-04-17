export interface Breakthrough {
  round: string;
  name: string;
  latency: number;
  category: 'dispatch' | 'recovery' | 'queue' | 'memory' | 'cache';
  description: string;
  active: boolean;
}

export const breakthroughs: Breakthrough[] = [
  { round: 'V7', name: 'Bitwise Jump-Dispatch', latency: 490, category: 'dispatch', description: 'Branch-free opcode routing via bit-shift tables', active: true },
  { round: 'V7', name: 'Mesh Self-Repair Matrix', latency: 320, category: 'recovery', description: 'Autonomous mesh topology with self-healing links', active: true },
  { round: 'V8', name: 'Ring-Bus SPSC Queue', latency: 487, category: 'queue', description: 'Single-producer single-consumer lock-free ring buffer', active: true },
  { round: 'V8', name: '1000ns Autonomous Error Recovery', latency: 500, category: 'recovery', description: 'Periodic heartbeat-based stall detection', active: true },
  { round: 'V9', name: 'FPGA-Parity Bitwise Pass', latency: 243, category: 'dispatch', description: 'Hardware-parity validated bit-level dispatch gate', active: true },
  { round: 'V9', name: 'Memory-Mapped Uint32 Arena', latency: 250, category: 'memory', description: 'Pre-allocated typed-array arena with zero GC pressure', active: true },
  { round: 'V9', name: 'Branchless Dispatch Gate', latency: 250, category: 'dispatch', description: 'CMOV-style conditional-move dispatch eliminating branch predictor misses', active: true },
  { round: 'V9', name: 'L1-D Cache Pre-Touch', latency: 250, category: 'cache', description: 'Prefetch intrinsics warming L1 data cache before dispatch', active: true },
];

export interface V10Design {
  name: string;
  mechanism: string;
  projectedLatency: number;
  textRendering: string;
  workerRecovery: string;
}

export const v10Design: V10Design = {
  name: 'Photon-Gate Dispatch Engine',
  mechanism: 'Userspace Ring + NUMA-Local Arena Fusion',
  projectedLatency: 187,
  textRendering: 'Canvas 2D Direct-Write',
  workerRecovery: 'Per-Worker Atomic Epoch Watchdog',
};
