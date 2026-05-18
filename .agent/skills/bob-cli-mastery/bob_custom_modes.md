# Custom modes

Tailor Bob's behavior by building custom modes with specialized roles, tool restrictions, and team workflows. Configure globally or per-project using YAML format.

### Why use custom modes
* **Specialization**: Optimize modes for specific tasks like documentation writing, test engineering, or security reviews
* **Safety**: Restrict commands or file access for sensitive operations
* **Team collaboration**: Share standardized workflows across your team
* **Experimentation**: Test different configurations without affecting other modes

## Mode components
| Component           | Description                                                                               |
| ------------------- | ----------------------------------------------------------------------------------------- |
| **Slug**            | Unique identifier used internally and for mode-specific instruction files                 |
| **Name**            | Display name shown in the Bob interface                                                   |
| **Role definition** | Core identity and expertise that defines Bob's personality and behavior                   |
| **When to use**     | (Optional) Guidance for when to use this mode, used by Orchestrator for task coordination |
| **Available tools** | Tool groups and file access permissions the mode can use                                  |
| **Custom rules**    | (Optional) Additional behavioral guidelines or rules                                      |

### How to create custom modes
You can create custom modes by asking Bob with the "Mode writer" mode, using the settings menu, or manually editing configuration files.

### Use the "Mode writer" mode
1. Click the Settings icon in the Bob panel to open Settings.
2. Select the **Modes** tab.
3. Click the Add icon button to create a new mode.

### Use the settings menu
1. Click the Settings icon in the Bob panel to open Settings.
2. Select the **Modes** tab.
3. Click the Add icon button to create a new mode.
4. Fill in the fields for **Name**, **Slug**, **Save Location**, **Role Definition**, **When to Use (optional)**, **Available Tools**, and **Custom Instructions**.
5. Click **Save**.

Bob saves the new mode in YAML format. You can add file type restrictions for the `edit` tool group by asking Bob or through manual YAML configuration.

### Edit configuration files manually
You can manually edit mode configuration files in YAML format:

* **Global modes**: Edit `custom_modes.yaml` via Settings → Modes → Edit Global Modes
* **Project modes**: Edit `.bob/custom_modes.yaml` in your project. Click Settings → Modes → Edit Project Modes

These files define an array of custom modes in YAML format.

**Example** (`custom_modes.yaml` or `.bob/custom_modes.yaml`):

```yaml
customModes:
- slug: docs-writer
  name: 📝 Documentation Writer
  roleDefinition: You are a technical writer specializing in clear documentation.
  whenToUse: Use this mode for writing and editing documentation.
  customInstructions: Focus on clarity and completeness in documentation.
  groups:
  - read
  - - edit
    - fileRegex: \.(md|mdx)$
      description: Markdown files only
  - browser
```

### Mode configuration properties
### Available tool groups
* `read`: Read files and directories
* `edit`: Modify files (can be restricted with `fileRegex`)
* `browser`: Use browser automation
* `command`: Execute terminal commands
* `mcp`: Access MCP servers
* `skill`: Access skills

### File restrictions for the edit tool
Restrict which files a mode can edit using `fileRegex` in YAML format (single backslash):

```yaml
groups:
- read
- - edit
  - fileRegex: \.(js|ts)$
    description: JavaScript and TypeScript files only
```

### Add mode-specific instructions
You can add mode-specific instructions using either a directory structure (preferred) or a single file.

**Preferred method** (directory structure):
Create a `.bob/rules-{mode-slug}/` directory in your project root. Add instruction files to the directory (for example, `01-style-guide.md`).

**Alternative method** (single file):
Create a `.bobrules-{mode-slug}` file.

### How to override default modes
You can override default Bob modes (such as Code, Ask, or Plan) by creating a custom mode with the same slug in your project configuration.

To override a default mode:
1. Click the Settings icon in the Bob panel to open Settings.
2. Select the **Modes** tab.
3. Click **Edit Project Modes** to edit the `.bob/custom_modes.yaml` file.
4. Add your mode configuration with the slug of the default mode you want to override:

```yaml
customModes:
- slug: code  # Matches default Code mode
  name: 💻 Code (Python Only)
  roleDefinition: You are a Python software engineer.
  whenToUse: Use for Python development tasks.
  customInstructions: Follow PEP 8 and use type hints.
  groups:
  - read
  - - edit
    - fileRegex: \.py$
      description: Python files only
  - command
```
