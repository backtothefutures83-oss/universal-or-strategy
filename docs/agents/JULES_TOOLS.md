# Jules AI Tools & Capabilities

## Overview

**Agent**: Jules AI  
**Role**: Primary non-src engineer for GitHub-based workflows  
**Strengths**: GitHub API integration, PR automation, issue management  
**Primary Use Cases**: Non-src PRs, documentation, workflow automation, GitHub operations

## Core Capabilities

### 1. GitHub-Native Workflows

Jules AI operates directly through GitHub's API, making it ideal for:

- **PR Creation & Management**: Automated PR workflows
- **Issue Tracking**: Create, update, close issues
- **Code Review**: Comment on PRs, request changes
- **Branch Management**: Create, merge, delete branches
- **GitHub Actions**: Trigger workflows, monitor runs

### 2. Non-src Engineering

Jules AI is the **primary engineer** for all non-src/ tasks:

- Documentation updates (`docs/`)
- Test file creation (`tests/`)
- Benchmark modifications (`benchmarks/`)
- Script updates (`scripts/`)
- Workflow changes (`.github/`)
- Configuration files (`.yml`, `.json`, `.md`)

### 3. PR Automation

**Automated PR Workflow**:
```bash
# Jules creates PR
jules "Create PR for RMA proximity documentation"

# Auto-labels
- non-src-only
- documentation
- auto-merge-candidate

# Fast-track merge (no bot audit required)
```

## Tool Access Matrix

| Tool Category | Access Level | Notes |
|---------------|--------------|-------|
| **Code Navigation** | ✅ Full | jCodemunch MCP (via GitHub) |
| **Architecture** | ⚠️ Limited | Read-only graphify access |
| **Knowledge Base** | ✅ Full | Jane Street KB (via API) |
| **Testing** | ✅ Full | Can create/modify test files |
| **Build & Deploy** | ❌ None | No local script execution |
| **PR Workflow** | ✅ Full | Native GitHub API |
| **GitHub Apps** | ✅ Full | Can trigger bot reviews |
| **MCP Servers** | ⚠️ Limited | GitHub-accessible only |

## When to Use Jules vs Bob

### Use Jules AI When:

1. **Non-src PRs**:
   - Documentation updates
   - Test file creation
   - Workflow modifications
   - Configuration changes

2. **GitHub Operations**:
   - PR creation/management
   - Issue tracking
   - Branch operations
   - GitHub Actions triggers

3. **Fast-Track Merges**:
   - Non-src changes (no bot audit)
   - Documentation fixes
   - Workflow improvements

4. **Automated Workflows**:
   - Scheduled documentation updates
   - Issue triage
   - PR labeling
   - Stale PR cleanup

### Use Bob CLI When:

1. **src/ Engineering**:
   - Production code changes
   - Refactoring
   - Architecture design
   - Performance optimization

2. **Local Development**:
   - Build verification
   - Local testing
   - Hard-link synchronization
   - NinjaTrader integration

3. **Complex Refactoring**:
   - Multi-file src/ changes
   - God-function splitting
   - Lock-free conversions

## Workflow Integration

### 1. Non-src PR Creation

```bash
# Jules creates PR
jules "Update RMA proximity documentation"

# Automatic actions:
1. Creates feature branch
2. Commits changes
3. Opens PR with labels
4. Requests fast-track review
```

**PR Template** (Jules auto-applies):
```markdown
## Type: Documentation

**Changes:**
- Updated RMA proximity monitoring docs
- Added Mermaid diagrams
- Fixed typos

**Verification:**
- [x] Non-src files only
- [x] No src/ changes
- [x] Fast-track eligible

**Merge Strategy:** Squash and merge (no bot audit required)
```

### 2. Issue Management

```bash
# Create issue
jules "Create issue: Add RMA proximity benchmarks"

# Auto-labels:
- enhancement
- benchmarks
- non-src

# Link to epic
jules "Link issue #123 to EPIC-6"
```

### 3. GitHub Actions Integration

```bash
# Trigger workflow
jules "Run epic6-testing workflow"

# Monitor status
jules "Check workflow status for PR #8"

# Extract logs
jules "Get CI logs for failed workflow"
```

## PR Separation Protocol

Jules AI **strictly enforces** the two-PR model:

### Non-src PR (Jules Handles)

