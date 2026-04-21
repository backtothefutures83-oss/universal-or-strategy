import { motion } from 'framer-motion';
import { Code2, Copy, Check } from 'lucide-react';
import { useState } from 'react';
import { V24_SOURCE_CODE, V24_EXPLANATION } from '../data/protocol';

interface CodeDisplayProps {
  activeTab: string;
  onTabChange: (tab: string) => void;
}

export default function CodeDisplay({ activeTab, onTabChange }: CodeDisplayProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    const text = activeTab === 'code' ? V24_SOURCE_CODE : V24_EXPLANATION;
    navigator.clipboard.writeText(text).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      className="bg-surface-800/50 rounded-2xl border border-sov-800/50 overflow-hidden"
    >
      {/* Header */}
      <div className="px-5 py-4 border-b border-sov-800/50 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-sov-600/20 flex items-center justify-center">
            <Code2 className="w-4 h-4 text-sov-400" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-white/90">V24_ROBUST_CODE</h3>
            <p className="text-xs text-white/40 font-mono">Complete Implementation</p>
          </div>
        </div>
        <button
          onClick={handleCopy}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-surface-700/50 border border-sov-700/30 hover:bg-surface-600/50 transition-colors"
        >
          {copied ? (
            <>
              <Check className="w-3.5 h-3.5 text-accent-green" />
              <span className="text-xs text-accent-green">Copied</span>
            </>
          ) : (
            <>
              <Copy className="w-3.5 h-3.5 text-white/40" />
              <span className="text-xs text-white/40">Copy</span>
            </>
          )}
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-sov-800/50">
        <button
          onClick={() => onTabChange('code')}
          className={`px-5 py-2.5 text-xs font-mono transition-colors ${
            activeTab === 'code'
              ? 'text-accent-cyan border-b-2 border-accent-cyan bg-accent-cyan/5'
              : 'text-white/40 hover:text-white/60'
          }`}
        >
          SovereignChannel.cs
        </button>
        <button
          onClick={() => onTabChange('explanation')}
          className={`px-5 py-2.5 text-xs font-mono transition-colors ${
            activeTab === 'explanation'
              ? 'text-accent-cyan border-b-2 border-accent-cyan bg-accent-cyan/5'
              : 'text-white/40 hover:text-white/60'
          }`}
        >
          Design Explanation
        </button>
      </div>

      {/* Content */}
      <div className="max-h-[600px] overflow-auto bg-surface-900/80">
        {activeTab === 'code' ? (
          <pre className="code-block p-5 text-white/70 whitespace-pre">
            {highlightCode(V24_SOURCE_CODE)}
          </pre>
        ) : (
          <div className="p-5 text-sm text-white/70 leading-relaxed whitespace-pre-wrap">
            {renderMarkdown(V24_EXPLANATION)}
          </div>
        )}
      </div>
    </motion.div>
  );
}

// Simple syntax highlighting
function highlightCode(code: string) {
  const lines = code.split('\n');
  return lines.map((line, i) => {
    let highlighted = line
      // Comments
      .replace(/(\/\/.*)/g, '<span class="text-white/30 italic">$1</span>')
      // Keywords
      .replace(/\b(using|namespace|public|private|static|unsafe|struct|enum|class|return|if|else|for|foreach|new|out|ref|this|fixed|byte|int|long|uint|ulong|double|float|bool|void|true|false|null)\b/g, '<span class="text-accent-purple">$1</span>')
      // Types
      .replace(/\b(HardwareTopology|SafetyInvariant|AdaptiveStripingEngine|SovereignChannel|ChannelMode|HAL|Marshal|Unsafe)\b/g, '<span class="text-accent-cyan">$1</span>')
      // Strings
      .replace(/(".*?")/g, '<span class="text-accent-green">$1</span>')
      // Numbers
      .replace(/\b(\d+\.?\d*)\b/g, '<span class="text-accent-amber">$1</span>')
      // Attributes
      .replace(/(\[.*?\])/g, '<span class="text-sov-400">$1</span>');

    return (
      <div key={i} className="flex">
        <span className="text-white/10 select-none w-10 text-right mr-4 flex-shrink-0">{i + 1}</span>
        <span dangerouslySetInnerHTML={{ __html: highlighted }} />
      </div>
    );
  });
}

function renderMarkdown(text: string) {
  return text.split('\n').map((line, i) => {
    if (line.startsWith('## ')) {
      return <h2 key={i} className="text-lg font-bold text-accent-cyan mb-3 mt-4">{line.replace('## ', '')}</h2>;
    }
    if (line.startsWith('### ')) {
      return <h3 key={i} className="text-sm font-bold text-white/80 mb-2 mt-3">{line.replace('### ', '')}</h3>;
    }
    if (line.startsWith('**') && line.endsWith('**')) {
      return <p key={i} className="text-sm font-semibold text-accent-purple mb-2">{line.replace(/\*\*/g, '')}</p>;
    }
    if (line.startsWith('- ')) {
      const content = line.replace('- ', '');
      const codeMatch = content.match(/`([^`]+)`/);
      return (
        <li key={i} className="text-xs text-white/50 ml-4 mb-1 list-disc">
          {codeMatch ? (
            <>
              {content.split('`').map((part, j) =>
                j % 2 === 1 ? <code key={j} className="text-accent-cyan bg-accent-cyan/10 px-1 rounded">{part}</code> : part
              )}
            </>
          ) : content}
        </li>
      );
    }
    if (line.trim() === '') return <br key={i} />;
    return <p key={i} className="text-xs text-white/50 mb-1">{line}</p>;
  });
}
