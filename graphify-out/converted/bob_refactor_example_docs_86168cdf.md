<!-- converted from bob_refactor_example_docs.docx -->



MISSION: Phase 7 Complexity Extraction Epic — V12 Photon Kernel
BUILD_TAG_BASELINE: 1111.007-phase7-t16
REPO: c:\WSGTA\universal-or-strategy
BRANCH: feature/phase7-sprint5-extraction
SPEC REF: docs/brain/phase7_complexity_epic_brief.md

Execute PLAN-THEN-EXECUTE PROTOCOL. Produce a written plan with helper
names, signatures, and caller impact. STOP and confirm before coding.
Post-edit: run deploy-sync.ps1 + complexity_audit.py + bump BUILD_TAG.

--- TICKET BELOW ---
[paste Traycer ticket content here]






# Starting an interactive session

Interactive sessions provide a conversational interface to Bob directly in your terminal, allowing real-time assistance with your development tasks.

### To start an interactive session:

Open a new terminal window.

When you start Bob Shell for the first time, you must login with your IBMid and accept the license agreement.

Navigate to the main directory of your project.

To start a Bob Shell interactive session, run:

```bash
bob
```

## Basic usage
### Interact with Bob Shell
* Type your instructions or questions directly in the terminal
* Press Enter to send your message to Bob
* Bob Shell will respond with its analysis and suggestions
* For tool usage (like reading or writing files), you'll be prompted to approve or decline each action

## Reference files
Use the `@` symbol to reference files in your project:

```bash
Explain the functionality in @src/main.js
```

This tells Bob Shell to read and analyze the specified file before responding.

### Use slash commands
Type `/` to access a menu of available commands:

* Built-in commands like `/help` for assistance
* Mode-switching commands like `/code` or `/ask`
* Custom commands you've created

For a complete list of available commands, see [Slash commands in Bob Shell](/docs/shell/features/slash-commands).

### View file changes
When Bob Shell needs to modify files, it will show you the proposed changes:

* By default, changes are displayed in the terminal with a CLI diff view.
* To use an external editor for reviewing changes, configure your preferred editor with the `/editor` command. Then select the "Show diff in editor" option when prompted to review changes.

## Advanced features
## Tool approvals
For security, Bob Shell requires your approval before:

* Reading files.
* Writing or modifying files.
* Executing commands.

You can approve or decline each action individually when prompted.

### Multi-turn conversations
Bob Shell maintains context throughout your conversation, allowing you to:

* Ask follow-up questions.
* Refine previous requests.
* Build on earlier responses.

### Tips for effective use
* Be specific in your requests to get more targeted responses.
* Use `@` references to provide code context when needed.
* For complex tasks, break them down into smaller steps.
* Use slash commands to quickly access common functionality.
* When working with large codebases, direct Bob Shell to the most relevant files.

### When to use interactive session
Interactive session works best for:

* Exploratory coding sessions.
* Debugging and troubleshooting.
* Learning new concepts or technologies.
* Tasks that require multiple back-and-forth exchanges.
* Projects where you need to review changes before they're applied.



# Starting a non-interactive session

Non-interactive session provide a method to use Bob Shell directly from the command line without entering an interactive session. Use for automation, scripting, and batch processing tasks.

### To start a non-interactive session:

Open a new terminal window.

Navigate to the main directory of your project.

Run the `bob -p` command to start Bob Shell in your terminal.

Before starting a non-interactive session for the first time, you must accept the license agreement. You can do this by either:

* Running the following command in your terminal: `bob --accept-license -p "Explain this project"`
* Starting Bob Shell with an interactive session first to review and accept the license.

For example:

```bash
bob -p "Explain this project"
```

## Basic usage
### Providing prompts to Bob Shell
Use the `bob -p` command to get Bob to address your prompt:

```bash
bob -p "Explain this project"
```

### Pipe content as input
You can pipe text content to Bob Shell:

```bash
cat buildError.txt | bob -p "Explain this build error"
```

### Save results to a file
Redirect the output to save results:

```bash
bob -p "Review @bigFile.java" > review.md
```

