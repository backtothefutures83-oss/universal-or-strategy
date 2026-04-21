# Graphify Hardening Runbook

This runbook defines the "Universal Knowledge Layer" for the V12 Universal OR Strategy project. Graphify builds a persistent, queryable knowledge graph to provide agents with 71x token efficiency during architectural research.

## 🚀 Commands for Agents

### 1. Repository Scan (Initial/Incremental)

Run this after major structural changes to sync the graph.

```powershell
graphify update .
```

### 2. Architectural Query

Agents should BFS the graph before reading files.

```powershell
graphify query "What are the dependencies of the OrderFlowTracker module?"
```

### 3. Multifactor Pathfinding

Find how two distant modules relate.

```powershell
graphify path "ExecutionEngine" "SmaCrossover"
```

## 📂 Output Artifacts

- **`graphify-out/graph.json`**: The raw knowledge graph (Machine-readable).
- **`graphify-out/GRAPH_REPORT.md`**: Human-readable architectural summary.
- **`graphify-out/graph.html`**: Interactive D3 visualization.

## 🧠 Multimodal Integration

Drop research papers (PDF), UI screenshots (PNG), or design specs (MD) into `docs/brain/corpus/`. Graphify will index them in the next scan.

---

_V12 Hardening Build 981_
