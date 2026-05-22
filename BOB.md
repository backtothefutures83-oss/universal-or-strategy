# Bob IDE Reference (V12 Project Mirror)

> Official Bob documentation compiled for V12 agent routing.
> Pattern: root-level agent file like CODEX.md, JULES.md, GEMINI.md.
> Source: Bob IDE official docs, session 2026-05-14.

---

## Role in Director's Gate

| Phase | Role | Mode |
|-------|------|------|
| P3 ARCHITECT (plan-only) | Epic Planning | v12-epic-planner (custom) |
| P4/P5 ENGINEER | Surgical src/ edits | v12-engineer (custom) |
| ORCHESTRATOR | Multi-phase YOLO chaining | Orchestrator mode |
| VERIFICATION | Shell gates, commits | Advanced mode |

Bob CLI binary: `bob` (alias or path).
Custom mode config: `.bob/custom_modes.yaml`.
Custom rules: `.bob/rules-{mode-slug}/` (directory, alphabetical load order).

---

## V12.20: Documentation & Output Hardening (MANDATORY)
... (existing content) ...

## V12.21: Internal Sentinel Protocol (MANDATORY)
- **Role Separation**: Bob's Orchestrator mode MUST spawn an independent sub-agent (Internal Sentinel) for Phase 2.3 (Planning Scan) and Phase 5 (Implementation Verification).
- **Sovereign Loop**: Verification tasks must be handled internally by spawned agents. Never delegate verification to external CLI instances.
- **PHS Authority**: Only the spawned Internal Sentinel can award a 100/100 PHS.

## 1. Modes

Bob has five built-in modes plus custom modes.

### Built-In Mode Table

| Mode | Tool Access | Primary Use |
|------|------------|-------------|
| **Code** | read, edit, command | Feature implementation, bug fixes, refactoring |
| **Ask** | read, browser, mcp | Analysis, explanations -- no file edits |
| **Plan** | read, edit (markdown only), browser, mcp | Architecture planning, specs before implementation |
| **Advanced** | read, edit, command, mcp | Full-access; complex workflows needing MCP + shell |
| **Orchestrator** | **NONE** | Multi-step coordination; delegates to other modes |

### CRITICAL: Orchestrator Has Zero Tool Access

The Orchestrator mode cannot read files, run commands, or edit files.
"Delegation" = Bob switching into the target mode for that sub-task.
ALL file reads, shell commands, and edits must be delegated to a mode with the right tools:
- Planning docs -> Plan / v12-epic-planner (markdown edit only)
- Code edits -> Code / v12-engineer (read, edit, command)
- Verification shell commands -> Advanced (command + mcp)
- Analysis / MCP queries -> Ask or Advanced

### Switching Modes

- Drop-down menu left of chat input
- Slash prefix: `/plan`, `/ask`, `/code`, `/advanced`, `/orchestrator`
- Keyboard: Ctrl+. (Windows/Linux) to cycle modes
- Accept mode-switch suggestions Bob offers mid-conversation

---

## 2. Custom Modes

Custom modes are specialized personas with specific tool access and behavioral rules.

### Configuration File

`.bob/custom_modes.yaml` (project-level) or `~/.bob/custom_modes.yaml` (global).

### Mode YAML Schema

```yaml
customModes:
  - slug: my-mode-slug        # unique ID; used for rules file naming
    name: Display Name
    roleDefinition: |
      Describe the persona and primary responsibilities here.
    customInstructions: |
      Additional behavioral rules merged with rules file content.
    groups:
      - read
      - - edit
        - fileRegex: "^docs/"   # restrict edits to docs/ only
          description: Planning docs only
      - command
      - mcp
      - browser
```

### Tool Groups

| Group | Capability |
|-------|-----------|
| read | Read files, list directories |
| edit | Write/modify files (add fileRegex to restrict paths) |
| command | Run shell commands |
| mcp | Call MCP server tools |
| browser | Web browsing |

