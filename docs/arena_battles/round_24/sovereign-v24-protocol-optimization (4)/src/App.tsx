import { NavHeader } from './components/NavHeader';
import { Hero } from './components/Hero';
import { TopologyVisualizer } from './components/TopologyVisualizer';
import { BenchmarkChart } from './components/BenchmarkChart';
import { AdaptiveStriping } from './components/AdaptiveStriping';
import { SafetyInvariants } from './components/SafetyInvariants';
import { ArchitectureDiagram } from './components/ArchitectureDiagram';
import { CodeDisplay } from './components/CodeDisplay';
import { Footer } from './components/Footer';

function App() {
  return (
    <div className="min-h-screen bg-sov-bg">
      <NavHeader />
      <Hero />
      <TopologyVisualizer />
      <BenchmarkChart />
      <AdaptiveStriping />
      <SafetyInvariants />
      <ArchitectureDiagram />
      <CodeDisplay />
      <Footer />
    </div>
  );
}

export default App;
