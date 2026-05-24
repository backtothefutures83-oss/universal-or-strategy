# Persistent Memory System - V12 Implementation Guide

**Version:** 1.0  
**Status:** Phase 1 Implementation  
**Last Updated:** 2026-05-24T01:32:00Z

---

## Overview

V12's persistent memory system enables cross-session learning without manual context injection. Inspired by Hermes Agent's memory architecture, this system allows agents to remember V12 DNA, project structure, user preferences, and past mistakes across sessions.

**Key Benefits:**
- Agent remembers V12 DNA across sessions (no relearning)
- Agent remembers user preferences (no re-asking)
- Agent remembers past mistakes (no repetition)
- Reduces system prompt size (facts in memory, not rules)

---

## Architecture

### Components

| Component | Purpose | Location |
|-----------|---------|----------|
| **MEMORY.md** | Agent's learned facts | `docs/brain/MEMORY.md` |
| **USER.md** | User profile | `docs/brain/USER.md` |
| **update_memory.py** | Structured memory updates | `.bob/tools/update_memory.py` |
| **load_memory.sh** | Session start hook | `.bob/hooks/session_start/load_memory.sh` |

### Data Flow

```
Session Start → load_memory.sh → Verify MEMORY.md + USER.md exist
                                ↓
                        Bob CLI loads files into system prompt
                                ↓
                        Agent has full memory context
                                ↓
Session Work → Agent learns new fact → update_memory.py → MEMORY.md updated
                                                         ↓
                                                    Next session: fact remembered
```

---

## MEMORY.md Format

### Structure

```markdown
# V12 Agent Memory

**Last Updated:** <timestamp>

## V12 DNA Constraints (Never Forget)
- Lock-free mandate
- ASCII-only compliance
- Complexity threshold
- Performance targets
- Correctness by construction

## Project Structure
- Repository details
- Build system
- Testing infrastructure
- Deployment process
- Hard links

## Architecture Patterns
- FSM/Actor model
- Atomic operations
- Zero-allocation patterns
- Microsecond latency

## Tools & Workflows
- Code navigation
- Quality gates
- PR workflow
- Testing

## Environment
- Operating system
- Development tools
- Agent ecosystem

## Past Mistakes (Learned)
- Mistake: <description>
  - Impact: <what broke>
  - Root Cause: <why it happened>
  - Solution: <how we fixed it>
  - Prevention: <how we prevent recurrence>

## Active Patterns
- Epic workflow
- Ticket workflow
- TDD workflow

## Knowledge Sources
- Jane Street KB
- Graphify
- jCodemunch MCP
- Routa CLI
```

### Content Guidelines

