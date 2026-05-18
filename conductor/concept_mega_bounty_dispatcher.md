# Mega-Bounty Dispatcher (V12.16 Concept)
**Status**: Living Brainstorm
**Owner**: Antigravity / Gemini CLI
**Purpose**: Automating the 7-Cluster parallel bug hunt across the entire multi-agent fleet (Bob, Jules, Codex, Gemini, Qwen).

---

## 🚀 The Vision
Yesterday's pilot proved that running 5 different agent "brains" against the same 7 clusters results in **Red Team Consensus** – where bugs found by multiple agents are high-signal and repair-ready. This script automates that manual pilot into a single command.

### 1. The Execution Matrix (35 Sessions)
| Agent | Infrastructure | Cluster Loop (S1-S7) |
| :--- | :--- | :--- |
| **Jules** | **Cloud (VM)** | Parallel (7 simultaneous dispatches). |
| **Bob** | **Local (CLI)** | Batched (S[i] with Codex). |
| **Codex** | **Local (CLI)** | Batched (S[i] with Bob). |
| **Gemini** | **Local (CLI)** | Batched (S[i] with Qwen). |
| **Qwen** | **Local (CLI)** | Batched (S[i] with Gemini). |

---

## 🛡️ Technical Design

### 1. Throttled Triggering (Local CPU Safety)
To prevent local system crashes, the script uses a **Batch-Gate**:
- **Phase 0**: Launch 7 Jules Cloud VMs (0% local CPU).
- **Phase 1-7**: 
  - Run `Bob` + `Codex` on S[i]. Wait for completion.
  - Run `Gemini` + `Qwen` on S[i]. Wait for completion.
  - Increment to S[i+1].

### 2. Unified Report Sink
Every agent is forced to write to a standardized path:
`docs/brain/bug_report_[cluster]_[agent].md`

### 3. Total Observability
Each of the 35 sessions triggers:
`python scripts/emit_fleet_telemetry.py [Agent] "Bounty_Sweep_S[i]" ...`

---

## 🚀 Next Steps
- [ ] Create internal `/bug-bounty` command files for Gemini, Qwen, and Codex.
- [ ] Draft `scripts/v12_mega_bounty_dispatch.ps1`.
- [ ] Test the "Batch-Gate" logic with a single cluster (S1).

---
**Last Sync**: 2026-05-18 | Session: 92c12f62
