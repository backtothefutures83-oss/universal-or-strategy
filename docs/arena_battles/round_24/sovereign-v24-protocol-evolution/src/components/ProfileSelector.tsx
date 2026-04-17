import { motion } from 'framer-motion';
import { Cpu, Play, Square, ChevronDown } from 'lucide-react';
import { useState } from 'react';
import { HARDWARE_PROFILES } from '../data/protocol';

interface ProfileSelectorProps {
  selected: string;
  onSelect: (profile: string) => void;
  isRunning: boolean;
  onStart: () => void;
  onStop: () => void;
}

export default function ProfileSelector({ selected, onSelect, isRunning, onStart, onStop }: ProfileSelectorProps) {
  const [isOpen, setIsOpen] = useState(false);
  const profiles = Object.keys(HARDWARE_PROFILES);

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      className="bg-surface-800/50 rounded-2xl border border-sov-800/50 overflow-hidden"
    >
      <div className="px-5 py-4 border-b border-sov-800/50">
        <h3 className="text-sm font-semibold text-white/90 flex items-center gap-2">
          <Cpu className="w-4 h-4 text-sov-400" />
          Hardware Profile Selection
        </h3>
      </div>

      <div className="p-5 space-y-4">
        {/* Profile Dropdown */}
        <div className="relative">
          <button
            onClick={() => setIsOpen(!isOpen)}
            disabled={isRunning}
            className="w-full flex items-center justify-between px-4 py-3 rounded-xl bg-surface-700/50 border border-sov-700/30 hover:border-sov-600/50 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <span className="text-sm font-mono text-white/80">{selected}</span>
            <ChevronDown className={`w-4 h-4 text-white/30 transition-transform ${isOpen ? 'rotate-180' : ''}`} />
          </button>

          {isOpen && (
            <motion.div
              initial={{ opacity: 0, y: -5 }}
              animate={{ opacity: 1, y: 0 }}
              className="absolute z-10 w-full mt-2 bg-surface-700 border border-sov-700/30 rounded-xl overflow-hidden shadow-xl"
            >
              {profiles.map((profile) => (
                <button
                  key={profile}
                  onClick={() => {
                    onSelect(profile);
                    setIsOpen(false);
                  }}
                  className={`w-full text-left px-4 py-3 text-sm font-mono transition-colors ${
                    profile === selected
                      ? 'bg-accent-cyan/10 text-accent-cyan'
                      : 'text-white/60 hover:bg-surface-600/50'
                  }`}
                >
                  {profile}
                </button>
              ))}
            </motion.div>
          )}
        </div>

        {/* Architecture Info */}
        <div className="bg-surface-900/50 rounded-xl p-4 border border-sov-700/20">
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div>
              <span className="text-white/30 block">Architecture</span>
              <span className="text-white/70 font-mono">{HARDWARE_PROFILES[selected].architecture}</span>
            </div>
            <div>
              <span className="text-white/30 block">Sockets</span>
              <span className="text-white/70 font-mono">{HARDWARE_PROFILES[selected].sockets}</span>
            </div>
            <div>
              <span className="text-white/30 block">Cores</span>
              <span className="text-white/70 font-mono">{HARDWARE_PROFILES[selected].totalCores}</span>
            </div>
            <div>
              <span className="text-white/30 block">Strip Width</span>
              <span className="text-white/70 font-mono">{HARDWARE_PROFILES[selected].detectedStripWidth}B</span>
            </div>
          </div>
        </div>

        {/* Control Buttons */}
        <div className="flex gap-3">
          {!isRunning ? (
            <button
              onClick={onStart}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-accent-cyan/20 border border-accent-cyan/40 text-accent-cyan font-mono text-sm font-bold hover:bg-accent-cyan/30 transition-colors"
            >
              <Play className="w-4 h-4" />
              INITIALIZE V24
            </button>
          ) : (
            <button
              onClick={onStop}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-accent-red/20 border border-accent-red/40 text-accent-red font-mono text-sm font-bold hover:bg-accent-red/30 transition-colors"
            >
              <Square className="w-4 h-4" />
              HALT
            </button>
          )}
        </div>
      </div>
    </motion.div>
  );
}
