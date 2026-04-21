import LatencyChart from './components/LatencyChart';
import V9Verdict from './components/V9Verdict';
import SubLatencyMechanism from './components/SubLatencyMechanism';
import TextRenderingDecision from './components/TextRenderingDecision';
import WorkerRecovery from './components/WorkerRecovery';
import ArchDiagram from './components/ArchDiagram';
import LiveMetrics from './components/LiveMetrics';

export default function App() {
  return (
    <div className="min-h-screen bg-gray-950 text-white">
      {/* Background pattern */}
      <div className="fixed inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-gray-900 via-gray-950 to-black" />
      <div
        className="fixed inset-0 opacity-[0.03]"
        style={{
          backgroundImage:
            'radial-gradient(circle at 1px 1px, white 1px, transparent 0)',
          backgroundSize: '40px 40px',
        }}
      />

      <div className="relative z-10">
        {/* Header */}
        <header className="border-b border-gray-800/50 backdrop-blur-sm bg-gray-950/50">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 py-6">
            <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
              <div>
                <div className="flex items-center gap-3 mb-2">
                  <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-500 to-blue-600 flex items-center justify-center shadow-lg shadow-cyan-500/20">
                    <span className="text-lg font-black text-white">⚡</span>
                  </div>
                  <div>
                    <h1 className="text-2xl sm:text-3xl font-black tracking-tight">
                      <span className="text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 via-blue-400 to-fuchsia-400">
                        V10 Phantom Gate
                      </span>
                    </h1>
                    <p className="text-xs text-gray-500 font-mono">
                      Low-Latency Dispatch Engine · Claude 3.5 Sonnet, Anthropic
                    </p>
                  </div>
                </div>
              </div>
              <div className="flex gap-4">
                <Stat label="Target" value="<200ns" accent="cyan" />
                <Stat label="Projected" value="180ns" accent="emerald" />
                <Stat label="Workers" value="12" accent="pink" />
                <Stat label="Recovery" value="350ns" accent="amber" />
              </div>
            </div>
          </div>
        </header>

        <main className="max-w-7xl mx-auto px-4 sm:px-6 py-8 space-y-6">
          {/* Section: V9 Decision */}
          <SectionTitle
            number="01"
            title="V9 Design Decision"
            subtitle="Resolving the foundation architecture"
          />
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <V9Verdict />
            <LatencyChart />
          </div>

          {/* Section: Sub-200ns */}
          <SectionTitle
            number="02"
            title="Sub-200ns Dispatch"
            subtitle="Breaking the 243ns floor with zero-syscall userspace ring"
          />
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <SubLatencyMechanism />
            <ArchDiagram />
          </div>

          {/* Section: Rendering + Recovery */}
          <SectionTitle
            number="03"
            title="Monitoring & Recovery"
            subtitle="Zero-reflow rendering + decentralized worker self-healing"
          />
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <TextRenderingDecision />
            <WorkerRecovery />
          </div>

          {/* Section: Live Telemetry */}
          <SectionTitle
            number="04"
            title="Live Telemetry Demo"
            subtitle="60fps numeric streaming with tabular-nums — zero layout recalculation"
          />
          <LiveMetrics />

          {/* Summary card */}
          <div className="bg-gradient-to-r from-cyan-500/10 via-fuchsia-500/10 to-pink-500/10 border border-gray-700/30 rounded-2xl p-6 sm:p-8">
            <h2 className="text-xl font-bold text-gray-200 mb-4">
              V10 Design Summary
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <SummaryItem
                label="V9 Verdict"
                value="Uint32 Arena = base, FPGA-Parity = validation layer. Orthogonal concerns; layer both."
              />
              <SummaryItem
                label="Sub-200ns Mechanism"
                value="Userspace ring + CPU pinning + IRQ isolation. 243ns → 180ns via zero-syscall critical path."
              />
              <SummaryItem
                label="Text Rendering"
                value="@chenglou/pretext — pure JS font measurement, zero layout reflow, tabular-nums for fixed-width digits."
              />
              <SummaryItem
                label="Worker Recovery"
                value="Neighbor-Watch CAS via SharedArrayBuffer. Ring topology, 350ns detection-to-recovery, zero coordination."
              />
            </div>
          </div>
        </main>

        {/* Footer */}
        <footer className="border-t border-gray-800/50 py-6 mt-8">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 flex flex-col sm:flex-row items-center justify-between gap-2">
            <span className="text-xs text-gray-600 font-mono">
              Claude 3.5 Sonnet · Anthropic · V10 Phantom Gate
            </span>
            <span className="text-xs text-gray-600">
              Compounding design: V7 → V8 → V9 → V10
            </span>
          </div>
        </footer>
      </div>
    </div>
  );
}

function Stat({
  label,
  value,
  accent,
}: {
  label: string;
  value: string;
  accent: string;
}) {
  const accentColors: Record<string, string> = {
    cyan: 'text-cyan-400',
    emerald: 'text-emerald-400',
    pink: 'text-pink-400',
    amber: 'text-amber-400',
  };
  return (
    <div className="text-center">
      <div
        className={`text-lg font-mono font-bold ${accentColors[accent] || 'text-white'}`}
      >
        {value}
      </div>
      <div className="text-[10px] text-gray-500">{label}</div>
    </div>
  );
}

function SectionTitle({
  number,
  title,
  subtitle,
}: {
  number: string;
  title: string;
  subtitle: string;
}) {
  return (
    <div className="flex items-center gap-4 pt-4">
      <span className="text-sm font-mono text-gray-600 font-bold">
        {number}
      </span>
      <div>
        <h2 className="text-lg font-bold text-gray-200">{title}</h2>
        <p className="text-xs text-gray-500">{subtitle}</p>
      </div>
      <div className="flex-1 h-px bg-gradient-to-r from-gray-700/50 to-transparent ml-4" />
    </div>
  );
}

function SummaryItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-gray-900/50 rounded-xl p-4 border border-gray-700/20">
      <div className="text-[10px] uppercase tracking-wider text-gray-500 mb-2 font-semibold">
        {label}
      </div>
      <p className="text-xs text-gray-300 leading-relaxed">{value}</p>
    </div>
  );
}
