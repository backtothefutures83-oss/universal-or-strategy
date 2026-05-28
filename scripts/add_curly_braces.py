#!/usr/bin/env python3
"""
Add curly braces to single-line control statements in C# files.
Handles if, else, for, foreach, while, do-while statements.
"""

import re
import sys
from pathlib import Path
from typing import List, Tuple

def needs_braces(line: str) -> bool:
    """Check if a control statement line needs braces."""
    stripped = line.strip()
    
    # Skip if already has opening brace
    if '{' in stripped:
        return False
    
    # Skip if it's just a closing brace or empty
    if stripped in ['', '}', '};']:
        return False
    
    # Check for control statements without braces
    control_patterns = [
        r'^\s*if\s*\(',
        r'^\s*else\s+if\s*\(',
        r'^\s*else\s*$',
        r'^\s*for\s*\(',
        r'^\s*foreach\s*\(',
        r'^\s*while\s*\(',
        r'^\s*do\s*$',
    ]
    
    for pattern in control_patterns:
        if re.match(pattern, line):
            return True
    
    return False

def get_indentation(line: str) -> str:
    """Extract the indentation from a line."""
    match = re.match(r'^(\s*)', line)
    return match.group(1) if match else ''

def add_braces_to_file(filepath: Path) -> Tuple[bool, int]:
    """
    Add braces to single-line control statements in a C# file.
    Returns (modified, count) where modified is True if changes were made.
    """
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except Exception as e:
        print(f"Error reading {filepath}: {e}", file=sys.stderr)
        return False, 0
    
    modified = False
    changes = 0
    i = 0
    new_lines = []
    
    while i < len(lines):
        line = lines[i]
        
        # Check if this line needs braces
        if needs_braces(line):
            # Look ahead to see if next line is a single statement
            if i + 1 < len(lines):
                next_line = lines[i + 1]
                next_stripped = next_line.strip()
                
                # Skip if next line is already a brace or empty
                if next_stripped and next_stripped not in ['{', '}', '};']:
                    # Get indentation
                    indent = get_indentation(line)
                    
                    # Add opening brace after control statement
                    new_lines.append(line.rstrip() + '\n')
                    new_lines.append(indent + '{\n')
                    
                    # Add the statement line
                    new_lines.append(next_line)
                    
                    # Add closing brace
                    new_lines.append(indent + '}\n')
                    
                    modified = True
                    changes += 1
                    i += 2  # Skip the next line since we already processed it
                    continue
        
        new_lines.append(line)
        i += 1
    
    if modified:
        try:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.writelines(new_lines)
        except Exception as e:
            print(f"Error writing {filepath}: {e}", file=sys.stderr)
            return False, 0
    
    return modified, changes

def main():
    """Process all C# files in src/ directory."""
    src_dir = Path('src')
    
    if not src_dir.exists():
        print("Error: src/ directory not found", file=sys.stderr)
        return 1
    
    total_files = 0
    total_changes = 0
    modified_files = []
    
    # Process all .cs files
    for cs_file in sorted(src_dir.glob('*.cs')):
        modified, changes = add_braces_to_file(cs_file)
        if modified:
            total_files += 1
            total_changes += changes
            modified_files.append(cs_file.name)
            print(f"[OK] {cs_file.name}: {changes} fixes")
    
    print(f"\nSummary:")
    print(f"  Files modified: {total_files}")
    print(f"  Total fixes: {total_changes}")
    
    if modified_files:
        print(f"\nModified files:")
        for filename in modified_files:
            print(f"  - {filename}")
    
    return 0

if __name__ == '__main__':
    sys.exit(main())

# Made with Bob
