# IBM Bob IDE & Shell: High-Fidelity Documentation Synthesis
**Version:** 2.0  
**Data Source:** Scraped from Live IBM Bob Docs (`bob.ibm.com/docs`)  
**Status:** ACTIVE & VERIFIED  

---

## 1. System Requirements & Installation

### A. System Requirements
*   **Operating Systems:** macOS, Linux, or Windows.
*   **Memory:** Minimum 4 GB RAM (8 GB recommended).
*   **Storage:** Minimum 500 MB available disk space.
*   **Network:** Active internet connection.
*   **Node.js:** Version 22.15.0 or later.
*   **Editor:** Package manager or Bob IDE installed.

### B. Installation Methods
1.  **Installation Script (macOS/Linux/Windows WSL):**
    ```bash
    curl -fsSL https://bob.ibm.com/download/bobshell.sh | bash
    ```
2.  **Package Managers (Windows / General npm):**
    Install via standard JavaScript package managers: `npm`, `pnpm`, or `yarn` (point to target path of manually downloaded release package if installing from archive).
3.  **Bob IDE Command Palette:**
    If Bob IDE is installed, open the command palette (`Ctrl+Shift+P` on Windows/Linux or `Cmd+Shift+P` on macOS) and run:
    *   `bobide` (installs command)
    *   `run bobshell` (spawns shell)

### C. Authentication Methods
*   **IBMid Authentication (Default):** Used for interactive sessions. Prompts to authenticate in browser, then closes browser to return to Bob Shell.
*   **API Key Authentication:** Ideal for automation, CI/CD, and non-interactive environments.
    1. Generate API key with scope set to `Inference` in the Bob web portal.
    2. Set the environment variable `BOBSHELL_API_KEY`:
       *   **macOS/Linux:** `export BOBSHELL_API_KEY="your-api-key-here"`
       *   **Windows (CMD/PowerShell):** `$env:BOBSHELL_API_KEY="your-api-key-here"`
    3. Launch Bob Shell using the `--auth-method api-key` parameter:
       ```bash
       bob --auth-method api-key -p "Explain this project"
       ```

---

## 2. Configuration System & Precedence

### A. Precedence Order (Highest to Lowest)
1.  **Command-line arguments** (e.g., `bob --option value`) - Session-specific.
2.  **Environment variables** (e.g., shell env or `.env` files).
3.  **System settings file** (`/etc/bobshell/settings.json`).
4.  **Project settings file** (`.bob/settings.json` in workspace root).
5.  **User settings file** (`~/.bob/settings.json` in user home directory).
6.  **System defaults file** (`/etc/bobshell/system-defaults.json`).
7.  **Hardcoded defaults** (built directly into Bob Shell/IDE).

### B. Core Configurations (`settings.json`)
*   **General Settings:**
    *   `preferredEditor`: String name of preferred text editor (e.g. `"code"`).
    *   `vimMode`: Boolean to enable Vim keybindings (default: `false`).
    *   `disableAutoUpdate` / `disableUpdateNag`: Booleans for update control.
    *   `checkpointing.enabled`: Enable automated session state saves (default: `false` but recommended).
*   **UI Settings:**
    *   `theme` (e.g. `"GitHub"`), `hideBanner`, `hideTips`, `showLineNumbers`, `showMemoryUsage`.
*   **Context Settings:**
    *   `fileName`: String or Array of files loaded for context (default: `["CONTEXT.md", "AGENTS.md"]`).
    *   `includeDirectories`: Array of additional directories to parse for context.
    *   `fileFiltering.respectGitIgnore` / `fileFiltering.respectBobIgnore`: Enable ignore parsing (default: `true`).
*   **Tools Settings:**
    *   `sandbox`: Boolean or string (e.g. `"docker"`) to configure sandboxing.
    *   `allowed`: Array of tool commands that run without confirmation (e.g., `["run_shell_command(git)"]`).
    *   `exclude`: Array of tools to omit from discovery.
*   **MCP Settings (`mcpServers`):**
    *   Map local servers: `"command": "bin/mcp_server.py"`
    *   Map remote SSE/HTTP servers: `"url": "https://example.com/mcp"` with `"headers"`, `"trust"`, `"includeTools"`, `"excludeTools"`.

### C. Context Files Hierarchy (`AGENTS.md`)
Instructions to the AI model are parsed recursively in this order:
1.  **Global context:** `~/.bob/AGENTS.md` (applies to all projects).
2.  **Project context:** `AGENTS.md` in project root and parent directories.
3.  **Local context:** `AGENTS.md` in subdirectories (target instructions for specific components).
*   *Management:* Reload context using `/memory refresh` and view active context using `/memory show`.

---

## 3. Custom Rules System

