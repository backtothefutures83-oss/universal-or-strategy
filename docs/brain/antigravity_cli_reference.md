# Google Antigravity & Antigravity CLI: Comprehensive Reference Manual

This document provides a highly structured reference for **Google Antigravity 2.0** and the **Antigravity CLI**. It covers architecture, installation, CLI startup parameters, interactive slash commands, configuration files, keyboard shortcuts, and the advanced programmatic hook system.

---

## 1. Architectural Overview & Paradigm Shift

Google Antigravity represents a fundamental shift in AI-assisted development, moving from traditional inline code completions to an **agent-first development paradigm**. 

*   **Agent-First Execution**: Instead of acting as a passive assistant, Antigravity treats the AI as an autonomous actor. Developers assume the role of an "architect" or "manager," defining high-level goals and reviewing plan/code deliverables.
*   **Tangible Deliverables (Artifacts)**: To bridge the trust gap, agents generate versioned, commentable files—such as markdown plans, architecture diagrams, code diffs, and browser recordings—that the developer reviews before approving execution.
*   **Dual Platforms (Desktop & Terminal)**:
    1.  **Antigravity 2.0 (Desktop)**: A standalone application built on a Visual Studio Code foundation. It features an Editor view and an **Agent Manager** view for concurrent orchestration of parallel workspaces and agent sessions.
    2.  **Antigravity CLI (Terminal)**: A lightweight, Go-based, terminal-centric user interface (TUI) sharing the same agent harness backend. Designed for remote SSH sessions, keyboard-only developers, and low-latency local terminal environments. It replaces the legacy *Gemini CLI*.

---

## 2. Installation and Authentication

The Antigravity CLI can be installed across all major operating systems.

### Installation Commands
*   **Mac/Linux (Bash)**:
    ```bash
    curl -fsSL https://antigravity.google/cli/install.sh | bash
    ```
*   **Windows (PowerShell)**:
    ```powershell
    irm https://antigravity.google/cli/install.ps1 | iex
    ```
*   **Windows (CMD)**:
    ```cmd
    curl -fsSL https://antigravity.google/cli/install.cmd -o install.cmd && install.cmd && del install.cmd
    ```

### Authentication Modes
The CLI uses the OS secure keyring for silent session storage and authentication.
*   **Local Session**: If no session exists, the CLI automatically opens the default web browser to the Google Sign-In page.
*   **Remote/SSH Session**: The CLI displays a secure authorization URL. Copy and open this URL in your local browser to log in, then paste the generated authorization code back into your remote terminal prompt.
*   **Session Termination**: Type `/logout` to clear saved credentials and terminate the session.

---

## 3. CLI Launcher Parameters (`agy`)

The `agy` command is the official launcher for the Google Antigravity editor and shell interface. It supports VS Code-compatible parameters:

```bash
agy [path/to/project_or_file] [options]
```

### Command Flags
| Flag | Short | Description |
| :--- | :--- | :--- |
| `--new-window` | `-n` | Open a new window instead of reusing an active window. |
| `--reuse-window` | `-r` | Force reuse of the most recently active window. |
| `--goto <file:line[:col]>` | `-g` | Open a specific file at a designated line and column. |
| `--diff <file1> <file2>` | `-d` | Open a diff editor comparing two files side by side. |
| `--add <folder>` | `-a` | Add the specified folder to the current active workspace. |
| `--wait` | `-w` | Wait for files to be closed in the editor before returning. |
| `--user-data-dir <dir>` | — | Specify a custom user data directory for configurations. |
| `--extensions-dir <dir>` | — | Specify a custom directory for installed extensions. |
| `--sandbox` | — | Force execution of agent processes inside a sandbox (overrides `settings.json`). |
| `--dangerously-skip-permissions`| — | Bypass interactive permission checks for the current session. |
| `--version` | `-v` | Display CLI version info and exit. |
| `--help` | `-h` | Display help instructions and exit. |

---

## 4. Interactive Slash Commands (TUI Interface)

Slash commands are typed directly into the prompt box inside the Antigravity CLI session to control agents, configure environments, and manage transcripts.

### Conversation & Session Management
*   **`/resume`** (or **`/switch`**): Displays the interactive conversation picker to resume or transition between previous threads.
*   **`/rewind`** (or **`/undo`**): Rolls back the conversation history to a previous step/checkpoint (removes subsequent history).
*   **`/fork`**: Branches the current session from a designated earlier point and spins it up in a separate workspace.
*   **`/clear`**: Clears the prompt and resets the session to a clean slate.
*   **`/rename <new_name>`**: Renames the active conversation thread.
*   **`/open <path>`**: Instantly opens the specified file in your preferred external editor.
*   **`/logout`**: Disconnects from Google servers and clears OAuth session tokens.

### Agent Control & Workflows
*   **`/goal <task_instruction>`**: Directs the agent to run continuously without intermediate prompts until the target task is complete.
*   **`/grill-me`**: Prompts the agent to ask clarification questions to align on project requirements before writing code.
*   **`/schedule`**: Configures instructions to execute as a one-shot timer or recurring cron job in the background.
*   **`/browser`**: Instructs the agent to explicitly use web-browser automation capabilities (requires Chrome permissions).
*   **`/agents`**: Opens the concurrent subagents panel to monitor active background tasks, view logs, or terminate execution.

### Settings & Model Configuration
*   **`/permissions`**: Configures agent autonomy levels:
    *   `strict`: Asks for authorization on all non-read actions.
    *   `request-review`: Requests approval for critical changes (default).
    *   `always-proceed` (or `yolo`): Automatically runs actions without prompting.
