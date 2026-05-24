#!/usr/bin/env python3
"""
Bot Findings Consolidation Script
Parses PR findings JSON files and generates comprehensive audit document
"""

import json
import re
from pathlib import Path
from collections import defaultdict
from datetime import datetime

def extract_violations_from_cubic(body):
    """Extract structured violations from cubic bot reviews"""
    violations = []
    # Match violation blocks
    pattern = r'<violation number="(\d+)" location="([^"]+)">([^<]+)</violation>'
    matches = re.findall(pattern, body, re.DOTALL)
    
    for match in matches:
        number, location, description = match
        description = description.strip()
        
        # Extract severity
        severity = "UNKNOWN"
        if "P0:" in description:
            severity = "CRITICAL"
        elif "P1:" in description:
            severity = "HIGH"
        elif "P2:" in description:
            severity = "MEDIUM"
        elif "P3:" in description:
            severity = "LOW"
        
        # Categorize
        category = "Maintainability"
        if any(word in description.lower() for word in ["secret", "token", "api key", "hardcoded", "bearer"]):
            category = "Security"
        elif any(word in description.lower() for word in ["allocation", "performance", "zero-allocation"]):
            category = "Performance"
        elif any(word in description.lower() for word in ["complexity", "cyclomatic"]):
            category = "Complexity"
        elif any(word in description.lower() for word in ["style", "formatting", "whitespace", "parenthesis"]):
            category = "Style"
        elif any(word in description.lower() for word in ["rollback", "race", "thread", "atomic", "lock", "counter", "drift"]):
            category = "ErrorProne"
        
        violations.append({
            "location": location,
            "description": description,
            "severity": severity,
            "category": category
        })
    
    return violations

def extract_test_suggestions_from_codacy(body):
    """Extract test suggestions from Codacy reviews"""
    suggestions = []
    # Match test suggestion items
    pattern = r'- \[ \] (.+?)(?=\n- \[ \]|\n\n|$)'
    matches = re.findall(pattern, body, re.DOTALL)
    
    for match in matches:
        suggestions.append({
            "location": "General",
            "description": f"Missing test: {match.strip()}",
            "severity": "MEDIUM",
            "category": "Maintainability"
        })
    
    return suggestions