### Reference project files
Use the `@` symbol to reference files in your project:

```bash
bob -p "Summarize the functionality in @src/main.js"
```

## Advanced options
### Enable file modifications
By default, Bob Shell only uses non-destructive tools (like reading files) in non-interactive session. To enable writing and updating files, add the `--yolo` flag:

```bash
bob -p "Fix bugs in @app.js" --yolo
```

Even with the `--yolo` flag enabled, Bob Shell will not write or update files outside the directory where it was started.

### Format output for processing
The output contains both Bob Shell's answer and its thinking steps. For easier processing, add instructions to format the output:

```bash
bob -p "What Java version is this application using? Check @pom.xml. Enclose the answer in markdown tags" > analysis.md
```

### When to use non-interactive session
Non-interactive session works best for:

* Integrating Bob Shell into automation scripts
* Processing multiple files with a single command
* Getting quick insights without starting an interactive session
* Generating documentation from code

### Tips for effective use
* For complex tasks that might require multiple tool uses, interactive session usually works better.
* When processing large files or projects, be specific about which files to analyze.
* Use structured output instructions (like "Format the output as JSON") for easier parsing in scripts.
* Consider creating shell aliases or scripts for frequently used Bob Shell commands.
* For multi-line prompts, save them to a file and pipe to Bob Shell:

```bash
cat prompt.txt | bob
```


# Custom modes

You can create custom modes to tailor Bob's behavior to specific tasks or workflows. Custom modes in Bob Shell work similarly to Bob IDE modes.

### Why use custom modes in Bob Shell
* **Shell-optimized workflows**: Create modes designed specifically for terminal-based development tasks
* **Command-line safety**: Restrict modes to safe operations when working in production environments
* **Environment-specific behavior**: Configure modes that adapt to different shell environments
* **Automation-friendly**: Design modes that work seamlessly in both interactive and non-interactive sessions
* **Team standardization**: Share shell-specific modes across your team for consistent workflows

### What's included in a custom mode
Custom modes in Bob Shell use the same core structure as Bob IDE modes:

| Property             | Description                                           | Shell-Specific Considerations                                  |
| -------------------- | ----------------------------------------------------- | -------------------------------------------------------------- |
| `slug`               | Unique internal identifier                            | Used in command-line arguments:<br />`bob --chat-mode=my-mode` |
| `name`               | Display name in the UI                                | Shown in interactive mode's mode selector                      |
| `description`        | Short description shown in the mode selector          | Briefly explain your mode's purpose                            |
| `roleDefinition`     | Core identity and expertise                           | Should consider shell context and command-line workflows       |
| `groups`             | Allowed toolsets and file access                      | Command running permissions are critical in shell environments |
| `whenToUse`          | Mode selection guidance                               | Helps Bob Shell choose appropriate modes for tasks             |
| `customInstructions` | Specific behavioral guidelines, or rules for the mode | Can reference Bob Shell development patterns                   |

## Available tools
### Available tool groups
* `read`: Read files and directories
* `edit`: Modify files (can be restricted with `fileRegex`)
* `browser`: Use browser automation
* `command`: Execute terminal commands
* `mcp`: Access MCP servers

### Creating custom modes
## Configuration files
Bob Shell uses the same configuration format as Bob IDE, supporting both YAML (preferred) and JSON formats.

## Global modes
Create or edit `~/.bob/custom_modes.yaml` for modes available across all projects:

```yaml
customModes:
- slug: shell-debug
name: 🐛 Shell Debugger
roleDefinition: >-
You are a debugging specialist focused on command-line troubleshooting.
You excel at analyzing shell output, environment variables, and system logs.
whenToUse: Use for debugging shell scripts, command failures, and environment issues.
customInstructions: |-
When debugging:
- Always check environment variables first
- Examine command exit codes
- Review relevant log files
- Test commands in isolation before suggesting fixes
groups:
- read
- command
- browser
```

### Project-specific modes
Create or edit `.bob/custom_modes.yaml` in your project root:

