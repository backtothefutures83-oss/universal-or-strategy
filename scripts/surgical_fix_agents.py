import os
import re

files_to_fix = [
    r"c:\WSGTA\universal-or-strategy\.gemini\agents\v12-adr-auditor.md",
    r"c:\WSGTA\universal-or-strategy\.gemini\agents\v12-amal-auditor.md"
]

for file_path in files_to_fix:
    if not os.path.exists(file_path):
        continue
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Simple search for the block between --- and ---
    match = re.search(r'---(.*?)---', content, re.DOTALL)
    if match:
        frontmatter = match.group(1)
        # 1. Remove all tools blocks
        frontmatter = re.sub(r'tools:.*?(?=\s*\w+:)', '', frontmatter, flags=re.DOTALL)
        # 2. Add a clean tools block
        frontmatter += '\ntools:\n  - "*"'
        # 3. Remove all empty lines
        lines = [line for line in frontmatter.splitlines() if line.strip()]
        new_frontmatter = '\n'.join(lines)
        
        # 4. Reconstruct the file
        new_content = "---\n" + new_frontmatter + "\n---\n" + content.split('---', 2)[2]
        
        with open(file_path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(new_content)
        print(f"Surgically Repaired: {file_path}")