def main():
    print("Consolidating bot findings from PRs 1, 2, 4, 6, 8...")
    
    prs = [1, 2, 4, 6, 8]
    all_findings = []
    
    # Load all PR findings
    for pr_num in prs:
        json_path = Path(f"docs/brain/pr{pr_num}_findings.json")
        if json_path.exists():
            with open(json_path, 'r', encoding='utf-8-sig') as f:
                pr_data = json.load(f)
                all_findings.append(pr_data)
            print(f"[OK] Loaded PR #{pr_num}")
        else:
            print(f"[MISSING] {json_path}")
    
    # Categorize findings
    categorized = defaultdict(list)
    issues_by_pr = defaultdict(int)
    issues_by_bot = defaultdict(int)
    issues_by_file = defaultdict(int)
    
    total_issues = 0
    
    # Process each PR
    for pr_data in all_findings:
        pr_num = pr_data['pr_number']
        
        # Process cubic findings
        for review in pr_data['bot_findings']['cubic']:
            body = review.get('body', '')
            if body:
                violations = extract_violations_from_cubic(body)
                for v in violations:
                    finding = {
                        'PR': pr_num,
                        'Bot': 'cubic',
                        **v
                    }
                    categorized[v['category']].append(finding)
                    total_issues += 1
                    issues_by_pr[pr_num] += 1
                    issues_by_bot['cubic'] += 1
                    
                    file = v['location'].split(':')[0]
                    issues_by_file[file] += 1
        
        # Process Codacy findings
        for review in pr_data['bot_findings']['Codacy']:
            body = review.get('body', '')
            if body:
                suggestions = extract_test_suggestions_from_codacy(body)
                for s in suggestions:
                    finding = {
                        'PR': pr_num,
                        'Bot': 'Codacy',
                        **s
                    }
                    categorized[s['category']].append(finding)
                    total_issues += 1
                    issues_by_pr[pr_num] += 1
                    issues_by_bot['Codacy'] += 1
    
    # Generate markdown report
    report_lines = [
        "# Deferred Work Audit - Bot Findings Consolidation",
        "",
        f"**Generated:** {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC')}  ",
        f"**PRs Analyzed:** #1, #2, #4, #6, #8  ",
        f"**Total Findings:** {total_issues}",
        "",
        "## Executive Summary",
        "",
        "### Findings by PR",
    ]
    
    for pr in sorted(prs):
        count = issues_by_pr.get(pr, 0)
        report_lines.append(f"- **PR #{pr}**: {count} findings")
    
    report_lines.extend([
        "",
        "### Findings by Bot",
    ])
    
    for bot in sorted(issues_by_bot.keys()):
        report_lines.append(f"- **{bot}**: {issues_by_bot[bot]} findings")
    
    report_lines.extend([
        "",
        "### Findings by Category",
    ])
    
    for cat in sorted(categorized.keys()):
        count = len(categorized[cat])
        report_lines.append(f"- **{cat}**: {count} findings")
    
    report_lines.extend([
        "",
        "### Top 10 Files with Most Issues",
    ])
    
    top_files = sorted(issues_by_file.items(), key=lambda x: x[1], reverse=True)[:10]
    for file, count in top_files:
        report_lines.append(f"- **{file}**: {count} findings")
    
    report_lines.extend([
        "",
        "---",
        "",
        "## Detailed Findings by Category",
        "",
    ])
    
    # Add detailed findings for each category
    priority_order = ["Security", "ErrorProne", "Complexity", "Performance", "Maintainability", "Style"]
    
    for category in priority_order:
        if category not in categorized:
            continue
        
        findings = categorized[category]
        priority_label = {
            "Security": "CRITICAL",
            "ErrorProne": "HIGH",
            "Complexity": "MEDIUM",
            "Performance": "MEDIUM",
            "Maintainability": "LOW",
            "Style": "LOW"
        }.get(category, "UNKNOWN")
        
        report_lines.extend([
            f"### {category} Issues (Priority: {priority_label})",
            f"**Count:** {len(findings)}",
            ""
        ])
        
        if findings:
            for f in findings:
                report_lines.extend([
                    f"#### PR #{f['PR']} - {f['location']}",
                    f"- **Bot:** {f['Bot']}",
                    f"- **Severity:** {f['severity']}",
                    f"- **Description:** {f['description']}",
                    ""
                ])
        else:
            report_lines.append(f"No {category.lower()} issues found.")
            report_lines.append("")
        
        report_lines.append("---")
        report_lines.append("")
    
    # Pattern analysis
    report_lines.extend([
        "## Pattern Analysis",
        "",
        "### Systemic Issues",
        ""
    ])
    
    # Identify patterns
    patterns = defaultdict(list)
    for cat, findings in categorized.items():
        for f in findings:
            desc = f['description'].lower()
            if any(word in desc for word in ["hardcoded", "secret", "token", "api key", "bearer"]):
                patterns["Hardcoded Secrets"].append(f)
            elif any(word in desc for word in ["complexity", "cyclomatic"]):
                patterns["High Complexity"].append(f)
            elif any(word in desc for word in ["allocation", "zero-allocation"]):
                patterns["Allocation Violations"].append(f)
            elif any(word in desc for word in ["rollback", "incomplete", "phantom"]):
                patterns["Incomplete Rollback Logic"].append(f)
            elif any(word in desc for word in ["test", "coverage", "verify"]):
                patterns["Missing Test Coverage"].append(f)
            elif any(word in desc for word in ["style", "formatting", "parenthesis"]):
                patterns["Style Violations"].append(f)
            elif any(word in desc for word in ["build artifact", "extracted.py"]):
                patterns["Build Artifacts"].append(f)
            else:
                patterns["Other"].append(f)
    
    for pattern in sorted(patterns.keys()):
        findings = patterns[pattern]
        prs = sorted(set(f['PR'] for f in findings))
        prs_str = ", ".join(f"#{pr}" for pr in prs)
        report_lines.append(f"- **{pattern}**: {len(findings)} occurrences across PRs {prs_str}")
    
    report_lines.extend([
        "",
        "### High-Impact Clusters",
        ""
    ])
    
    # Group by file
    file_groups = defaultdict(list)
    for cat, findings in categorized.items():
        for f in findings:
            file = f['location'].split(':')[0]
            file_groups[file].append(f)
    
    top_files = sorted(file_groups.items(), key=lambda x: len(x[1]), reverse=True)[:5]
    for file, findings in top_files:
        categories = sorted(set(f['category'] for f in findings))
        report_lines.append(f"- **{file}**: {len(findings)} findings ({', '.join(categories)})")
    
    report_lines.extend([
        "",
        "---",
        "",
        "## EPIC-7-QUALITY Ticket Specifications",
        "",
        "### Ticket 1: Security - Remove Hardcoded Secrets",
        "- **Scope:** Multiple files across PRs #1, #2, #6",
        f"- **Findings:** {len(patterns.get('Hardcoded Secrets', []))} hardcoded API keys/tokens",
        "- **Effort:** Medium (M)",
        "- **Priority:** P0 (CRITICAL)",
        "- **Action:** Rotate all exposed tokens, move to environment variables, add to .gitignore",
        "- **Files Affected:**",
    ])
    
    secret_files = set()
    for f in patterns.get("Hardcoded Secrets", []):
        secret_files.add(f['location'].split(':')[0])
    for file in sorted(secret_files):
        report_lines.append(f"  - {file}")
    
    report_lines.extend([
        "",
        "### Ticket 2: Error-Prone - Complete Circuit Breaker Rollback",
        "- **Scope:** src/V12_002.SIMA.Dispatch.cs",
        f"- **Findings:** {len([f for f in patterns.get('Incomplete Rollback Logic', []) if 'Dispatch.cs' in f['location']])} incomplete rollback issues",
        "- **Effort:** Small (S)",
        "- **Priority:** P1 (HIGH)",
        "- **Action:** Add dictionary cleanup and registeredForCleanup reset",
        "",
        "### Ticket 3: Maintainability - Add Missing Test Coverage",
        "- **Scope:** Circuit breaker, counter sync, dispatch logic",
        f"- **Findings:** {len(patterns.get('Missing Test Coverage', []))} test gaps identified",
        "- **Effort:** Large (L)",
        "- **Priority:** P2 (MEDIUM)",
        "- **Action:** Implement unit tests for trip/reset thresholds, state rollback",
        "",
        "### Ticket 4: Style - Fix StyleCop Violations",
        "- **Scope:** src/V12_002.SIMA.Dispatch.cs, src/V12_002.SIMA.Fleet.cs",
        f"- **Findings:** {len(patterns.get('Style Violations', []))} style violations",
        "- **Effort:** Small (S)",
        "- **Priority:** P3 (LOW)",
        "- **Action:** Auto-fix with dotnet format, verify build",
        "",
        "### Ticket 5: Maintainability - Clean Up Build Artifacts",
        "- **Scope:** Root directory",
        f"- **Findings:** {len(patterns.get('Build Artifacts', []))} accidentally committed build artifacts",
        "- **Effort:** Extra Small (XS)",
        "- **Priority:** P2 (MEDIUM)",
        "- **Action:** Remove files, add patterns to .gitignore",
        "",
        "---",
        "",
        "## Next Steps",
        "",
        "1. **Immediate (P0):** Rotate all exposed API tokens (Greptile, etc.)",
        "2. **High Priority (P1):** Fix circuit breaker rollback logic",
        "3. **Medium Priority (P2):** Add test coverage for critical paths",
        "4. **Low Priority (P3):** Clean up style violations",
        "",
        "**Estimated Total Effort:** 2-3 sprints (assuming 2-week sprints)",
        "",
        "---",
        "",
        "**End of Report**"
    ])
    
    # Write report
    output_path = Path("docs/brain/DEFERRED_WORK_AUDIT.md")
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write('\n'.join(report_lines))
    
    print(f"\n[OK] Report generated: {output_path}")
    print(f"[OK] Total findings: {total_issues}")
    print(f"[OK] Categories: {len(categorized)}")
    print(f"[OK] Patterns identified: {len(patterns)}")

if __name__ == "__main__":
    main()

# Made with Bob
