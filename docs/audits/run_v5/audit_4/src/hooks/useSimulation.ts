import { useState, useEffect, useCallback, useRef } from 'react';

export interface PipeMetrics {
  latencyNs: number;
  throughputGbps: number;
  jitterNs: number;
  cacheMissRate: number;
  slabUtilization: number;
}

export interface ActorState {
  id: string;
  core: number;
  status: 'active' | 'stalled' | 'mirror-inject' | 'idle';
  messagesProcessed: number;
  currentLatencyNs: number;
}

export interface SimulationState {
  tick: number;
  actors: ActorState[];
  pipeMetrics: PipeMetrics;
  mirrorActive: boolean;
  mirrorTakeoverUs: number;
  nmiDetected: boolean;
  l1SidebandActive: boolean;
  gatePassed: boolean;
  gateTimeNs: number;
}

const INITIAL_ACTORS: ActorState[] = [
  { id: 'INGRESS', core: 1, status: 'active', messagesProcessed: 0, currentLatencyNs: 45 },
  { id: 'PARSER', core: 2, status: 'active', messagesProcessed: 0, currentLatencyNs: 62 },
  { id: 'STRATEGY', core: 3, status: 'active', messagesProcessed: 0, currentLatencyNs: 180 },
  { id: 'RISK', core: 4, status: 'active', messagesProcessed: 0, currentLatencyNs: 95 },
  { id: 'SHARD-A', core: 5, status: 'active', messagesProcessed: 0, currentLatencyNs: 120 },
  { id: 'SHARD-B', core: 6, status: 'active', messagesProcessed: 0, currentLatencyNs: 115 },
  { id: 'SHARD-C', core: 7, status: 'active', messagesProcessed: 0, currentLatencyNs: 130 },
  { id: 'SHARD-D', core: 8, status: 'active', messagesProcessed: 0, currentLatencyNs: 110 },
  { id: 'EGRESS', core: 9, status: 'active', messagesProcessed: 0, currentLatencyNs: 50 },
  { id: 'AUDIT', core: 10, status: 'active', messagesProcessed: 0, currentLatencyNs: 35 },
  { id: 'MIRROR', core: 11, status: 'idle', messagesProcessed: 0, currentLatencyNs: 0 },
  { id: 'SIDEBAND', core: 12, status: 'active', messagesProcessed: 0, currentLatencyNs: 200 },
];

export function useSimulation() {
  const [state, setState] = useState<SimulationState>({
    tick: 0,
    actors: INITIAL_ACTORS,
    pipeMetrics: {
      latencyNs: 780,
      throughputGbps: 42.5,
      jitterNs: 12,
      cacheMissRate: 0.02,
      slabUtilization: 0.73,
    },
    mirrorActive: false,
    mirrorTakeoverUs: 0,
    nmiDetected: false,
    l1SidebandActive: true,
    gatePassed: true,
    gateTimeNs: 780,
  });

  const nmiTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const interval = setInterval(() => {
      setState(prev => {
        const newTick = prev.tick + 1;
        const actors = prev.actors.map(a => ({
          ...a,
          messagesProcessed: a.status === 'active' ? a.messagesProcessed + Math.floor(Math.random() * 3 + 1) : a.messagesProcessed,
          currentLatencyNs: a.status === 'active'
            ? a.currentLatencyNs + (Math.random() - 0.5) * 10
            : a.currentLatencyNs,
        }));

        const baseLatency = 780;
        const jitter = Math.sin(newTick * 0.1) * 15 + (Math.random() - 0.5) * 20;

        return {
          ...prev,
          tick: newTick,
          actors,
          pipeMetrics: {
            latencyNs: Math.max(650, baseLatency + jitter),
            throughputGbps: 42.5 + Math.sin(newTick * 0.05) * 2,
            jitterNs: Math.abs(jitter),
            cacheMissRate: 0.02 + Math.random() * 0.01,
            slabUtilization: 0.73 + Math.sin(newTick * 0.03) * 0.05,
          },
          gateTimeNs: Math.max(650, baseLatency + jitter),
          gatePassed: (baseLatency + jitter) < 1000,
        };
      });
    }, 100);

    return () => clearInterval(interval);
  }, []);

  const triggerNMI = useCallback((shardId: string) => {
    setState(prev => ({
      ...prev,
      nmiDetected: true,
      actors: prev.actors.map(a =>
        a.id === shardId
          ? { ...a, status: 'stalled' as const }
          : a.id === 'MIRROR'
          ? { ...a, status: 'mirror-inject' as const }
          : a
      ),
    }));

    if (nmiTimeoutRef.current) clearTimeout(nmiTimeoutRef.current);
    nmiTimeoutRef.current = setTimeout(() => {
      setState(prev => ({
        ...prev,
        mirrorActive: true,
        mirrorTakeoverUs: 1.47 + Math.random() * 0.4,
        actors: prev.actors.map(a =>
          a.status === 'stalled'
            ? { ...a, status: 'idle' as const }
            : a.id === 'MIRROR'
            ? { ...a, status: 'active' as const, core: prev.actors.find(x => x.status === 'stalled')?.core || 11 }
            : a
        ),
      }));

      setTimeout(() => {
        setState(prev => ({
          ...prev,
          nmiDetected: false,
          mirrorActive: false,
          mirrorTakeoverUs: 0,
          actors: INITIAL_ACTORS.map(a => ({
            ...a,
            messagesProcessed: prev.actors.find(x => x.id === a.id)?.messagesProcessed || 0,
          })),
        }));
      }, 3000);
    }, 1500);
  }, []);

  return { state, triggerNMI };
}
