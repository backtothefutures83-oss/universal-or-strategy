# PR Audit Report Workflow ($prreport)

**Description:** Generates a comprehensive, non-prescriptive report of all Pull Request audit reviews, status checks, and comments for the Architect. This ensures visibility of all automated reviews (including background actions like Jules, OpenCode, and Qwen) to aid in structural decision-making without prescribing specific fixes.

## Trigger
- User command: `$prreport`

## Steps

1. **Fetch PR Status and Checks:**
   - Identify active/relevant PRs (e.g., via branch `phase-5-distributed-pipeline-v2`).
   - Use the `gh` CLI to fetch the PR checks: `gh pr checks <PR_NUMBER>`.
   - Verify that all checks (other than those intentionally skipped due to limits, e.g., Sourcery) executed.

2. **Fetch PR Comments and Reviews:**
   - Run `gh pr view <PR_NUMBER> --comments` to gather automated and manual review comments.

3. **Verify Execution of Critical Audits:**
   - Explicitly verify the presence/execution of:
     - Jules (or equivalent configured reviewers)
     - OpenCode (GLM)
     - Qwen (QwenLM)
   - *Note:* Some actions may be configured as `continue-on-error: true` and only post comments or silent logs.

4. **Compile the Report:**
   - Generate an artifact (e.g., `artifacts/pr_audit_report.md`).
   - Organize the report by PR number.
   - For each PR, list:
     - **Status Checks Summary** (Pass/Fail/Skip).
     - **Audit Findings** (extracted from comments like Cubic, Gemini Code Assist, SonarCloud, CodeRabbit, etc.).
   - **Crucial Rule:** Ensure the language is *non-prescriptive*. Report the facts, findings, and identified locations without dictating the architectural solution.

5. **Present to Architect:**
   - Provide the generated artifact to the Architect (Claude) as part of the context for the next planning phase.
