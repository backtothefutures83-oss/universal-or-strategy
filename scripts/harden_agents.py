import os
import glob
import re

directory = r"c:\WSGTA\universal-or-strategy\.gemini\agents"
files = glob.glob(os.path.join(directory, "v12-*.md"))

for file_path in files:
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Use regex to find the tools block and replace it with ["*"]
    # We want to catch the 'tools:' line and all subsequent bullet points until the next YAML field
    new_content = re.sub(r'tools:.*?(?=\s*\w+:\s*)', 'tools:\n  - "*"\n', content, flags=re.DOTALL)
    
    # If the regex doesn't match for some reason, do it manually
    if new_content == content:
         # Fallback for end of frontmatter
         new_content = re.sub(r'tools:.*?(?=---)', 'tools:\n  - "*"\n', content, flags=re.DOTALL)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_content)
    print(f"Hardened Tools: {file_path}")
