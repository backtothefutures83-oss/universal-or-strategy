import { useState, useEffect, useRef, useCallback } from 'react';
import type { MetricPoint, SafetyInvariant, StripingMode } from '../types';

export function usePerformanceSimulation() {
  const [latencyData, setLatencyData] = useState<MetricPoint[]>([]);
  const [throughputData, setThroughputData] = useState<MetricPoint[]>([]);
  const [contentionData, setContentionData] = useState<MetricPoint[]>([]);
  const [stripingMode, setStripingMode] = useState<StripingMode>({
    mode: 'L1_LOCAL',
    latency: 0.42,
    throughput: 98.7,
    contention: 2.1,
  });
  const [handshakeCount, setHandshakeCount] = useState(0);
  const [currentPhase, setCurrentPhase] = useState(0);

  const phaseNames = ['HW_DETECT', 'CACHE_PROBE', 'TSO_VERIFY', 'STRIPING_INIT', 'FENCELESS_GO'];
  
  const tick = useRef(0);

  const simulateTick = useCallback(() => {
    tick.current += 1;
    const t = tick.current;
    const now = Date.now();

    // Latency: oscillate around 0.42ns with occasional spikes
    const latency = 0.42 + Math.sin(t * 0.1) * 0.03 + (Math.random() - 0.5) * 0.02;
    setLatencyData(prev => [...prev.slice(-60), { timestamp: now, value: latency }]);

    // Throughput: high but fluctuating
    const throughput = 99.2 + Math.sin(t * 0.05) * 0.5 + (Math.random() - 0.5) * 0.3;
    setThroughputData(prev => [...prev.slice(-60), { timestamp: now, value: throughput }]);

    // Contention: low with occasional bursts
    const contention = Math.max(0.1, 2.0 + Math.sin(t * 0.15) * 1.0 + Math.random() * 0.5);
    setContentionData(prev => [...prev.slice(-60), { timestamp: now, value: contention }]);

    // Adaptive striping based on contention
    if (contention > 3.0) {
      setStripingMode({ mode: 'L2_STRIPED', latency: 0.48, throughput: 97.5, contention });
    } else if (contention > 2.5) {
      setStripingMode({ mode: 'L3_DISTRIBUTED', latency: 0.45, throughput: 98.2, contention });
    } else {
      setStripingMode({ mode: 'L1_LOCAL', latency: 0.42, throughput: 99.2, contention });
    }

    setHandshakeCount(prev => prev + Math.floor(Math.random() * 1200 + 800));
    setCurrentPhase(t % 5);
  }, []);

  useEffect(() => {
    // Initial boot sequence
    const boot = setTimeout(() => {
      for (let i = 0; i < 30; i++) {
        simulateTick();
      }
    }, 500);

    const interval = setInterval(simulateTick, 200);
    return () => {
      clearInterval(interval);
      clearTimeout(boot);
    };
  }, [simulateTick]);

  const safetyInvariants: SafetyInvariant[] = [
    {
      id: 'TSO-PARITY',
      name: 'Hardware TSO Parity Check',
      status: 'PASS',
      description: 'Total Store Order verified across all sockets via sequence-differencing',
      latency: 0.00,
    },
    {
      id: 'SHADOW-BITS',
      name: 'Bitwise Sequence-Shadow Validation',
      status: 'PASS',
      description: 'Shadow bit vectors confirm zero-copy integrity without barriers',
      latency: 0.00,
    },
    {
      id: 'NUMA-DIST',
      name: 'NUMA Distance-Aware Alignment',
      status: 'PASS',
      description: 'Cross-node access latencies within bounded tolerance',
      latency: 0.00,
    },
    {
      id: 'FENCELESS',
      name: 'ADR-015 Fence-Less Discipline',
      status: 'PASS',
      description: 'No MemoryBarrier/Interlocked/volatile-barriers detected in hot path',
      latency: 0.00,
    },
    {
      id: 'CACHE-LINE',
      name: 'Auto-Aligned Cache Line Stripes',
      status: 'PASS',
      description: 'L1/L2/L3 line widths dynamically detected and honored',
      latency: 0.00,
    },
    {
      id: 'SOCKET-SAFE',
      name: 'Multi-Socket Safety Invariant',
      status: 'PASS',
      description: 'Fence-less model proven safe across heterogeneous topologies',
      latency: 0.00,
    },
  ];

  return {
    latencyData,
    throughputData,
    contentionData,
    stripingMode,
    handshakeCount,
    currentPhase,
    phaseNames,
    safetyInvariants,
  };
}
