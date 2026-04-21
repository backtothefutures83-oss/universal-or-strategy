interface Props {
  title: string;
  language: string;
  code: string;
  accent?: string;
}

export default function CodeBlock({ title, language, code, accent = '#C0A040' }: Props) {
  return (
    <div className="rounded-xl overflow-hidden border border-[#C0A040]/10 bg-[#0D0D14]">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2.5 bg-[#12121A] border-b border-[#C0A040]/10">
        <div className="flex items-center gap-3">
          <div className="flex gap-1.5">
            <div className="w-3 h-3 rounded-full bg-[#DC2626]/60" />
            <div className="w-3 h-3 rounded-full bg-[#C0A040]/60" />
            <div className="w-3 h-3 rounded-full bg-[#10B981]/60" />
          </div>
          <span className="font-mono text-xs text-[#B0AFA8]">{title}</span>
        </div>
        <span className="font-mono text-[10px] px-2 py-0.5 rounded text-[#B0AFA8]/50 bg-[#1A1A28]">{language}</span>
      </div>
      {/* Code */}
      <pre className="p-4 overflow-x-auto">
        <code className="font-mono text-xs leading-relaxed text-[#B0AFA8]" style={{ tabSize: 2 }}>
          {code.split('\n').map((line, i) => (
            <div key={i} className="flex">
              <span className="w-8 flex-shrink-0 text-right pr-4 select-none" style={{ color: `${accent}30` }}>
                {i + 1}
              </span>
              <span dangerouslySetInnerHTML={{ __html: highlightSyntax(line, accent) }} />
            </div>
          ))}
        </code>
      </pre>
    </div>
  );
}

function highlightSyntax(line: string, accent: string): string {
  let result = line
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // Comments
  result = result.replace(/(\/\/.*$)/, `<span style="color: #555">$1</span>`);

  // Keywords
  const keywords = ['struct', 'fn', 'let', 'mut', 'pub', 'const', 'static', 'impl', 'unsafe', 'use', 'mod', 'type', 'enum', 'match', 'if', 'else', 'while', 'for', 'return', 'self', 'Self', 'where', 'trait', 'async', 'await', 'alignas', 'thread_local', 'inline', 'constexpr', 'volatile', 'atomic', 'noexcept', 'typename', 'template', 'namespace', 'class', 'void', 'auto', 'register', 'extern'];
  keywords.forEach(kw => {
    result = result.replace(new RegExp(`\\b(${kw})\\b`, 'g'), `<span style="color: ${accent}">$1</span>`);
  });

  // Types
  const types = ['u8', 'u16', 'u32', 'u64', 'usize', 'i32', 'i64', 'f64', 'bool', 'AtomicU64', 'AtomicBool', 'Ordering', 'NonNull', 'MmapRegion', 'SlabPool', 'SpscPipe', 'ActorStation', 'MirrorState', 'SidebandPipe', 'PageAligned', 'CacheLine', 'size_t', 'uint64_t', 'uint8_t', 'int', 'char'];
  types.forEach(t => {
    result = result.replace(new RegExp(`\\b(${t})\\b`, 'g'), `<span style="color: #22D3EE">$1</span>`);
  });

  // Strings
  result = result.replace(/(&quot;[^&]*&quot;|"[^"]*")/g, `<span style="color: #10B981">$1</span>`);

  // Numbers
  result = result.replace(/\b(\d+)\b/g, `<span style="color: #A855F7">$1</span>`);

  // Macros / attributes
  result = result.replace(/(#\[.*?\])/g, `<span style="color: #C0A040; opacity: 0.7">$1</span>`);

  return result;
}
