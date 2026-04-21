import { FileCode, ShieldCheck, Cpu } from 'lucide-react';

export const CodeViewer: React.FC = () => {
  const code = `// SOVEREIGN V24_ROBUST_CODE
// Zero-Friction Handshake Pipeline (< 0.5ns)
// ADR-015 Compliant: Total Fence-Less Discipline

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SovereignChannel {
    // Hardware-Auto-Detect Topology
    private static readonly int CacheLineSize;
    private static readonly int NumaDistance;

    // Zero-Friction "Safety Invariants"
    private SequenceShadow _shadow;
    private byte* _unmanagedTelemetry;

    static SovereignChannel() {
        // Auto-Detect Logic (BANNED: Hardcoded 256B)
        CacheLineSize = TopologyDetector.GetL1Stripe();
        NumaDistance = TopologyDetector.GetNumaDistance();
    }

    public SovereignChannel() {
        // Marshal-allocated unmanaged telemetry
        _unmanagedTelemetry = (byte*)NativeMemory.AlignedAlloc(
            (nuint)CacheLineSize, (nuint)CacheLineSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(long value) {
        // Pure hardware sequence-differencing
        // BANNED: Thread.MemoryBarrier(), Interlocked.*, lock()
        long nextSequence = _shadow.Sequence + 1;
        
        // Adaptive Adaptive Striping
        if (ContentionDiagnostics.IsHighContention(NumaDistance)) {
            WriteL2Striped(nextSequence, value);
        } else {
            WriteL1Local(nextSequence, value);
        }

        // Hardware-level TSO parity guarantees zero-copy integrity
        _shadow.Sequence = nextSequence;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteL1Local(long sequence, long value) {
        long* ptr = (long*)_unmanagedTelemetry;
        ptr[0] = value;
        // Implicit x86 TSO Store-Store ordering
        ptr[1] = sequence;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteL2Striped(long sequence, long value) {
        // Striped alignment based on detected width
        long* ptr = (long*)(_unmanagedTelemetry + (CacheLineSize * 2));
        ptr[0] = value;
        ptr[1] = sequence;
    }
}`;

  return (
    <div className="bg-zinc-950 border border-emerald-900/50 rounded-lg overflow-hidden flex flex-col h-full shadow-[0_0_20px_rgba(16,185,129,0.05)]">
      <div className="bg-emerald-950/30 p-3 border-b border-emerald-900/50 flex justify-between items-center">
        <div className="flex items-center space-x-2 text-emerald-400">
          <FileCode size={18} />
          <span className="font-bold text-sm tracking-widest">SOVEREIGN_CHANNEL.CS</span>
        </div>
        <div className="flex space-x-4 text-xs">
          <span className="flex items-center text-cyan-400" title="Fence-Less">
            <Cpu size={14} className="mr-1" /> ADR-015
          </span>
          <span className="flex items-center text-amber-400" title="Safety">
            <ShieldCheck size={14} className="mr-1" /> V24 Validated
          </span>
        </div>
      </div>
      <div className="p-4 overflow-y-auto flex-1 font-mono text-xs sm:text-sm bg-[#0d1117] text-[#c9d1d9] scrollbar-thin scrollbar-thumb-emerald-800 scrollbar-track-transparent">
        <pre className="whitespace-pre-wrap break-all leading-relaxed">
          {code.split('\n').map((line, i) => (
            <div key={i} className="flex hover:bg-white/5 transition-colors">
              <span className="w-8 flex-shrink-0 text-gray-600 select-none text-right pr-4 border-r border-gray-800 mr-4">
                {i + 1}
              </span>
              <span dangerouslySetInnerHTML={{ 
                __html: line
                  .replace(/\/\/.*/g, '<span class="text-gray-500">$&</span>')
                  .replace(/\b(public|private|static|readonly|unsafe|struct|class|long|void|int|byte|if|else|new)\b/g, '<span class="text-blue-400">$1</span>')
                  .replace(/\[.*\]/g, '<span class="text-purple-400">$&</span>')
                  .replace(/\b(SovereignChannel|TopologyDetector|NativeMemory|ContentionDiagnostics)\b/g, '<span class="text-emerald-400">$1</span>')
                  .replace(/\b(GetL1Stripe|GetNumaDistance|AlignedAlloc|Publish|IsHighContention|WriteL2Striped|WriteL1Local)\b/g, '<span class="text-yellow-200">$1</span>')
                  .replace(/\b(_shadow|_unmanagedTelemetry|CacheLineSize|NumaDistance)\b/g, '<span class="text-cyan-300">$1</span>')
              }} />
            </div>
          ))}
        </pre>
      </div>
    </div>
  );
};