```yaml
customModes:
- slug: deploy-helper
name: 🚀 Deployment Assistant
roleDefinition: You are a deployment specialist for this project's infrastructure.
whenToUse: Use for deployment tasks, infrastructure changes, and release management.
customInstructions: |-
Deployment guidelines:
- Always verify the target environment before running commands
- Check for running processes that might be affected
- Validate configuration files before applying changes
- Create backups before destructive operations
groups:
- read
- - edit
- fileRegex: \.(yaml|yml|sh|env)$
description: Configuration and script files only
- command
```

### Command-line mode selection
Specify a mode when starting Bob Shell:

```bash
# Start Bob Shell in a specific mode
bob --chat-mode=shell-debug

# Combine with other options
bob --chat-mode=deploy-helper --sandbox
```

### Interactive mode switching
In interactive mode, switch modes using slash commands:

```bash
# Switch to a custom mode
/mode shell-debug

# Or use the mode's slug directly
/shell-debug
```

### Shell-specific configurations
### Production safety mode
Create a safety-focused mode for production environments:

```yaml
customModes:
- slug: prod-ops
name: 🔒 Production Operations
roleDefinition: >-
You are a production operations specialist with a strong focus on safety.
You never run destructive commands without explicit confirmation.
whenToUse: Use when working with production systems or sensitive environments.
customInstructions: |-
Production safety rules:
- NEVER run destructive commands without explicit user confirmation
- Always verify the target environment before any operation
- Suggest dry-run options when available
- Check for active connections or processes before changes
- Recommend backup procedures before modifications
groups:
- read
- browser
# Note: No edit or command groups for maximum safety
```

### Script development mode
Create a mode for shell script development:

```yaml
customModes:
- slug: script-dev
name: 📜 Script Developer
roleDefinition: >-
You are a shell scripting expert specializing in bash, zsh, and POSIX-compliant scripts.
whenToUse: Use for creating, debugging, or improving shell scripts.
customInstructions: |-
Shell scripting best practices:
- Use shellcheck-compliant syntax
- Include proper error handling with set -e and set -u
- Add usage documentation at the top of scripts
- Quote variables to prevent word splitting
- Provide exit codes for different error conditions
groups:
- read
- - edit
- fileRegex: \.(sh|bash|zsh)$
description: Shell script files only
- command
```

### Command running permissions
### Restricting command access
Control which commands a mode can run by omitting the `command` group:

```yaml
customModes:
- slug: safe-reviewer
name: 👀 Safe Code Reviewer
roleDefinition: You are a code reviewer focused on analysis, not modification.
whenToUse: Use for code reviews and analysis without making changes.
groups:
- read
- browser
# No command or edit groups - read-only mode
```

### Allowing specific commands
Use Bob Shell's `allowed` tools configuration with custom modes in your settings file:

```json
{
"tools": {
"allowed": [
"run_shell_command(git status)",
"run_shell_command(git log)",
"run_shell_command(git diff)"
]
}
}
```

### Interactive vs non-interactive behavior
### Designing for both modes
Create modes that work well in both interactive and non-interactive sessions:

```yaml
customModes:
- slug: test-runner
name: 🧪 Test Runner
roleDefinition: >-
You are a testing specialist who runs and analyzes test suites.
You adapt your output based on the running context.
whenToUse: Use for running tests, analyzing test results, and debugging test failures.
customInstructions: |-
Testing guidelines:
- In interactive mode: Provide detailed explanations and suggestions
- In non-interactive mode: Focus on concise, actionable output
- Always report test results clearly
- Suggest fixes for failing tests
groups:
- read
- command
```

### Non-interactive usage
Use modes in non-interactive mode:

```bash
# Use a mode designed for automation
bob --chat-mode=test-runner -p "Run the test suite and report failures"

# Combine with output processing
bob --chat-mode=test-runner -p "Run tests" --hide-intermediary-output > results.txt
```

### Mode-specific instructions via files
### Directory-based instructions
Create mode-specific instruction files in `.bob/rules-{mode-slug}/`:

Example instruction file (`.bob/rules-shell-debug/01-environment-checks.md`):