### A. Configuration Scopes & Paths
*   **Global rules:** Applied across all projects.
    *   **macOS/Linux:** `~/.bob/rules/`
    *   **Windows:** `%USERPROFILE%\.bob\rules\`
*   **Workspace rules:** Applied to current project only.
    *   **Single-file format:** `.bobrules` (general workspace rules), `.bobrules-code` (Code mode), `.bobrules-{modeSlug}`.
    *   **Directory-based format:** `.bob/rules/` (general rules), `.bob/rules-code/` (Code mode specific), `.bob/rules-{modeSlug}/`.

### B. Precedence & Filtering Behavior
*   **Priority:** Global rules load first, followed by Workspace rules. Workspace rules override global rules. Within each level, mode-specific rules load before general rules.
*   **AGENTS.md Loading:** Team-standard `AGENTS.md` in workspace root is loaded automatically after mode-specific rules but before general workspace rules. Can be disabled via `"bob-shell.useAgentRules": false`.
*   **Behavioral Rules:**
    *   **Recursive:** Subdirectories under rules are recursively scanned.
    *   **Alphabetical:** Files are loaded in alphabetical order.
    *   **Filtered:** Bob automatically ignores backup and log formats: `.DS_Store`, `*.bak`, `*.cache`, `*.log`, `*.tmp`, `Thumbs.db`.
    *   **Symlinks:** Supported up to a maximum depth of 5. Empty files are silently skipped.

---

## 4. Custom Modes

### A. Structure of a Custom Mode
Custom modes configure Bob's persona and constraints:
*   `slug`: Unique identifier (used in command line `bob --chat-mode=my-mode` or `/mode my-mode`).
*   `name`: Display name in the UI/TUI mode selector.
*   `description`: Short explanation of the mode's target role.
*   `roleDefinition`: Identity, expertise, and operational style.
*   `whenToUse`: Context-matching triggers for mode recommendation.
*   `customInstructions`: Operational constraints and rules (can load from directories under `.bob/rules-{mode-slug}/` or fallback to `.bobrules-{mode-slug}`).
*   `groups`: Configures access to toolsets:
    *   `read`: Access file/directory tools.
    *   `edit`: Modify files (can scope using `fileRegex`, e.g. `fileRegex: "\\.(sh|env)$"`).
    *   `browser`: Browser automation access.
    *   `command`: Execute terminal commands.
    *   `mcp`: MCP tool access.

### B. Creating Custom Modes (Examples)
*   **Global Custom Modes:** Configure in `~/.bob/custom_modes.yaml`.
*   **Project-Specific Modes:** Configure in `.bob/custom_modes.yaml`.

*Example: Safe Production Operations Mode:*
```yaml
customModes:
  - slug: prod-ops
    name: 🔒 Production Operations
    roleDefinition: >-
      You are a production operations specialist focused on safety.
      You never run destructive commands without explicit confirmation.
    whenToUse: Use when working with production systems or sensitive environments.
    customInstructions: |-
      Production safety rules:
      - NEVER run destructive commands without user approval.
      - Suggest dry-run options when available.
    groups:
      - read
      - browser
