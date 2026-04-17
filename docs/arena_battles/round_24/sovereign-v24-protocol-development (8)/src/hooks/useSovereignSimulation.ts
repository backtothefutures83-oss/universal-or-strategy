import { useState, useEffect, useCallback, useRef } from 'react';

// ─── Hardware Topology Types ───

export interface CacheLevel {
  level: number;
  lineSize: number;
  size: string;
  associativity: number;
  hitRate: number;
  latency: number; // in ns
}

export interface NumaNode {
  id: number;
  cores: number[];
  distance: number[];
  memoryBandwidth: string;
  utilization: number;
}

export interface CpuSocket {
  id: number;
  architecture: string;
  cores: number;
  l1: CacheLevel;
  l2: CacheLevel;
  l3: CacheLevel;
  numaNodes: NumaNode[];
  tsoParity: number;
}

export interface TopologyData {
  sockets: CpuSocket[];
  detectedLineSize: number;
  crossSocketLatency: number;
  numaAwareness: boolean;
}

// ─── Latency Data Types ───

export interface LatencyPoint {
  time: string;
  latency: number;
  mode: 'L1-local' | 'L2-striped' | 'L3-fallback';
}

// ─── Striping Types ───

export interface StripeConfig {
  mode: 'L1-local' | 'L2-striped' | 'L3-fallback';
  stripeSize: number;
  activeStripes: number;
  contentionScore: number;
  switchThreshold: number;
  lastSwitch: string;
}

// ─── Safety Invariant Types ───

export interface InvariantCheck {
  id: string;
  name: string;
  status: 'pass' | 'fail' | 'checking';
  bitwise: string;
  duration: number;
}

// ─── Telemetry Types ───

export interface TelemetryData {
  throughput: number;
  p50: number;
  p99: number;
  p999: number;
  maxLatency: number;
  cacheHitRate: number;
  stripeSwitches: number;
  invariantChecks: number;
  invariantPassRate: number;
  fenceCount: number;
  atomicOpsCount: number;
  cpuCycles: number;
}

// ─── Simulation Engine ───

function generateTopology(): TopologyData {
  const lineSize = [64, 128, 256][Math.floor(Math.random() * 3)];
  
  return {
    sockets: [
      {
        id: 0,
        architecture: 'x86-64-v4',
        cores: 64,
        l1: { level: 1, lineSize, size: '48 KB', associativity: 12, hitRate: 99.7, latency: 0.5 },
        l2: { level: 2, lineSize, size: '1280 KB', associativity: 20, hitRate: 97.3, latency: 1.2 },
        l3: { level: 3, lineSize, size: '256 MB', associativity: 16, hitRate: 89.1, latency: 4.8 },
        numaNodes: [
          { id: 0, cores: Array.from({length: 32}, (_, i) => i), distance: [10, 21], memoryBandwidth: '204.8 GB/s', utilization: 34 + Math.random() * 20 },
          { id: 1, cores: Array.from({length: 32}, (_, i) => i + 32), distance: [21, 10], memoryBandwidth: '204.8 GB/s', utilization: 28 + Math.random() * 18 },
        ],
        tsoParity: 0x00,
      },
      {
        id: 1,
        architecture: 'ARM Neoverse V2',
        cores: 128,
        l1: { level: 1, lineSize, size: '64 KB', associativity: 16, hitRate: 99.5, latency: 0.4 },
        l2: { level: 2, lineSize, size: '1024 KB', associativity: 16, hitRate: 96.8, latency: 1.0 },
        l3: { level: 3, lineSize, size: '512 MB', associativity: 32, hitRate: 91.2, latency: 3.2 },
        numaNodes: [
          { id: 2, cores: Array.from({length: 64}, (_, i) => i), distance: [10, 18, 25, 25], memoryBandwidth: '409.6 GB/s', utilization: 42 + Math.random() * 15 },
          { id: 3, cores: Array.from({length: 64}, (_, i) => i + 64), distance: [18, 10, 25, 25], memoryBandwidth: '409.6 GB/s', utilization: 38 + Math.random() * 20 },
        ],
        tsoParity: 0x00,
      }
    ],
    detectedLineSize: lineSize,
    crossSocketLatency: 12 + Math.random() * 8,
    numaAwareness: true,
  };
}

function generateLatencyPoint(_prevLatency: number): LatencyPoint {
  const rand = Math.random();
  let mode: 'L1-local' | 'L2-striped' | 'L3-fallback';
  let baseLatency: number;
  
  if (rand < 0.82) {
    mode = 'L1-local';
    baseLatency = 0.32 + Math.random() * 0.14;
  } else if (rand < 0.97) {
    mode = 'L2-striped';
    baseLatency = 0.48 + Math.random() * 0.18;
  } else {
    mode = 'L3-fallback';
    baseLatency = 0.42 + Math.random() * 0.07; // Adaptive keeps it under 0.5
  }
  
  // Occasional micro-spike (simulating interrupt context switch)
  const spike = Math.random() < 0.02 ? 0.08 + Math.random() * 0.05 : 0;
  const latency = baseLatency + spike;
  
  const now = new Date();
  const time = `${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}.${now.getMilliseconds().toString().padStart(3, '0')}`;
  
  return { time, latency: Math.min(latency, 0.499), mode };
}

