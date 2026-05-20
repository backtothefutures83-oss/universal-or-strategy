import re
import os

files = [
    r"%USERPROFILE%\.agents\skills\google-agents-cli-eval\SKILL.md",
    r"%USERPROFILE%\.agents\skills\google-agents-cli-observability\SKILL.md",
    r"%USERPROFILE%\.agents\skills\google-agents-cli-workflow\SKILL.md"
]

pattern = re.compile(r"requires:\s+bins:\s+- agents-cli\s+install:\s+\"uv tool install google-agents-cli\"", re.DOTALL)
replacement = 'requires: "agents-cli (uv tool install google-agents-cli)"'

for file_path in files:
    if os.path.exists(file_path):
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = pattern.sub(replacement, content)
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Fixed {file_path}")
    else:
        print(f"Not found: {file_path}")