```

### C. Invocation & Mode Switching
*   **Command Line:** Start Bob using `bob --chat-mode=slug` or `bob --chat-mode=slug --sandbox`.
*   **Interactive TUI:** Switch via `/mode slug` or `/slug` shortcut.

---

## 5. Slash Commands

### A. Built-in vs. Custom Workflow Commands
*   **Built-in Mode Commands:** Commands like `/mode code`, `/mode ask` or custom mode slugs `/prod-ops` switch the AI context and cannot be overridden by custom workflow files.
*   **Custom Workflow Commands:** Automated tasks created by adding Markdown files to:
    *   **Project-specific:** `.bob/commands/{command-name}.md`
    *   **Global:** `~/.bob/commands/{command-name}.md`
    *   *Note:* The filename determines the slug. For example, `review-security.md` registers as `/review-security`. Names are normalized to lowercase with spaces replaced by dashes.

### B. Command Templates and Argument Hints
You can define metadata in the Markdown frontmatter to configure input parameters. Positionals are substituted inside the markdown text as `$1`, `$2`, etc.

*Example custom command `.bob/commands/api-endpoint.md`:*
```markdown
---
description: Create a new API endpoint
argument-hint: <endpoint-name> <http-method>
---
Create a new API endpoint called $1 that handles $2 requests.
Include proper error handling and documentation.
```
*   **Usage in Chat:** `/api-endpoint users GET` replaces `$1` with `users` and `$2` with `GET`.
*   **Fuzzy Autocomplete:** Typing `/` brings up a unified fuzzy-search menu displaying descriptions and hints (e.g. `/sam` filters to `/sample-command`).
*   **Conflicts:** Project commands override global commands with identical names.

---

## 6. Security & Sandboxing

### A. File Access Restrictions
*   `.bobignore` files use standard `.gitignore` syntax to prevent Bob from reading/writing sensitive directories (e.g. `.env`, `secrets/`, `*.key`). Bob monitors this file and applies changes immediately.
*   **Limitations:** Only restricts workspace-relative access via Bob's tools; does not block host-level bypasses.

### B. Trusted Folders
*   On first run in a directory, Bob prompts for: **Trust folder**, **Trust parent folder**, or **Don't trust**.
*   **Safe Mode (Untrusted):**
    *   Project settings and environment variables are ignored.
    *   Tool auto-approval is disabled.
    *   MCP servers do not connect.
    *   Custom commands are not loaded.

### C. Auto-Approve Settings
Settings file can whitelist safe commands bypassing approval prompts via `tools.allowed`:
```json
{
  "tools": {
    "allowed": [
      "run_shell_command(git status)",
      "run_shell_command(git log)"
    ]
  }
}
```
*   *Warning:* Bypassing edit or execution confirmations increases security risk and can lead to code corruption.

### D. Sandboxing Configuration
Bob isolates file operations and shell commands from the host system using OS-level or container tools:
1.  **macOS Seatbelt (macOS only):** Invokes `sandbox-exec`. Configured via `SEATBELT_PROFILE` env var:
    *   `permissive-open` (Default - restricts write outside workspace, allows network).
    *   `permissive-closed` (No network, project write allowed).
    *   `permissive-proxied` (Proxied network).
    *   `restrictive-open` (Network allowed, strict write limits).
    *   `restrictive-closed` (Maximum offline isolation).
2.  **Container Sandboxing (Docker/Podman):** Cross-platform process and filesystem isolation.
    *   Enable via flag: `bob -s` or `bob --sandbox`.
    *   Enable via env: `export BOB_SHELL_SANDBOX=docker` (or `podman`).
    *   Enable via settings: `"tools": { "sandbox": "docker" }`.
    *   *Custom Image:* Base on `bobshell-sandbox`, add dependencies in `.bob/sandbox.Dockerfile` and run `BUILD_SANDBOX=1 bob -s`.
    *   *Sandbox Flags:* Inject arguments via `SANDBOX_FLAGS` (e.g., `export SANDBOX_FLAGS="--memory=4g --cpus=2"`).

---

## 7. Tool Categories & Workflow

When a natural language query is entered, Bob maps it to a specific tool parameter block, requests confirmation (unless auto-approved), and executes:

*   **Read Tools:**
    *   `read_file`: Inspect contents of one or more files.
    *   `search_files`: Execute regex pattern matching across workspace files.
    *   `list_files`: Read directory structures.
    *   `list_code_definition_names`: Extract classes, functions, and method names from code files.
*   **Write Tools:**
    *   `write_to_file`: Complete write or rewrite of a target file.
    *   `apply_diff`: Perform surgical changes to specific lines.
    *   `insert_content`: Add lines at specific offsets (e.g., imports).
*   **Command Tools:**
    *   `execute_command`: Run CLI processes inside the workspace directory (or sandbox).
*   **MCP Tools:**
    *   `use_mcp_tool`: Invoke tools from configured MCP endpoints.
*   **Mode Tools:**
    *   `switch_mode`: Transition chat context between modes.
*   **Question Tools:**
    *   `ask_followup_question`: Prompt user for necessary details or clarification.

---

## 8. Pull Requests & Code Reviews

### A. Pull Request (PR) Generation
*   **Trigger Methods:** 
    1. Click the PR icon in the Source Control panel.
    2. Command palette: "Bob: Generate PR".
    3. Type `/create-pr` in the Bob chat interface (*Note: Not compatible with Sandboxing*).
*   **Process:** Bob analyzes local branch commits, remote branches, and issue templates, suggesting a title and description.
*   **PR Template Locations (Searched in order):**
    *   `${cwd}/pull_request_template.md`
    *   `${cwd}/docs/pull_request_template.md`
    *   `${cwd}/.github/pull_request_template.md`
    *   `${cwd}/.github/PULL_REQUEST_TEMPLATE/pull_request_template.md`
    *   `${cwd}/PULL_REQUEST_TEMPLATE/pull_request_template.md`
    *   `${cwd}/docs/PULL_REQUEST_TEMPLATE/pull_request_template.md`
*   *Default Template:* Used if no template is detected. Summarizes changes, implementation, testing, and references.
*   *Edits:* Bob opens the generated markdown text in a temporary file to let you edit and click "Done" before submitting.

### B. Code Review Flows
Reviews are executed in the IDE sidebar **Review Panel** or using chat commands:
*   **Review Modes:**
    *   **Branch Comparison:** Compare workspace or branch commits against target branches.
    *   **Issue Coverage:** Validate changes match GitHub issue descriptions (requires GitHub integration).
*   **Chat Commands:**
    *   `/review`: Audit uncommitted workspace changes.
    *   `/review <branch>`: Audit changes against target branch.
    *   `/review <issue-url> --issue-coverage`: Validate changes against GitHub issue requirements.
*   **Exclusions:** Add glob patterns under Bob Settings -> Bob Findings -> Review Exclusions (e.g., `*.test.ts`, `dist/**`).
*   **Bob Findings Panel:** Displays findings as a hierarchical list.
    *   *Inline Annotations:* Shows markers in-file next to the target lines.
    *   *Actions on Findings:* **Fix with Bob** (creates code task), **Mark as Resolved**, **Mark as Open**, **Dismiss**.
    *   *Finding Statuses:* `Open`, `In Progress`, `Resolved`.
