import type { TelemetryData } from '../hooks/useSovereignSimulation';

interface Props {
  telemetry: TelemetryData;
}

function formatNumber(n: number): string {
  if (n >= 1_000_000_000) return `${(n / 1_000_000_000).toFixed(1)}B`;
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(1)}K`;
  return n.toFixed(0);
}

export function TelemetryPanel({ telemetry }: Props) {
  const metrics = [
    { label: 'Throughput', value: `${formatNumber(telemetry.throughput)} ops/s`, color: 'text-sov-cyan', icon: '📊' },
    { label: 'P50 Latency', value: `${telemetry.p50.toFixed(3)} ns`, color: 'text-sov-green', icon: '⏱️' },
    { label: 'P99 Latency', value: `${telemetry.p99.toFixed(3)} ns`, color: telemetry.p99 < 0.5 ? 'text-sov-green' : 'text-sov-amber', icon: '⏱️' },
    { label: 'P99.9 Latency', value: `${telemetry.p999.toFixed(3)} ns`, color: telemetry.p999 < 0.5 ? 'text-sov-green' : 'text-sov-amber', icon: '⏱️' },
    { label: 'Max Latency', value: `${telemetry.maxLatency.toFixed(3)} ns`, color: telemetry.maxLatency < 0.5 ? 'text-sov-green' : 'text-sov-red', icon: '⚠️' },
    { label: 'Cache Hit Rate', value: `${telemetry.cacheHitRate.toFixed(2)}%`, color: 'text-sov-cyan', icon: '💾' },
    { label: 'Fence Count', value: `${telemetry.fenceCount}`, color: 'text-sov-green', icon: '🚫' },
    { label: 'Atomic Ops', value: `${telemetry.atomicOpsCount}`, color: 'text-sov-green', icon: '🚫' },
    { label: 'CPU Cycles', value: `${telemetry.cpuCycles} cyc`, color: 'text-sov-amber', icon: '⚙️' },
    { label: 'Stripe Switches', value: formatNumber(telemetry.stripeSwitches), color: 'text-sov-text', icon: '🔄' },
    { label: 'Invariant Checks', value: formatNumber(telemetry.invariantChecks), color: 'text-sov-text', icon: '🛡️' },
    { label: 'Pass Rate', value: `${telemetry.invariantPassRate}%`, color: 'text-sov-green', icon: '✅' },
  ];

  return (
    <div className="panel-glow rounded-xl border border-sov-border bg-sov-panel p-5 relative overflow-hidden">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-sov-blue/20 to-sov-blue/5 border border-sov-blue/30 flex items-center justify-center text-sm">
          📈
        </div>
        <div>
          <h3 className="text-sm font-semibold text-sov-text">Telemetry</h3>
          <p className="text-xs text-sov-text-muted font-mono">Real-Time Performance Metrics</p>
        </div>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
        {metrics.map((metric) => (
          <div key={metric.label} className="bg-sov-dark/40 rounded-lg p-2.5 border border-sov-border hover:border-sov-border-bright transition-colors">
            <div className="flex items-center gap-1.5 mb-1">
              <span className="text-xs">{metric.icon}</span>
              <span className="text-xs text-sov-text-muted">{metric.label}</span>
            </div>
            <div className={`text-base font-bold font-mono ${metric.color}`}>
              {metric.value}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