*   **`/model <model_name>`**: Selects and persists the default Google model (e.g., `gemini-3.1-pro-preview`).
*   **`/config`** (or **`/settings`**): Opens a full-screen interactive overlay configuration menu.
*   **`/keybindings`**: Opens the keyboard shortcut editor.
*   **`/statusline`**: Customizes indicators in the CLI status bar.
*   **`/skills`**: Lists and manages local/global encapsulated agent capabilities.
*   **`/mcp`**: Configures Model Context Protocol (MCP) servers.
*   **`/hooks [panel | enable | disable]`**: Manages the local programmatic hook system.
*   **`/usage`** (or **`/help`** or **`?`**): Displays the inline interactive help manual.

---

## 5. Configurations: `settings.json` & `keybindings.json`

Persistent configurations are stored in the user directory:
*   CLI Config Directory: `~/.gemini/antigravity-cli/`
*   IDE Config Directory: `~/.gemini/antigravity/`

### Key Settings in `settings.json`
```json
{
  "colorScheme": "dark",
  "enableTelemetry": false,
  "trustedWorkspaces": [
    "C:\\WSGTA\\universal-or-strategy"
  ],
  "enableTerminalSandbox": true,
  "autoAcceptV2.blockedCommands": [
    "rm -rf",
    "format"
  ],
  "experimental.enableAgents": true,
  "terminal.integrated.shellIntegration.enabled": false
}
```

*   **`enableTerminalSandbox`** (boolean, default: `false`): Restricts agent shell command execution. Uses native OS mechanisms (`nsjail` on Linux, `sandbox-exec` on macOS, or `AppContainer` on Windows) to prevent dangerous operations or network access without heavy VM overhead.
*   **`autoAcceptV2.blockedCommands`** (array): Lists terminal commands that the agent is strictly prohibited from auto-accepting, even if operating in `always-proceed` / `yolo` mode.
*   **`terminal.integrated.shellIntegration.enabled`** (boolean): Set to `false` if ANSI escape sequences cause terminal blind spots or read parsing errors for the agent.

### Default Shortcuts in `keybindings.json`
Keyboard mapping can be customized directly or via `/keybindings`:
*   `Ctrl+L`: Clear terminal screen.
*   `Esc` (twice): Clear active input prompt (when not streaming).
*   `Ctrl+C` or `Esc`: Interrupt/cancel active agent processing.
*   `Ctrl+D`: Exit active session.
*   `Ctrl+K`: Approve subagent action requests instantly.
*   `Ctrl+G`: Open file/editor.
*   `Alt+Enter` / `Ctrl+J` / `Shift+Enter`: Insert newline in prompt box.
*   `@`: Trigger autocomplete suggestions for local paths.
*   `!`: Execute local terminal commands directly (e.g. `!git status`).

---

## 6. Programmatic Hook System (`hooks.json`)

The hook system acts as a synchronous middleware pipeline that intercepts agent activities. 

*   **Configuration File**: Defined inside `hooks.json` in the user configuration directory (`~/.gemini/config/`) or in a project `.agents/` folder. It can also be defined inline in the `hooks` section of `settings.json`.

### Schema Configuration
```json
{
  "hooks": {
    "BeforeTool": [
      {
        "matcher": "write_file|replace|run_command",
        "hooks": [
          {
            "name": "safety-scan",
            "type": "command",
            "command": "python $GEMINI_PROJECT_DIR/scripts/safety_scan.py",
            "timeout": 5000
          }
        ]
      }
    ],
    "AfterTool": [
      {
        "matcher": ".*",
        "hooks": [
          {
            "name": "auto-reindex",
            "type": "command",
            "command": "graphify update ."
          }
        ]
      }
    ]
  }
}
```

### Stdin/Stdout Data Contracts
Hooks execute synchronously and communicate with the CLI via JSON over standard I/O:

#### 1. Input Contract (`stdin`)
The CLI passes context information to the hook script as a JSON object:
```json
{
  "session_id": "6ccd62c6-7ffa-442b-a01c-bc5cbeed52db",
  "transcript_path": "/path/to/transcripts/session_log.json",
  "cwd": "C:\\WSGTA\\universal-or-strategy",
  "hook_event_name": "BeforeTool",
  "tool_name": "run_command",
  "tool_input": {
    "CommandLine": "rm -rf docs"
  }
}
```

#### 2. Output Contract (`stdout`)
The script must print only a JSON response to `stdout` (silence is mandatory for non-JSON printouts). 
*   **Allow**: `{"decision": "allow"}`
*   **Block**: `{"decision": "deny", "reason": "Destructive shell command rejected by project security rule."}`

#### 3. Standard Error (`stderr`)
Any debugging/logging messages must be sent to `stderr` (`>&2` in Bash, or `sys.stderr` in Python).

### System Exit Codes
The CLI parses the script's exit status to determine execution flow:
*   **`0`**: Hook ran successfully. `stdout` is evaluated for `decision`.
*   **`2`**: System Block. CLI aborts execution immediately and treats `stderr` contents as the rejection message.
*   **Other**: Non-fatal warning; the CLI displays the warning but proceeds.

---

## 7. Model Context Protocol (MCP) Integration

Antigravity CLI utilizes the Model Context Protocol to interface with external servers, databases, and APIs.
*   **Config File**: Located at `~/.gemini/antigravity/mcp_config.json`.
*   **Tool Registration**: MCP servers are declared in the `mcpServers` object, including command paths, arguments, environment variables, and active states.
*   **Configuration Command**: To register new servers programmatically, run:
    ```bash
    antigravity mcp add <server_name> --command <command> --args <args>
    ```
