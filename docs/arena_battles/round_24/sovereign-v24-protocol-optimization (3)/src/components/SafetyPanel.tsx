import { useSafetyValidation } from '../hooks/useSimulation';
import { cn } from '../utils/cn';

export default function SafetyPanel() {
  const { validations, successRate, totalChecks, passedChecks } = useSafetyValidation();

  return (
    <section id="safety" className="py-20 relative">
      <div className="max-w-6xl mx-auto px-4">
        <div className="text-center mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-sov-green/20 bg-sov-green/5 mb-4">
            <span className="text-xs font-mono text-sov-green">MANDATE #2</span>
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-sov-text-bright mb-3">
            Zero-Friction <span className="text-sov-green">Safety Invariants</span>
          </h2>
          <p className="text-sov-text-dim max-w-xl mx-auto">
            Non-latency-summing bitwise sequence-shadow validation with hardware-level TSO parity.
          </p>
        </div>

        {/* Success rate dashboard */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-8">
          <div className="p-6 rounded-xl border border-sov-green/20 bg-sov-surface/50 text-center">
            <div className="text-xs font-mono text-sov-text-dim mb-2">SUCCESS RATE</div>
            <div className={cn(
              'text-4xl font-black font-mono',
              successRate >= 95 ? 'text-sov-green glow-green' : 'text-sov-amber'
            )}>
              {successRate.toFixed(1)}%
            </div>
            <div className="text-xs font-mono text-sov-text-dim mt-1">
              {passedChecks}/{totalChecks} validations
            </div>
          </div>

          <div className="p-6 rounded-xl border border-sov-border bg-sov-surface/50 text-center">
            <div className="text-xs font-mono text-sov-text-dim mb-2">INVARIANT 1</div>
            <div className="text-sm font-mono text-sov-text-bright mb-1">Sequence-Shadow Parity</div>
            <div className="text-xs font-mono text-sov-green">shadow = ~sequence</div>
            <div className="text-xs font-mono text-sov-text-dim mt-1">Detects torn writes</div>
          </div>

          <div className="p-6 rounded-xl border border-sov-border bg-sov-surface/50 text-center">
            <div className="text-xs font-mono text-sov-text-dim mb-2">INVARIANT 2</div>
            <div className="text-sm font-mono text-sov-text-bright mb-1">CRC-64 Payload Integrity</div>
            <div className="text-xs font-mono text-sov-purple">checksum = CRC64(seq ^ payload)</div>
            <div className="text-xs font-mono text-sov-text-dim mt-1">Detects corruption</div>
          </div>
        </div>

        {/* Validation log */}
        <div className="rounded-xl border border-sov-border bg-sov-surface/30 overflow-hidden">
          <div className="px-4 py-3 border-b border-sov-border flex items-center justify-between">
            <span className="text-xs font-mono text-sov-text-dim">VALIDATION STREAM</span>
            <span className="w-2 h-2 rounded-full bg-sov-green animate-glow-pulse" />
          </div>
          <div className="max-h-80 overflow-y-auto font-mono text-xs">
            {validations.map(v => (
              <div
                key={v.id}
                className={cn(
                  'px-4 py-2 border-b border-sov-border/50 flex flex-wrap gap-x-4 gap-y-1 items-center',
                  v.valid ? 'bg-sov-green/3' : 'bg-sov-red/5'
                )}
              >
                <span className={cn(
                  'w-2 h-2 rounded-full flex-shrink-0',
                  v.valid ? 'bg-sov-green' : 'bg-sov-red'
                )} />
                <span className="text-sov-text-dim">seq={v.seq}</span>
                <span className="text-sov-text-dim">shadow={v.shadow}</span>
                <span className="text-sov-text-dim">payload={v.payload}</span>
                <span className="text-sov-text-dim">crc={v.checksum}</span>
                <span className={cn(
                  'ml-auto px-2 py-0.5 rounded text-[10px]',
                  v.valid
                    ? 'bg-sov-green/10 text-sov-green border border-sov-green/20'
                    : 'bg-sov-red/10 text-sov-red border border-sov-red/20'
                )}>
                  {v.valid ? 'VALID' : 'INVALID'}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* Invariant explanation */}
        <div className="mt-8 p-6 rounded-xl border border-sov-border bg-sov-surface/30">
          <h3 className="text-sm font-semibold text-sov-text-bright mb-4 font-mono">
            HOW IT WORKS — FENCE-LESS SAFETY PROOF
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <div className="text-xs font-mono text-sov-cyan mb-2">WRITE PATH</div>
              <ol className="text-xs font-mono text-sov-text-dim space-y-1.5">
                <li className="flex gap-2"><span className="text-sov-cyan">1.</span> Read current sequence (atomic by alignment)</li>
                <li className="flex gap-2"><span className="text-sov-cyan">2.</span> Compute newSeq = seq + 1</li>
                <li className="flex gap-2"><span className="text-sov-cyan">3.</span> Compute shadow = ~newSeq</li>
                <li className="flex gap-2"><span className="text-sov-cyan">4.</span> Compute CRC-64 checksum</li>
                <li className="flex gap-2"><span className="text-sov-cyan">5.</span> Write shadow FIRST (safety gate)</li>
                <li className="flex gap-2"><span className="text-sov-cyan">6.</span> Write payload</li>
                <li className="flex gap-2"><span className="text-sov-cyan">7.</span> Write checksum</li>
                <li className="flex gap-2"><span className="text-sov-cyan">8.</span> Write sequence LAST (publish)</li>
              </ol>
            </div>
            <div>
              <div className="text-xs font-mono text-sov-green mb-2">READ PATH</div>
              <ol className="text-xs font-mono text-sov-text-dim space-y-1.5">
                <li className="flex gap-2"><span className="text-sov-green">1.</span> Snapshot sequence</li>
                <li className="flex gap-2"><span className="text-sov-green">2.</span> Snapshot shadow</li>
                <li className="flex gap-2"><span className="text-sov-green">3.</span> Validate: seq ^ shadow == 0xFFFF...</li>
                <li className="flex gap-2"><span className="text-sov-green">4.</span> Read payload (safe — seq was valid)</li>
                <li className="flex gap-2"><span className="text-sov-green">5.</span> Verify CRC-64 checksum</li>
                <li className="flex gap-2"><span className="text-sov-green">6.</span> Return data or retry</li>
              </ol>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
