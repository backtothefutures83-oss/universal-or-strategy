import { motion } from 'framer-motion';
import { Cpu, Database, Network, Shield } from 'lucide-react';
import type { TopologyProfile } from '../data/protocol';

interface TopologyViewProps {
  profile: TopologyProfile;
}

export default function TopologyView({ profile }: TopologyViewProps) {
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
            <Cpu className="w-4 h-4 text-sov-400" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-white/90">Hardware Topology</h3>
            <p className="text-xs text-white/40 font-mono">{profile.architecture} • Auto-Detected</p>
          </div>
        </div>
        <div className="flex items-center gap-2 px-2.5 py-1 rounded-md bg-accent-cyan/10 border border-accent-cyan/20">
          <Shield className="w-3 h-3 text-accent-cyan" />
          <span className="text-xs font-mono text-accent-cyan">TSO: {profile.tsoCompliant ? 'YES' : 'NO'}</span>
        </div>
      </div>

      <div className="p-5 space-y-5">
        {/* CPU Summary */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <StatCard label="Sockets" value={profile.sockets.toString()} icon={Network} />
          <StatCard label="Total Cores" value={profile.totalCores.toString()} icon={Cpu} />
          <StatCard label="Total Threads" value={profile.totalThreads.toString()} icon={Cpu} />
          <StatCard label="Strip Width" value={`${profile.detectedStripWidth}B`} icon={Database} />
        </div>

        {/* Cache Hierarchy */}
        <div>
          <h4 className="text-xs uppercase tracking-wider text-white/40 mb-3">Cache Hierarchy (Auto-Detected)</h4>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <CacheCard level="L1" count={profile.l1Caches.length} size={profile.l1Caches[0]?.sizeKB || 0} lineSize={profile.l1Caches[0]?.lineSizeBytes || 0} latency={profile.l1Caches[0]?.latencyCycles || 0} color="accent-cyan" />
            <CacheCard level="L2" count={profile.l2Caches.length} size={profile.l2Caches[0]?.sizeKB || 0} lineSize={profile.l2Caches[0]?.lineSizeBytes || 0} latency={profile.l2Caches[0]?.latencyCycles || 0} color="sov-400" />
            <CacheCard level="L3" count={profile.l3Caches.length} size={profile.l3Caches[0]?.sizeKB || 0} lineSize={profile.l3Caches[0]?.lineSizeBytes || 0} latency={profile.l3Caches[0]?.latencyCycles || 0} color="accent-purple" />
          </div>
        </div>

        {/* NUMA Topology */}
        <div>
          <h4 className="text-xs uppercase tracking-wider text-white/40 mb-3">NUMA Topology</h4>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {profile.numaNodes.map((node) => (
              <div key={node.id} className="bg-surface-700/50 rounded-xl p-4 border border-sov-700/30">
                <div className="flex items-center justify-between mb-3">
                  <span className="text-sm font-semibold text-white/80">NUMA Node {node.id}</span>
                  <span className="text-xs font-mono text-white/40">{node.cores} cores • {node.memoryGB}GB</span>
                </div>
                <div className="flex items-center gap-4 text-xs font-mono">
                  <span className="text-accent-green">Local: {node.localLatency}ns</span>
                  <span className="text-accent-amber">Remote: {node.remoteLatency}ns</span>
                </div>
                <div className="mt-2 flex gap-1">
                  {node.distanceToOthers.map((d, i) => (
                    <div key={i} className="flex items-center gap-1 text-[10px] text-white/30">
                      <span>N{i}: {d}</span>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </motion.div>
  );
}

function StatCard({ label, value, icon: Icon }: { label: string; value: string; icon: React.ElementType }) {
  return (
    <div className="bg-surface-700/30 rounded-xl p-3 border border-sov-700/20">
      <div className="flex items-center gap-2 mb-1">
        <Icon className="w-3 h-3 text-white/30" />
        <span className="text-[10px] uppercase tracking-wider text-white/40">{label}</span>
      </div>
      <span className="text-lg font-mono font-bold text-white/90">{value}</span>
    </div>
  );
}

function CacheCard({ level, count, size, lineSize, latency, color }: { level: string; count: number; size: number; lineSize: number; latency: number; color: string }) {
  const sizeStr = size >= 1024 ? `${(size / 1024).toFixed(0)}MB` : `${size}KB`;
  return (
    <div className={`bg-surface-700/30 rounded-xl p-4 border border-${color}/20`}>
      <div className="flex items-center justify-between mb-2">
        <span className={`text-sm font-bold text-${color}`}>{level} Cache</span>
        <span className="text-[10px] font-mono text-white/30">×{count}</span>
      </div>
      <div className="space-y-1 text-xs font-mono">
        <div className="flex justify-between">
          <span className="text-white/40">Size</span>
          <span className="text-white/70">{sizeStr}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-white/40">Line</span>
          <span className="text-white/70">{lineSize}B</span>
        </div>
        <div className="flex justify-between">
          <span className="text-white/40">Latency</span>
          <span className="text-white/70">{latency} cyc</span>
        </div>
      </div>
    </div>
  );
}
