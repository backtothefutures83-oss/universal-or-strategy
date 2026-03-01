# BMad Hardened Infrastructure Protocol

**Purpose:** To eliminate the "Old Code" failure mode by ensuring a single, verifiable source of truth between the development environment and NinjaTrader 8.

---

## 🛡️ 1. The "One Source of Truth" (HardLink)
All strategy files in the NinjaTrader directory MUST be **HardLinks** to the files in `C:\WSGTA\universal-or-strategy\src`.
*   **Verification Command (Run by AI at start of session):**
    `fsutil hardlink list "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\UniversalORStrategyV12_002_Dev.cs"`
*   **Success Criteria:** The command MUST return both the NinjaTrader path and the `C:\WSGTA` path. If it only returns one path, the link is broken.

## 🛠️ 2. Deployment Protocol
If the link is broken or files need refreshing, run the deployment script:
*   **Script:** `C:\WSGTA\universal-or-strategy\deploy-sync.ps1`
*   **Policy:** Never manually copy/paste code into the NinjaTrader editor. Always edit in the Repo and let the HardLink propagate.

## 🏷️ 3. Version Traceability (Build Tags)
Every forensic or architectural change MUST be accompanied by an increment to the `BUILD_TAG` in `UniversalORStrategyV12_002_Dev.cs`.
*   **Current Build:** `930`
*   **Audit Step:** On strategy start, verify the NinjaTrader Output window shows:
    `🛡 BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE | Build: 930`

## 🔎 4. Zero-Trust Policy
Never assume the code on GitHub is the code running in NinjaTrader.
1.  **Always** verify the local Build Tag.
2.  **Always** verify HardLinks using `fsutil` at the start of every session.
3.  **Mandatory Script:** Use `deploy-sync.ps1` to restore or establish links. Manual copy-pasting is a violation of the Security Shield.

## 🛠️ 5. Live Repair Protocol (Emergency)
If a bug is detected during active trading:
1.  **Repair Location:** Edit files ONLY in `C:\WSGTA\universal-or-strategy\src`.
2.  **Build Increment:** Immediately increment the `BUILD_TAG` (e.g., `923B` -> `923C`).
3.  **Propagation:** Save the file; the HardLink will update NinjaTrader's file instantly.
4.  **User Action:** Instruct the user: "HardLink updated. Please press F5 in NinjaTrader to compile."
5.  **Audit:** Verify the NinjaTrader Output window shows the NEW Build Tag.
6.  **Commit:** Commit the repair to the current branch immediately, but **Hold Merge** until the session reset.

## 🏁 6. Git & Pull Request Workflow
To maintain code integrity, all changes must be committed to a branch and merged via Pull Request using the GitHub CLI (`gh`).
1.  **Branching:** Always work on a descriptive feature branch (e.g., `audit/full-codebase-review`).
2.  **Commit Policy:** Every commit must reference the `BUILD_TAG`.
3.  **PR Action:** When a build is verified, the AI must automatically prepare the PR:
    *   `git push origin [branch-name]`
    *   `gh pr create --title "Build [BUILD_TAG]: [Brief Description]" --body "Forensic Audit & Repairs for Build [BUILD_TAG]."`
4.  **Merge Rule:** Do not merge into `main` during live trading. Merges should happen after the market close/reset.

## 🏁 7. Handoff Requirements
When closing a coding session, the AI must confirm:
1.  **Sync Status:** "All files HardLinked and verified via fsutil."
2.  **Build Tag:** "Current code signature is [BUILD_TAG]."
3.  **Compile Status:** "User advised to press F5 in NinjaTrader."
