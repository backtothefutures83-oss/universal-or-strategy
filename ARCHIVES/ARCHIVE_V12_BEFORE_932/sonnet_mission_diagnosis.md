# $MISSION: Forensic Repair — Order Cancellations & Spontaneous Executions

**Target Environment:** NinjaTrader 8 | UniversalORStrategyV12_002_Dev
**Baseline Code:** Build 923A (Hardened Sync & HardLinks Confirmed Active)
**Objective:** Diagnose and fix the root causes of spontaneous follower order cancellations AND the new spontaneous order executions.

---

## 🚩 Emergency Briefing: Failure Modes

### 🔍 Case Study 1: Spontaneous Cancellations
Follower orders are cancelling while the Master remains active. 
- *Suspects:* Move-Sync logic in `Callbacks.cs`, or REAPER "False Positive" flattens.
- *Check:* Does the `Cancel -> Create -> Submit` sequence in `PropagateMasterEntryMove` leave a "Null Hole" where the order disappears?

### 🔍 Case Study 2: Spontaneous Execution (Hallucinated Repair)
Follower accounts are executing "Repair" entries when the Master is flat/cancelled and no price trigger occurred.
- *Suspects:* `REAPER.cs` -> `ProcessReaperRepairQueue`.
- *Hypothesis:* The system "hallucinates" a need for entry on followers because `expectedPositions` was not cleared for the fleet when a Master order was rejected or manually cancelled.

---

## 🛠️ Requirements for Sonnet
1.  **Analyze & Trace**: Read the current strategy files via the HardLink. Perform a "Cross-Check" scan across `REAPER.cs`, `Callbacks.cs`, and `SIMA.cs`.
2.  **Diagnose**: Explain why the system (a) drops legitimate orders and (b) creates "Phantom" orders.
3.  **Propose**: Detail the forensic repair plan (e.g., stricter Master-Check guards) before writing code.
4.  **Implement**: Fix the logic while maintaining the "One Source of Truth" protocol. Increment the BUILD_TAG upon completion.

---

## 🚦 Instructions for User
1.  Open the other Sonnet session.
2.  Now that I've updated this mission file, tell her: **"I have an emergency update. Read the latest sonnet_mission_diagnosis.md in the root directory and begin the mission to fix BOTH the cancellations and the hallucinated follower entries."**