**Allowed Files**:
- `docs/**`
- `tests/**`
- `benchmarks/**`
- `scripts/**`
- `.github/**`
- `*.md` (root level)
- `*.yml`, `*.yaml`, `*.json` (config)

**Workflow**:
1. Jules creates PR
2. Auto-labels: `non-src-only`
3. Fast-track review (no bot audit)
4. Squash and merge

### src/ PR (Bob Handles)

**Restricted Files**:
- `src/**`

**Workflow**:
1. Bob creates PR
2. Full bot audit (CodeRabbit, Codacy, Semgrep)
3. `/pr-loop` to 100/100 PHS
4. Manual F5 verification
5. Merge after approval

## Configuration

### GitHub Token

```bash
# Required environment variable
GITHUB_TOKEN=ghp_your_token_here

# Scopes required:
- repo (full control)
- workflow (trigger actions)
- read:org (organization access)
```

### Jules AI Settings

```json
{
  "agent": "jules",
  "github": {
    "owner": "mdasdispatch-hash",
    "repo": "universal-or-strategy",
    "default_branch": "main"
  },
  "pr_defaults": {
    "auto_label": true,
    "fast_track_non_src": true,
    "require_separation": true
  }
}
```

## Best Practices

### 1. PR Separation

**ALWAYS verify** before creating PR:
```bash
# Check file list
jules "List changed files"

# Verify separation
powershell -File .\scripts\verify_pr_separation.ps1 -PrNumber <N>
```

**If mixed files detected**:
```bash
# Split into two PRs
jules "Split PR #<N> into src and non-src"

# Or use Bob command
/pr-split <N>
```

### 2. Fast-Track Criteria

Non-src PRs are **fast-track eligible** if:
- ✅ Zero src/ files
- ✅ Documentation or tests only
- ✅ No breaking changes
- ✅ Passes basic CI checks

**Fast-track merge**:
```bash
gh pr merge <N> --squash --auto
```

### 3. Issue Linking

**Always link** PRs to issues:
```bash
# In PR description
Closes #123
Fixes #456
Related to #789
```

Jules auto-detects and links.

## Common Commands

### PR Operations

```bash
# Create PR
jules "Create PR: Update documentation"

# List open PRs
jules "Show open PRs"

# Merge PR
jules "Merge PR #8 (fast-track)"

# Close PR
jules "Close PR #7 (duplicate)"
```

### Issue Operations

```bash
# Create issue
jules "Create issue: Add benchmarks"

# Update issue
jules "Update issue #123: Add progress notes"

# Close issue
jules "Close issue #123 (completed)"
```

### Workflow Operations

```bash
# Trigger workflow
jules "Run epic6-testing"

# Check status
jules "Status of workflow run #456"

# Cancel workflow
jules "Cancel workflow run #456"
```

## Limitations

### 1. No Local File Access

Jules AI operates through GitHub API only:

- ❌ Cannot read local files
- ❌ Cannot execute local scripts
- ❌ Cannot verify NinjaTrader builds
- ❌ Cannot run PowerShell commands

**Workaround**: Use Gemini CLI for local operations.

### 2. No src/ Engineering

Jules AI is **banned** from src/ changes:

- ❌ Cannot modify production code
- ❌ Cannot refactor src/ files
- ❌ Cannot create src/ files

**Enforcement**: GitHub branch protection rules.

### 3. Limited MCP Access

Jules can only access MCP servers with GitHub integration:

- ✅ jCodemunch (via GitHub)
- ❌ Local MCP servers
- ❌ File-based MCP resources

## Troubleshooting

### GitHub API Rate Limits

```bash
# Check rate limit
jules "Check API rate limit"

# If limited:
- Wait for reset (1 hour)
- Use personal token (higher limits)
- Batch operations
```

### PR Creation Failures

```bash
# Common issues:
1. Branch already exists
   - Delete old branch first
   
2. No changes detected
   - Verify files are modified
   
3. Merge conflicts
   - Rebase onto main first
```

### Fast-Track Rejection

```bash
# If fast-track denied:
1. Verify non-src only
2. Check for src/ files
3. Run separation script
4. Split if mixed
```

## References

- [GitHub API Docs](https://docs.github.com/en/rest)
- [Jules AI Documentation](https://jules.ai/docs)
- [PR Separation Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md#pr-separation)
- [Workflow Integration](docs/WORKFLOW_INTEGRATION.md)
- [Universal Agent Protocol](docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md)