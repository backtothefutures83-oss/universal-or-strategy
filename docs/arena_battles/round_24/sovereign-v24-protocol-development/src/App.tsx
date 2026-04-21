import { useState } from "react";
import HeroSection from "./components/HeroSection";
import LatencyTable from "./components/LatencyTable";
import CacheTopologyViz from "./components/CacheTopologyViz";
import FenceAnalysis from "./components/FenceAnalysis";
import RealPatterns from "./components/RealPatterns";
import ClaimsAudit from "./components/ClaimsAudit";
import Footer from "./components/Footer";

type Tab = "audit" | "latency" | "topology" | "fences" | "patterns";

const TABS: { id: Tab; label: string; icon: string }[] = [
  { id: "audit",    label: "Claims Audit",      icon: "🔍" },
  { id: "latency",  label: "Latency Reference",  icon: "⏱️" },
  { id: "topology", label: "Cache Topology",     icon: "🖥️" },
  { id: "fences",   label: "Fence Analysis",     icon: "🚧" },
  { id: "patterns", label: "Real Patterns",      icon: "⚙️" },
];

export default function App() {
  const [activeTab, setActiveTab] = useState<Tab>("audit");

  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 font-mono">
      <HeroSection />

      {/* Tab Navigation */}
      <nav className="sticky top-0 z-50 bg-gray-900/95 backdrop-blur border-b border-gray-800 shadow-xl">
        <div className="max-w-7xl mx-auto px-4">
          <div className="flex overflow-x-auto scrollbar-hide gap-1 py-2">
            {TABS.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`
                  flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold whitespace-nowrap transition-all duration-200
                  ${activeTab === tab.id
                    ? "bg-red-600 text-white shadow-lg shadow-red-900/40"
                    : "text-gray-400 hover:text-white hover:bg-gray-800"
                  }
                `}
              >
                <span>{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            ))}
          </div>
        </div>
      </nav>

      {/* Tab Content */}
      <main className="max-w-7xl mx-auto px-4 py-10">
        {activeTab === "audit"    && <ClaimsAudit />}
        {activeTab === "latency"  && <LatencyTable />}
        {activeTab === "topology" && <CacheTopologyViz />}
        {activeTab === "fences"   && <FenceAnalysis />}
        {activeTab === "patterns" && <RealPatterns />}
      </main>

      <Footer />
    </div>
  );
}
