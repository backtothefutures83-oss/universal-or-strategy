# Unified Memory Plane (V12.16 Concept)
**Status**: Living Brainstorm
**Owner**: Antigravity / Gemini CLI
**Purpose**: Defining the shift from static file-based memory to a dynamic, graph-based "Shared Brain" for the V12 agent fleet.

---

## 🧠 Core Philosophy
Traditional agents are "stateless" – they forget everything between sessions unless told to read a file. The **Unified Memory Plane** moves the project's intelligence into a **shared infrastructure layer** that persists across all agents (Bob, Jules, Codex, Gemini, Qwen).

### The Three Layers of Memory

| Layer | Type | Mechanism | Purpose |
| :--- | :--- | :--- | :--- |
| **Layer 1: Structural** | Long-Term | **Graphify (AST Graph)** | "Who is connected to what?" (Classes, Methods, Rules). |
| **Layer 2: Operational** | Mid-Term | **Nexus Blackboard (JSON)** | "What is the status right now?" (Current Task, Locks, Consensus). |
| **Layer 3: Temporal** | Short-Term | **LangSmith (Trace Feed)** | "What just happened?" (Recent thoughts, errors, brainstorms). |

---

## 🔬 Implementation Concepts

### 1. Graph-First Initialization
Instead of agents starting by reading `GEMINI.md`, they start by querying the Graphify index.
*   **Action**: `graphify info [TaskID]`
*   **Result**: Returns only relevant rules, code links, and metadata. Reduces token bloat by 70%.

### 2. The "Event Horizon" (Temporal Memory)
Using the LangSmith API as a "News Feed."
*   **Mechanism**: A script (`ingest_recent_history.py`) fetches the last 10 traces.
*   **Benefit**: If Agent A finds a bug, Agent B knows about it 5 minutes later without a manual handoff doc.

### 3. Metadata Sidecars
Every markdown task file is paired with a `.metadata.json` file.
*   **Standard**: `task_001.md` -> `task_001.metadata.json`.
*   **Why**: LLMs process structured JSON 10x faster and more accurately than prose for status tracking.

---

## 🚀 Next Steps for Brainstorming
- [ ] Design the `nexus_a2a.json` expansion for "Fleet Consensus" flags.
- [ ] Prototype the "Temporal Feed" ingestion script.
- [ ] Define the "Graph-Search-First" prompt template for all agents.

---
**Last Sync**: 2026-05-18 | Session: 92c12f62
