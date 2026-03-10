# BMad Hardened Infrastructure Protocol

**Purpose:** Eliminate the "Old Code" failure mode by enforcing a single, verifiable source of truth between the repo and NinjaTrader 8.

---

## 1. One Source of Truth (HardLink)

All strategy files in the NinjaTrader directory MUST be hardlinks to files in `C:\WSGTA\universal-or-strategy\src`.

- **Verification Command (run at session start):**
  `fsutil hardlink list "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\V12_002.cs"`
- **Success Criteria:** The command must return both the NinjaTrader path and the matching `C:\WSGTA\universal-or-strategy\src\V12_002.cs` path. If it returns only one path, the hardlink is broken.

## 2. Deployment Protocol

If a hardlink is broken or files need refreshing, run the deployment script:

- **Script:** `C:\WSGTA\universal-or-strategy\deploy-sync.ps1`
- **Policy:** Never manually copy and paste code into the NinjaTrader editor. Always edit in the repo and let the hardlink propagate.

## 3. Version Traceability (Build Tags)

Every forensic or architectural change must increment `BUILD_TAG` in `src\V12_002.cs`.

- **Current Build:** `960`
- **Audit Step:** On strategy start, verify the NinjaTrader Output window includes both:
  `[OK] BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE`
  `Build: 960`

## 4. Zero-Trust Policy

Never assume the code on GitHub is the code running in NinjaTrader.

1. Always verify the local build tag.
2. Always verify hardlinks using `fsutil` at the start of every session.
3. Always use `deploy-sync.ps1` to restore or establish links. Manual copy-pasting is a protocol violation.

## 5. Live Repair Protocol

If a bug is detected during active trading:

1. Edit files only in `C:\WSGTA\universal-or-strategy\src`.
2. Immediately increment the `BUILD_TAG` (for example, `959` -> `960`).
3. Save the file so the hardlink updates the NinjaTrader file instantly.
4. Instruct the user: "HardLink updated. Please press F5 in NinjaTrader to compile."
5. Verify the NinjaTrader Output window shows the new build tag.
6. Commit the repair to the current branch immediately, but hold merge until the session reset.

## 6. Git and Pull Request Workflow

To maintain code integrity, all changes must be committed to a branch and merged via pull request using the GitHub CLI.

1. Always work on a descriptive feature branch.
2. Every commit must reference the `BUILD_TAG`.
3. When a build is verified, the AI must prepare the PR:
   `git push origin [branch-name]`
   `gh pr create --title "Build [BUILD_TAG]: [Brief Description]" --body "Forensic Audit & Repairs for Build [BUILD_TAG]."`
4. Do not merge into `main` during live trading. Merge after the market close or reset.
5. AI-led persistence is mandatory: the AI agent is authorized and required to `git push` all verified repairs before closing a session.

## 8. Proactive Support & Clipboard Handoffs

To eliminate friction in multi-agent workflows, the AI agent MUST be proactive in supporting the USER's cross-platform interactions:

1. **Clipboard Proactivity:** Whenever the AI generates a prompt, mission brief, or technical report intended for another agent (e.g., Grok, Claude, Codex), it MUST automatically copy that content to the system clipboard using the `Set-Clipboard` command.
2. **Context Bundling:** Handoffs to expert models MUST include the forensic timeline, critical code snippets, and specific failure hypotheses in-line. Avoid relying on external ZIP files unless explicitly requested, to prevent "unsupported file" rejections.
3. **Confirmation:** The AI must explicitly state: "Handoff prompt copied to clipboard. Ready for [Agent Name] analysis."

---
