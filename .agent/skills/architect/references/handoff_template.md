# Director's Handoff Block Template

End EVERY response with this block. Fill in all sections. Never omit it.

---

````
---
## Director's Handoff Block

**Status**: [PLAN READY FOR APPROVAL | SIGNED OFF | CONDITIONAL PASS | FAIL — ESCALATE | AWAITING DIRECTOR CLARIFICATION]
**Plan Location**: docs/brain/implementation_plan.md
**Build Tag**: [BUILD_TAG]

**UltraPlan Result:**
  Gates Passed : [N]/[total]
  Gates Failed : [list or NONE]
  Defects Fixed: [list or NONE]
  Ralph Wiggum Found: [highest-risk issue Agent B surfaced, or NONE]

---
### Director Action Required

1. [Primary action — e.g. "Press F5 in NinjaTrader"]
2. [Verify action — e.g. "Confirm banner shows Build 1111.002-v28.0"]
3. [Validation action — e.g. "Run 10-min paper session; check Process Explorer for handle leaks"]
4. Report PASS/FAIL back to Antigravity for Nexus state update.

---
### Engineer Prompt (copy-paste this entire block to Codex/Jules)

[ENGINEER MISSION -- P4 EXECUTION]

You are the P4 ENGINEER. The Director has approved the implementation_plan.md.
Execute it exactly. Do not deviate. Do not interpret -- copy-paste the code blocks as written.

**Files to modify:**
- `src/[File1.cs]` -- [what changes]
- `src/[File2.cs]` -- [what changes]

**Instructions:**
[Paste the full Before/After code blocks from the plan here.
Do NOT summarize. Do NOT paraphrase. The ENGINEER gets exact code.]

**Self-Audit before handoff (mandatory):**
1. `Monitor(command="grep -rn \"lock(stateLock)\" src/")` -- must return 0 results
2. `Monitor(command="python scripts/check_ascii.py")` -- must PASS
3. `Monitor(command="grep -rn \"PendingCancel\" src/")` -- FSM guard lines present (if follower orders changed)
4. Dry-run regression: walk through the fix logic step-by-step vs. the Mission Brief

**Post-Edit Deployment (mandatory -- never skip):**
`Monitor(command="powershell -File .\\deploy-sync.ps1")`
ASCII Gate must PASS in streamed output.
Then tell Director: "Press F5 in NinjaTrader to compile" and verify banner shows new BUILD_TAG.

**Output expected:**
- Diff summary of all changes
- Self-audit results (pass/fail per check)
- Any anomalies found during audit (report even if you fixed them)

## [END ENGINEER MISSION]

---
### Next Agent
NEXT AGENT: [Antigravity (if PASS) | Codex via Monitor (if patch needed) | Director hold]
NEXT PHASE: [brief description — e.g. "R29 Arena cycle" or "Patch 28.1 execution"]
```

## Rules for Filling the Template

1. The Engineer Prompt must be **self-contained** — the ENGINEER has no other context.
2. Do NOT write "refer to the plan" — paste the actual code blocks inline.
3. If the fix spans multiple files, list each file with its own instruction block.
4. **Status** must be one of: `PLAN READY FOR APPROVAL`, `SIGNED OFF`, `CONDITIONAL PASS`, `FAIL — ESCALATE`, or `AWAITING DIRECTOR CLARIFICATION`.
5. **UltraPlan Result** must always be filled — never leave as placeholder.
6. **Ralph Wiggum Found** must name the highest-risk issue Agent B surfaced, even if resolved.
7. All shell commands in the Engineer Prompt must use `Monitor(command="...")` syntax.
````
