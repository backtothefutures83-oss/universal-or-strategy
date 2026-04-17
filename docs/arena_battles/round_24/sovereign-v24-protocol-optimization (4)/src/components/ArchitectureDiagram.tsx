import { useState } from 'react';
import { motion } from 'framer-motion';
import { ArrowDown, ArrowRight, Layers, Shield, Zap, Cpu, Database } from 'lucide-react';

interface ArchLayer {
  id: string;
  title: string;
  subtitle: string;
  icon: React.ElementType;
  color: string;
  items: string[];
}

const layers: ArchLayer[] = [
  {
    id: 'app',
    title: 'Application Layer',
    subtitle: 'Zero-Friction API Surface',
    icon: Layers,
    color: '#e8eaf0',
    items: ['Publish(seq, data)', 'Consume() → (seq, data, safe)', 'No locks, no barriers, no GC'],
  },
  {
    id: 'safety',
    title: 'Safety Invariant Layer',
    subtitle: 'Zero-Latency Validation',
    icon: Shield,
    color: '#00ff87',
    items: ['TSO Parity Check', 'Sequence-Shadow Validation', 'Multi-Socket Integrity Proof'],
  },
  {
    id: 'adaptive',
    title: 'Adaptive Striping Engine',
    subtitle: 'Real-Time Mode Selection',
    icon: Zap,
    color: '#b388ff',
    items: ['L1-Local Mode (< 0.40ns)', 'L2-Striped Mode (< 0.50ns)', 'PMC-Driven Contention Detection'],
  },
  {
    id: 'topology',
    title: 'Hardware Topology Layer',
    subtitle: 'Auto-Detection & Alignment',
    icon: Cpu,
    color: '#00e5ff',
    items: ['CPUID Leaf 0x04 Interrogation', 'NUMA Distance Probing', 'Cache Stripe Width Discovery'],
  },
  {
    id: 'memory',
    title: 'Unmanaged Memory Layer',
    subtitle: 'Marshal-Allocated Telemetry',
    icon: Database,
    color: '#ffc857',
    items: ['Zero-GC Channel Buffer', 'Unmanaged Telemetry Writes', 'Direct Hardware Access'],
  },
];

export function ArchitectureDiagram() {
  const [hoveredLayer, setHoveredLayer] = useState<string | null>(null);

  return (
    <section id="architecture" className="py-20 px-4 max-w-4xl mx-auto">
      <div className="text-center mb-12">
        <div className="inline-flex items-center gap-2 text-sov-amber font-mono text-sm mb-4">
          <Layers className="w-4 h-4" />
          <span>ARCHITECTURE OVERVIEW</span>
        </div>
        <h2 className="text-3xl sm:text-4xl font-bold mb-3">
          Sovereign V24 Pipeline
        </h2>
        <p className="text-sov-text-dim max-w-2xl mx-auto">
          Five-layer architecture from hardware discovery to application-level zero-friction API.
        </p>
      </div>

      <div className="space-y-2">
        {layers.map((layer, i) => (
          <motion.div
            key={layer.id}
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ delay: i * 0.1 }}
            onMouseEnter={() => setHoveredLayer(layer.id)}
            onMouseLeave={() => setHoveredLayer(null)}
            className={`glass-panel rounded-xl p-5 cursor-default transition-all duration-300 ${
              hoveredLayer === layer.id ? 'scale-[1.02] glow-cyan' : ''
            }`}
            style={{ borderColor: hoveredLayer === layer.id ? `${layer.color}40` : undefined }}
          >
            <div className="flex items-start gap-4">
              {/* Layer number & icon */}
              <div className="flex-shrink-0">
                <div className="w-12 h-12 rounded-xl flex items-center justify-center" style={{ backgroundColor: `${layer.color}15` }}>
                  <layer.icon className="w-5 h-5" style={{ color: layer.color }} />
                </div>
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-xs font-mono px-1.5 py-0.5 rounded bg-sov-surface-2" style={{ color: layer.color }}>
                    L{i + 1}
                  </span>
                  <h3 className="font-bold text-sov-text">{layer.title}</h3>
                </div>
                <p className="text-xs text-sov-text-dim font-mono mb-3">{layer.subtitle}</p>

                {/* Items */}
                <motion.div
                  className="grid grid-cols-1 sm:grid-cols-3 gap-2"
                  animate={{ opacity: hoveredLayer === layer.id ? 1 : 0.7 }}
                >
                  {layer.items.map((item, j) => (
                    <div key={j} className="flex items-center gap-2 text-xs font-mono">
                      <ArrowRight className="w-3 h-3 flex-shrink-0" style={{ color: layer.color }} />
                      <span className="text-sov-text-dim">{item}</span>
                    </div>
                  ))}
                </motion.div>
              </div>
            </div>
          </motion.div>
        ))}
      </div>

      {/* Data flow arrow */}
      <div className="flex justify-center my-4">
        <ArrowDown className="w-5 h-5 text-sov-text-dim/30" />
      </div>

      {/* Performance summary */}
      <div className="glass-panel rounded-xl p-6 text-center">
        <div className="text-xs font-mono text-sov-text-dim mb-3">END-TO-END PIPELINE BUDGET</div>
        <div className="grid grid-cols-4 gap-4">
          {[
            { label: 'Topology Detect', value: '~0.02ns', desc: 'One-time init' },
            { label: 'Safety Check', value: '0.00ns', desc: 'Zero-overhead' },
            { label: 'Mode Decision', value: '~0.01ns', desc: 'PMC read' },
            { label: 'Channel I/O', value: '< 0.47ns', desc: 'Core operation' },
          ].map((item, i) => (
            <div key={i} className="text-center">
              <div className="text-lg font-bold font-mono text-sov-green">{item.value}</div>
              <div className="text-[10px] text-sov-text-dim font-mono mt-1">{item.label}</div>
              <div className="text-[10px] text-sov-text-dim/50">{item.desc}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