**DO:**
- Keep entries concise (1-3 sentences per fact)
- Cross-reference detailed docs (don't duplicate)
- Update timestamps when modifying
- Prune outdated facts quarterly

**DON'T:**
- Duplicate content from protocol docs
- Include implementation details (link to docs instead)
- Let memory grow unbounded (bounded memory principle)
- Store temporary session state (use for persistent facts only)

---

## USER.md Format

### Structure

```markdown
# V12 User Profile

**Last Updated:** <timestamp>

## User Identity
- Name
- Role
- Communication style

## Development Preferences
- Shell & environment
- Code style
- Agent preferences
- Workflow preferences

## Decision History
- Architecture decisions
- Tool adoption

## Coding Standards
- Naming conventions
- File organization
- Error handling
- Performance

## Quality Standards
- Testing
- Code review
- Documentation

## Communication Patterns
- Feedback style
- Response expectations
- Decision making

## Project Context
- Current focus
- Recent wins
- Known pain points
```

### Content Guidelines

**DO:**
- Record user preferences explicitly stated
- Document past decisions with rationale
- Update when preferences change
- Keep current focus section up-to-date

**DON'T:**
- Assume preferences (ask if unsure)
- Store temporary project state
- Include technical facts (those go in MEMORY.md)
- Let decision history grow unbounded (keep last 10-15)

---

## update_memory.py Tool

### Usage

```bash
# Update MEMORY.md
python .bob/tools/update_memory.py \
  --type memory \
  --category "Past Mistakes" \
  --content "Forgot deploy-sync.ps1 after src/ edit"

# Update USER.md
python .bob/tools/update_memory.py \
  --type user \
  --category "Development Preferences" \
  --content "User prefers PowerShell over Bash"

# Replace entire section (use sparingly)
python .bob/tools/update_memory.py \
  --type memory \
  --category "V12 DNA Constraints" \
  --content "Updated DNA constraints" \
  --operation replace
```

### Parameters

| Parameter | Required | Values | Description |
|-----------|----------|--------|-------------|
| `--type` | Yes | `memory`, `user` | Which file to update |
| `--category` | Yes | Section name | Section to update |
| `--content` | Yes | Text | Content to add |
| `--operation` | No | `append`, `replace` | Append (default) or replace section |

### Examples

```bash
# Record a new mistake
python .bob/tools/update_memory.py \
  --type memory \
  --category "Past Mistakes" \
  --content "Used Unicode in C# string → ASCII gate FAIL → rollback"

# Update user preference
python .bob/tools/update_memory.py \
  --type user \
  --category "Agent Preferences" \
  --content "Prefers Bob CLI over Claude for src/ work"

# Record architecture decision
python .bob/tools/update_memory.py \
  --type user \
  --category "Decision History" \
  --content "Adopted Hermes memory system for cross-session learning"
```

---

## load_memory.sh Hook

### Purpose

Automatically loads MEMORY.md and USER.md at session start, injecting them into the system prompt.

### Behavior

1. **Session Start:** Hook runs automatically when Bob CLI session starts
2. **File Check:** Verifies MEMORY.md and USER.md exist
3. **Load:** Bob CLI loads files into system prompt
4. **Summary:** Prints memory summary to console

### Output

```
[load_memory] Loading persistent memory...
[load_memory] ✓ MEMORY.md found (267 lines)
[load_memory] ✓ USER.md found (213 lines)
[load_memory] Memory will be injected into system prompt

[load_memory] Memory Summary:
  V12 DNA: Lock-free, ASCII-only, CYC ≤15, 0B allocation, <300μs
  Project: universal-or-strategy (C# .NET 8, NinjaTrader 8)
  User: Mohammed Khalid (Director, PowerShell, Bob CLI primary)
```

### Troubleshooting

**Problem:** Memory not loading  
**Solution:** Check files exist at `docs/brain/MEMORY.md` and `docs/brain/USER.md`

**Problem:** Hook not running  
**Solution:** Verify `.bob/hooks/session_start/load_memory.sh` is executable

**Problem:** Memory outdated  
**Solution:** Run `update_memory.py` to add new facts

---

## Integration with mistake_log.jsonl

### Current State

- **mistake_log.jsonl:** Append-only log of errors (scripts/mistake_log.jsonl)
- **analyze_mistakes.ps1:** Pattern detection script
- **Integration:** Manual (not yet automated)

### Phase 1 Integration

**Goal:** Automatically promote patterns from mistake_log.jsonl to MEMORY.md

**Implementation:**

1. **analyze_mistakes.ps1** detects repetitive patterns
2. **Script outputs:** Suggested memory updates
3. **Agent reviews:** Confirms updates are valid
4. **update_memory.py:** Adds to MEMORY.md "Past Mistakes" section

**Example Flow:**

```powershell
# Run pattern detection
powershell -File .\scripts\analyze_mistakes.ps1

# Output:
# [PATTERN DETECTED] Forgot deploy-sync.ps1 (3 occurrences)
# [SUGGESTION] Add to MEMORY.md:
#   Mistake: Forgot deploy-sync.ps1 after src/ edit
#   Impact: Hard link desync → stale DLL → BUILD_TAG mismatch
#   Solution: Post-tool-use hook auto-runs deploy-sync.ps1

# Agent confirms and updates memory
python .bob/tools/update_memory.py \
  --type memory \
  --category "Past Mistakes" \
  --content "Forgot deploy-sync.ps1 after src/ edit → Hard link desync → Post-tool-use hook now auto-runs"
```

---

## Best Practices

### When to Update Memory

**MEMORY.md:**
- After learning a new V12 DNA constraint
- After discovering a project structure detail
- After making a mistake (once root cause is understood)
- After establishing a new workflow pattern
- After adopting a new tool

**USER.md:**
- After user explicitly states a preference
- After user makes an architecture decision
- After user changes their workflow
- After user adopts a new tool
- After user provides feedback on agent behavior

### When NOT to Update Memory

**Don't update for:**
- Temporary session state (use session context instead)
- Implementation details (link to docs instead)
- Speculative information (wait for confirmation)
- Duplicate information (check if already recorded)
- Trivial facts (only record significant learnings)

### Memory Maintenance

**Quarterly Review:**
1. Read through MEMORY.md and USER.md
2. Remove outdated facts
3. Consolidate duplicate entries
4. Update cross-references to docs
5. Verify timestamps are current

**Pruning Guidelines:**
- Keep last 10-15 past mistakes (remove older)
- Keep last 10-15 architecture decisions (remove older)
- Keep all V12 DNA constraints (never remove)
- Keep all active patterns (remove when deprecated)

---

## Testing

### Manual Testing

```bash
# Test update_memory.py
python .bob/tools/update_memory.py \
  --type memory \
  --category "Past Mistakes" \
  --content "Test mistake entry"

# Verify update
grep -A 5 "Test mistake entry" docs/brain/MEMORY.md

# Test load_memory.sh
bash .bob/hooks/session_start/load_memory.sh

# Expected output: Memory summary
```

### Integration Testing

1. **Start Bob CLI session**
2. **Verify memory loaded:** Check console output for memory summary
3. **Test memory recall:** Ask agent about V12 DNA constraints
4. **Expected:** Agent responds without re-reading docs
5. **Test memory update:** Use update_memory.py to add fact
6. **Restart session:** Verify new fact is remembered

---

## Troubleshooting

### Memory Not Loading

**Symptoms:** Agent doesn't remember V12 DNA, asks repeated questions

**Diagnosis:**
```bash
# Check files exist
ls -la docs/brain/MEMORY.md docs/brain/USER.md

# Check hook exists
ls -la .bob/hooks/session_start/load_memory.sh

# Test hook manually
bash .bob/hooks/session_start/load_memory.sh
```

**Solutions:**
- Create missing files (use templates in this doc)
- Make hook executable: `chmod +x .bob/hooks/session_start/load_memory.sh`
- Verify Bob CLI settings.json has hooks enabled

### Memory Update Fails

**Symptoms:** update_memory.py errors or doesn't update file

**Diagnosis:**
```bash
# Test update_memory.py
python .bob/tools/update_memory.py \
  --type memory \
  --category "V12 DNA Constraints" \
  --content "Test entry"

# Check for errors in output
```

**Solutions:**
- Verify category name matches exactly (case-sensitive)
- Check file permissions (must be writable)
- Verify Python 3.x is installed

### Memory Outdated

**Symptoms:** Agent remembers old facts, not recent changes

**Diagnosis:**
```bash
# Check last updated timestamp
head -5 docs/brain/MEMORY.md | grep "Last Updated"

# Check recent updates
git log --oneline docs/brain/MEMORY.md
```

**Solutions:**
- Run update_memory.py to add new facts
- Manually edit MEMORY.md if needed
- Restart Bob CLI session to reload memory

---

## Future Enhancements (Phase 2+)

### Phase 2: Automatic Memory Updates
- Hook into mistake_log.jsonl for automatic pattern promotion
- Auto-detect new V12 DNA constraints from protocol doc changes
- Auto-update user preferences from feedback patterns

### Phase 3: Memory Search
- FTS5 full-text search (like Hermes)
- Query memory via CLI: `bob memory search "lock-free"`
- Semantic search via embeddings

### Phase 4: Memory Providers
- Pluggable backends (Honcho, Mem0, etc.)
- Cross-session user modeling
- Personalization beyond bounded memory

---

## References

- **Hermes Analysis:** `docs/brain/HERMES_ARCHITECTURE_ANALYSIS.md`
- **Universal Protocol:** `docs/protocol/UNIVERSAL_AGENT_PROTOCOL.md`
- **AGENTS.md:** Section 21 - Self-Improving Architecture
- **mistake_log.jsonl:** `scripts/mistake_log.jsonl`
- **analyze_mistakes.ps1:** `scripts/analyze_mistakes.ps1`

---

**Last Updated:** 2026-05-24T01:32:00Z  
**Status:** Phase 1 Complete - Ready for Testing  
**Next Review:** After 1 week of production use