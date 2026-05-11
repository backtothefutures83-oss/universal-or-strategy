#!/usr/bin/env python3
"""
Full codebase complexity audit for V12 Universal OR Strategy.
Analyzes all src/*.cs files for:
1. Cyclomatic complexity (CYC)
2. M5 dispatch candidates (switch/if chains >= 4 branches)
3. LOC health (methods > 80 LOC)
"""
import re
import os
from pathlib import Path
from typing import List
from dataclasses import dataclass

@dataclass
class MethodMetrics:
    name: str
    loc: int
    cyc: int
    is_m5_candidate: bool
    file: str
    line_start: int

def estimate_cyclomatic_complexity(method_body: str) -> int:
    """Estimate CYC by counting decision points."""
    cyc = 1  # Base complexity
    
    # Count decision points
    patterns = [
        (r'\bif\s*\(', 1),
        (r'\belse\s+if\s*\(', 1),
        (r'\bwhile\s*\(', 1),
        (r'\bfor\s*\(', 1),
        (r'\bforeach\s*\(', 1),
        (r'\bcase\s+', 1),
        (r'\bcatch\s*\(', 1),
        (r'\b\?\s*', 1),  # Ternary
        (r'\&\&', 1),
        (r'\|\|', 1),
    ]
    
    for pattern, weight in patterns:
        cyc += len(re.findall(pattern, method_body)) * weight
    
    return cyc

def detect_m5_candidate(method_body: str) -> bool:
    """
    Detect M5 dispatch candidates: switch/if chains with >= 4 branches
    on string/enum that call distinct named methods.
    """
    # Look for switch statements with 4+ cases
    switch_matches = re.finditer(r'switch\s*\([^)]+\)\s*\{', method_body, re.DOTALL)
    for match in switch_matches:
        start_pos = match.end()
        # Find the matching closing brace
        brace_count = 1
        pos = start_pos
        while pos < len(method_body) and brace_count > 0:
            if method_body[pos] == '{':
                brace_count += 1
            elif method_body[pos] == '}':
                brace_count -= 1
            pos += 1
        
        switch_body = method_body[start_pos:pos]
        case_count = len(re.findall(r'\bcase\s+', switch_body))
        if case_count >= 4:
            # Check if cases call distinct methods
            method_calls = re.findall(r'(\w+)\s*\(', switch_body)
            if len(set(method_calls)) >= 3:
                return True
    
    # Look for if-else chains with 4+ branches
    if_pattern = r'if\s*\([^)]*(?:==|!=|<|>)[^)]*\)'
    if_matches = list(re.finditer(if_pattern, method_body))
    if len(if_matches) >= 4:
        # Check if they're part of an if-else chain
        chain_count = 1
        for i in range(len(if_matches) - 1):
            # Check if next if is preceded by else
            between = method_body[if_matches[i].end():if_matches[i+1].start()]
            if 'else' in between:
                chain_count += 1
        if chain_count >= 4:
            return True
    
    return False

def extract_methods(file_path: str) -> List[MethodMetrics]:
    """Extract all methods from a C# file with metrics."""
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            content = f.read()
    except Exception as e:
        print(f"Error reading {file_path}: {e}")
        return []
    
    methods = []
    lines = content.split('\n')
    i = 0
    
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        
        # Skip empty lines, comments, attributes, using statements, namespace declarations
        if (not stripped or 
            stripped.startswith('//') or 
            stripped.startswith('/*') or
            stripped.startswith('*') or
            stripped.startswith('[') or
            stripped.startswith('using ') or
            stripped.startswith('namespace ') or
            stripped.startswith('#')):
            i += 1
            continue
        
        # Look for method signatures
        # Pattern: modifiers + return type + method name + parameters
        # Handle multi-line signatures
        potential_method = line
        line_start = i
        
        # Collect lines until we find an opening brace or semicolon
        j = i
        while j < len(lines) and '{' not in lines[j] and ';' not in lines[j]:
            j += 1
            if j < len(lines):
                potential_method += ' ' + lines[j].strip()
        
        # Check if this looks like a method declaration
        method_match = re.search(
            r'(?:public|private|protected|internal|static|virtual|override|async|sealed|abstract|\s)+\s+'
            r'(?:void|bool|int|string|double|decimal|float|long|Task|IEnumerable|List|Dictionary|Action|Func|\w+)'
            r'(?:<[^>]+>)?(?:\[\])?\s+'
            r'(\w+)\s*\([^)]*\)\s*\{',
            potential_method
        )
        
        if method_match and '{' in potential_method:
            method_name = method_match.group(1)
            
            # Skip properties (get/set)
            if 'get;' in potential_method or 'set;' in potential_method or '=>' in potential_method.split('{')[0]:
                i = j + 1
                continue
            
            # Find the line with the opening brace
            brace_line = i
            while brace_line <= j and '{' not in lines[brace_line]:
                brace_line += 1
            
            # Now find the matching closing brace
            brace_count = 1
            method_lines = []
            k = brace_line
            
            # Add lines from method signature start to opening brace
            for idx in range(i, brace_line + 1):
                method_lines.append(lines[idx])
            
            k = brace_line + 1
            while k < len(lines) and brace_count > 0:
                current_line = lines[k]
                method_lines.append(current_line)
                brace_count += current_line.count('{') - current_line.count('}')
                k += 1
            
            method_body = '\n'.join(method_lines)
            
            # Count LOC (non-empty, non-comment, non-brace-only lines)
            loc = 0
            for l in method_lines:
                stripped_line = l.strip()
                if (stripped_line and 
                    not stripped_line.startswith('//') and 
                    not stripped_line.startswith('/*') and
                    not stripped_line.startswith('*') and
                    stripped_line not in ['{', '}']):
                    loc += 1
            
            cyc = estimate_cyclomatic_complexity(method_body)
            is_m5 = detect_m5_candidate(method_body)
            
            methods.append(MethodMetrics(
                name=method_name,
                loc=loc,
                cyc=cyc,
                is_m5_candidate=is_m5,
                file=os.path.basename(file_path),
                line_start=line_start + 1
            ))
            
            i = k
        else:
            i += 1
    
    return methods

