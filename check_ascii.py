#!/usr/bin/env python3
"""
V12 ASCII Purity Checker
Validates that staged C# files contain only ASCII characters.
Used by pre-commit hook to enforce encoding standards.
"""
import sys
import os

def check_ascii(filepath):
    """Check if file contains only ASCII characters."""
    try:
        with open(filepath, 'rb') as f:
            content = f.read()
            try:
                content.decode('ascii')
                return True
            except UnicodeDecodeError as e:
                print(f"Non-ASCII found in {filepath}: {e}")
                return False
    except Exception as e:
        print(f"Error reading {filepath}: {e}")
        return False

def main():
    if len(sys.argv) < 2:
        print("Usage: check_ascii.py <file1> [file2] ...")
        sys.exit(1)
    
    all_valid = True
    for filepath in sys.argv[1:]:
        if not os.path.exists(filepath):
            print(f"File not found: {filepath}")
            all_valid = False
            continue
        
        if not check_ascii(filepath):
            all_valid = False
    
    sys.exit(0 if all_valid else 1)

if __name__ == "__main__":
    main()

# Made with Bob