### V12 Custom Modes (Active)

```yaml
# v12-epic-planner: Plan-only for epic phase generation
- slug: v12-epic-planner
  groups:
    - read
    - [edit, fileRegex: "^docs/"]  # docs/ only -- NEVER src/
    - mcp

# v12-engineer: Full surgical access for ticket execution
- slug: v12-engineer
  groups:
    - read
    - edit
    - command
```

---

## 3. Slash Commands

Custom slash commands live in `.bob/commands/` (project) or `~/.bob/commands/` (global).
Each command is a `.md` file. Fuzzy search and autocomplete available via `/` in chat.

### Frontmatter

```markdown
---
description: Short description shown in the command picker
argument-hint: <arg1> <arg2>
---
# Command Title
$1 = first argument, $2 = second argument
```

### Active V12 Commands

| Command | File | Purpose |
|---------|------|---------|
| `/epic-intake` | `.bob/commands/epic-intake.md` | Phase 1: Scope definition |
| `/epic-plan` | `.bob/commands/epic-plan.md` | Phase 2: Analysis + approach |
| `/epic-validate` | `.bob/commands/epic-validate.md` | Phase 3: DNA compliance audit |
| `/epic-tickets` | `.bob/commands/epic-tickets.md` | Phase 4: Ticket generation |
| `/ticket` | `.bob/commands/ticket.md` | Single ticket execution |
| `/epic-run` | `.bob/commands/epic-run.md` | YOLO-parity full orchestration |

Built-in commands: `/init`, `/review`, `/compact`, `/help`.

---

## 4. Custom Rules

Rules files inject behavioral constraints into a mode automatically.

### File Naming Convention

| Location | General rules | Mode-specific rules |
|----------|--------------|-------------------|
| Project | `.bob/rules/` | `.bob/rules-{mode-slug}/` |
| Global | `~/.bob/rules/` | `~/.bob/rules-{mode-slug}/` |

Directory method is preferred. Single-file alternative: `.bobrules-{mode-slug}`.

Files load alphabetically within each directory. Mode-specific rules load before general rules.
All files in a directory are read recursively. Empty files are silently skipped.

### Rule Priority (High to Low)

1. Global rules (`~/.bob/rules/`)
2. Workspace rules (`.bob/rules/`)
3. Within each: mode-specific before general; workspace overrides global

### AGENTS.md Loading

Bob automatically loads `AGENTS.md` from workspace root after mode-specific rules.
Disable with `"bob-code.useAgentRules": false` in settings.

### V12 Active Rules Files

```
.bob/rules-v12-epic-planner/
  01-planning-protocol.md      # Enforces docs/-only, DNA compliance, gate protocol
.bob/rules-v12-engineer/
  dna.md                       # Lock-free, ASCII-only, deploy-sync requirements
```

---

## 5. Code Actions

Code actions appear as a lightbulb icon in the editor gutter when code is selected.

| Action | Description | Shortcut |
|--------|-------------|---------|
| Add to Context | Adds code + file path/line numbers to chat | First in menu |
| Explain Code | Asks Bob to explain selection | Second |
| Improve Code | Asks Bob to suggest improvements | Third |
| Inline Chat | Opens chat at cursor position | Ctrl+K (Win) |
| Move to Chat | Sends selection to chat panel with context | Ctrl+L (Win) |

Context mention format: `@myFile.cs:15:25` (file:startLine:endLine).
Use line ranges for targeted context to minimize token consumption.

---

## 6. Checkpoints

Bob automatically creates a checkpoint before every file modification.
Uses a shadow Git repository separate from main version control.
No commands needed -- checkpoints are fully automatic.

### Key Facts

- Created BEFORE file modifications (not before commands)
- Task-scoped: checkpoints belong to the task that created them
- Not created for external edits (manual saves, other tools)
- Large binary files may impact performance

### Restore Options (via Chat UI)

