import { motion } from 'framer-motion';
import { Shield, Zap, Activity, Gauge, GitBranch } from 'lucide-react';

interface HeaderProps {
  isRunning: boolean;
  avgLatency: number;
  mode: string;
}

export default function Header({ isRunning, avgLatency, mode }: HeaderProps) {
  return (
    <motion.header
      initial={{ opacity: 0, y: -20 }}
      animate={{ opacity: 1, y: 0 }}
      className="relative overflow-hidden border-b border-sov-800/50 bg-surface-900/80 backdrop-blur-xl"
    >
      {/* Scan line effect */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute inset-x-0 h-px bg-gradient-to-r from-transparent via-accent-cyan/30 to-transparent animate-scan-line" />
      </div>

      <div className="max-w-[1600px] mx-auto px-6 py-4">
        <div className="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-4">
          {/* Logo & Title */}
          <div className="flex items-center gap-4">
            <div className="relative">
              <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-cyan/20 to-sov-600/20 border border-accent-cyan/30 flex items-center justify-center">
                <Zap className="w-6 h-6 text-accent-cyan" />
              </div>
              {isRunning && (
                <div className="absolute -top-1 -right-1 w-3 h-3 rounded-full bg-accent-green animate-pulse" />
              )}
            </div>
            <div>
              <h1 className="text-xl font-bold tracking-tight">
                <span className="text-accent-cyan">SOVEREIGN</span>
                <span className="text-white/60 ml-2">V24</span>
              </h1>
              <p className="text-xs text-white/40 font-mono tracking-widest">
                GLOBAL ZERO-FRICTION HANDSHAKE • ADR-015 COMPLIANT
              </p>
            </div>
          </div>

          {/* Status Bar */}
          <div className="flex items-center gap-6 flex-wrap">
            <StatusBadge icon={Activity} label="Status" value={isRunning ? 'ACTIVE' : 'IDLE'} active={isRunning} />
            <StatusBadge icon={Gauge} label="Avg Latency" value={`${avgLatency.toFixed(3)}ns`} />
            <StatusBadge icon={GitBranch} label="Mode" value={mode} />
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-accent-green/10 border border-accent-green/30">
              <Shield className="w-4 h-4 text-accent-green" />
              <span className="text-xs font-mono text-accent-green">FENCE_COUNT = 0</span>
            </div>
          </div>
        </div>
      </div>
    </motion.header>
  );
}

function StatusBadge({ icon: Icon, label, value, active }: { icon: React.ElementType; label: string; value: string; active?: boolean }) {
  return (
    <div className="flex items-center gap-2">
      <Icon className={`w-4 h-4 ${active ? 'text-accent-cyan' : 'text-white/30'}`} />
      <div className="flex flex-col">
        <span className="text-[10px] uppercase tracking-wider text-white/30">{label}</span>
        <span className={`text-sm font-mono font-semibold ${active ? 'text-accent-cyan' : 'text-white/70'}`}>{value}</span>
      </div>
    </div>
  );
}
