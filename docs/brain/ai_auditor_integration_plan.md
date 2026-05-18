# Implementation Plan: AI PR Auditor Integration (Qwen & GLM 5.1)

## 1. Goal
Integrate Qwen and GLM 5.1 as automated PR auditors following the established V12 forensic audit pattern to ensure compliance before the upcoming major refactor.

## 2. Proposed Workflows

### A. Qwen PR Audit (`.github/workflows/qwen-pr-audit.yml`)
- **Trigger**: `pull_request` [opened, synchronize, reopened]
- **Authentication**: Uses `secrets.QWEN_API_KEY`.
- **Logic**: Custom Node.js script using `fetch` to call the DashScope OpenAI-compatible API.
- **Protocol**: Includes V12 DNA (Zero-Trust IPC, FSM, ASCII-Only).

### B. OpenCode GLM PR Audit (`.github/workflows/opencode-pr-audit.yml`)
- **Trigger**: `pull_request` [opened, synchronize, reopened]
- **Authentication**: Uses `secrets.OPENCODE_API_KEY`.
- **Logic**: Custom Node.js script using `fetch` to call the Zhipu AI OpenAI-compatible API.
- **Protocol**: Includes V12 DNA (Zero-Trust IPC, FSM, ASCII-Only).

## 3. Security Guards
- **Truncation**: Diff content capped at 100,000 characters to prevent prompt injection and token overflow.
- **Sanitization**: All input content is wrapped in `JSON.stringify()` before being injected into the system prompt.

## 4. Required Secrets
The user must add the following Repository Secrets to GitHub:
1. `QWEN_API_KEY`: From Alibaba Cloud DashScope.
2. `OPENCODE_API_KEY`: From Zhipu AI (BigModel).

## 5. Verification Plan
- [ ] Verify YAML syntax for both workflows.
- [ ] Confirm models respond correctly to the V12 prompt.
- [ ] Verify that audit comments are correctly posted to PRs.
