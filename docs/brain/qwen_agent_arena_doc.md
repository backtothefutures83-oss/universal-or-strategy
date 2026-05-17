# Qwen Agent Arena Feature Guide

Agent Arena allows you to dispatch multiple AI models simultaneously to execute the same task, compare their solutions side-by-side, and select the best result to apply to your workspace.

> [!WARNING]
> Agent Arena is experimental. It has known limitations around display modes and session management.

Each model runs as a fully independent agent in its own isolated Git worktree, so file operations never interfere. When all agents finish, you compare results and select a winner to merge back into your main workspace.

Unlike subagents, which delegate focused subtasks within a single session, Arena agents are complete, top-level agent instances — each with its own model, context window, and full tool access.

---

## When to Use Agent Arena
Agent Arena is most effective when you want to evaluate or compare how different models tackle the same problem. The strongest use cases are:
* **Model benchmarking**: Evaluate different models’ capabilities on real tasks in your actual codebase, not synthetic benchmarks.
* **Best-of-N selection**: Get multiple independent solutions and pick the best implementation.
* **Exploring approaches**: See how different models reason about and solve the same problem — useful for learning and insight.
* **Risk reduction**: For critical changes, validate that multiple models converge on a similar approach before committing.

*Note: Agent Arena uses significantly more tokens than a single session. It works best when the value of comparison justifies the cost.*

---

## Start an Arena Session
Use the `/arena` slash command to launch a session. Specify the models you want to compete and the task:

```bash
/arena --models qwen3.5-plus,glm-5,kimi-k2.5 "Refactor the authentication module to use JWT tokens"
```

If you omit `--models`, an interactive model selection dialog appears, letting you pick from your configured providers.

### What Happens Behind the Scenes:
1. **Worktree Setup**: Qwen Code creates isolated Git worktrees for each agent at `~/.qwen/arena/<session-id>/worktrees/<model-name>/`. Each worktree mirrors your current working directory state exactly (including staged, unstaged, and untracked files).
2. **Agent Spawning**: Each agent starts in its own worktree with full tool access and its configured model. Agents are launched sequentially but execute in parallel.
3. **Execution**: All agents work on the task independently with no shared state.
4. **Completion**: When all agents finish (or fail), you enter the result comparison phase.

---

## Interact with Agents
Currently supports **in-process mode**, where all agents run asynchronously within the same terminal process. A tab bar at the bottom of the terminal lets you switch between agents.

### Navigation Shortcuts:

| Shortcut | Action |
| :--- | :--- |
| **Right Arrow** | Switch to the next agent tab |
| **Left Arrow** | Switch to the previous agent tab |
| **Up Arrow** | Switch focus to the input box |
| **Down Arrow** | Switch focus to the agent tab bar |

### Tab Bar Indicators:
* ● : Running or idle
* ✓ : Completed successfully
* ✗ : Failed
* ○ : Cancelled

*Each agent is a full, independent session. You can scroll history, send messages, and approve tool calls within each tab.*

---

## Compare Results & Select a Winner
When all agents complete, you enter the result comparison phase. You’ll see:
* **Status Summary**: Which agents succeeded, failed, or were cancelled.
* **Execution Metrics**: Duration, rounds of reasoning, token usage, and tool call counts.
* **Arena Comparison Summary**: Common vs. single-agent files changed, line-change counts, token efficiency, and a high-level approach summary generated from each agent’s diff, metrics, and conversation.

A selection dialog presents the successful agents:
* Press `p` to toggle a quick preview for the highlighted agent.
* Press `d` to toggle that agent’s detailed diff.
* Choose one to apply its changes to your main workspace (which automatically cleans up worktrees and temporary branches), or discard all results.

---

## Configuration Settings (`settings.json`)

```json
{
  "arena": {
    "worktreeBaseDir": "~/.qwen/arena",
    "maxRoundsPerAgent": 50,
    "timeoutSeconds": 600
  }
}
```

| Setting | Description | Default |
| :--- | :--- | :--- |
| `arena.worktreeBaseDir` | Base directory for arena worktrees | `~/.qwen/arena` |
| `arena.maxRoundsPerAgent` | Maximum reasoning rounds per agent | `50` |
| `arena.timeoutSeconds` | Timeout for each agent in seconds | `600` |

---

## Best Practices
1. **Choose models that complement each other**: Compare across providers (e.g. Qwen, GLM, Kimi) to get diverse conceptual approaches.
2. **Keep tasks self-contained**: Tasks should be fully describable in the initial prompt without requiring extensive back-and-forth.
3. **Limit the number of agents**: 2-3 agents provide the best balance of comparison value to token/time resources. Max is 5 concurrent agents.
4. **Use Arena for high-impact decisions**: Ideal for choosing architectures, selecting refactoring approaches, or validating critical bug fixes from multiple angles.

---

## Limitations
* **In-process mode only**: Split-pane display via tmux/iTerm2 is not yet implemented.
* **No diff preview before selection**: There is no side-by-side diff viewer before choosing.
* **No worktree retention**: Ephemeral worktrees are always deleted after selection.
* **No session resumption**: Closing the terminal mid-session orphans the worktrees, requiring `git worktree prune`.
* **Git repository required**: Cannot be used in non-Git directories.

---

## Multi-Agent Modes Comparison

| Mode | Goal | Communication | Isolation | Best For |
| :--- | :--- | :--- | :--- | :--- |
| **Agent Arena** | **Competitive**: Find best solution to same task | No inter-agent communication | **Full**: separate Git worktrees | Benchmarking, choosing between model approaches |
| **Agent Team** *(Planned)* | **Collaborative**: Tackle different aspects together | Direct peer-to-peer messaging | Independent sessions, shared task list | Research, complex cross-layer work |
| **Agent Swarm** *(Planned)* | **Batch Parallel**: Ephemeral workers for bulk tasks | One-way: results aggregated by parent | Lightweight ephemeral context per worker | Batch operations, data processing, map-reduce |
