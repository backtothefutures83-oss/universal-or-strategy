import React from 'react';
import { ChevronDown, FileText, Terminal, ExternalLink } from 'lucide-react';

const Footer: React.FC = () => (
  <footer className="py-16 px-4 border-t border-slate-800/50">
    <div className="max-w-6xl mx-auto">
      <div className="grid md:grid-cols-3 gap-8 mb-12">
        {/* Brand */}
        <div>
          <div className="text-2xl font-black text-shimmer mb-2">SOVEREIGN</div>
          <div className="text-xs font-mono text-slate-500 mb-3">V24 — Global Zero-Friction Handshake</div>
          <p className="text-xs text-slate-400 leading-relaxed">
            Hardware-agnostic, fence-less communication protocol achieving sub-0.5ns 
            latency across heterogeneous CPU topologies.
          </p>
        </div>

        {/* Protocol Specs */}
        <div>
          <h4 className="text-sm font-semibold text-slate-300 mb-3">Protocol Specifications</h4>
          <ul className="space-y-2 text-xs text-slate-400">
            <li className="flex items-center gap-2">
              <ChevronDown className="w-3 h-3 text-cyan-400" />
              ADR-015: Fence-Less Discipline
            </li>
            <li className="flex items-center gap-2">
              <ChevronDown className="w-3 h-3 text-cyan-400" />
              SOV-V24-GLOBAL-ROBUST
            </li>
            <li className="flex items-center gap-2">
              <ChevronDown className="w-3 h-3 text-cyan-400" />
              Cross-Platform Resilient
            </li>
            <li className="flex items-center gap-2">
              <ChevronDown className="w-3 h-3 text-cyan-400" />
              Hardware TSO Parity
            </li>
          </ul>
        </div>

        {/* Resources */}
        <div>
          <h4 className="text-sm font-semibold text-slate-300 mb-3">Resources</h4>
          <div className="space-y-2">
            <a href="#" className="flex items-center gap-2 text-xs text-slate-400 hover:text-cyan-300 transition-colors">
              <ExternalLink className="w-3 h-3" />
              Source Repository
            </a>
            <a href="#" className="flex items-center gap-2 text-xs text-slate-400 hover:text-cyan-300 transition-colors">
              <FileText className="w-3 h-3" />
              Architecture Document
            </a>
            <a href="#" className="flex items-center gap-2 text-xs text-slate-400 hover:text-cyan-300 transition-colors">
              <Terminal className="w-3 h-3" />
              Benchmark Suite
            </a>
          </div>
        </div>
      </div>

      {/* Bottom bar */}
      <div className="flex flex-col md:flex-row items-center justify-between pt-8 border-t border-slate-800/50 gap-4">
        <div className="text-[10px] font-mono text-slate-600">
          PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST
        </div>
        <div className="flex items-center gap-4">
          <span className="text-[10px] font-mono text-slate-600">V23.1 Baseline: 0.87ns → V24 Target: &lt;0.5ns</span>
          <span className="px-2 py-0.5 rounded-full bg-emerald-500/10 border border-emerald-500/20 text-[10px] font-mono text-emerald-400">
            ACHIEVED
          </span>
        </div>
      </div>
    </div>
  </footer>
);

export default Footer;
