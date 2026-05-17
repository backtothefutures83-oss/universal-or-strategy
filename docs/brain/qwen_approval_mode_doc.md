# Qwen Approval Mode Features

Qwen Code offers four distinct permission modes that allow you to flexibly control how AI interacts with your code and system based on task complexity and risk level.

## Permission Modes Comparison

| Mode | File Editing | Shell Commands | Best For | Risk Level |
| :--- | :--- | :--- | :--- | :--- |
| **Plan** | ❌ Read-only analysis only | ❌ Not executed | • Code exploration<br>• Planning complex changes<br>• Safe code review | Lowest |
| **Default** | ✅ Manual approval required | ✅ Manual approval required | • New/unfamiliar codebases<br>• Critical systems<br>• Team collaboration<br>• Learning and teaching | Low |
| **Auto-Edit** | ✅ Auto-approved | ❌ Manual approval required | • Daily development tasks<br>• Refactoring and code improvements<br>• Safe automation | Medium |
| **YOLO** | ✅ Auto-approved | ✅ Auto-approved | • Trusted personal projects<br>• Automated scripts/CI/CD<br>• Batch processing tasks | Highest |

---

## Quick Reference Guide

* **Start in Plan Mode**: Great for understanding before making changes.
* **Work in Default Mode**: The balanced choice for most development work.
* **Switch to Auto-Edit**: When you're making lots of safe code changes.
* **Use YOLO sparingly**: Only for trusted automation in controlled environments.

> [!TIP]
> You can quickly cycle through modes during a session using **Shift+Tab** (or **Tab** on Windows). The terminal status bar shows your current mode, so you always know what permissions Qwen Code has.

---

## 1. Use Plan Mode for Safe Code Analysis

Plan Mode instructs Qwen Code to create a plan by analyzing the codebase with read-only operations, perfect for exploring codebases, planning complex changes, or reviewing code safely.

### When to Use Plan Mode
* **Multi-step implementation**: When your feature requires making edits to many files.
* **Code exploration**: When you want to research the codebase thoroughly before changing anything.
* **Interactive development**: When you want to iterate on the direction with Qwen Code.

### How to Use Plan Mode
* **Turn on Plan Mode during a session**: Cycle through modes using Shift+Tab (or Tab on Windows).
* **Use the `/plan` command**:
  ```bash
  /plan                          # Enter plan mode
  /plan refactor the auth module # Enter plan mode and start planning
  /plan exit                     # Exit plan mode, restore previous mode
  ```
* **Start a new session in Plan Mode**:
  ```bash
  /approval-mode plan
  ```
* **Run "headless" queries in Plan Mode**:
  ```bash
  qwen --prompt "What is machine learning?"
  ```

---

## 2. Use Default Mode for Controlled Interaction

Default Mode is the standard way to work with Qwen Code. In this mode, you maintain full control over all potentially risky operations - Qwen Code will ask for your approval before making any file changes or executing shell commands.

### When to Use Default Mode
* **New to a codebase**: Safe, slow exploration.
* **Critical systems**: Working on production code or sensitive data.
* **Learning and teaching**: Understanding each step Qwen Code takes.
* **Team collaboration**: Shared projects.

### How to Use Default Mode
* **Turn on Default Mode during a session**: Press Shift+Tab (or Tab on Windows) until no mode indicator appears.
* **Start a new session in Default Mode**:
  ```bash
  /approval-mode default
  ```
* **Run "headless" queries in Default Mode**:
  ```bash
  qwen --prompt "Analyze this code for potential bugs"
  ```

---

## 3. Auto Edits Mode

Auto-Edit Mode instructs Qwen Code to automatically approve file edits while requiring manual approval for shell commands, ideal for accelerating development workflows while maintaining system safety.

### When to Use Auto-Accept Edits Mode
* **Daily development**: Ideal for most coding tasks.
* **Safe automation**: Allows file modification but blocks accidental execution of dangerous commands.

### How to Switch to this Mode
* **Switch via command**:
  ```bash
  /approval-mode auto-edit
  ```
* **Keyboard Shortcut**: Press **Shift+Tab** (or **Tab** on Windows) until `⏵⏵ accept edits` appears at the bottom.

---

## 4. YOLO Mode - Full Automation

YOLO Mode grants Qwen Code the highest permissions, automatically approving all tool calls including file editing and shell commands.

### When to Use YOLO Mode
* **Automated scripts**: Running predefined automated tasks.
* **CI/CD pipelines**: Automated execution in controlled environments.
* **Personal projects**: Rapid iteration in fully trusted environments.
* **Batch processing**: Tasks requiring multi-step command chains.

> [!WARNING]
> Use YOLO Mode with caution. AI can execute any command with your terminal permissions. Ensure you trust the codebase, understand all actions the AI will perform, and back up or commit files first.

### How to Enable YOLO Mode
* **Temporarily enable (current session only)**:
  ```bash
  /approval-mode yolo
  ```
* **Set as project default**:
  ```bash
  /approval-mode yolo --project
  ```
* **Set as user global default**:
  ```bash
  /approval-mode yolo --user
  ```

---

## Mode Switching & Configuration

### Keyboard Shortcut Switching
Press **Shift+Tab** (or **Tab** on Windows) to quickly cycle:
```
Default Mode ➔ Auto-Edit Mode ➔ YOLO Mode ➔ Plan Mode ➔ Default Mode
```

### Persistent Configuration
Configure default permissions in project-level (`./.qwen/settings.json`) or user-level (`~/.qwen/settings.json`) configuration files:
```json
{
  "permissions": {
    "defaultMode": "auto-edit",
    "confirmShellCommands": true,
    "confirmFileEdits": true
  }
}
```
