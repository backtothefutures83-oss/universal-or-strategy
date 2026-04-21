import { useState } from "react";
import IngressBridge from "./components/IngressBridge";
import TaggedPointers from "./components/TaggedPointers";
import CacheConcurrencyGuard from "./components/CacheConcurrencyGuard";
import LatencySummary from "./components/LatencySummary";
import Header from "./components/Header";

const TABS = [
  { id: "ingress", label: "① Ingress Bridge", shortLabel: "Ingress" },
  { id: "tagged", label: "② Tagged Pointers", shortLabel: "Tagged Ptrs" },
  { id: "cache", label: "③ Cache Guard", shortLabel: "Cache Guard" },
  { id: "summary", label: "⊕ Latency Budget", shortLabel: "Budget" },
];

export default function App() {
  const [activeTab, setActiveTab] = useState("ingress");

  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 font-mono">
      <Header />

      {/* Tab Navigation */}
      <div className="sticky top-0 z-40 bg-gray-950/95 backdrop-blur border-b border-gray-800">
        <div className="max-w-7xl mx-auto px-4">
          <div className="flex gap-0 overflow-x-auto scrollbar-hide">
            {TABS.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`
                  relative px-5 py-3.5 text-xs font-bold tracking-widest uppercase transition-all whitespace-nowrap
                  ${activeTab === tab.id
                    ? "text-cyan-400 border-b-2 border-cyan-400 bg-cyan-400/5"
                    : "text-gray-500 hover:text-gray-300 border-b-2 border-transparent hover:bg-gray-800/40"
                  }
                `}
              >
                <span className="hidden sm:inline">{tab.label}</span>
                <span className="sm:hidden">{tab.shortLabel}</span>
                {activeTab === tab.id && (
                  <span className="absolute bottom-0 left-0 right-0 h-px bg-cyan-400/30 blur-sm" />
                )}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Content */}
      <main className="max-w-7xl mx-auto px-4 py-8">
        {activeTab === "ingress" && <IngressBridge />}
        {activeTab === "tagged" && <TaggedPointers />}
        {activeTab === "cache" && <CacheConcurrencyGuard />}
        {activeTab === "summary" && <LatencySummary />}
      </main>

      {/* Footer */}
      <footer className="border-t border-gray-800/60 mt-16 py-6 text-center text-xs text-gray-600">
        <span className="text-gray-500">HFT Packet Pipeline · Engineering Design Reference</span>
        <span className="mx-3 text-gray-700">|</span>
        <span className="text-cyan-900">Target: &lt;5ns total cycle budget</span>
      </footer>
    </div>
  );
}
