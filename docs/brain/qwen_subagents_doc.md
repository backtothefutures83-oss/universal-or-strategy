# Qwen Subagents Feature Guide

Subagents are specialized AI assistants that handle specific types of tasks within Qwen Code. They allow you to delegate focused work to AI agents that are configured with task-specific prompts, tools, and behaviors.

## What are Subagents?
Subagents are independent AI assistants that:
* **Specialize in specific tasks** - Each Subagent is configured with a focused system prompt for particular types of work.
* **Have separate context** - They maintain their own conversation history, separate from your main chat.
* **Use controlled tools** - You can configure which tools each Subagent has access to.
* **Work autonomously** - Once given a task, they work independently until completion or failure.
* **Provide detailed feedback** - You can see their progress, tool usage, and execution statistics in real-time.

---

## Fork Subagent (Implicit Fork)
In addition to named subagents, Qwen Code supports implicit forking — when the AI omits the `subagent_type` parameter, it triggers a fork that inherits the parent’s full conversation context.

### How Fork Differs from Named Subagents

| Dimension | Named Subagent | Fork Subagent |
| :--- | :--- | :--- |
| **Context** | Starts fresh, no parent history | Inherits parent’s full conversation history |
| **System Prompt** | Uses its own configured prompt | Uses parent’s exact system prompt (for cache sharing) |
| **Execution** | Blocks the parent until done | Runs in background, parent continues immediately |
| **Use Case** | Specialized tasks (testing, docs) | Parallel tasks that need the current context |

### When Fork is Used
The AI automatically uses fork when it needs to:
1. Run multiple research tasks in parallel (e.g., “investigate module A, B, and C”).
2. Perform background work while continuing the main conversation.
3. Delegate tasks that require understanding of the current conversation context.

### Prompt Cache Sharing
All forks share the parent’s exact API request prefix (system prompt, tools, conversation history), enabling DashScope prompt cache hits. When 3 forks run in parallel, the shared prefix is cached once and reused — saving 80%+ token costs compared to independent subagents.

### Recursive Fork Prevention
Fork children cannot create further forks. This is enforced at runtime — if a fork attempts to spawn another fork, it receives an error instructing it to execute tasks directly.

### Current Limitations
* **No result feedback**: Fork results are reflected in the UI progress display but are not automatically fed back into the main conversation. The parent AI sees a placeholder message and cannot act on the fork’s output.
* **No worktree isolation**: Forks share the parent’s working directory. Concurrent file modifications from multiple forks may conflict.

---

## CLI Commands
Subagents are managed through the `/agents` slash command and its subcommands:
* **`/agents create`**: Creates a new Subagent through a guided step wizard.
* **`/agents manage`**: Opens an interactive management dialog for viewing and managing existing Subagents.

---

## Storage Locations
Subagents are stored as Markdown files in multiple locations:
1. **Project-level**: `.qwen/agents/` (highest precedence)
2. **User-level**: `~/.qwen/agents/` (fallback)
3. **Extension-level**: Provided by installed extensions

---

## File Format
Subagents are configured using Markdown files with YAML frontmatter.

### Basic Structure

```markdown
---
name: agent-name
description: Brief description of when and how to use this agent
model: inherit # Optional: inherit or model-id
approvalMode: auto-edit # Optional: default, plan, auto-edit, yolo
tools:         # Optional: allowlist of tools
  - tool1
  - tool2
disallowedTools: # Optional: blocklist of tools
  - tool3
---
System prompt content goes here.
Multiple paragraphs are supported.
```

---

## Key Settings

### 1. Model Selection
* **`inherit`** (or omitted): Use the same model as the parent.
* **`glm-5`**: Use that model ID.
* **`openai:gpt-4o`**: Use a different provider (resolves credentials from env vars).

### 2. Permission Mode (`approvalMode`)
* **`default`**: Tools require interactive approval.
* **`plan`**: Analyze-only mode — the agent plans but does not execute changes.
* **`auto-edit`**: Tools are auto-approved without prompting (recommended).
* **`yolo`**: All tools auto-approved, including potentially destructive ones.

*Note: The parent session's permissive modes still take priority. For example, if the parent is in yolo mode, a subagent with approvalMode: plan will still run in yolo mode.*

### 3. Tool Configuration
* **`tools`** (allowlist): When specified, the subagent can only use the listed tools.
* **`disallowedTools`** (blocklist): Blocks listed tools. MCP server-level patterns are supported (e.g. `mcp__slack` to block a whole server, or `mcp__server__tool_name`).

---

## Examples

### Testing Specialist
```markdown
---
name: testing-expert
description: Writes comprehensive unit tests, integration tests, and handles test automation with best practices
tools:
  - read_file
  - write_file
  - read_many_files
  - run_shell_command
---
You are a testing specialist focused on creating high-quality, maintainable tests.
...
```

### Code Reviewer
```markdown
---
name: code-reviewer
description: Reviews code for best practices, security issues, performance, and maintainability
tools:
  - read_file
  - read_many_files
---
You are an experienced code reviewer focused on quality, security, and maintainability.
...
```

### React Specialist
```markdown
---
name: react-specialist
description: Expert in React development, hooks, component patterns, and modern React best practices
tools:
  - read_file
  - write_file
  - read_many_files
  - run_shell_command
---
You are a React specialist with deep expertise in modern React development.
...
```
