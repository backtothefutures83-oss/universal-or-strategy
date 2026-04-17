import React, { useState } from "react";

interface CodeBlockProps {
  code: string;
  language?: string;
}

// Simple tokenizer for C#-like syntax highlighting
function tokenize(code: string): React.ReactNode[] {
  const keywords = new Set([
    "public", "private", "sealed", "unsafe", "static", "readonly", "volatile",
    "ref", "out", "new", "return", "void", "bool", "int", "long", "ulong",
    "ushort", "byte", "nuint", "fixed", "where", "class", "struct", "interface",
    "null", "true", "false", "this", "for", "do", "while", "if", "else",
    "throw", "in", "var", "const",
  ]);

  const nodes: React.ReactNode[] = [];
  const lines = code.split("\n");

  lines.forEach((line, lineIdx) => {
    // Comment line
    if (line.trimStart().startsWith("//")) {
      const isHeader = line.includes("═") || line.includes("──") || line.includes("┌") || line.includes("┐") || line.includes("└") || line.includes("┘") || line.includes("│");
      nodes.push(
        <span key={`l${lineIdx}`} className={isHeader ? "text-cyan-400/70" : "text-slate-500"}>
          {line}
          {"\n"}
        </span>
      );
      return;
    }

    // Tokenize the line into chunks
    const parts: React.ReactNode[] = [];
    let remaining = line;
    let partIdx = 0;

    while (remaining.length > 0) {
      // String literal
      const strMatch = remaining.match(/^("(?:[^"\\]|\\.)*")/);
      if (strMatch) {
        parts.push(<span key={partIdx++} className="text-amber-300">{strMatch[1]}</span>);
        remaining = remaining.slice(strMatch[1].length);
        continue;
      }

      // Numeric literal
      const numMatch = remaining.match(/^(\b\d[\d_]*(?:\.\d+)?(?:L|UL|u|f|d|m)?\b)/);
      if (numMatch) {
        parts.push(<span key={partIdx++} className="text-purple-400">{numMatch[1]}</span>);
        remaining = remaining.slice(numMatch[1].length);
        continue;
      }

      // Attribute [...]
      const attrMatch = remaining.match(/^(\[(?:FieldOffset|StructLayout|MethodImpl|LayoutKind)[^\]]*\])/);
      if (attrMatch) {
        parts.push(<span key={partIdx++} className="text-yellow-500">{attrMatch[1]}</span>);
        remaining = remaining.slice(attrMatch[1].length);
        continue;
      }

      // Type names (PascalCase identifiers)
      const typeMatch = remaining.match(/^([A-Z][a-zA-Z0-9_]*(?:\*|\[\])?)/);
      if (typeMatch && !keywords.has(typeMatch[1].toLowerCase())) {
        parts.push(<span key={partIdx++} className="text-emerald-400">{typeMatch[1]}</span>);
        remaining = remaining.slice(typeMatch[1].length);
        continue;
      }

      // Keywords
      const wordMatch = remaining.match(/^([a-z_][a-zA-Z0-9_]*)/);
      if (wordMatch) {
        const word = wordMatch[1];
        if (keywords.has(word)) {
          parts.push(<span key={partIdx++} className="text-blue-400 font-medium">{word}</span>);
        } else {
          parts.push(<span key={partIdx++} className="text-slate-200">{word}</span>);
        }
        remaining = remaining.slice(word.length);
        continue;
      }

      // Operators / symbols
      const opMatch = remaining.match(/^([=<>!&|^~+\-*/%?:;,.()\[\]{}\s]+)/);
      if (opMatch) {
        const op = opMatch[1];
        const isOp = /[=<>!&|^~+\-*/%]/.test(op.trim());
        parts.push(
          <span key={partIdx++} className={isOp && op.trim() ? "text-slate-400" : "text-slate-300"}>
            {op}
          </span>
        );
        remaining = remaining.slice(op.length);
        continue;
      }

      // Fallback: consume one char
      parts.push(<span key={partIdx++} className="text-slate-300">{remaining[0]}</span>);
      remaining = remaining.slice(1);
    }

    nodes.push(
      <span key={`l${lineIdx}`}>
        {parts}
        {"\n"}
      </span>
    );
  });

  return nodes;
}

export default function CodeBlock({ code }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  const lines = code.split("\n");

  return (
    <div className="relative group rounded-xl overflow-hidden border border-slate-700/60 shadow-2xl shadow-black/40">
      {/* Header bar */}
      <div className="flex items-center justify-between px-4 py-2 bg-slate-800/90 border-b border-slate-700/60">
        <div className="flex gap-1.5">
          <div className="w-3 h-3 rounded-full bg-red-500/80" />
          <div className="w-3 h-3 rounded-full bg-yellow-500/80" />
          <div className="w-3 h-3 rounded-full bg-green-500/80" />
        </div>
        <span className="text-xs font-mono text-slate-500 uppercase tracking-widest">C# · unsafe</span>
        <button
          onClick={handleCopy}
          className="flex items-center gap-1.5 text-xs text-slate-400 hover:text-cyan-400 transition-colors duration-150 cursor-pointer"
        >
          {copied ? (
            <>
              <svg className="w-3.5 h-3.5 text-green-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
              <span className="text-green-400">Copied!</span>
            </>
          ) : (
            <>
              <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                  d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
              Copy
            </>
          )}
        </button>
      </div>

      {/* Code content */}
      <div className="overflow-x-auto bg-slate-900/95 max-h-[520px] overflow-y-auto">
        <table className="min-w-full">
          <tbody>
            {lines.map((line, i) => (
              <tr key={i} className="group/line hover:bg-white/[0.025] transition-colors">
                <td className="select-none text-right pr-4 pl-4 py-0 text-slate-600 text-xs font-mono leading-6 w-10 min-w-[2.5rem] border-r border-slate-800">
                  {i + 1}
                </td>
                <td className="pl-4 pr-6 py-0 leading-6">
                  <code className="text-sm font-mono whitespace-pre">
                    {tokenize(line).map((n, j) =>
                      React.isValidElement(n)
                        ? React.cloneElement(n as React.ReactElement, { key: `${i}-${j}` })
                        : n
                    )}
                  </code>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
