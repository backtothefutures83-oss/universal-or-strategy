import re

with open("docs/arena_dashboard.html", "r", encoding="utf-8") as f:
    content = f.read()

# Make the layout smoother, sharper but bubbly

# 1. Update letter-spacing and uppercase to be more natural
content = re.sub(r'letter-spacing:\s*2px;', 'letter-spacing: 0.5px;', content)
content = re.sub(r'letter-spacing:\s*1px;', 'letter-spacing: 0.2px;', content)
content = re.sub(r'text-transform:\s*uppercase;', 'text-transform: none;', content)

# 2. Add border radiuses and soft shadows to standard container elements if they don't have them
# Looking at the file, `.glass-node-box` has border-radius: 12px and box-shadow.
# Let's target badges and buttons
content = re.sub(r'border-radius:\s*2px;', 'border-radius: 8px;', content)
content = re.sub(r'border-radius:\s*4px;', 'border-radius: 10px;', content)
content = content.replace('.badge-pill {\n        font-family: var(--tactical-font);\n        font-size: 8px;', 
                          '.badge-pill {\n        font-family: var(--font-heading);\n        font-size: 10px;\n        box-shadow: 0 2px 5px rgba(0,0,0,0.2);')

content = content.replace('.nav-btn { ', '.nav-btn { \n        border-radius: 8px; ')

# 3. Soften borders from 1px solid var(--border) to something more bubbly if applicable
content = content.replace('border-right: 1px solid var(--border);', 'border-right: 1px solid var(--border); box-shadow: 2px 0 10px rgba(0,0,0,0.1);')

# 4. Enhance font choices to Anthropic defaults
content = content.replace('font-family: var(--tactical-font);', 'font-family: var(--font-heading);')
content = content.replace('font-family: var(--display-font);', 'font-family: var(--font-heading);')
content = content.replace('font-family: "JetBrains Mono", monospace;', 'font-family: var(--font-heading);')

# 5. Fix card backgrounds to be soft and bubbly (let's assume they are marked with border: 1px solid...)
content = re.sub(r'border:\s*1px solid var\(--border\);(?!.*border-radius)', r'border: 1px solid var(--border); border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.15); transition: transform 0.2s ease, box-shadow 0.2s ease;', content)

# Add hover effects to cards? Not sure what the class name is, replacing on `border: 1px solid var(--border);` is too generic.
# Let's read the file and do more targeted replacements.
content = content.replace('.main-content {\n        flex-grow: 1;\n        min-width: 0;\n      }',
                          '.main-content {\n        flex-grow: 1;\n        min-width: 0;\n        background: var(--surface2);\n        border-radius: 16px;\n        padding: 30px;\n        box-shadow: inset 0 0 0 1px rgba(255,255,255,0.05), 0 8px 32px rgba(0,0,0,0.2);\n      }')

content = content.replace('.sidebar {\n        width: 240px;\n        flex-shrink: 0;\n        display: flex;\n        flex-direction: column;\n        height: calc(100vh - 48px);\n        position: sticky;\n        top: 24px;\n        border-right: 1px solid var(--border);\n        padding-right: 20px;\n      }',
                          '.sidebar {\n        width: 240px;\n        flex-shrink: 0;\n        display: flex;\n        flex-direction: column;\n        height: calc(100vh - 48px);\n        position: sticky;\n        top: 24px;\n        border-right: none;\n        padding-right: 20px;\n      }')

with open("docs/arena_dashboard_new2.html", "w", encoding="utf-8") as f:
    f.write(content)
print("Updated successfully")
