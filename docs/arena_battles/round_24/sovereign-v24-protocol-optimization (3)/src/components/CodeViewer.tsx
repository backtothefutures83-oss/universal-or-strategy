import { useState, useMemo } from 'react';
import { sovereignV24Code } from '../data/v24Code';
import { cn } from '../utils/cn';

interface LineHighlight {
  start: number;
  end: number;
  label: string;
  color: string;
}

const highlights: LineHighlight[] = [
  { start: 1, end: 6, label: 'Header', color: 'bg-sov-text-dim/10' },
  { start: 20, end: 45, label: 'Hardware Topology Struct', color: 'bg-sov-cyan/5' },
  { start: 47, end: 95, label: 'Dynamic Topology Detection', color: 'bg-sov-cyan/10' },
  { start: 112, end: 145, label: 'Safety Invariants', color: 'bg-sov-green/10' },
  { start: 147, end: 180, label: 'Validation Logic', color: 'bg-sov-green/10' },
  { start: 195, end: 225, label: 'Adaptive Striping', color: 'bg-sov-amber/10' },
  { start: 240, end: 280, label: 'Write Path (Fence-Less)', color: 'bg-sov-purple/10' },
  { start: 282, end: 310, label: 'Read Path (Fence-Less)', color: 'bg-sov-purple/10' },
  { start: 315, end: 340, label: 'CRC-64 & Telemetry', color: 'bg-sov-text-dim/5' },
  { start: 355, end: 385, label: 'Fence-Less Theorem Proof', color: 'bg-sov-red/10' },
];

export default function CodeViewer() {
  const [activeHighlight, setActiveHighlight] = useState<number | null>(null);
  const [copied, setCopied] = useState(false);

  const lines = useMemo(() => sovereignV24Code.split('\n'), []);

  const handleCopy = () => {
    navigator.clipboard.writeText(sovereignV24Code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  const getLineHighlight = (lineNum: number) => {
    if (activeHighlight === null) return '';
    const h = highlights[activeHighlight];
    if (lineNum >= h.start && lineNum <= h.end) return h.color;
    return '';
  };

  const syntaxHighlight = (line: string) => {
    // Simple syntax highlighting
    let result = line;
    
    // Comments
    if (line.trimStart().startsWith('//') || line.trimStart().startsWith('*') || line.trimStart().startsWith('/*')) {
      return <span className="text-sov-text-dim/70">{line}</span>;
    }
    
    // Keywords
    const keywords = ['using', 'namespace', 'public', 'private', 'static', 'struct', 'enum', 'class', 'return', 'if', 'else', 'switch', 'case', 'default', 'const', 'void', 'int', 'long', 'ulong', 'byte', 'bool', 'true', 'false', 'null', 'out', 'ref', 'this', 'new', 'typeof'];
    const types = ['HardwareTopology', 'SequenceSlot', 'StripingMode', 'TelemetryHeader', 'SovereignChannel', 'MethodImpl', 'StructLayout', 'LayoutKind', 'IntPtr', 'Marshal', 'Environment'];
    
    const parts = result.split(/(\s+|[{}();,.<>=\[\]&|!~^%*+\-?:])/);
    
    return parts.map((part, i) => {
      if (keywords.includes(part)) {
        return <span key={i} className="text-sov-purple">{part}</span>;
      }
      if (types.includes(part)) {
        return <span key={i} className="text-sov-cyan">{part}</span>;
      }
      if (/^\d+$/.test(part) || /^\d+\.\d+$/.test(part)) {
        return <span key={i} className="text-sov-amber">{part}</span>;
      }
      if (part.startsWith('"') || part.startsWith("'")) {
        return <span key={i} className="text-sov-green">{part}</span>;
      }
      if (part.startsWith('#')) {
        return <span key={i} className="text-sov-red">{part}</span>;
      }
      return <span key={i} className="text-sov-text">{part}</span>;
    });
  };

  return (
    <section id="code" className="py-20 relative">
      <div className="max-w-6xl mx-auto px-4">
        <div className="text-center mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full border border-sov-purple/20 bg-sov-purple/5 mb-4">
            <span className="text-xs font-mono text-sov-purple">V24_ROBUST_CODE</span>
          </div>
          <h2 className="text-3xl sm:text-4xl font-bold text-sov-text-bright mb-3">
            Complete <span className="text-sov-purple">Source Code</span>
          </h2>
          <p className="text-sov-text-dim max-w-xl mx-auto">
            The full SovereignChannel v24 implementation with all V24 safety & robustness mandates.
          </p>
        </div>

        {/* Highlight navigation */}
        <div className="flex flex-wrap gap-2 mb-4 justify-center">
          {highlights.map((h, i) => (
            <button
              key={i}
              onClick={() => setActiveHighlight(activeHighlight === i ? null : i)}
              className={cn(
                'px-3 py-1.5 rounded-md border text-xs font-mono transition-all',
                activeHighlight === i
                  ? `${h.color} border-sov-cyan/30 text-sov-cyan`
                  : 'border-sov-border text-sov-text-dim hover:text-sov-text'
              )}
            >
              {h.label}
            </button>
          ))}
        </div>

        {/* Code block */}
        <div className="rounded-xl border border-sov-border bg-sov-surface/50 overflow-hidden">
          <div className="px-4 py-3 border-b border-sov-border flex items-center justify-between bg-sov-surface">
            <div className="flex items-center gap-3">
              <div className="flex gap-1.5">
                <div className="w-3 h-3 rounded-full bg-red-500/60" />
                <div className="w-3 h-3 rounded-full bg-yellow-500/60" />
                <div className="w-3 h-3 rounded-full bg-green-500/60" />
              </div>
              <span className="text-xs font-mono text-sov-text-dim">SovereignChannel.cs</span>
            </div>
            <button
              onClick={handleCopy}
              className="px-3 py-1 rounded-md border border-sov-border text-xs font-mono text-sov-text-dim hover:text-sov-text hover:border-sov-cyan/30 transition-all"
            >
              {copied ? '✓ Copied!' : 'Copy'}
            </button>
          </div>
          
          <div className="overflow-x-auto max-h-[600px] overflow-y-auto">
            <table className="w-full text-xs font-mono leading-5">
              <tbody>
                {lines.map((line, i) => (
                  <tr
                    key={i}
                    className={cn(
                      'hover:bg-white/5 transition-colors',
                      getLineHighlight(i + 1)
                    )}
                  >
                    <td className="w-12 text-right pr-4 text-sov-text-dim/40 select-none border-r border-sov-border/30">
                      {i + 1}
                    </td>
                    <td className="pl-4 whitespace-pre">
                      {syntaxHighlight(line)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Stats */}
        <div className="mt-6 grid grid-cols-2 sm:grid-cols-4 gap-3">
          {[
            { label: 'Total Lines', value: lines.length.toString() },
            { label: 'Sections', value: '7' },
            { label: 'Safety Invariants', value: '3' },
            { label: 'Banned Ops', value: '5' },
          ].map(stat => (
            <div key={stat.label} className="p-3 rounded-lg border border-sov-border bg-sov-surface/30 text-center">
              <div className="text-[10px] font-mono text-sov-text-dim">{stat.label}</div>
              <div className="text-lg font-bold font-mono text-sov-text-bright">{stat.value}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
