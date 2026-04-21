import { useState } from 'react';

interface ChallengeProps {
  number: string;
  icon: string;
  title: string;
  subtitle: string;
  accentColor: string;
  children: React.ReactNode;
}

export default function ChallengeSection({ number, icon, title, subtitle, accentColor, children }: ChallengeProps) {
  const [expanded, setExpanded] = useState(true);

  return (
    <section className="relative py-16 px-4">
      <div className="max-w-6xl mx-auto">
        {/* Section header */}
        <div
          className="flex items-start gap-6 mb-10 cursor-pointer group"
          onClick={() => setExpanded(!expanded)}
        >
          <div
            className="flex-shrink-0 w-16 h-16 rounded-xl flex items-center justify-center text-2xl border"
            style={{
              borderColor: `${accentColor}40`,
              backgroundColor: `${accentColor}08`,
              boxShadow: `0 0 20px ${accentColor}10`,
            }}
          >
            {icon}
          </div>
          <div className="flex-1">
            <div className="flex items-center gap-3 mb-1">
              <span className="font-mono text-xs tracking-widest uppercase" style={{ color: `${accentColor}99` }}>
                Challenge {number}
              </span>
              <div className="flex-1 h-px" style={{ backgroundColor: `${accentColor}20` }} />
              <span className="font-mono text-xs text-[#B0AFA8]/40">
                {expanded ? '▼' : '▶'}
              </span>
            </div>
            <h3 className="text-3xl md:text-4xl font-bold text-[#E5E4E2] group-hover:text-[#C0A040] transition-colors">
              {title}
            </h3>
            <p className="text-[#B0AFA8] mt-1 text-sm">{subtitle}</p>
          </div>
        </div>

        {/* Content */}
        <div className={`transition-all duration-500 overflow-hidden ${expanded ? 'max-h-[5000px] opacity-100' : 'max-h-0 opacity-0'}`}>
          {children}
        </div>
      </div>

      {/* Bottom border */}
      <div className="absolute bottom-0 left-0 right-0 h-px" style={{
        background: `linear-gradient(to right, transparent, ${accentColor}30, transparent)`
      }} />
    </section>
  );
}
