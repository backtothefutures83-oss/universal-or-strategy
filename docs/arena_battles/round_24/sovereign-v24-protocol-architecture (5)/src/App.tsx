import React from 'react';
import Navigation from './components/Navigation';
import HeroSection from './components/HeroSection';
import MetricsPanel from './components/MetricsPanel';
import TopologyMap from './components/TopologyMap';
import StripingViz from './components/StripingViz';
import SafetyPanel from './components/SafetyPanel';
import CodeDisplay from './components/CodeDisplay';
import Footer from './components/Footer';

const App: React.FC = () => {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-200">
      {/* Fixed grid background */}
      <div className="fixed inset-0 grid-bg animate-grid-flow pointer-events-none z-0" />
      
      {/* Content */}
      <div className="relative z-10">
        <Navigation />
        <HeroSection />
        <MetricsPanel />
        <TopologyMap />
        <StripingViz />
        <SafetyPanel />
        <CodeDisplay />
        <Footer />
      </div>
    </div>
  );
};

export default App;