```markdown
# Environment Debugging Checklist

When debugging shell issues, always check:

1. **Environment Variables**: PATH, SHELL, TERM, and application-specific variables
2. **Shell Configuration**: .bashrc, .zshrc, .profile files
3. **System State**: Current working directory, file permissions, disk space
4. **Command Availability**: Use `command -v <cmd>` to verify commands exist
```

### Single file instructions (fallback)
Alternatively, use a single file `.bobrules-{mode-slug}` in your workspace root.

## Configuration precedence
Mode configurations are applied in this order:

. Command-line arguments (`--chat-mode=mode-slug`)
. Project-level modes (`.bob/custom_modes.yaml`
. User-level modes (`~/.bob/custom_modes.yaml`)
. System-level modes (platform-specific locations)
. Default modes

### Sandboxing with custom modes
Combine custom modes with Bob Shell's sandbox feature for safe experimentation:

```bash
# Start a custom mode in sandbox
bob --chat-mode=script-dev --sandbox

# Use with Docker sandbox
bob --chat-mode=deploy-helper --sandbox
```

### Migrating modes from Bob IDE
## Key differences
When migrating custom modes from Bob IDE to Bob Shell:

| Aspect          | Bob IDE                       | Bob Shell                        |
| --------------- | ----------------------------- | -------------------------------- |
| UI interaction  | Visual interface with panels  | Terminal-based interface         |
| File editing    | In-editor diffs and previews  | CLI diff view or external editor |
| Command running | Integrated terminal           | Direct shell running             |
| Mode switching  | UI dropdown or slash commands | Slash commands or CLI arguments  |
| Configuration   | Settings UI + files           | Configuration files only         |

## Adaptation checklist
When adapting Bob IDE modes for Bob Shell:

* Review `roleDefinition` for shell-specific context
* Update `customInstructions` to reference command-line workflows
* Consider command running safety in `groups` configuration
* Test mode in both interactive and non-interactive sessions
* Verify file path handling works in shell context
* Test with sandbox mode if applicable

## Example migration
**Original Bob IDE mode:**

```yaml
customModes:
- slug: code-reviewer
name: 👀 Code Reviewer
roleDefinition: You are a code reviewer who provides detailed feedback.
groups:
- read
- browser
```

**Adapted for Bob Shell:**

```yaml
customModes:
- slug: code-reviewer
name: 👀 Code Reviewer
roleDefinition: >-
You are a code reviewer who provides detailed feedback.
You work efficiently in terminal environments and provide clear, actionable suggestions.
whenToUse: Use for code reviews, pull request analysis, and code quality checks.
customInstructions: |-
Code review guidelines:
- Provide feedback in a structured format suitable for terminal output
- Reference specific line numbers and file paths
- Suggest concrete improvements with examples
- Format output for easy parsing if used in non-interactive mode
groups:
- read
- command  # Added for git operations
- browser
```

# Integrating with Bob IDE

Connect Bob Shell directly to your code editor for a seamless development experience. This integration enhances Bob Shell's capabilities by providing real-time workspace awareness and enabling powerful features like in-editor diff viewing.

## Overview
| Feature               | Description                                          | Benefit                               |
| --------------------- | ---------------------------------------------------- | ------------------------------------- |
| Workspace awareness   | Bob Shell sees your recent files and cursor position | More contextually relevant assistance |
| Native diff viewing   | Review code changes in your editor's diff tool       | Easier code review and modification   |
| Command integration   | Access Bob Shell features from editor commands       | Streamlined workflow                  |
| Seamless file editing | Accept changes directly in your editor               | Faster implementation of suggestions  |

Currently, Bob IDE and compatible editors are supported. Additional editor support may be added in future releases.

## Getting started
. Start Bob Shell from your Bob IDE terminal
. Accept the automatic integration prompt when it appears
. Use Bob Shell normally - it now has access to your workspace context
. When Bob Shell suggests code changes, they'll appear in Bob IDE's diff viewer

### Benefits of IDE integration
### Enhanced context awareness
Without IDE integration, Bob Shell only knows about files you explicitly reference. With integration enabled, Bob Shell automatically gains:

* Access to your **10 most recently accessed files**
* Knowledge of your **current cursor position**
* Visibility of your **selected text** (up to 16KB)

This contextual awareness allows Bob Shell to provide more relevant assistance without requiring you to manually reference files.

### Seamless code modifications
When Bob Shell suggests code changes:

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│                 │     │                 │     │                 │
│  Bob Shell       │────▶│  Bob IDE        │────▶│  Your codebase  │
│  suggests       │     │  diff viewer    │     │  with changes   │
│  changes        │     │  shows changes  │     │  applied        │
│                 │     │                 │     │                 │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

This workflow allows you to:

* Review changes in your familiar editor environment
* Edit the suggested changes before accepting them
* Accept or reject changes using standard editor commands
* Keep your hands on the keyboard throughout the process

## Installation methods
Choose the installation method that works best for you:

### Method 1: Automatic setup (recommended)
When you run Bob Shell inside Bob IDE's integrated terminal, it automatically detects the environment and offers to set up integration:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ > Do you want to connect Bob IDE to Bob Shell?                               │
│ If you select Yes, we'll install an extension that allows the CLI to ac ... │
│                                                                             │
│   1. Yes                                                                    │
|   2. No                                                                     │
|   3. No, don't ask again                                                    │
└─────────────────────────────────────────────────────────────────────────────┘
```

Selecting "Yes" will:

. Install the Bob Shell Companion extension
. Configure the necessary connection settings
. Establish the connection automatically

### Method 2: Command-line installation
If you dismissed the automatic prompt or need to reinstall, use the built-in command:

```bash
/ide install
```

This command:

* Detects your current IDE
* Finds the appropriate extension
* Installs it automatically
* Provides setup instructions

### Managing the connection
## Connection commands
| Command        | Purpose                             | Example        |
| -------------- | ----------------------------------- | -------------- |
| `/ide enable`  | Activate the IDE connection         | `/ide enable`  |
| `/ide disable` | Deactivate the IDE connection       | `/ide disable` |
| `/ide status`  | Check connection status and context | `/ide status`  |
| `/ide install` | Install the companion extension     | `/ide install` |

### Checking connection status
To verify your connection and see what context Bob Shell has access to:

```bash
/ide status
```

Example output:

```
🟢 Connected to Bob IDE
Recent files:
- src/components/Header.tsx
- src/utils/api.js
- package.json
```

### Working with code changes
When Bob Shell suggests code modifications, you'll see them in your editor's diff viewer.

## Accepting changes
You have multiple ways to accept suggested changes:

| Method          | Action                                                              |
| --------------- | ------------------------------------------------------------------- |
| Editor UI       | Click the ✓ checkmark in the diff editor's title bar                |
| Keyboard        | Press <kbd>Cmd+S</kbd> (macOS) or <kbd>Ctrl+S</kbd> (Windows/Linux) |
| Command Palette | Run **Bob Shell: Accept Diff**                                      |
| Bob Shell CLI   | Type `yes` when prompted                                            |

## Rejecting changes
To reject suggested changes:

| Method          | Action                                          |
| --------------- | ----------------------------------------------- |
| Editor UI       | Click the ✗ icon in the diff editor's title bar |
| Keyboard        | Close the diff editor tab                       |
| Command Palette | Run **Bob Shell: Close Diff Editor**            |
| Bob Shell CLI   | Type `no` when prompted                         |

## Modifying suggestions
You can edit the suggested code directly in the diff editor before accepting it. This allows you to:

* Fix minor issues in the suggestion
* Adapt the code to your specific needs
* Add comments or additional logic
* Ensure the changes match your coding style

For frequently repeated changes, select "Yes, allow always" in Bob Shell to auto-accept similar changes in the future.

## Advanced usage
### Bob IDE commands
Access Bob Shell features directly from Bob IDE's Command Palette (<kbd>Cmd+Shift+P</kbd> or <kbd>Ctrl+Shift+P</kbd>):

| Command                               | Function                                      |
| ------------------------------------- | --------------------------------------------- |
| `Bob Shell: Run`                      | Start a new Bob Shell session in the terminal |
| `Bob Shell: Accept Diff`              | Accept changes in the active diff editor      |
| `Bob Shell: Close Diff Editor`        | Reject changes and close the diff editor      |
| `Bob Shell: View Third-Party Notices` | Show third-party notices                      |

### Using with sandboxed environments
## macOS sandboxing
When using Bob Shell with macOS Seatbelt sandboxing:

* The IDE integration requires network access
* Use a Seatbelt profile that allows network connections
* The default `permissive-open` profile works with IDE integration

## Docker containers
When running Bob Shell in a Docker container:

* The integration can still connect to Bob IDE on your host machine
* Bob Shell automatically finds the IDE server via `host.docker.internal`
* Ensure your Docker networking allows container-to-host connections

## Troubleshooting
### Common issues and solutions
| Issue              | Possible Causes                     | Solution                                |
| ------------------ | ----------------------------------- | --------------------------------------- |
| Connection fails   | Extension not installed or running  | Install extension and run `/ide enable` |
| Directory mismatch | Bob Shell running outside workspace | `cd` to your workspace directory        |
| Connection lost    | Network issue or IDE restart        | Run `/ide enable` to reconnect          |
| No workspace open  | Missing workspace in IDE            | Open a folder/workspace in your IDE     |

### Error message reference
## Connection errors
```
🔴 Disconnected: Failed to connect to IDE companion extension in [IDE Name]
```

**Solution:**

. Verify the Bob Shell Companion extension is installed and enabled
. Open a new terminal in your IDE
. Run `/ide enable`

```
🔴 Disconnected: IDE connection error. The connection was lost unexpectedly
```

**Solution:**

. Run `/ide enable` to reconnect
. If the issue persists, restart your IDE

## Configuration errors
```
🔴 Disconnected: Directory mismatch
```

**Solution:**

. Navigate to the same directory that's open in your IDE
. Restart Bob Shell

```
🔴 Disconnected: To use this feature, please open a workspace folder
```

**Solution:**

. Open a folder or workspace in your IDE
. Restart Bob Shell

## General errors
```
IDE integration is not supported in your current environment
```

**Solution:**
Run Bob Shell from within a supported IDE's integrated terminal

```
No installer is available for IDE
```

**Solution:**
Install the Bob Shell Companion extension manually from your IDE's marketplace

## Best practices
* **Start Bob Shell from your IDE's terminal** for automatic integration
* **Keep your workspace focused** on relevant files for better context
* **Review diffs carefully** before accepting changes
* **Use keyboard shortcuts** for faster workflow
* **Check connection status** if Bob Shell seems to be missing context


# Slash commands

Create custom slash commands to automate workflows and standardize team practices.

To get started, type `/` in Bob Shell to see all available commands, or create your own by adding a markdown file to `.bob/commands/` or `~/.bob/commands/`.

### Why use slash commands?
Slash commands provide several key benefits:

* **Workflow automation**: Turn complex multi-step processes into single commands
* **Team standardization**: Share commands across your team for consistent practices
* **Context preservation**: Include project-specific context in every command
* **Quick access**: Fuzzy search and autocomplete for instant command discovery

### How slash commands work
When you type `/` in Bob Shell, a menu appears showing all available commands. These commands come from two sources:

| Command Type             | Source                                 | Purpose                                    |
| ------------------------ | -------------------------------------- | ------------------------------------------ |
| Custom workflow commands | `.bob/commands/` or `~/.bob/commands/` | User-created automation for specific tasks |
| Mode commands            | Built-in and custom modes              | Switch Bob's operational context           |

### Creating custom commands
Custom commands extend Bob's functionality by adding markdown files to specific directories:

| Location         | Scope                             | Path                                      |
| ---------------- | --------------------------------- | ----------------------------------------- |
| Project-specific | Available in current project only | `.bob/commands/` in your workspace root   |
| Global           | Available in all projects         | `~/.bob/commands/` in your home directory |

The filename becomes the command name. For example:

```
.bob/commands/
├── review.md         → /review
├── test-api.md       → /test-api
└── deploy-check.md   → /deploy-check
```

### Command name processing
When creating commands through the UI, command names are automatically processed:

* Converted to lowercase
* Spaces replaced with dashes
* Special characters removed
* Leading/trailing dashes removed

Example: "My Cool Command!" becomes `my-cool-command`

### Basic command format
Create a simple command by adding a markdown file:

```markdown
Help me review this code for security issues and suggest improvements.
```

### Advanced command with frontmatter
Add metadata using frontmatter for enhanced functionality:

```markdown
---
description: Create a new API endpoint
argument-hint: <endpoint-name> <http-method>
---
Create a new API endpoint called $1 that handles $2 requests.
Include proper error handling and documentation.
```

## Frontmatter fields
| Field           | Purpose                     | Example                             |
| --------------- | --------------------------- | ----------------------------------- |
| `description`   | Appears in the command menu | "Create a new API endpoint"         |
| `argument-hint` | Shows expected arguments    | "`<endpoint-name>` `<http-method>`" |

### Command management in Bob Shell
Bob Shell supports the same slash commands as Bob IDE. While Bob Shell does not provide a dedicated UI for managing commands, you can:

. Create command files manually in the `.bob/commands/` directory in your project or `~/.bob/commands/` in your home directory
. Edit existing command files with any text editor

### Using slash commands
Type `/` in Bob Shell to see a unified menu containing the following types of commands:

. **Unified menu**: Custom commands and mode-switching commands appear together
. **Autocomplete**: Start typing to filter commands (e.g., `/sam` shows `sample-command-name`)
. **Fuzzy search**: Find commands even with partial matches
. **Description preview**: See command descriptions in the menu
. **Visual indicators**: Mode commands are distinguished from custom commands with special icons

```
/mode code     Switch to Code mode
/mode ask      Switch to Ask mode
/review        Review code for security issues
/api-endpoint  <endpoint-name> <http-method>
```

## Argument hints
Argument hints provide instant help for slash commands, showing you what kind of information to provide when a command expects additional input.

When you type `/` to bring up the command menu, commands that expect arguments will display a light gray hint next to them. This hint tells you what kind of argument the command is expecting.

For example:

* `/mode <mode_slug>` - The hint `<mode_slug>` indicates you should provide a mode name like `code` or `debug`
* `/api-endpoint <endpoint-name> <http-method>` - Shows you need both an endpoint name and HTTP method

After selecting the command, it will be inserted into the chat input followed by a space. The hint is not inserted; it is only a visual guide to help you know what to type next. You must then manually type the argument after the command.

### Adding argument hints to custom commands
You can add argument hints to your custom commands using the `argument-hint` field in the frontmatter:

```markdown
---
description: Create a new API endpoint
argument-hint: <endpoint-name> <http-method>
---
Create a new API endpoint called $1 that handles $2 requests.
Include proper error handling and documentation.
```

This will display as `/api-endpoint <endpoint-name> <http-method>` in the command menu.

### Best practices for argument hints
* **Be specific**: Use descriptive placeholders like `<file-path>` instead of generic ones like `<arg>`
* **Show multiple arguments**: If your command needs multiple inputs, show them all: `<source> <destination>`
* **Use consistent format**: Always wrap placeholders in angle brackets: `<placeholder>`
* **Keep it concise**: Hints should be brief and clear

### Common questions about arguments
| Question                                        | Answer                                                                                                                                                          |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| What if I don't provide the argument?           | The command might not work as expected, or it might prompt you for more information. The hint is there to help you get it right the first time.                 |
| Do all commands have hints?                     | No, only commands that are designed to take arguments will have hints. Commands that work without additional input won't show hints.                            |
| Can I use a command without replacing the hint? | The hint text (like `<mode_slug>`) needs to be replaced with actual values. Leaving the hint text will likely cause the command to fail or behave unexpectedly. |

## Best practices
## Command naming
* Use descriptive, action-oriented names
* Keep names concise but clear
* Use hyphens for multi-word commands
* Avoid generic names like `help` or `test`
* Note: Names are automatically slugified (lowercase, special characters removed)
* The `.md` extension is automatically added/removed as needed

## Command content
* Start with a clear directive
* Use structured formats (lists, sections)
* Include specific requirements
* Reference project conventions
* Keep commands focused on a single task

## Organization
* Group related commands in subdirectories
* Use consistent naming patterns
* Document complex commands
* Version control your commands
* Share team commands in the project repository

## Troubleshooting
### Commands not appearing
If your commands aren't showing up in the menu:

* **Check file location**: Ensure custom command files are in `.bob/commands/` or `~/.bob/commands/`
* **Verify file extension**: Custom commands must be `.md` files

### Command not found
When a slash command isn't found, the LLM will see:

```
The slash command '/unknown-command' was not found. Please check the command name and try again.
```

## Command conflicts
* Custom project commands override global custom commands with the same name
* Use unique names to avoid conflicts
* When creating duplicate names through the UI, numbers are appended (e.g., `new-command-1`, `new-command-2`)

### About mode commands
The slash menu includes mode-switching commands (like `/mode code`, `/mode ask`) that fundamentally change the AI's operational mode - they don't just inject text but switch the entire AI context. Custom modes you create also appear as slash commands (e.g., a mode with slug `reviewer` becomes `/reviewer`). These mode commands cannot be overridden by custom workflow commands.

### Cross-platform compatibility
Slash commands work identically across both Bob Shell and Bob IDE. This means:

* Commands created in one environment can be used in the other
* The same command syntax and behavior applies in both environments
* Team-shared commands work consistently regardless of which interface team members use





One open clarification before ticket-breakdown
The Approach is otherwise complete and locked, but D-D3 (signature lock policy) required me to fill 3 gaps in your Q8 enumeration. I classified per the strict Q8 rule + safety-critical override — confirm or correct in one click below before I draft the 14 tickets.
Traycer has a follow-up question
Text mode
Confirm signature classification for the 3 unspecified targets (T4, T12, T15)
Your Q8 strict rule named only 4 LOCKED + 7 FREE = 11 of 14. I filled the remaining 3 in §D-D3 as follows:
T12 ExecuteWatchdogLeadAccountFlatten → LOCKED (Enqueue lambda — fits Q8 strict rule even though closure has no captures).
T4 SubmitBracketOrders (2 callers, BUILD 981) → SOFT-LOCK (safety-critical override on top of Q8 strict).
T15 ExecuteWatchdogDirectFallback (1 caller, watchdog escalation) → SOFT-LOCK (safety-critical override).
"SOFT-LOCK" = default position is preserve signature; Bob may propose changes only at EXTRACT-GATE with explicit safety-impact justification.
A. Accept as drafted — T12 LOCKED; T4 + T15 SOFT-LOCK with safety-critical override convention.
B. Strict Q8 only — T12 LOCKED; T4 + T15 → FREE (Bob may freely change signatures since each has ≤2 callers).
C. All 3 LOCKED — T4, T12, T15 fully locked; safety-critical wins over caller-count rule.
D. All 3 FREE — Q8 only counts ≥3-caller fan-out; Enqueue with no captures isn't a closure risk; safety-critical doesn't lock signature, it's a behavior concern.
Suggested Next Steps
Once you confirm D-D3 (one click), the Approach spec is final and we move to producing the 14 tickets. Per your stated next step ("proceed to /ticket-breakdown for all 14 tickets"), /ticket-breakdown is the recommended next command.
/ticket-breakdown Generate all 14 tickets (T3-T16): 14 markdown files at docs/brain/phase7_sprint5_t03_.md through t16_.md, 14 paired Epic tickets, hybrid pre-population per Q3=C, flat naming per Q5=C, all per the locked Approach spec
/architecture-validation Adversarial stress-test of the Approach before tickets — focus on Photon publish sequence (INV-2), BUILD 981 protocol (INV-3), watchdog two-stage (INV-5), StopSync (INV-6)
/plan-refactor Refine the Approach — answer D-D3 above to finalize the signature lock policy



“/trigger-workflow EPIC: Phase 7 Photon Kernel Hardening -- Sprint 5 (T3-T16)
REPO: mkalhitti-cloud/universal-or-strategy
BRANCH: feature/phase7-sprint5-extraction
BUILD: 1111.007-phase7-t2
“