| Option | Effect |
|--------|--------|
| Restore files | Reverts workspace files only; keeps chat history |
| Restore files & task | Reverts files AND removes subsequent conversation messages (irreversible) |

### What Checkpoints Do NOT Cover

- Shell command output (only file mutations)
- Files excluded by `.gitignore` or `.bobignore`
- External changes made outside Bob tasks

### V12 Workflow Implication

The checkpoint safety net means no need for manual checkpoint commands in epic workflows.
If a ticket edit goes wrong, Director restores from checkpoint via UI before the next ticket.

---

## 7. Context Window Management

Bob's context window: **200,000 tokens total**.
Reserved for responses: ~50,000 tokens.
Effective usable window: ~150,000 tokens.

### Quality Thresholds

| Threshold | Effect |
|-----------|--------|
| ~100k tokens | Quality noticeably degrades; responses become less precise |
| 140k tokens | Auto-condensation triggers (lossy -- edge cases may be lost) |
| 200k tokens | Hard limit |

### What Consumes Tokens

- System instructions + mode rules (always present)
- Full conversation history (every message, tool call, result)
- File contents via `@` mentions
- MCP tool definitions (each connected server adds tokens)
- Bob's own responses

### Best Practices

- Start a new chat when switching tasks -- do not let unrelated context accumulate
- Use `@file:startLine-endLine` for targeted mentions, not whole directories
- Only connect MCP servers you actively use (each adds token overhead)
- For large files, reference only the relevant section
- Break complex tasks into focused sub-sessions

### V12 Epic Session Strategy

```
Planning session (phases 1-4): stays under 100k for most epics
Execution session: fresh session per batch of 3-4 tickets
Resume state: EXECUTION_GUIDE.md carries all context between sessions
Rule: split planning and execution for any epic with > 3 tickets
```

---

## 8. Context Poisoning

Context poisoning = inaccurate or irrelevant data contaminating the active context.
Once poisoned, the context cannot be reliably repaired with prompts. Only a new session fixes it.

### Symptoms

- Degraded output quality (nonsensical, repetitive, irrelevant suggestions)
- Tool misalignment (tool calls don't match requests)
- **Orchestration failures: chains stall, loop indefinitely, or fail to complete**
- Temporary fixes work briefly then revert
- Tool usage confusion (Bob forgets how to use tools from system prompt)

### Common Causes

- Model hallucination treated as factual context in subsequent turns
- Outdated or incorrect code comments misinterpreted
- Pasted logs containing hidden control characters
- Context window overflow causing poisoned data to dominate

### Recovery

**No prompt reliably fixes context poisoning.** The corrupted text persists in session history.

Recovery sequence:
1. Abandon the current session
2. Start a new session
3. Load resume state from `EXECUTION_GUIDE.md` or the relevant ticket file
4. Continue from the last confirmed-complete step

### V12 Orchestrator Red Flags (Stop Immediately)

- Orchestrator re-runs a phase it already completed
- Gate questions reference wrong epic slug or wrong ticket number
- Sub-task brief points to non-existent file paths
- Orchestrator tries to delegate to a mode that doesn't exist
- Any of the above: STOP, save progress to `EXECUTION_GUIDE.md`, start fresh session

---

## 9. Session Management Summary for V12 Epics

```
DO:
  - Start a new session for each distinct epic
  - Split planning and execution into separate sessions (> 3 tickets)
  - Use @file:line-range for targeted context
  - Let checkpoints handle rollback (no manual checkpoint commands)
  - Watch for context poisoning signals in long orchestrator sessions
  - Resume from EXECUTION_GUIDE.md after any session restart

DO NOT:
  - Run all tickets for a large epic in one session
  - Use broad @dir mentions
  - Try to "wake up" a poisoned orchestrator with corrective prompts
  - Run shell commands from Orchestrator mode (no tool access)
  - Leave unused MCP servers connected (each adds token overhead)
```

---

_Last Updated: 2026-05-14 (Session: Bob CLI YOLO Orchestration Hardening)_
