#!/usr/bin/env python3
"""
Parse Codacy Export and Extract Security + Error-Prone Issues
V12 Universal OR Strategy - Codacy Remediation
"""

import re
from collections import defaultdict
from pathlib import Path
from datetime import datetime

def parse_codacy_export(input_file="docs/codacy.txt", output_dir="docs/brain"):
    """Parse Codacy text export and extract Security + Error-prone issues."""
    
    print("=== Codacy Export Parser ===")
    print(f"Input: {input_file}")
    print(f"Output: {output_dir}")
    print()
    
    # Read file
    with open(input_file, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # Data structures
    security_issues = []
    error_prone_issues = []
    current_issue = None
    in_issue_block = False
    
    # State machine parser
    prev_line_was_src = False
    for i, line in enumerate(lines):
        line = line.strip()
        
        # Track if previous line was "src/"
        if line == 'src/':
            prev_line_was_src = True
            continue
        
        # Detect file path (comes after "src/" line)
        if prev_line_was_src and re.match(r'^[\w\./]+\.(cs|ps1|py)$', line):
            # Save previous issue
            if current_issue and current_issue.get('category'):
                if current_issue['category'] == 'Security':
                    security_issues.append(current_issue)
                elif current_issue['category'] == 'Error prone':
                    error_prone_issues.append(current_issue)
            
            # Start new issue
            current_issue = {
                'file': f"src/{line}",  # Add src/ prefix
                'line': None,
                'severity': None,
                'category': None,
                'pattern': None,
                'code_snippet': None
            }
            in_issue_block = True
            prev_line_was_src = False
            continue
        
        prev_line_was_src = False
        
        # Detect line number
        if in_issue_block and re.match(r'^\d+$', line):
            current_issue['line'] = line
            continue
        
        # Detect severity
        if in_issue_block and line in ['HIGH', 'MEDIUM', 'LOW']:
            current_issue['severity'] = line
            continue
        
        # Detect category
        if in_issue_block and line in ['Security', 'Error prone', 'Code Style', 'Performance', 'Compatibility']:
            current_issue['category'] = line
            continue
        
        # Detect pattern name
        if (in_issue_block and current_issue.get('category') and 
            not current_issue.get('pattern') and line and len(line) < 100):
            if not re.match(r'^(if|else|for|while|return|var|private|public|protected|internal)', line):
                current_issue['pattern'] = line
        
        # Capture code snippet
        if (in_issue_block and current_issue.get('pattern') and 
            not current_issue.get('code_snippet') and line):
            current_issue['code_snippet'] = line
    
    # Save last issue
    if current_issue and current_issue.get('category'):
        if current_issue['category'] == 'Security':
            security_issues.append(current_issue)
        elif current_issue['category'] == 'Error prone':
            error_prone_issues.append(current_issue)
    
    print(f"Parsing complete!")
    print(f"Security issues found: {len(security_issues)}")
    print(f"Error-prone issues found: {len(error_prone_issues)}")
    print()
    
    # Export Security Issues
    output_dir_path = Path(output_dir)
    output_dir_path.mkdir(parents=True, exist_ok=True)
    
    security_output = output_dir_path / "codacy_security_issues.txt"
    with open(security_output, 'w', encoding='utf-8') as f:
        for issue in security_issues:
            f.write(f"File: {issue['file']}\\n")
            f.write(f"Line: {issue['line']}\\n")
            f.write(f"Severity: {issue['severity']}\\n")
            f.write(f"Pattern: {issue['pattern']}\\n")
            f.write(f"Code: {issue['code_snippet']}\\n")
            f.write("---\\n")
    
    print(f"[OK] Security issues exported to: {security_output}")
    
    # Export Error-Prone Issues
    error_prone_output = output_dir_path / "codacy_errorprone_issues.txt"
    with open(error_prone_output, 'w', encoding='utf-8') as f:
        for issue in error_prone_issues:
            f.write(f"File: {issue['file']}\\n")
            f.write(f"Line: {issue['line']}\\n")
            f.write(f"Severity: {issue['severity']}\\n")
            f.write(f"Pattern: {issue['pattern']}\\n")
            f.write(f"Code: {issue['code_snippet']}\\n")
            f.write("---\\n")
    
    print(f"[OK] Error-prone issues exported to: {error_prone_output}")
    print()
    
    # Create File Clustering Analysis
    print("Creating file clustering analysis...")
    
    # Group by file
    security_by_file = defaultdict(list)
    for issue in security_issues:
        security_by_file[issue['file']].append(issue)
    
    error_prone_by_file = defaultdict(list)
    for issue in error_prone_issues:
        error_prone_by_file[issue['file']].append(issue)
    
    all_issues_by_file = defaultdict(list)
    for issue in security_issues + error_prone_issues:
        all_issues_by_file[issue['file']].append(issue)
    
    # Sort by count
    security_sorted = sorted(security_by_file.items(), key=lambda x: len(x[1]), reverse=True)
    error_prone_sorted = sorted(error_prone_by_file.items(), key=lambda x: len(x[1]), reverse=True)
    combined_sorted = sorted(all_issues_by_file.items(), key=lambda x: len(x[1]), reverse=True)
    
    cluster_output = output_dir_path / "codacy_sec_errorprone_clusters.md"
    with open(cluster_output, 'w', encoding='utf-8') as f:
        f.write("# Security + Error-Prone File Clusters\\n\\n")
        f.write(f"**Generated**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\\n")
        f.write(f"**Total Security Issues**: {len(security_issues)}\\n")
        f.write(f"**Total Error-Prone Issues**: {len(error_prone_issues)}\\n")
        f.write(f"**Total Combined**: {len(security_issues) + len(error_prone_issues)}\\n\\n")
        f.write("---\\n\\n")
        
        # Security Issues
        f.write(f"## Security Issues ({len(security_issues)} total)\\n\\n")
        f.write("| File | Count | Patterns |\\n")
        f.write("|------|-------|----------|\\n")
        for file, issues in security_sorted:
            patterns = ", ".join(set(i['pattern'] for i in issues if i['pattern']))
            f.write(f"| {file} | {len(issues)} | {patterns} |\\n")
        
        f.write("\\n---\\n\\n")
        
        # Error-Prone Issues (top 20)
        f.write(f"## Error-Prone Issues ({len(error_prone_issues)} total)\\n\\n")
        f.write("| File | Count | Patterns |\\n")
        f.write("|------|-------|----------|\\n")
        for file, issues in error_prone_sorted[:20]:
            patterns = ", ".join(set(i['pattern'] for i in issues if i['pattern']))
            f.write(f"| {file} | {len(issues)} | {patterns} |\\n")
        
        f.write("\\n---\\n\\n")
        
        # Top 10 Combined
        f.write("## Top 10 Files (Combined Security + Error-Prone)\\n\\n")
        f.write("| Rank | File | Security | Error-Prone | Total | Key Patterns |\\n")
        f.write("|------|------|----------|-------------|-------|--------------|\\n")
        for rank, (file, issues) in enumerate(combined_sorted[:10], 1):
            sec_count = len([i for i in issues if i['category'] == 'Security'])
            err_count = len([i for i in issues if i['category'] == 'Error prone'])
            patterns = ", ".join(list(set(i['pattern'] for i in issues if i['pattern']))[:3])
            f.write(f"| {rank} | {file} | {sec_count} | {err_count} | {len(issues)} | {patterns} |\\n")
    
    print(f"[OK] File clustering analysis exported to: {cluster_output}")
    print()
    
    # Summary Report
    print("=== SUMMARY ===")
    sec_status = "[OK]" if len(security_issues) == 12 else "[WARN]"
    err_status = "[OK]" if len(error_prone_issues) == 105 else "[WARN]"
    print(f"{sec_status} Security Issues: {len(security_issues)}")
    print(f"{err_status} Error-Prone Issues: {len(error_prone_issues)}")
    print()
    print("Top 5 Files with Most Issues:")
    for file, issues in combined_sorted[:5]:
        sec_count = len([i for i in issues if i['category'] == 'Security'])
        err_count = len([i for i in issues if i['category'] == 'Error prone'])
        print(f"  {file}: {sec_count} Security + {err_count} Error-Prone = {len(issues)} total")
    print()
    
    return {
        'security_issues': security_issues,
        'error_prone_issues': error_prone_issues,
        'security_by_file': security_by_file,
        'error_prone_by_file': error_prone_by_file,
        'combined_by_file': all_issues_by_file
    }

if __name__ == "__main__":
    parse_codacy_export()

# Made with Bob
