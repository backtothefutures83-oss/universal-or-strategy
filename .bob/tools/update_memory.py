#!/usr/bin/env python3
"""
update_memory.py - Structured memory update tool for V12 persistent memory system

Purpose: Enable agents to update MEMORY.md and USER.md with learned facts across sessions.
Inspired by Hermes Agent's memory system.

Usage:
    python .bob/tools/update_memory.py --type memory --category "V12 DNA" --content "New fact about lock-free patterns"
    python .bob/tools/update_memory.py --type user --category "Preferences" --content "User prefers X over Y"
"""

import argparse
import os
import sys
from datetime import datetime
from pathlib import Path


def get_memory_file(memory_type: str) -> Path:
    """Get the path to the appropriate memory file."""
    base_dir = Path(__file__).parent.parent.parent
    if memory_type == "memory":
        return base_dir / "docs" / "brain" / "MEMORY.md"
    elif memory_type == "user":
        return base_dir / "docs" / "brain" / "USER.md"
    else:
        raise ValueError(f"Invalid memory type: {memory_type}. Must be 'memory' or 'user'")


def update_memory(memory_type: str, category: str, content: str, operation: str = "append") -> None:
    """
    Update memory file with new content.
    
    Args:
        memory_type: 'memory' or 'user'
        category: Section to update (e.g., "V12 DNA Constraints", "User Preferences")
        content: Content to add
        operation: 'append' (add to section) or 'replace' (replace section)
    """
    memory_file = get_memory_file(memory_type)
    
    if not memory_file.exists():
        print(f"[ERROR] Memory file not found: {memory_file}")
        sys.exit(1)
    
    # Read current content
    with open(memory_file, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # Find the category section
    category_line_idx = None
    next_section_idx = None
    
    for i, line in enumerate(lines):
        if line.strip().startswith("##") and category.lower() in line.lower():
            category_line_idx = i
        elif category_line_idx is not None and line.strip().startswith("##"):
            next_section_idx = i
            break
    
    if category_line_idx is None:
        print(f"[ERROR] Category '{category}' not found in {memory_file.name}")
        print(f"[HINT] Available categories:")
        for line in lines:
            if line.strip().startswith("##"):
                print(f"  - {line.strip()[3:]}")
        sys.exit(1)
    
    # Determine insertion point
    if next_section_idx is None:
        # Category is the last section
        insert_idx = len(lines)
    else:
        insert_idx = next_section_idx
    
    # Format content
    timestamp = datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")
    formatted_content = f"\n### {content}\n- **Added:** {timestamp}\n- **Source:** Agent memory update\n\n"
    
    if operation == "append":
        # Insert before next section or at end
        lines.insert(insert_idx, formatted_content)
    elif operation == "replace":
        # Replace entire section content (between category and next section)
        del lines[category_line_idx + 1:insert_idx]
        lines.insert(category_line_idx + 1, formatted_content)
    else:
        print(f"[ERROR] Invalid operation: {operation}. Must be 'append' or 'replace'")
        sys.exit(1)
    
    # Update timestamp in header
    for i, line in enumerate(lines):
        if line.startswith("**Last Updated:**"):
            lines[i] = f"**Last Updated:** {timestamp}\n"
            break
    
    # Write updated content
    with open(memory_file, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    
    print(f"[SUCCESS] Updated {memory_file.name}")
    print(f"  Category: {category}")
    print(f"  Operation: {operation}")
    print(f"  Content: {content[:50]}..." if len(content) > 50 else f"  Content: {content}")


def main():
    parser = argparse.ArgumentParser(
        description="Update V12 persistent memory (MEMORY.md or USER.md)"
    )
    parser.add_argument(
        "--type",
        required=True,
        choices=["memory", "user"],
        help="Memory type: 'memory' (MEMORY.md) or 'user' (USER.md)"
    )
    parser.add_argument(
        "--category",
        required=True,
        help="Section to update (e.g., 'V12 DNA Constraints', 'User Preferences')"
    )
    parser.add_argument(
        "--content",
        required=True,
        help="Content to add to the section"
    )
    parser.add_argument(
        "--operation",
        default="append",
        choices=["append", "replace"],
        help="Operation: 'append' (add to section) or 'replace' (replace section)"
    )
    
    args = parser.parse_args()
    
    try:
        update_memory(args.type, args.category, args.content, args.operation)
    except Exception as e:
        print(f"[ERROR] {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()

# Made with Bob
