import { useState } from 'react';

interface CodeBlockProps {
  code: string;
  language?: string;
}

export default function CodeBlock({ code, language = 'csharp' }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  // Simple syntax highlighting for C#
  const highlightCode = (code: string) => {
    if (language !== 'csharp') return code;

    const keywords = [
      'using', 'namespace', 'class', 'struct', 'interface', 'enum', 'public', 'private',
      'protected', 'internal', 'static', 'readonly', 'const', 'volatile', 'unsafe',
      'void', 'int', 'long', 'byte', 'short', 'bool', 'string', 'float', 'double',
      'if', 'else', 'while', 'for', 'foreach', 'do', 'switch', 'case', 'default',
      'return', 'break', 'continue', 'new', 'this', 'base', 'ref', 'out', 'in',
      'true', 'false', 'null', 'typeof', 'sizeof', 'throw', 'try', 'catch', 'finally',
    ];

    const types = ['TaggedPointer', 'IngressRing', 'PacketSlot', 'ProcessingCore', 'PacketMetadata'];

    let highlighted = code;

    // Highlight comments
    highlighted = highlighted.replace(/(\/\/\/.*$|\/\/.*$|\/\*[\s\S]*?\*\/)/gm, 
      '<span class="text-slate-500">$1</span>');

    // Highlight strings
    highlighted = highlighted.replace(/(".*?")/g, '<span class="text-green-400">$1</span>');

    // Highlight keywords
    keywords.forEach(keyword => {
      const regex = new RegExp(`\\b(${keyword})\\b`, 'g');
      highlighted = highlighted.replace(regex, '<span class="text-blue-400">$1</span>');
    });

    // Highlight types
    types.forEach(type => {
      const regex = new RegExp(`\\b(${type})\\b`, 'g');
      highlighted = highlighted.replace(regex, '<span class="text-cyan-400">$1</span>');
    });

    // Highlight numbers
    highlighted = highlighted.replace(/\b(\d+)\b/g, '<span class="text-purple-400">$1</span>');

    // Highlight attributes
    highlighted = highlighted.replace(/(\[[\w\s,=.()]+\])/g, '<span class="text-amber-400">$1</span>');

    return highlighted;
  };

  return (
    <div className="relative group">
      <div className="absolute right-3 top-3 z-10">
        <button
          onClick={handleCopy}
          className="px-3 py-1.5 rounded-lg bg-slate-700 hover:bg-slate-600 border border-slate-600 text-xs text-slate-300 transition-all"
        >
          {copied ? '✓ Copied!' : 'Copy'}
        </button>
      </div>
      
      <div className="bg-slate-950 border border-slate-700 rounded-lg overflow-hidden">
        <div className="bg-slate-900/50 border-b border-slate-700 px-4 py-2 flex items-center gap-2">
          <div className="flex gap-1.5">
            <div className="w-3 h-3 rounded-full bg-red-500/50"></div>
            <div className="w-3 h-3 rounded-full bg-amber-500/50"></div>
            <div className="w-3 h-3 rounded-full bg-green-500/50"></div>
          </div>
          <span className="text-xs text-slate-400 ml-2">{language}</span>
        </div>
        
        <div className="overflow-x-auto">
          <pre className="p-4 text-sm leading-relaxed">
            <code 
              className="text-slate-300 font-mono"
              dangerouslySetInnerHTML={{ __html: highlightCode(code) }}
            />
          </pre>
        </div>
      </div>
    </div>
  );
}
