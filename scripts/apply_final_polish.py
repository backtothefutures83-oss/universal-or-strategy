import re

with open("docs/arena_dashboard.html", "r", encoding="utf-8") as f:
    content = f.read()

# Let's cleanly overwrite the core layout CSS blocks instead of patching them.
# We will use targeted regex replacements for the CSS classes.

styles_to_replace = {
    r'\.app-wrapper\s*\{[^}]+\}': 
        '.app-wrapper {\n        display: flex;\n        gap: 40px;\n        max-width: 1600px;\n        margin: 0 auto;\n        position: relative;\n        padding-top: 12px;\n      }',
    
    r'\.sidebar\s*\{[^}]+\}':
        '.sidebar {\n        width: 280px;\n        flex-shrink: 0;\n        display: flex;\n        flex-direction: column;\n        height: calc(100vh - 72px);\n        position: sticky;\n        top: 36px;\n      }',
        
    r'\.main-content\s*\{[^}]+\}':
        '.main-content {\n        flex-grow: 1;\n        min-width: 0;\n        background: rgba(30, 30, 29, 0.4);\n        backdrop-filter: blur(24px);\n        border-radius: 24px;\n        padding: 40px;\n        box-shadow: 0 8px 32px rgba(0,0,0,0.2), inset 0 1px 0 rgba(255,255,255,0.05);\n        border: 1px solid rgba(250, 249, 245, 0.05);\n      }',

    r'\.sidebar-header\s*\{[^}]+\}':
        '.sidebar-header {\n        padding-bottom: 32px;\n        margin-bottom: 32px;\n        border-bottom: 1px solid rgba(250, 249, 245, 0.08);\n      }',

    r'\.logo\s*\{[^}]+\}\s*\.logo\s*span\s*\{[^}]+\}':
        '.logo {\n        font-family: var(--font-heading);\n        font-size: 18px;\n        font-weight: 600;\n        color: var(--brand-blue);\n        letter-spacing: -0.2px;\n        display: flex;\n        align-items: center;\n        gap: 8px;\n      }\n      .logo span {\n        color: var(--brand-mid-gray);\n        font-weight: 400;\n      }',

    r'\.nav-tabs\s*\{[^}]+\}':
        '.nav-tabs {\n        display: flex;\n        flex-direction: column;\n        gap: 8px;\n        margin-bottom: 40px;\n        flex-grow: 1;\n      }',

    r'\.nav-btn\s*\{[^}]+\}':
        '.nav-btn {\n        background: transparent;\n        border: none;\n        border-radius: 12px;\n        color: var(--text-dim);\n        padding: 12px 20px;\n        font-family: var(--font-heading);\n        font-size: 13px;\n        font-weight: 500;\n        cursor: pointer;\n        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);\n        text-align: left;\n        display: flex;\n        align-items: center;\n        gap: 12px;\n      }',

    r'\.nav-btn:hover\s*\{[^}]+\}':
        '.nav-btn:hover {\n        color: var(--text-bright);\n        background: rgba(250, 249, 245, 0.06);\n        transform: translateX(4px);\n      }',

    r'\.nav-btn\.active\s*\{[^}]+\}':
        '.nav-btn.active {\n        color: var(--brand-dark);\n        background: var(--brand-blue);\n        font-weight: 600;\n        box-shadow: 0 4px 12px rgba(106, 155, 204, 0.3);\n      }',

    r'\.profile-anchor\s*\{[^}]+\}':
        '.profile-anchor {\n        margin-top: auto;\n        padding-top: 24px;\n        border-top: 1px solid rgba(250, 249, 245, 0.08);\n        display: flex;\n        align-items: center;\n        gap: 16px;\n        padding-bottom: 12px;\n      }',

    r'\.profile-avatar\s*\{[^}]+\}':
        '.profile-avatar {\n        width: 40px;\n        height: 40px;\n        border-radius: 14px;\n        background: linear-gradient(135deg, var(--brand-orange), var(--brand-blue));\n        display: flex;\n        align-items: center;\n        justify-content: center;\n        font-weight: 600;\n        color: #fff;\n        font-size: 14px;\n        box-shadow: 0 4px 12px rgba(0,0,0,0.2);\n      }',

    r'\.card\s*\{[^}]+\}\s*\.card:nth-child\(1\)\s*\{[^}]+\}\s*\.card:nth-child\(2\)\s*\{[^}]+\}\s*\.card:nth-child\(3\)\s*\{[^}]+\}':
        '.card {\n        background: rgba(20, 20, 19, 0.6);\n        border: 1px solid rgba(250, 249, 245, 0.08);\n        border-radius: 20px;\n        padding: 36px;\n        position: relative;\n        overflow: hidden;\n        box-shadow: 0 10px 30px rgba(0,0,0,0.2), inset 0 1px 0 rgba(255,255,255,0.03);\n        transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.3s ease;\n        opacity: 0;\n        animation: cardEntry 0.6s cubic-bezier(0.4, 0, 0.2, 1) forwards;\n        backdrop-filter: blur(12px);\n      }\n      .card:hover {\n        transform: translateY(-4px);\n        box-shadow: 0 20px 40px rgba(0,0,0,0.3), inset 0 1px 0 rgba(255,255,255,0.05);\n      }\n      .card:nth-child(1) { animation-delay: 0.1s; }\n      .card:nth-child(2) { animation-delay: 0.2s; }\n      .card:nth-child(3) { animation-delay: 0.3s; }',

    r'\.toolbelt-item\s*\{[^}]+\}':
        '.toolbelt-item {\n        background: rgba(20, 20, 19, 0.6);\n        border: 1px solid rgba(250, 249, 245, 0.08);\n        border-radius: 16px;\n        border-top: 4px solid var(--brand-orange);\n        padding: 28px;\n        position: relative;\n        transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.3s ease;\n        opacity: 0;\n        animation: cardEntry 0.5s cubic-bezier(0.4, 0, 0.2, 1) forwards;\n        backdrop-filter: blur(10px);\n      }\n      .toolbelt-item:hover {\n        transform: translateY(-6px);\n        box-shadow: 0 16px 32px rgba(0,0,0,0.3);\n      }',
        
    r'\.toolbelt-preview\s*\{[^}]+\}':
        '.toolbelt-preview {\n        background: rgba(0, 0, 0, 0.2);\n        border-radius: 12px;\n        margin: 16px 0;\n        padding: 16px;\n        overflow: hidden;\n        position: relative;\n        box-shadow: inset 0 4px 12px rgba(0,0,0,0.3);\n        border: 1px solid rgba(255,255,255,0.03);\n      }',
        
    r'\.badge-pill\s*\{[^}]+\}':
        '.badge-pill {\n        font-family: var(--font-heading);\n        font-size: 11px;\n        font-weight: 500;\n        padding: 6px 14px;\n        border-radius: 12px;\n        box-shadow: 0 2px 8px rgba(0,0,0,0.15);\n      }',
}

for pattern, replacement in styles_to_replace.items():
    content = re.sub(pattern, replacement, content)

with open("docs/arena_dashboard_polished.html", "w", encoding="utf-8") as f:
    f.write(content)

print("Pro-Max Polish Applied.")
