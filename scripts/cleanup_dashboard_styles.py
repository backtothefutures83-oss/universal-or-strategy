import re

with open("docs/arena_dashboard.html", "r", encoding="utf-8") as f:
    content = f.read()

# Clean up redundant border-radius and messy double-definitions
content = content.replace('border-radius: 12px !important; \n        border-radius: 8px; \n        background: var(--card-bg); \n        border: 1px solid var(--border); border-radius: 12px;',
                          'border-radius: 12px !important; \n        background: var(--card-bg); \n        border: 1px solid var(--border);')

content = re.sub(r'border: 1px solid var\(--border\);\s*border-radius: 12px;\s*box-shadow: 0 4px 20px rgba\(0,0,0,0\.15\);', 
                 'border: 1px solid var(--border); border-radius: 16px; box-shadow: 0 10px 30px rgba(0,0,0,0.1);', content)

# Remove any remaining "banned" fonts or aesthetics
content = content.replace('font-family: var(--tactical-font);', 'font-family: var(--font-body);')

with open("docs/arena_dashboard_clean.html", "w", encoding="utf-8") as f:
    f.write(content)

print("Cleanup complete.")
