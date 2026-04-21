export function Footer() {
  return (
    <footer className="py-12 px-4 border-t border-sov-border/50">
      <div className="max-w-6xl mx-auto text-center">
        <div className="text-2xl font-black mb-2">
          <span className="text-sov-text">SOVEREIGN</span>
          <span className="text-sov-cyan"> V24</span>
        </div>
        <div className="text-xs font-mono text-sov-text-dim mb-6">
          PROMPT_BUILD_TAG: SOV-V24-GLOBAL-ROBUST
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 max-w-3xl mx-auto mb-8 text-xs font-mono text-sov-text-dim">
          <div>
            <div className="text-sov-text font-bold mb-1">MANDATE #1</div>
            Hardware-Auto-Detect Topology
          </div>
          <div>
            <div className="text-sov-text font-bold mb-1">MANDATE #2</div>
            Zero-Friction Safety Invariants
          </div>
          <div>
            <div className="text-sov-text font-bold mb-1">MANDATE #3</div>
            Adaptive Friction-Less Striping
          </div>
        </div>
        <div className="text-[10px] text-sov-text-dim/40 font-mono">
          Sovereign Protocol v24 — The Global Zero-Friction Handshake
        </div>
      </div>
    </footer>
  );
}