def generate_report():
    """Generate full complexity audit report."""
    src_dir = Path('src')
    cs_files = sorted(src_dir.glob('*.cs'))
    
    all_methods = []
    total_methods = 0
    cyc_over_20 = []
    cyc_15_to_20 = []
    m5_candidates = []
    loc_over_80 = []
    
    print("=" * 80)
    print("V12 UNIVERSAL OR STRATEGY - FULL CODEBASE COMPLEXITY AUDIT")
    print("=" * 80)
    print()
    
    for cs_file in cs_files:
        methods = extract_methods(str(cs_file))
        all_methods.extend(methods)
        total_methods += len(methods)
        
        if not methods:
            continue
        
        print(f"=== FILE: {cs_file.name} ===")
        print(f"| {'Method':<40} | {'LOC':>5} | {'Est. CYC':>8} | {'M5 Candidate?':^14} | {'Action':<20} |")
        print(f"|{'-'*42}|{'-'*7}|{'-'*10}|{'-'*16}|{'-'*22}|")
        
        # Sort by CYC descending
        sorted_methods = sorted(methods, key=lambda m: m.cyc, reverse=True)
        
        for method in sorted_methods:
            action = []
            if method.cyc > 20:
                action.append("CRITICAL-REFACTOR")
                cyc_over_20.append(f"{cs_file.name}::{method.name} (CYC={method.cyc}, LOC={method.loc})")
            elif method.cyc >= 15:
                action.append("WATCH")
                cyc_15_to_20.append(f"{cs_file.name}::{method.name} (CYC={method.cyc}, LOC={method.loc})")
            
            if method.loc > 80:
                action.append("LOC>80")
                loc_over_80.append(f"{cs_file.name}::{method.name} (LOC={method.loc})")
            
            m5_flag = "YES" if method.is_m5_candidate else ""
            if method.is_m5_candidate:
                m5_candidates.append(f"{cs_file.name}::{method.name}")
            
            action_str = ", ".join(action) if action else "OK"
            
            print(f"| {method.name:<40} | {method.loc:>5} | {method.cyc:>8} | {m5_flag:^14} | {action_str:<20} |")
        
        print()
    
    # Final summary
    print("=" * 80)
    print("=== PHASE 7 CLOSE REPORT ===")
    print("=" * 80)
    print(f"Total methods audited: {total_methods}")
    print()
    
    print(f"CYC > 20 remaining: {len(cyc_over_20)}")
    if cyc_over_20:
        for item in cyc_over_20:
            print(f"  - {item}")
    else:
        print("  NONE")
    print()
    
    print(f"CYC 15-20 (watch list): {len(cyc_15_to_20)}")
    if cyc_15_to_20:
        for item in cyc_15_to_20:
            print(f"  - {item}")
    else:
        print("  NONE")
    print()
    
    print(f"M5 dispatch candidates: {len(m5_candidates)}")
    if m5_candidates:
        for item in m5_candidates:
            print(f"  - {item}")
    else:
        print("  NONE")
    print()
    
    print(f"LOC > 80: {len(loc_over_80)}")
    if loc_over_80:
        for item in loc_over_80:
            print(f"  - {item}")
    print()
    
    print("[CODEBASE-AUDIT-COMPLETE]")

if __name__ == '__main__':
    generate_report()

# Made with Bob
