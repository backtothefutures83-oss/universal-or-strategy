export type Role = 'Ingress' | 'Router' | 'Transform' | 'Actor' | 'Egress' | 'Mirror';

export interface Node {
  id: number;
  role: Role;
  coreId: number;
  isHotPath: boolean;
  load: number; // 0-100
  latency: number; // in microseconds
}

export interface Pipe {
  from: number;
  to: number;
  bandwidth: number;
  utilization: number;
}

export type MeshMode = '12-CORE' | '6-CORE' | '4-CORE';

export const ROLE_LATENCY_BUDGET: Record<Role, number> = {
  'Ingress': 1.2,
  'Router': 0.8,
  'Transform': 2.5,
  'Actor': 3.0,
  'Egress': 1.5,
  'Mirror': 1.0,
};
