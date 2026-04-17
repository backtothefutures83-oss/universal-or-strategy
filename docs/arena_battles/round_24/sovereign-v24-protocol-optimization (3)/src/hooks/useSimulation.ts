import { useState, useEffect, useRef, useCallback } from 'react';

export interface LatencySample {
  timestamp: number;
  value: number;
  mode: string;
}

export function useLatencySimulator() {
  const [samples, setSamples] = useState<LatencySample[]>([]);
  const [currentLatency, setCurrentLatency] = useState(0.31);
  const [avgLatency, setAvgLatency] = useState(0.31);
  const [minLatency, setMinLatency] = useState(0.18);
  const [maxLatency, setMaxLatency] = useState(0.47);
  const [mode, setMode] = useState('L1-Local');
  const [contention, setContention] = useState(12);
  const [isRunning, setIsRunning] = useState(true);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const samplesRef = useRef<LatencySample[]>([]);

  const generateSample = useCallback(() => {
    const modes = ['L1-Local', 'L1-Local', 'L1-Local', 'L2-Striped', 'L2-Striped', 'L3-Shared'];
    const newMode = modes[Math.floor(Math.random() * modes.length)];
    
    let baseLatency: number;
    switch (newMode) {
      case 'L1-Local':
        baseLatency = 0.18 + Math.random() * 0.15;
        break;
      case 'L2-Striped':
        baseLatency = 0.25 + Math.random() * 0.18;
        break;
      case 'L3-Shared':
        baseLatency = 0.35 + Math.random() * 0.15;
        break;
      default:
        baseLatency = 0.22 + Math.random() * 0.2;
    }
    
    // Occasional spike to simulate context switch
    const spike = Math.random() > 0.97 ? Math.random() * 0.3 : 0;
    const value = Math.min(baseLatency + spike, 0.49);
    
    const newContention = Math.floor(5 + Math.random() * 40);
    
    setMode(newMode);
    setCurrentLatency(value);
    setContention(newContention);
    
    const sample: LatencySample = {
      timestamp: Date.now(),
      value,
      mode: newMode,
    };
    
    const newSamples = [...samplesRef.current, sample].slice(-60);
    samplesRef.current = newSamples;
    setSamples(newSamples);
    
    const avg = newSamples.reduce((a, b) => a + b.value, 0) / newSamples.length;
    const min = Math.min(...newSamples.map(s => s.value));
    const max = Math.max(...newSamples.map(s => s.value));
    
    setAvgLatency(avg);
    setMinLatency(min);
    setMaxLatency(max);
  }, []);

  useEffect(() => {
    if (isRunning) {
      intervalRef.current = setInterval(generateSample, 200);
    }
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [isRunning, generateSample]);

  return {
    samples,
    currentLatency,
    avgLatency,
    minLatency,
    maxLatency,
    mode,
    contention,
    isRunning,
    setIsRunning,
  };
}

export interface TopologyNode {
  id: string;
  type: 'cpu' | 'l1' | 'l2' | 'l3' | 'numa' | 'memory';
  label: string;
  x: number;
  y: number;
  children: string[];
  detected?: boolean;
  cacheLine?: number;
  latency?: number;
}

export function useTopologyDetection() {
  const [detected, setDetected] = useState(false);
  const [progress, setProgress] = useState(0);
  const [nodes, setNodes] = useState<TopologyNode[]>([]);
  const [currentStep, setCurrentStep] = useState('');

  const detect = useCallback(() => {
    setDetected(false);
    setProgress(0);
    
    const steps = [
      { label: 'Reading CPUID leaf 0x80000006...', progress: 15, delay: 400 },
      { label: 'Detecting L1 cache line width...', progress: 25, delay: 350 },
      { label: 'Detecting L2 cache stripe width...', progress: 40, delay: 300 },
      { label: 'Detecting L3 shared cache width...', progress: 55, delay: 350 },
      { label: 'Enumerating NUMA topology...', progress: 70, delay: 400 },
      { label: 'Computing optimal stripe width...', progress: 85, delay: 300 },
      { label: 'Validating TSO support...', progress: 95, delay: 250 },
      { label: 'Topology detection complete.', progress: 100, delay: 200 },
    ];

    let i = 0;
    const runStep = () => {
      if (i >= steps.length) {
        setDetected(true);
        setNodes([
          { id: 'cpu0', type: 'cpu', label: 'CPU 0', x: 200, y: 100, children: ['l1_0', 'l1_1'], detected: true },
          { id: 'cpu1', type: 'cpu', label: 'CPU 1', x: 500, y: 100, children: ['l1_2', 'l1_3'], detected: true },
          { id: 'l1_0', type: 'l1', label: 'L1-I: 32KB', x: 100, y: 200, children: [], detected: true, cacheLine: 64, latency: 4 },
          { id: 'l1_1', type: 'l1', label: 'L1-D: 48KB', x: 200, y: 200, children: [], detected: true, cacheLine: 64, latency: 4 },
          { id: 'l1_2', type: 'l1', label: 'L1-I: 32KB', x: 400, y: 200, children: [], detected: true, cacheLine: 64, latency: 4 },
          { id: 'l1_3', type: 'l1', label: 'L1-D: 48KB', x: 500, y: 200, children: [], detected: true, cacheLine: 64, latency: 4 },
          { id: 'l2_0', type: 'l2', label: 'L2: 1.25MB', x: 150, y: 300, children: [], detected: true, cacheLine: 64, latency: 12 },
          { id: 'l2_1', type: 'l2', label: 'L2: 1.25MB', x: 450, y: 300, children: [], detected: true, cacheLine: 64, latency: 12 },
          { id: 'l3', type: 'l3', label: 'L3: 36MB Shared', x: 350, y: 400, children: [], detected: true, cacheLine: 64, latency: 40 },
          { id: 'numa0', type: 'numa', label: 'NUMA Node 0', x: 200, y: 500, children: [], detected: true, latency: 80 },
          { id: 'numa1', type: 'numa', label: 'NUMA Node 1', x: 500, y: 500, children: [], detected: true, latency: 80 },
          { id: 'mem', type: 'memory', label: 'DDR5-5600', x: 350, y: 600, children: [], detected: true, latency: 100 },
        ]);
        return;
      }
      
      setCurrentStep(steps[i].label);
      setProgress(steps[i].progress);
      i++;
      setTimeout(runStep, steps[i - 1].delay);
    };
    
    runStep();
  }, []);

  return { detected, progress, nodes, currentStep, detect };
}

export function useSafetyValidation() {
  const [validations, setValidations] = useState<Array<{
    id: number;
    seq: string;
    shadow: string;
    payload: string;
    checksum: string;
    valid: boolean;
    timestamp: number;
  }>>([]);
  const [successRate, setSuccessRate] = useState(100);
  const [totalChecks, setTotalChecks] = useState(0);
  const [passedChecks, setPassedChecks] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      const seq = Math.floor(Math.random() * 0xFFFFFFFF).toString(16).toUpperCase().padStart(16, '0');
      const payload = Math.floor(Math.random() * 0xFFFFFFFF).toString(16).toUpperCase().padStart(16, '0');
      
      // 98% success rate to simulate realistic behavior
      const isValid = Math.random() > 0.02;
      const shadow = isValid 
        ? (~BigInt('0x' + seq)).toString(16).toUpperCase().replace('-', '').padStart(16, '0')
        : Math.floor(Math.random() * 0xFFFFFFFF).toString(16).toUpperCase().padStart(16, '0');
      const checksum = Math.floor(Math.random() * 0xFFFFFFFF).toString(16).toUpperCase().padStart(16, '0');

      const check = {
        id: Date.now(),
        seq: '0x' + seq.slice(-16),
        shadow: '0x' + shadow.slice(-16),
        payload: '0x' + payload.slice(-16),
        checksum: '0x' + checksum.slice(-16),
        valid: isValid,
        timestamp: Date.now(),
      };

      setValidations(prev => [check, ...prev].slice(0, 20));
      setTotalChecks(prev => prev + 1);
      if (isValid) setPassedChecks(prev => prev + 1);
    }, 500);

    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    if (totalChecks > 0) {
      setSuccessRate((passedChecks / totalChecks) * 100);
    }
  }, [passedChecks, totalChecks]);

  return { validations, successRate, totalChecks, passedChecks };
}
