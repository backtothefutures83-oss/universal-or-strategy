# Brain Pulse Protocol (V12.16 Concept)
**Status**: Living Brainstorm
**Owner**: Antigravity / Gemini CLI
**Purpose**: Defining the physical synchronization protocol between local and cloud agent memory.

---

## 💓 The "Heartbeat" Philosophy
To have a "Shared Brain," all agents must be looking at the same data at the same time. The **Brain Pulse** is the automated mechanism that pushes local "Synapse Updates" (file changes, graph updates) to the cloud so remote agents (Jules) are never out of sync.

### The Pulse Workflow

| Step | Action | Tool | Effect |
| :--- | :--- | :--- | :--- |
| **1. Trigger** | Task Completion | Agent Hook | Triggered automatically at the end of a session. |
| **2. Index** | AST Update | `graphify update` | Rebuilds the local knowledge graph with new changes. |
| **3. Pulse** | Shadow Push | `git push shadow` | Pushes the graph and metadata to a hidden `memory` branch. |
| **4. Inhale** | Remote Pull | `git fetch shadow` | Cloud agents pull the branch before starting their task. |

---

## 🛡️ Implementation Details

### 1. The Shadow Branch Pattern
We avoid cluttering the `main` branch with thousands of metadata updates. Instead, we use a `shadow-memory` branch specifically for `.json` and `graph.json` files.
*   **Benefit**: High-frequency updates without "git log" noise.

### 2. The `brain_pulse.ps1` Script
A centralized engine that handles the sequence:
1.  Verify environment health.
2.  Run Graphify.
3.  Commit only `docs/brain/` and `graphify-out/`.
4.  Force-push to the shadow origin.

### 3. "Inhale" Before Work
Cloud agents (Jules, Qwen) are programmed to run `powershell scripts/inhale_brain.ps1` as their first tool call. This ensures they "wake up" with the latest local context.

---

## 🚀 Next Steps for Brainstorming
- [ ] Draft the `brain_pulse.ps1` and `inhale_brain.ps1` scripts.
- [ ] Configure the "Shadow Branch" on GitHub.
- [ ] Test the latency between "Local Pulse" and "Cloud Inhale."

---
**Last Sync**: 2026-05-18 | Session: 92c12f62