function generateStripingConfig(contentionScore: number): StripeConfig {
  const threshold = 0.65;
  const mode = contentionScore > threshold ? 'L2-striped' : 'L1-local';
  
  return {
    mode,
    stripeSize: mode === 'L1-local' ? 64 : 256,
    activeStripes: mode === 'L1-local' ? 1 : 4 + Math.floor(Math.random() * 8),
    contentionScore,
    switchThreshold: threshold,
    lastSwitch: `${new Date().toLocaleTimeString()}.${new Date().getMilliseconds()}`,
  };
}

function generateInvariantChecks(): InvariantCheck[] {
  return [
    { id: 'TSO-01', name: 'Hardware TSO Parity', status: 'pass', bitwise: '0x0000_0000', duration: 0.001 },
    { id: 'SSV-02', name: 'Sequence-Shadow Validation', status: 'pass', bitwise: '0xFF_FF_FF_FF', duration: 0.003 },
    { id: 'ADR-03', name: 'Atomicity-Durability Ring', status: 'pass', bitwise: '0xCA_FE_00_00', duration: 0.002 },
    { id: 'ZCP-04', name: 'Zero-Copy Parity Lock', status: 'pass', bitwise: '0xDE_AD_BE_EF', duration: 0.004 },
    { id: 'NUM-05', name: 'NUMA Distance Invariant', status: 'pass', bitwise: '0x0A_0B_0C_0D', duration: 0.002 },
    { id: 'CLN-06', name: 'Cache-Line Alignment', status: 'pass', bitwise: '0x00_00_01_00', duration: 0.001 },
  ];
}

function generateTelemetry(latencyHistory: LatencyPoint[]): TelemetryData {
  const latencies = latencyHistory.map(p => p.latency);
  const sorted = [...latencies].sort((a, b) => a - b);
  const len = sorted.length;
  
  return {
    throughput: 2_800_000_000 + Math.floor(Math.random() * 200_000_000),
    p50: len > 0 ? sorted[Math.floor(len * 0.50)] : 0.35,
    p99: len > 0 ? sorted[Math.floor(len * 0.99)] : 0.47,
    p999: len > 0 ? sorted[Math.floor(len * 0.999)] : 0.49,
    maxLatency: len > 0 ? sorted[len - 1] : 0.49,
    cacheHitRate: 99.2 + Math.random() * 0.7,
    stripeSwitches: Math.floor(Math.random() * 500),
    invariantChecks: Math.floor(Math.random() * 1_000_000),
    invariantPassRate: 100,
    fenceCount: 0,
    atomicOpsCount: 0,
    cpuCycles: 3 + Math.floor(Math.random() * 2),
  };
}

// ─── Main Hook ───

export function useSovereignSimulation() {
  const [topology] = useState<TopologyData>(generateTopology);
  const [latencyHistory, setLatencyHistory] = useState<LatencyPoint[]>([]);
  const [currentLatency, setCurrentLatency] = useState<LatencyPoint>({
    time: '--:--:--.---', latency: 0, mode: 'L1-local'
  });
  const [striping, setStriping] = useState<StripeConfig>(generateStripingConfig(0.4));
  const [invariants, setInvariants] = useState<InvariantCheck[]>(generateInvariantChecks);
  const [contentionScore, setContentionScore] = useState(0.4);
  const [isRunning, setIsRunning] = useState(true);
  const [tick, setTick] = useState(0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const startSimulation = useCallback(() => {
    setIsRunning(true);
  }, []);

  const stopSimulation = useCallback(() => {
    setIsRunning(false);
  }, []);

  const resetSimulation = useCallback(() => {
    setLatencyHistory([]);
    setTick(0);
    setCurrentLatency({ time: '--:--:--.---', latency: 0, mode: 'L1-local' });
    setStriping(generateStripingConfig(0.4));
    setContentionScore(0.4);
  }, []);

  useEffect(() => {
    if (!isRunning) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      return;
    }

    intervalRef.current = setInterval(() => {
      setTick(p => p + 1);
      
      // Update contention with smooth drift
      setContentionScore(prev => {
        const drift = (Math.random() - 0.48) * 0.08;
        const newScore = Math.max(0.1, Math.min(0.95, prev + drift));
        return newScore;
      });
      
      // Generate new latency point
      const newPoint = generateLatencyPoint(currentLatency.latency);
      setCurrentLatency(newPoint);
      setLatencyHistory(prev => {
        const updated = [...prev, newPoint];
        return updated.slice(-120); // Keep last 120 points
      });

      // Update striping based on contention
      setStriping(() => {
        const newContention = contentionScore;
        return generateStripingConfig(newContention);
      });

      // Periodically refresh invariants
      setTick(t => {
        if (t % 50 === 0) {
          setInvariants(generateInvariantChecks());
        }
        return t;
      });
    }, 80); // ~12.5 updates per second

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [isRunning, contentionScore, currentLatency.latency]);

  const telemetry = generateTelemetry(latencyHistory);

  return {
    topology,
    latencyHistory,
    currentLatency,
    striping,
    invariants,
    contentionScore,
    isRunning,
    tick,
    telemetry,
    startSimulation,
    stopSimulation,
    resetSimulation,
  };
}
