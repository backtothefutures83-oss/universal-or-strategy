import re

with open("docs/arena_dashboard.html", "r", encoding="utf-8") as f:
    content = f.read()

# 1. Remove tactical jagged clip-paths on nav buttons
content = re.sub(r'clip-path:\s*polygon[^;]+;', '', content)
content = content.replace('margin-right: -10px;', '') # Remove the overlap used for tactical buttons

# 2. Fix the .nav-tabs to be bubbly
content = content.replace('gap: 2px;', 'gap: 8px;')
content = content.replace('.nav-btn { ', '.nav-btn { \n        border-radius: 12px !important; ')

# 3. Clean up the .card and .panel tactical shadows and overlapping border-radius
content = re.sub(r'border-radius:\s*0;', '', content)  # Allow the 12px border radius to apply
content = re.sub(r'box-shadow:\s*10px 10px 0 rgba\(0,0,0,0\.5\);', 'box-shadow: 0 12px 40px rgba(0,0,0,0.3);', content)
content = re.sub(r'background:\s*#0d0d0d;', 'background: var(--card-bg);', content)
content = content.replace('padding: 24px;', 'padding: 32px;')

# 4. Enhance headers
content = content.replace('border-bottom: 1px dashed', 'border-bottom: 2px solid')
content = content.replace('border-bottom: 1px dotted', 'border-bottom: 1px solid rgba(250,249,245,0.1)')
content = re.sub(r'font-size:\s*10px;\s*opacity:\s*0\.4;', 'font-size: 11px; opacity: 0.6; font-family: var(--font-body);', content)

# 5. Fix card entry animations / initial appearance so it's smoother
content = content.replace('from { opacity: 0; transform: translateX(-20px); }', 'from { opacity: 0; transform: translateY(20px); }')

# 6. .toolbelt-item fix
content = content.replace('.toolbelt-preview {\n        background: var(--brand-dark);\n        border: 1px solid #1a2236;\n        margin: 14px 0;\n        overflow: hidden;\n        position: relative;\n      }',
                          '.toolbelt-preview {\n        background: var(--surface2);\n        border-radius: 8px;\n        margin: 14px 0;\n        padding: 12px;\n        overflow: hidden;\n        position: relative;\n        box-shadow: inset 0 2px 10px rgba(0,0,0,0.2);\n      }')

# 7. More spacing in the main wrapper
content = content.replace('gap: 24px;\n        max-width: 1600px;', 'gap: 48px;\n        max-width: 1600px;')
# Increase sidebar width a bit to give nav item text space
content = content.replace('width: 240px;', 'width: 280px;')

# 8. Soften profile-anchor
content = content.replace('border-top: 1px solid var(--border);', 'border-top: 1px solid rgba(250,249,245,0.05); padding-top: 24px;')

# 9. Clean up tactical labels
content = re.sub(r'font-size:\s*8px;', 'font-size: 10px;', content)
content = re.sub(r'font-size:\s*9px;', 'font-size: 11px;', content)
content = re.sub(r'font-size:\s*7px;', 'font-size: 10px;', content)
content = re.sub(r'letter-spacing:\s*1px;', 'letter-spacing: 0.2px;', content)
content = re.sub(r'text-transform:\s*uppercase;', 'text-transform: none;', content)
content = content.replace('.badge-pill {\n        font-family: var(--font-heading);\n        font-size: 10px;\n        box-shadow: 0 2px 5px rgba(0,0,0,0.2);',
                          '.badge-pill {\n        font-family: var(--font-heading);\n        font-size: 11px;\n        font-weight: 600;\n        padding: 6px 12px;\n        border-radius: 8px;\n        box-shadow: 0 2px 5px rgba(0,0,0,0.2);')

# 10. Update typography of main heading / logos
content = content.replace('.logo {\n        font-family: var(--font-heading);', '.logo {\n        font-family: var(--font-heading);\n        font-size: 20px;\n        letter-spacing: -0.5px;\n        font-weight: 600;')
content = content.replace('.card::before {\n        content:', '.card::before {\n        content: "Anthropic Sovereign OS // Confidential";\n        font-family: var(--font-heading); font-size: 9px; opacity: 0.4;')

# 11. Soften glass-nodes
content = content.replace('.glass-node-box {\n        background: rgba(10, 10, 15, 0.65)', 
                          '.glass-node-box {\n        background: rgba(40, 40, 40, 0.65)')
content = content.replace('border-radius: 12px;\n        padding: 12px 20px;', 'border-radius: 16px;\n        padding: 16px 24px;\n        box-shadow: 0 12px 30px rgba(0,0,0,0.4);')

with open("docs/arena_dashboard_new2.html", "w", encoding="utf-8") as f:
    f.write(content)

print("Second wave of styling enhancements applied.")
