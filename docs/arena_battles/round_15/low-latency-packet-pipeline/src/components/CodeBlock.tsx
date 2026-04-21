import { useState } from "react";

interface CodeBlockProps {
  code: string;
  language?: string;
  title?: string;
  highlight?: number[];
}

export default function CodeBlock({ code, language = "csharp", title, highlight = [] }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  const lines = code.split("\n");

  const colorizeToken = (token: string): string => {
    // Keywords
    const keywords = /\b(unsafe|fixed|struct|class|public|private|static|readonly|const|ref|out|in|void|long|ulong|int|uint|byte|bool|new|return|if|else|while|for|foreach|var|this|using|namespace|get|set|where|partial|internal|sealed|override|virtual|abstract|interface|enum|delegate|event|async|await|lock|try|catch|finally|throw|null|true|false|typeof|sizeof|stackalloc|volatile)\b/g;
    // Types
    const types = /\b(Interlocked|Thread|Volatile|MemoryMarshal|NativeMemory|Unsafe|Span|Memory|GC|StructLayoutAttribute|FieldOffsetAttribute|MethodImplAttribute|CompilerGeneratedAttribute|RuntimeHelpers|MemoryBarrier|Fence|MemoryOrderHint)\b/g;
    // Attributes
    const attrs = /(\[.*?\])/g;
    // Strings
    const strings = /(".*?")/g;
    // Numbers
    const nums = /\b(0x[0-9A-Fa-f]+|\d+(?:UL|ul|L|l|U|u)?)\b/g;
    // Comments
    const comments = /(\/\/.*$)/gm;
    // Preprocessor
    const preproc = /(#\w+)/g;

    return token
      .replace(comments, '<span class="text-gray-500 italic">$1</span>')
      .replace(strings, '<span class="text-amber-300">$1</span>')
      .replace(attrs, '<span class="text-yellow-400">$1</span>')
      .replace(preproc, '<span class="text-pink-400">$1</span>')
      .replace(types, '<span class="text-emerald-400">$1</span>')
      .replace(keywords, '<span class="text-violet-400 font-semibold">$1</span>')
      .replace(nums, '<span class="text-orange-400">$1</span>');
  };

  return (
    <div className="rounded-xl overflow-hidden border border-gray-700/60 bg-gray-900/80 shadow-2xl">
      {/* Title bar */}
      <div className="flex items-center justify-between px-4 py-2.5 bg-gray-800/80 border-b border-gray-700/60">
        <div className="flex items-center gap-3">
          <div className="flex gap-1.5">
            <div className="w-3 h-3 rounded-full bg-red-500/70" />
            <div className="w-3 h-3 rounded-full bg-yellow-500/70" />
            <div className="w-3 h-3 rounded-full bg-green-500/70" />
          </div>
          {title && (
            <span className="text-xs text-gray-400 font-medium tracking-wide">{title}</span>
          )}
        </div>
        <div className="flex items-center gap-3">
          <span className="text-xs text-gray-600 uppercase tracking-widest">{language}</span>
          <button
            onClick={handleCopy}
            className="text-xs px-2.5 py-1 rounded bg-gray-700/60 hover:bg-gray-700 text-gray-400 hover:text-gray-200 transition-all border border-gray-600/40 hover:border-gray-500/60"
          >
            {copied ? "✓ Copied" : "Copy"}
          </button>
        </div>
      </div>

      {/* Code area */}
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-xs">
          <tbody>
            {lines.map((line, idx) => {
              const lineNum = idx + 1;
              const isHighlighted = highlight.includes(lineNum);
              return (
                <tr
                  key={idx}
                  className={`
                    group transition-colors
                    ${isHighlighted
                      ? "bg-cyan-500/10 border-l-2 border-cyan-500"
                      : "hover:bg-gray-800/40 border-l-2 border-transparent"
                    }
                  `}
                >
                  <td className="select-none px-3 py-0.5 text-right text-gray-600 w-10 text-[10px] leading-6 align-top">
                    {lineNum}
                  </td>
                  <td
                    className={`px-4 py-0.5 leading-6 whitespace-pre align-top ${isHighlighted ? "text-cyan-100" : "text-gray-200"}`}
                    dangerouslySetInnerHTML={{ __html: colorizeToken(line) }}
                  />
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
