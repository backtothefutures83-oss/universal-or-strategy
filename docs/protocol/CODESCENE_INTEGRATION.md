# CodeScene Integration Protocol

## Overview

CodeScene provides behavioral code analysis to identify technical debt hotspots by combining:
- **Code Complexity** (cyclomatic complexity)
- **Change Frequency** (git churn)
- **Coupling Analysis** (files that change together)

This aligns perfectly with V12's EPIC-CCN-10 (complexity reduction) and Jane Street's cognitive simplicity mandate.

## Installation

### VS Code Extension (Free)
1. Install from marketplace: `CodeScene` by Empear AB
2. Provides inline hotspot visualization in editor
3. Limited features (no CLI, no automated reports)

### CodeScene CLI (Paid - Enterprise)
```powershell
# Installation requires CodeScene Enterprise license
# Contact: https://codescene.com/pricing
npm install -g codescene-cli
```

## VS Code Extension Usage

### Hotspot Visualization
- Open any C# file in `src/`
- CodeScene highlights high-risk code sections with colored underlines:
  - 🔴 **Red**: Critical hotspots (high complexity + high churn)
  - 🟡 **Yellow**: Warning hotspots (moderate risk)
  - 🟢 **Green**: Healthy code

### Code Health Metrics
- Click CodeScene icon in status bar
- View file-level metrics:
  - **Code Health Score** (0-10)
  - **Complexity Trend** (improving/degrading)
  - **Change Coupling** (files that change together)

## Integration with V12 Workflows

### 1. Pre-Refactoring Analysis

**Before starting EPIC-CCN-10 work:**
```powershell
# 1. Open target file in VS Code
# 2. Check CodeScene status bar for Code Health Score
# 3. Identify red/yellow hotspots
# 4. Prioritize extraction based on:
#    - Complexity (CYC > 15)
#    - Churn rate (commits in last 90 days)
#    - CodeScene hotspot severity
```

**Decision Matrix:**
| Complexity | Churn | CodeScene | Priority | Action |
|------------|-------|-----------|----------|--------|
| >20 | >10 commits | Red | P0 | Extract immediately |
| 15-20 | >5 commits | Yellow | P1 | Extract in current sprint |
| 10-15 | >5 commits | Yellow | P2 | Backlog for next sprint |
| <10 | Any | Green | P3 | Monitor only |

### 2. Coupling Detection

**Use Case**: Identify God-modules that change with everything

**Workflow:**
1. Open `V12_002.cs` (main strategy file)
2. CodeScene shows "Change Coupling" panel
3. Lists files that frequently change together
4. **Action**: If >5 files couple to one module → extract shared logic

**Example:**
```
V12_002.cs couples with:
- V12_002.Orders.Management.cs (87% coupling)
- V12_002.Entries.cs (76% coupling)
- V12_002.Data.cs (65% coupling)

→ Indicates shared state or tight coupling
→ Candidate for Actor/FSM extraction
```

### 3. Refactoring Validation

**After extraction (e.g., EPIC-8 through EPIC-14):**
1. Re-open extracted files in VS Code
2. Verify CodeScene hotspot colors improved:
   - Red → Yellow or Green
   - Yellow → Green
3. Check Code Health Score increased (target: >7.0)
4. Document improvement in PR description

## Automated Reporting (Enterprise Only)

If you have CodeScene Enterprise license:

### Weekly Hotspot Report
```powershell
# scripts/codescene_report.ps1
codescene analyze --repo . --output hotspots.json
python scripts/parse_codescene_hotspots.py hotspots.json
```

### Pre-Push Hotspot Check
Add to `scripts/pre_push_validation.ps1`:
```powershell
# Check if changed files are hotspots
$changedFiles = git diff --name-only HEAD origin/main
codescene check-hotspots --files $changedFiles --threshold critical
```

## Integration with Existing Tools

### Codacy + CodeScene Synergy
- **Codacy**: Static analysis (complexity, style, security)
- **CodeScene**: Behavioral analysis (churn, coupling, trends)
- **Combined**: Identify files that are BOTH complex AND frequently changed

### Complexity Audit + CodeScene
```powershell
# 1. Run complexity audit
python scripts/complexity_audit.py --threshold 15 > complexity.txt

# 2. Cross-reference with CodeScene hotspots
# 3. Prioritize files in BOTH lists for EPIC-CCN-10
```

## Metrics to Track

### Before EPIC-CCN-10
- **Hotspot Count**: 31 files (32% of codebase)
- **Average Code Health**: 6.2/10
- **Critical Hotspots**: 8 files (V12_002.cs, V12_002.Atm.cs, etc.)

### Target After EPIC-CCN-10
- **Hotspot Count**: <15 files (<15% of codebase)
- **Average Code Health**: >7.5/10
- **Critical Hotspots**: 0 files

## Limitations

### VS Code Extension (Free)
- ✅ Real-time hotspot visualization
- ✅ File-level Code Health scores
- ✅ Change coupling detection
- ❌ No CLI automation
- ❌ No historical trend reports
- ❌ No team-wide dashboards

### Enterprise CLI (Paid)
- ✅ All free features
- ✅ Automated reporting
- ✅ CI/CD integration
- ✅ Team dashboards
- ✅ Historical trend analysis
- 💰 Requires paid license

## Recommended Workflow

### Daily Development
1. Open file in VS Code
2. Check CodeScene status bar before editing
3. If red/yellow hotspot → consider refactoring first
4. After changes → verify hotspot color improved

### Sprint Planning
1. Review CodeScene hotspot list
2. Cross-reference with EPIC-CCN-10 backlog
3. Prioritize red hotspots for current sprint
4. Track Code Health Score improvement

### PR Review
1. Reviewer checks CodeScene status of changed files
2. If introducing new hotspots → request refactoring
3. If improving hotspots → celebrate in PR comments

## Jane Street Alignment

CodeScene's methodology aligns with Jane Street principles:
- **Cognitive Simplicity**: Hotspots = hard-to-reason-about code
- **Predictable Performance**: High-churn code = unpredictable behavior
- **Correctness by Construction**: Coupling = hidden dependencies

## References

- **CodeScene Docs**: https://codescene.com/docs
- **VS Code Extension**: https://marketplace.visualstudio.com/items?itemName=codescene.codescene-vscode
- **Pricing**: https://codescene.com/pricing
- **V12 EPIC-CCN-10**: `docs/brain/EPIC-QUALITY-DEBT-EPIC4.md`