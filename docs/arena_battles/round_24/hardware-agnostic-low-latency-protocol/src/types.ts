export interface CacheTopology {
  l1LineSize: number;
  l2LineSize: number;
  l3LineSize: number;
  numaNodes: NUMANode[];
  cpuTopology: CPUTopology;
}

export interface NUMANode {
  id: number;
  cores: number;
  distance: number[];
  localLatency: number;
  remoteLatency: number;
}

export interface CPUTopology {
  sockets: number;
  coresPerSocket: number;
  threadsPerCore: number;
  totalCores: number;
  tsoCapable: boolean;
}

export interface StripingMode {
  mode: 'L1_LOCAL' | 'L2_STRIPED' | 'L3_DISTRIBUTED' | 'NUMA_OPTIMIZED';
  latency: number;
  throughput: number;
  contention: number;
}

export interface SafetyInvariant {
  id: string;
  name: string;
  status: 'PASS' | 'CHECKING' | 'WARN';
  description: string;
  latency: number;
}

export interface MetricPoint {
  timestamp: number;
  value: number;
}

export interface ProtocolPhase {
  id: number;
  name: string;
  cycles: number;
  description: string;
}
