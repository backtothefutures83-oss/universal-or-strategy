# Qwen Headless Mode Feature Guide

Headless mode allows you to run Qwen Code programmatically from command line scripts and automation tools without any interactive UI. This is ideal for scripting, automation, CI/CD pipelines, and building AI-powered tools.

## Overview
The headless mode provides a headless interface to Qwen Code that:
* Accepts prompts via command line arguments or stdin.
* Returns structured output (text or JSON).
* Supports file redirection and piping.
* Enables automation and scripting workflows.
* Provides consistent exit codes for error handling.
* Can resume previous sessions scoped to the current project for multi-step automation.

---

## Basic Usage

### Direct Prompts
```bash
qwen --prompt "What is machine learning?"
# Or short flag:
qwen -p "What is machine learning?"
```

### Stdin Input
```bash
echo "Explain this code" | qwen
```

### Combining with File Input
```bash
cat README.md | qwen --prompt "Summarize this documentation"
```

---

## Session Resumption (Headless)
Reuse conversation context from the current project in headless scripts:
```bash
# Continue the most recent session for this project and run a new prompt
qwen --continue -p "Run the tests again and summarize failures"
 
# Resume a specific session ID directly (no UI)
qwen --resume 123e4567-e89b-12d3-a456-426614174000 -p "Apply the follow-up refactor"
```
*Note: Session data is stored as project-scoped JSONL under `~/.qwen/projects/<sanitized-cwd>/chats`.*

---

## System Prompt Overrides

### Replace Built-in Prompt (`--system-prompt`)
```bash
qwen -p "Review this patch" --system-prompt "You are a terse release reviewer. Report only blocking issues."
```

### Append Extra Instructions (`--append-system-prompt`)
```bash
qwen -p "Review this patch" --append-system-prompt "Be terse and focus on concrete findings."
```

*Note: Custom prompts apply only to the current CLI run. Loaded memory and context files (like `QWEN.md`) are still appended after `--system-prompt`.*

---

## Output Formats

### 1. Text (Default)
Standard human-readable text.

### 2. JSON (`--output-format json`)
Returns structured data as a JSON array containing system, assistant, and result messages with execution statistics and token usage.

### 3. Stream-JSON (`--output-format stream-json`)
Emits line-delimited JSON messages immediately as they occur during execution.
* Add `--include-partial-messages` to stream real-time tokens (`message_start`, `content_block_delta`) for real-time UI/dashboard updates.

---

## Key CLI Configuration Options

| Option | Description | Example |
| :--- | :--- | :--- |
| **`--prompt`, `-p`** | Run in headless mode with prompt | `qwen -p "query"` |
| **`--output-format`, `-o`** | Output format (`text`, `json`, `stream-json`) | `qwen -p "query" -o json` |
| **`--input-format`** | Input format (`text`, `stream-json`) | `qwen --input-format text` |
| **`--include-partial-messages`** | Stream partial token deltas | `qwen -p "query" -o stream-json --include-partial-messages` |
| **`--system-prompt`** | Override system prompt | `qwen -p "query" --system-prompt "Terse reviewer."` |
| **`--append-system-prompt`** | Append system prompt | `qwen -p "query" --append-system-prompt "Focus on bugs."` |
| **`--yolo`, `-y`** | Auto-approve all tool actions | `qwen -p "query" --yolo` |
| **`--approval-mode`** | Set approval mode (`auto_edit`, `plan`, `yolo`) | `qwen -p "query" --approval-mode auto_edit` |
| **`--continue`** | Resume the most recent session | `qwen --continue -p "next step"` |
| **`--resume [sessionId]`** | Resume specific session ID | `qwen --resume 123e... -p "next"` |
| **`--all-files`, `-a`** | Include all files in context | `qwen -p "query" --all-files` |
| **`--include-directories`** | Include directories | `qwen -p "query" --include-directories src` |

---

## Persistent Retry Mode (CI/CD Optimization)
When Qwen Code runs in unattended environments, brief API outages (rate limits, service overloads) will not terminate the task. 

### How it works:
* Retries **transient errors** indefinitely (HTTP `429` Rate Limit, `529` Overloaded).
* Exponential backoff, capped at 5 minutes per retry.
* Heartbeat printed to `stderr` every 30 seconds to prevent CI runners from killing the process due to inactivity.

### Activation:
Set the `QWEN_CODE_UNATTENDED_RETRY` environment variable:
```bash
export QWEN_CODE_UNATTENDED_RETRY=1
```
*(Required opt-in: `CI=true` alone does not activate it to prevent infinite-wait CI hangs).*
