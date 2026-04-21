import { useState, useEffect, useRef, useCallback } from 'react';
import type { BenchmarkResult, AdaptiveState, SafetyInvariant } from '../data/protocol';
import { HARDWARE_PROFILES } from '../data/protocol';

export function useSimulation() {
  const [selectedProfile, setSelectedProfile] = useState<string>('AMD EPYC 9654');
  const [isRunning, setIsRunning] = useState(false);
  const [benchmarks, setBenchmarks] = useState<BenchmarkResult[]>([]);
  const [adaptiveState, setAdaptiveState] = useState<AdaptiveState>({
    mode: 'L1-local',
    contentionScore: 0,
    switchThreshold: 500,
    lastSwitch: 0,
    stripeWidth: 64,
    activeStripes: 1,
  });
  const [safetyInvariants, setSafetyInvariants] = useState<SafetyInvariant[]>([
    { name: 'Sequence-Shadow XOR', status: 'pending', description: 'Monotonic counter XOR validation', metric: 'seqΔ | shadowΔ', threshold: '== 0', value: '—' },
    { name: 'TSO Parity Check', status: 'pending', description: 'Hardware-level TSO parity verification', metric: 'tsoΔ', threshold: '== 0', value: '—' },
    { name: 'Epoch Coherence', status: 'pending', description: 'Generation counter ABA detection', metric: 'epochΔ', threshold: '== 0', value: '—' },
    { name: 'FenceCount Invariant', status: 'pending', description: 'ADR-015: Zero fence discipline', metric: 'fence_count', threshold: '== 0', value: '—' },
    { name: 'Cache Alignment', status: 'pending', description: 'Natural alignment to detected stripe', metric: 'addr % stripe', threshold: '== 0', value: '—' },
    { name: 'Ring Buffer Integrity', status: 'pending', description: 'Head-Tail consistency check', metric: 'head - tail', threshold: '>= 0', value: '—' },
  ]);
  const [avgLatency, setAvgLatency] = useState(0);
  const [minLatency, setMinLatency] = useState(0);
  const [maxLatency, setMaxLatency] = useState(0);
  const [p99Latency, setP99Latency] = useState(0);
  const [totalOps, setTotalOps] = useState(0);
  const [modeSwitches, setModeSwitches] = useState(0);

  const intervalRef = useRef<number | null>(null);
  const opsRef = useRef(0);

  const profile = HARDWARE_PROFILES[selectedProfile];

  const generateBenchmark = useCallback((): BenchmarkResult => {
    const contention = Math.random() * 1000;
    let mode: 'L1-local' | 'L2-striped' | 'L3-fallback';
    let baseLatency: number;

    if (contention < 300) {
      mode = 'L1-local';
      baseLatency = 0.28 + Math.random() * 0.15;
    } else if (contention < 700) {
      mode = 'L2-striped';
      baseLatency = 0.32 + Math.random() * 0.18;
    } else {
      mode = 'L3-fallback';
      baseLatency = 0.38 + Math.random() * 0.12;
    }

    return {
      timestamp: Date.now(),
      latencyNs: Math.round(baseLatency * 1000) / 1000,
      throughputMops: Math.round((1000 / baseLatency) * 100) / 100,
      cacheHitRate: 95 + Math.random() * 4.9,
      contentionLevel: contention,
      mode,
      safetyCheckPass: Math.random() > 0.001,
      fenceCount: 0,
    };
  }, []);

  const startSimulation = useCallback(() => {
    setIsRunning(true);
    opsRef.current = 0;
    setBenchmarks([]);
    setTotalOps(0);
    setModeSwitches(0);
    setAvgLatency(0);
    setMinLatency(Infinity);
    setMaxLatency(0);
    setP99Latency(0);

    intervalRef.current = window.setInterval(() => {
      const batch: BenchmarkResult[] = [];
      for (let i = 0; i < 5; i++) {
        batch.push(generateBenchmark());
      }

      setBenchmarks(prev => {
        const updated = [...prev, ...batch].slice(-200);
        
        // Calculate stats
        const latencies = updated.map(b => b.latencyNs);
        const avg = latencies.reduce((a, b) => a + b, 0) / latencies.length;
        const min = Math.min(...latencies);
        const max = Math.max(...latencies);
        const sorted = [...latencies].sort((a, b) => a - b);
        const p99 = sorted[Math.floor(sorted.length * 0.99)];

        setAvgLatency(Math.round(avg * 1000) / 1000);
        setMinLatency(Math.round(min * 1000) / 1000);
        setMaxLatency(Math.round(max * 1000) / 1000);
        setP99Latency(Math.round(p99 * 1000) / 1000);

        // Update adaptive state
        const last = batch[batch.length - 1];
        setAdaptiveState(_prev => ({
          mode: last.mode,
          contentionScore: Math.round(last.contentionLevel),
          switchThreshold: 500,
          lastSwitch: Date.now(),
          stripeWidth: profile.detectedStripWidth,
          activeStripes: last.mode === 'L1-local' ? 1 : last.mode === 'L2-striped' ? profile.numaNodes.length : profile.numaNodes.length * 2,
        }));

        // Update safety invariants
        setSafetyInvariants(prev => prev.map(inv => ({
          ...inv,
          status: last.safetyCheckPass ? 'pass' : 'fail',
          value: last.safetyCheckPass ? '✓ PASS' : '✗ FAIL',
        })));

        // Track mode switches
        setModeSwitches(prevCount => {
          if (prevCount === 0) return 1;
          const prevMode = updated[updated.length - 6]?.mode;
          return prevMode !== last.mode ? prevCount + 1 : prevCount;
        });

        return updated;
      });

      opsRef.current += 5;
      setTotalOps(opsRef.current * 1000000); // Simulated ops
    }, 100);
  }, [generateBenchmark, profile]);

  const stopSimulation = useCallback(() => {
    setIsRunning(false);
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  useEffect(() => {
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, []);

  return {
    profile,
    selectedProfile,
    setSelectedProfile,
    isRunning,
    startSimulation,
    stopSimulation,
    benchmarks,
    adaptiveState,
    safetyInvariants,
    stats: { avgLatency, minLatency, maxLatency, p99Latency, totalOps, modeSwitches },
  };
}
